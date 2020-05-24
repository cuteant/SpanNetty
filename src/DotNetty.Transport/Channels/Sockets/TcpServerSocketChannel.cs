// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    //using DotNetty.Common.Internal.Logging;

    /// <summary>
    ///     A <see cref="IServerSocketChannel" /> implementation which uses Socket-based implementation to accept new
    ///     connections.
    /// </summary>
    public partial class TcpServerSocketChannel<TServerChannel, TChannelFactory> : AbstractSocketChannel<TServerChannel, TcpServerSocketChannel<TServerChannel, TChannelFactory>.TcpServerSocketChannelUnsafe>, IServerSocketChannel
    {
        //static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<TcpServerSocketChannel>(); ## 苦竹 屏蔽 ##
        static readonly ChannelMetadata METADATA = new ChannelMetadata(false);

        static readonly Action<object, object> ReadCompletedSyncCallback = OnReadCompletedSync;

        readonly IServerSocketChannelConfiguration config;

        SocketChannelAsyncOperation<TServerChannel, TcpServerSocketChannelUnsafe> acceptOperation;

        /// <summary>
        ///     Create a new instance
        /// </summary>
        public TcpServerSocketChannel()
          : this(SocketEx.CreateSocket()) //new Socket(SocketType.Stream, ProtocolType.Tcp))
        {
        }

        /// <summary>
        ///     Create a new instance
        /// </summary>
        public TcpServerSocketChannel(AddressFamily addressFamily)
            : this(SocketEx.CreateSocket(addressFamily)) //new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp))
        {
        }

        ///// <summary>
        /////     Create a new instance using the given <see cref="Socket"/>.
        ///// </summary>
        //public TcpServerSocketChannel(Socket socket)
        //    : base(null, socket)
        //{
        //    this.config = new TcpServerSocketChannelConfig(this, socket);
        //}

        public override IChannelConfiguration Configuration => this.config;

        public override bool Active
        {
            // 待测试
            // As IsBound will continue to return true even after the channel was closed
            // we will also need to check if it is open.
            get => this.Open && this.Socket.IsBound;
        }

        public override ChannelMetadata Metadata => METADATA;

        protected override EndPoint RemoteAddressInternal => null;

        protected override EndPoint LocalAddressInternal => this.Socket.LocalEndPoint;

        SocketChannelAsyncOperation<TServerChannel, TcpServerSocketChannelUnsafe> AcceptOperation => this.acceptOperation ?? (this.acceptOperation = new SocketChannelAsyncOperation<TServerChannel, TcpServerSocketChannelUnsafe>((TServerChannel)this, false));

        //protected override IChannelUnsafe NewUnsafe() => new TcpServerSocketChannelUnsafe(this); ## 苦竹 屏蔽 ##

        protected override void DoBind(EndPoint localAddress)
        {
            this.Socket.Bind(localAddress);
            this.Socket.Listen(this.config.Backlog);
            this.SetState(StateFlags.Active);

            this.CacheLocalAddress();
        }

        protected override void DoClose()
        {
            if (this.TryResetState(StateFlags.Open | StateFlags.Active))
            {
                this.Socket.SafeClose(); // this.Socket.Dispose();
            }
        }

        protected override void ScheduleSocketRead()
        {
            var closed = false;
            var aborted = false;
            var operation = this.AcceptOperation;
            while (!closed)
            {
                try
                {
                    bool pending = this.Socket.AcceptAsync(operation);
                    if (!pending)
                    {
                        this.EventLoop.Execute(ReadCompletedSyncCallback, this.Unsafe, operation);
                    }
                    return;
                }
                catch (SocketException ex) when (ex.SocketErrorCode.IsSocketAbortError())
                {
                    this.Socket.SafeClose(); // Unbind......
                    this.Pipeline.FireExceptionCaught(ex);
                    aborted = true;
                }
                catch (SocketException ex)
                {
                    // socket exceptions here are internal to channel's operation and should not go through the pipeline
                    // especially as they have no effect on overall channel's operation
                    if (Logger.InfoEnabled) Logger.ExceptionOnAccept(ex);
                }
                catch (ObjectDisposedException)
                {
                    closed = true;
                }
                catch (Exception ex)
                {
                    this.Pipeline.FireExceptionCaught(ex);
                    closed = true;
                }
            }
            if (this.Open)
            {
                if (closed) { this.Unsafe.Close(this.Unsafe.VoidPromise()); }
                else if (aborted) { this.CloseSafe(); }
            }
        }

        static void OnReadCompletedSync(object u, object p) => ((ISocketChannelUnsafe)u).FinishRead((SocketChannelAsyncOperation<TServerChannel, TcpServerSocketChannelUnsafe>)p);

        protected override bool DoConnect(EndPoint remoteAddress, EndPoint localAddress)
        {
            throw new NotSupportedException();
        }

        protected override void DoFinishConnect(SocketChannelAsyncOperation<TServerChannel, TcpServerSocketChannelUnsafe> operation)
        {
            throw new NotSupportedException();
        }

        protected override void DoDisconnect()
        {
            throw new NotSupportedException();
        }

        protected override void DoWrite(ChannelOutboundBuffer input)
        {
            throw new NotSupportedException();
        }

        protected sealed override object FilterOutboundMessage(object msg)
        {
            throw new NotSupportedException();
        }

        public sealed class TcpServerSocketChannelUnsafe : AbstractSocketUnsafe
        {
            public TcpServerSocketChannelUnsafe() //TcpServerSocketChannel channel)
                : base() //channel)
            {
            }

            //new TcpServerSocketChannel Channel => (TcpServerSocketChannel)this.channel;

            public override void FinishRead(SocketChannelAsyncOperation<TServerChannel, TcpServerSocketChannelUnsafe> operation)
            {
                Debug.Assert(this.channel.EventLoop.InEventLoop);

                var ch = this.channel;
                if (0u >= (uint)(ch.ResetState(StateFlags.ReadScheduled) & StateFlags.Active))
                {
                    return; // read was signaled as a result of channel closure
                }
                IChannelConfiguration config = ch.Configuration;
                IChannelPipeline pipeline = ch.Pipeline;
                IRecvByteBufAllocatorHandle allocHandle = ch.Unsafe.RecvBufAllocHandle;
                allocHandle.Reset(config);

                var closed = false;
                var aborted = false;
                Exception exception = null;

                try
                {
                    Socket connectedSocket = null;
                    try
                    {
                        connectedSocket = operation.AcceptSocket;
                        operation.AcceptSocket = null;
                        operation.Validate();

                        var message = this.PrepareChannel(connectedSocket);

                        connectedSocket = null;
                        ch.ReadPending = false;
                        pipeline.FireChannelRead(message);
                        allocHandle.IncMessagesRead(1);

                        if (!config.AutoRead && !ch.ReadPending)
                        {
                            // ChannelConfig.setAutoRead(false) was called in the meantime.
                            // Completed Accept has to be processed though.
                            return;
                        }

                        while (allocHandle.ContinueReading())
                        {
                            connectedSocket = ch.Socket.Accept();
                            message = this.PrepareChannel(connectedSocket);

                            connectedSocket = null;
                            ch.ReadPending = false;
                            pipeline.FireChannelRead(message);
                            allocHandle.IncMessagesRead(1);
                        }
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode.IsSocketAbortError())
                    {
                        ch.Socket.SafeClose(); // Unbind......
                        exception = ex;
                        aborted = true;
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
                    {
                    }
                    catch (SocketException ex)
                    {
                        // socket exceptions here are internal to channel's operation and should not go through the pipeline
                        // especially as they have no effect on overall channel's operation
                        if (Logger.InfoEnabled) Logger.ExceptionOnAccept(ex);
                    }
                    catch (ObjectDisposedException)
                    {
                        closed = true;
                    }
                    catch (Exception ex)
                    {
                        exception = ex;
                    }

                    allocHandle.ReadComplete();
                    pipeline.FireChannelReadComplete();

                    if (exception is object)
                    {
                        // ServerChannel should not be closed even on SocketException because it can often continue
                        // accepting incoming connections. (e.g. too many open files)

                        pipeline.FireExceptionCaught(exception);
                    }

                    if (ch.Open)
                    {
                        if (closed) { this.Close(this.VoidPromise()); }
                        else if (aborted) { ch.CloseSafe(); }
                    }
                }
                finally
                {
                    // Check if there is a readPending which was not processed yet.
                    if (!closed && (ch.ReadPending || config.AutoRead))
                    {
                        ch.DoBeginRead();
                    }
                }
            }

            ISocketChannel PrepareChannel(Socket socket)
            {
                try
                {
                    return this.channel._channelFactory.CreateChannel(this.channel, socket); // ## 苦竹 修改 ## return new TcpSocketChannel(this.channel, socket, true);
                }
                catch (Exception ex)
                {
                    var warnEnabled = Logger.WarnEnabled;
                    if (warnEnabled) Logger.FailedToCreateANewChannelFromAcceptedSocket(ex);
                    try
                    {
                        socket.Dispose();
                    }
                    catch (Exception ex2)
                    {
                        if (warnEnabled) Logger.FailedToCloseASocketCleanly(ex2);
                    }
                    throw;
                }
            }
        }

        sealed class TcpServerSocketChannelConfig : DefaultServerSocketChannelConfig
        {
            public TcpServerSocketChannelConfig(TServerChannel channel, Socket javaSocket)
                : base(channel, javaSocket)
            {
            }

            protected override void AutoReadCleared() => ((TServerChannel)this.Channel).ReadPending = false;
        }
    }
}