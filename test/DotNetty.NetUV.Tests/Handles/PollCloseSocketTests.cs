// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using DotNetty.NetUV.Handles;
    using Xunit;

    public sealed class PollCloseSocketTests : IDisposable
    {
        const int Port = 9989;
        Loop loop;
        Socket socket;
        int closeCount;
        SocketAsyncEventArgs eventArgs;

        [Fact]
        public void Run()
        {
            this.loop = new Loop();

            var endPoint = new IPEndPoint(IPAddress.Loopback, Port);

            this.eventArgs = new SocketAsyncEventArgs
            {
                RemoteEndPoint = endPoint
            };

            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            // There should be nothing listening on this
            this.socket.ConnectAsync(this.eventArgs);

            IntPtr handle = TestHelper.GetHandle(this.socket);

            this.loop
                .CreatePoll(handle)
                .Start(PollMask.Writable, this.OnPoll);

            this.loop.RunDefault();
            Assert.Equal(1, this.closeCount);
        }

        void OnPoll(Poll poll, PollStatus status)
        {
            this.eventArgs.Dispose();
            poll.Start(PollMask.Readable, this.OnPoll);
            this.socket?.Dispose();
            poll.CloseHandle(this.OnClose);
        }

        void OnClose(Poll handle)
        {
            handle.Dispose();
            this.closeCount++;
        }

        public void Dispose()
        {
            this.socket?.Dispose();

            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
