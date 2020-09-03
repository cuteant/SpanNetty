// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using DotNetty.NetUV.Handles;
    using DotNetty.NetUV.Native;
    using Xunit;

    public sealed class PollTests : IDisposable
    {
        const int Port = 9889;
        const int NumberOfClients = 5;
        const int TransferBytes = 1 << 16;

        Loop loop;
        int closedConnections;
        int spuriousWritableWakeups;
        int validWritableWakeups;
        IPEndPoint endPoint;
        bool deplux;

        ServerContext serverContext;

        class ServerContext
        {
            public ServerContext(Socket socket, Poll handle)
            {
                this.Socket = socket;
                this.Handle = handle;
            }

            public Socket Socket { get; }

            public Poll Handle { get; }

            public int ConnectionCount { get; set; }
        }

        class ConnectionContext
        {
            public ConnectionContext(Socket socket, Poll handle, bool isServerConnection)
            {
                this.Socket = socket;
                this.PollHandle = handle;
                this.IsServerConnection = isServerConnection;
            }

            public bool IsServerConnection { get; }

            public Socket Socket { get; }

            public Poll PollHandle { get; }

            public Timer TimerHandle { get; set; }

            public int Receive { get; set; }

            public bool ReceiveFinished { get; set; }

            public int Sent { get; set; }

            public bool SentFinished { get; set; }

            public bool Disconnected { get; set; }

            public PollMask EventMask { get; set; }

            public PollMask DelayedEventMask { get; set; }

            public int OpenHandles { get; set; }
        }

        public PollTests()
        {
            this.loop = new Loop();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Run(bool depluxMode)
        {
            this.deplux = depluxMode;
            this.endPoint = new IPEndPoint(IPAddress.Loopback, depluxMode ? Port : Port + 1);

            this.StartServer();

            for (int i = 0; i < NumberOfClients; i++)
            {
                this.StartClient();
            }

            this.loop.RunDefault();
            Assert.Equal(NumberOfClients * 2, this.closedConnections);

            /* Assert that at most five percent of the writable wakeups was spurious. */
            Assert.True(this.spuriousWritableWakeups == 0 
                || (this.validWritableWakeups + this.spuriousWritableWakeups) / this.spuriousWritableWakeups > 20);
        }

        void StartClient()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var anyEndPoint = new IPEndPoint(IPAddress.Loopback, IPEndPoint.MinPort);
            socket.Bind(anyEndPoint);

            IntPtr handle = TestHelper.GetHandle(socket);
            const PollMask Mask = PollMask.Readable | PollMask.Writable | PollMask.Disconnect;
            Poll poll = this.loop.CreatePoll(handle).Start(Mask, this.OnPollConnection);

            Timer timer = this.loop.CreateTimer();
            var context = new ConnectionContext(socket, poll, false)
            {
                TimerHandle = timer,
                EventMask = Mask,
                OpenHandles = 2
            };
            timer.UserToken = context;
            poll.UserToken = context;

            // Kick off the connect
            try
            {
                socket.Connect(this.endPoint);
            }
            catch (SocketException exception)
            {
                if (!IsErrorAgain(exception))
                {
                    throw;
                }
            }
        }

        void OnPollConnection(Poll handle, PollStatus status)
        {
            var context = (ConnectionContext)handle.UserToken;

            PollMask pollMask = status.Mask;
            PollMask newEvents = context.EventMask;
            var random = new Random(10);

            if ((pollMask & PollMask.Readable) == PollMask.Readable)
            {
                int action = random.Next() % 7;

                if (action == 0 
                    || action == 1)
                {
                    // Read a couple of bytes.
                    var buffer = new byte[74];
                    int count = TryReceive(context.Socket, buffer);
                    if (count > 0)
                    {
                        context.Receive += count;
                    }
                    else
                    {
                        // Got FIN.
                        context.ReceiveFinished = true;
                        newEvents &= ~PollMask.Readable;
                    }
                }
                else if (action == 2 
                    || action == 3)
                {
                    // Read until EAGAIN.
                    var buffer = new byte[931];
                    int count = TryReceive(context.Socket, buffer);
                    while (count > 0)
                    {
                        context.Receive += count;
                        count = TryReceive(context.Socket, buffer);
                    }

                    if (count == 0)
                    {
                        // Got FIN.
                        context.ReceiveFinished = true;
                        newEvents &= ~PollMask.Readable;
                    }
                }
                else if (action == 4)
                {
                    // Ignore.
                }
                else if (action == 5)
                {
                    // Stop reading for a while. Restart in timer callback.
                    newEvents &= ~PollMask.Readable;

                    if (!context.TimerHandle.IsActive)
                    {
                        context.DelayedEventMask = PollMask.Readable;
                        context.TimerHandle.Start(this.OnTimerDelay, 10, 0);
                    }
                    else
                    {
                        context.DelayedEventMask |= PollMask.Readable;
                    }
                }
                else if (action == 6)
                {
                    // Fudge with the event mask.
                    context.PollHandle.Start(PollMask.Writable, this.OnPollConnection);
                    context.PollHandle.Start(PollMask.Readable, this.OnPollConnection);
                    context.EventMask = PollMask.Readable;
                }
            }

            if ((pollMask & PollMask.Writable) == PollMask.Writable 
                && !this.deplux && context.IsServerConnection)
            {
                // We have to send more bytes.
                int action = random.Next() % 7;

                if (action == 0 
                    || action == 1)
                {
                    // Send a couple of bytes.
                    var buffer = new byte[103];

                    int send = Math.Min(TransferBytes - context.Sent, buffer.Length);
                    int count = TrySend(context.Socket, buffer, send);
                    if (count < 0)
                    {
                        this.spuriousWritableWakeups++;
                    }
                    else
                    {
                        context.Sent += count;
                        this.validWritableWakeups++;
                    }
                }
                else if (action == 2 
                    || action == 3)
                {
                    // Send until EAGAIN.
                    var buffer = new byte[1234];
                    int send = Math.Min(TransferBytes - context.Sent, buffer.Length);
                    int count = TrySend(context.Socket, buffer, send);
                    if (count < 0)
                    {
                        this.spuriousWritableWakeups++;
                    }
                    else
                    {
                        context.Sent += count;
                        this.validWritableWakeups++;
                    }

                    while (context.Sent < TransferBytes)
                    {
                        send = Math.Min(TransferBytes - context.Sent, buffer.Length);
                        count = TrySend(context.Socket, buffer, send);
                        if (count < 0)
                        {
                            break;
                        }
                        else
                        {
                            context.Sent += count;
                        }
                    }
                }
                else if (action == 4)
                {
                    // Ignore.
                }
                else if (action == 5)
                {
                    // Stop sending for a while. Restart in timer callback.
                    newEvents &= ~PollMask.Writable;
                    if (!context.TimerHandle.IsActive)
                    {
                        context.DelayedEventMask = PollMask.Writable;
                        context.TimerHandle.Start(this.OnTimerDelay, 100, 0);
                    }
                    else
                    {
                        context.DelayedEventMask |= PollMask.Writable;
                    }
                }
                else if (action == 6)
                {
                    // Fudge with the event mask.
                    context.PollHandle.Start(PollMask.Readable, this.OnPollConnection);
                    context.PollHandle.Start(PollMask.Writable, this.OnPollConnection);
                    context.EventMask = PollMask.Writable;
                }
            }
            else
            {      
                // Nothing more to write. Send FIN.
                context.Socket.Shutdown(SocketShutdown.Send);
                context.SentFinished = true;
                newEvents &= ~PollMask.Writable;
            }

            if ((pollMask & PollMask.Disconnect) == PollMask.Disconnect)
            {
                context.Disconnected = true;
                newEvents &= ~PollMask.Disconnect;
            }

            if (context.SentFinished 
                || context.ReceiveFinished 
                || context.Disconnected)
            {
                if (context.SentFinished
                    || context.ReceiveFinished)
                {
                    this.DestroyConnectionContext(context);
                }
                else
                {
                    /* Poll mask changed. Call uv_poll_start again. */
                    context.EventMask = newEvents;
                    context.PollHandle.Start(newEvents, this.OnPollConnection);
                }
            }
        }

        static int TrySend(Socket socket, byte[] buffer, int length)
        {
            try
            {
                return socket.Send(buffer, 0, length, SocketFlags.None);
            }
            catch (SocketException exception)
            {
                if (!IsErrorAgain(exception))
                {
                    throw;
                }

                return -1;
            }
        }

        static int TryReceive(Socket socket, byte[] buffer)
        {
            try
            {
                return socket.Receive(buffer);
            }
            catch (SocketException exception)
            {
                if (!IsErrorAgain(exception))
                {
                    throw;
                }

                return -1;
            }
        }

        static bool IsErrorAgain(SocketException error) =>
            error.SocketErrorCode == SocketError.ConnectionAborted
            || error.SocketErrorCode == SocketError.InProgress
            || error.SocketErrorCode == SocketError.TryAgain
            || error.SocketErrorCode == SocketError.WouldBlock;

        void OnTimerDelay(Timer handle)
        {
            var context = (ConnectionContext)handle.UserToken;
            context.EventMask |= context.DelayedEventMask;
            context.PollHandle.Start(context.EventMask, this.OnPollConnection);
        }

        void StartServer()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(this.endPoint);

            // Allow reuse of the port.
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
            socket.Listen(100);

            IntPtr handle = TestHelper.GetHandle(socket);
            Poll poll = this.loop
                .CreatePoll(handle)
                .Start(PollMask.Readable, this.OnPollServer);

            this.serverContext = new ServerContext(socket, poll);
        }

        void OnPollServer(Poll handle, PollStatus status)
        {
            Socket socket = this.serverContext.Socket.Accept();
            IntPtr socketHandle = TestHelper.GetHandle(socket);

            const PollMask Mask = PollMask.Readable | PollMask.Writable | PollMask.Disconnect;
            Poll poll = this.loop
                .CreatePoll(socketHandle)
                .Start(Mask, this.OnPollConnection);

            Timer timer = this.loop.CreateTimer();
            var context = new ConnectionContext(socket, poll, true)
            {
                TimerHandle = timer,
                EventMask = Mask,
                OpenHandles = 2
            };
            timer.UserToken = context;
            poll.UserToken = context;

            this.serverContext.ConnectionCount++;
            if (this.serverContext.ConnectionCount < NumberOfClients)
            {
                return;
            }

            DestroyServerContext(this.serverContext);
        }

        static void DestroyServerContext(ServerContext context)
        {
            context.Socket.Dispose();
            context.Handle.CloseHandle(OnClose);
        }

        static void OnClose(ScheduleHandle handle) => handle.Dispose();

        void DestroyConnectionContext(ConnectionContext context)
        {
            context.Socket.Dispose();
            context.PollHandle.CloseHandle(this.OnConnectionClosed);
            context.TimerHandle.CloseHandle(this.OnConnectionClosed);
        }

        void OnConnectionClosed(ScheduleHandle handle)
        {
            var context = (ConnectionContext)handle.UserToken;
            context.OpenHandles--;

            if (context.OpenHandles > 0)
            {
                return;
            }

            if (this.deplux 
                || context.IsServerConnection)
            {
                if (context.Receive == TransferBytes)
                {
                    this.closedConnections++;
                }
            }
            else
            {
                if (context.Receive == 0)
                {
                    this.closedConnections++;
                }
            }

            if (this.deplux 
                || !context.IsServerConnection)
            {
                if (context.Sent == TransferBytes)
                {
                    this.closedConnections++;
                }
                else
                {
                    if (context.Sent == 0)
                    {
                        this.closedConnections++;
                    }
                }
            }
        }

        [Fact]
        public void BadFileDescriptorType()
        {
            if (Platform.IsDarwin)
            {
                // On macOS, the create poll actually sucessfully created.
                return;
            }

            using (FileStream file = TestHelper.OpenTempFile())
            {
                Assert.NotNull(file);
                Assert.NotNull(file.SafeFileHandle);
                IntPtr handle = file.SafeFileHandle.DangerousGetHandle();
                var error = Assert.Throws<OperationException>(() => this.loop.CreatePoll(handle));
                Assert.True(error.ErrorCode == ErrorCode.ENOTSOCK || error.ErrorCode == ErrorCode.EPERM);
            }
        }

        public void Dispose()
        {
            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
