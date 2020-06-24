// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Net;
    using System.Net.Sockets;

    public sealed class TcpServerSocketChannel : TcpServerSocketChannel<TcpServerSocketChannel, TcpSocketChannelFactory>
    {
        public TcpServerSocketChannel() : base() { }

        /// <summary>Create a new instance</summary>
        public TcpServerSocketChannel(AddressFamily addressFamily) : base(addressFamily) { }

        /// <summary>Create a new instance using the given <see cref="Socket"/>.</summary>
        public TcpServerSocketChannel(Socket socket) : base(socket) { }
    }

    /// <summary>
    ///     A <see cref="IServerSocketChannel" /> implementation which uses Socket-based implementation to accept new
    ///     connections.
    /// </summary>
    public partial class TcpServerSocketChannel<TServerChannel, TChannelFactory> : AbstractSocketChannel<TServerChannel, TcpServerSocketChannel<TServerChannel, TChannelFactory>.TcpServerSocketChannelUnsafe>, IServerSocketChannel
        where TServerChannel : TcpServerSocketChannel<TServerChannel, TChannelFactory>
        where TChannelFactory : ITcpSocketChannelFactory, new()
    {
        private static readonly ChannelMetadata METADATA = new ChannelMetadata(false);

        private static readonly Action<object, object> ReadCompletedSyncCallback = OnReadCompletedSync;

        private readonly TChannelFactory _channelFactory;

        private readonly IServerSocketChannelConfiguration _config;

        private SocketChannelAsyncOperation<TServerChannel, TcpServerSocketChannelUnsafe> _acceptOperation;

        /// <summary>
        ///     Create a new instance
        /// </summary>
        public TcpServerSocketChannel()
          : this(SocketEx.CreateSocket())
        {
        }

        /// <summary>
        ///     Create a new instance
        /// </summary>
        public TcpServerSocketChannel(AddressFamily addressFamily)
            : this(SocketEx.CreateSocket(addressFamily))
        {
        }

        /// <summary>
        ///     Create a new instance using the given <see cref="Socket"/>.
        /// </summary>
        public TcpServerSocketChannel(Socket socket)
            : base(null, socket)
        {
            _config = new TcpServerSocketChannelConfig((TServerChannel)this, socket);
            _channelFactory = new TChannelFactory();
        }

        public override IChannelConfiguration Configuration => _config;

        public override bool IsActive
        {
            // As IsBound will continue to return true even after the channel was closed
            // we will also need to check if it is open.
            get => IsOpen && Socket.IsBound;
        }

        public override ChannelMetadata Metadata => METADATA;

        protected override EndPoint RemoteAddressInternal => null;

        protected override EndPoint LocalAddressInternal => Socket.LocalEndPoint;

        SocketChannelAsyncOperation<TServerChannel, TcpServerSocketChannelUnsafe> AcceptOperation => _acceptOperation ??= new SocketChannelAsyncOperation<TServerChannel, TcpServerSocketChannelUnsafe>((TServerChannel)this, false);

        //protected override IChannelUnsafe NewUnsafe() => new TcpServerSocketChannelUnsafe(this); ## 苦竹 屏蔽 ##

        protected override void DoBind(EndPoint localAddress)
        {
            Socket.Bind(localAddress);
            Socket.Listen(_config.Backlog);
            SetState(StateFlags.Active);

            _ = CacheLocalAddress();
        }

        protected override void DoClose()
        {
            if (TryResetState(StateFlags.Open | StateFlags.Active))
            {
                Socket.SafeClose(); // this.Socket.Dispose();
            }
        }

        protected override void ScheduleSocketRead()
        {
            var closed = false;
            var aborted = false;
            var operation = AcceptOperation;
            while (!closed)
            {
                try
                {
                    bool pending = Socket.AcceptAsync(operation);
                    if (!pending)
                    {
                        EventLoop.Execute(ReadCompletedSyncCallback, Unsafe, operation);
                    }
                    return;
                }
                catch (SocketException ex) when (ex.SocketErrorCode.IsSocketAbortError())
                {
                    Socket.SafeClose(); // Unbind......
                    _ = Pipeline.FireExceptionCaught(ex);
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
                    _ = Pipeline.FireExceptionCaught(ex);
                    closed = true;
                }
            }
            if (IsOpen)
            {
                if (closed) { Unsafe.Close(Unsafe.VoidPromise()); }
                else if (aborted) { this.CloseSafe(); }
            }
        }

        static void OnReadCompletedSync(object u, object p) => ((ISocketChannelUnsafe)u).FinishRead((SocketChannelAsyncOperation<TServerChannel, TcpServerSocketChannelUnsafe>)p);

        protected override bool DoConnect(EndPoint remoteAddress, EndPoint localAddress)
        {
            throw ThrowHelper.GetNotSupportedException();
        }

        protected override void DoFinishConnect(SocketChannelAsyncOperation<TServerChannel, TcpServerSocketChannelUnsafe> operation)
        {
            throw ThrowHelper.GetNotSupportedException();
        }

        protected override void DoDisconnect()
        {
            throw ThrowHelper.GetNotSupportedException();
        }

        protected override void DoWrite(ChannelOutboundBuffer input)
        {
            throw ThrowHelper.GetNotSupportedException();
        }

        protected sealed override object FilterOutboundMessage(object msg)
        {
            throw ThrowHelper.GetNotSupportedException();
        }
    }
}