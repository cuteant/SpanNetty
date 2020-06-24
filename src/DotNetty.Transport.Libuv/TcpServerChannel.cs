// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;
    using TcpListener = Native.TcpListener;

    public sealed class TcpServerChannel : TcpServerChannel<TcpServerChannel, TcpChannelFactory>
    {
        public TcpServerChannel() : base() { }
    }

    public partial class TcpServerChannel<TServerChannel, TChannelFactory> : NativeChannel<TServerChannel, TcpServerChannel<TServerChannel, TChannelFactory>.TcpServerChannelUnsafe>, IServerChannel
        where TServerChannel : TcpServerChannel<TServerChannel, TChannelFactory>
        where TChannelFactory : ITcpChannelFactory, new()
    {
        private static readonly ChannelMetadata TcpServerMetadata = new ChannelMetadata(false);

        private readonly TChannelFactory _channelFactory;

        private readonly TcpServerChannelConfig _config;
        private TcpListener _tcpListener;
        private bool _isBound;

        public TcpServerChannel() : base(null)
        {
            _config = new TcpServerChannelConfig(this);
            _channelFactory = new TChannelFactory();
        }

        public override IChannelConfiguration Configuration => _config;

        public override ChannelMetadata Metadata => TcpServerMetadata;

        protected override EndPoint LocalAddressInternal => _tcpListener?.GetLocalEndPoint();

        protected override EndPoint RemoteAddressInternal => null;

        internal override bool IsBound => _isBound;

        protected override void DoBind(EndPoint localAddress)
        {
            if (!IsOpen)
            {
                return;
            }

            Debug.Assert(EventLoop.InEventLoop);
            if (!IsInState(StateFlags.Active))
            {
                var address = (IPEndPoint)localAddress;
                var loopExecutor = (LoopExecutor)EventLoop;

                uint flags = PlatformApi.GetAddressFamily(address.AddressFamily);
                _tcpListener = new TcpListener(loopExecutor.UnsafeLoop, flags);

                // Apply the configuration right after the tcp handle is created
                // because SO_REUSEPORT cannot be configured after bind
                _config.Apply();

                _tcpListener.Bind(address);
                _isBound = true;

                _tcpListener.Listen(Unsafe, _config.Backlog);

                _ = CacheLocalAddress();
                SetState(StateFlags.Active);
            }
        }

        //protected override IChannelUnsafe NewUnsafe() => new TcpServerChannelUnsafe(this); ## 苦竹 屏蔽 ##

        internal override NativeHandle GetHandle()
        {
            if (_tcpListener is null)
            {
                ThrowHelper.ThrowInvalidOperationException_HandleNotInit();
            }

            return _tcpListener;
        }

        protected override void DoClose()
        {
            if (TryResetState(StateFlags.Open | StateFlags.Active))
            {
                _tcpListener?.CloseHandle();
                _tcpListener = null;
            }
        }

        protected override void DoBeginRead()
        {
            if (!IsOpen)
            {
                return;
            }

            if (!IsInState(StateFlags.ReadScheduled))
            {
                if (EventLoop is DispatcherEventLoop dispatcher)
                {
                    // Set up the dispatcher callback, all dispatched handles 
                    // need to call Accept on this channel to setup pipeline
                    dispatcher.Register(Unsafe);
                }
                SetState(StateFlags.ReadScheduled);
            }
        }


        protected override void DoDisconnect() => throw new NotSupportedException($"{nameof(TcpServerChannel)}");

        protected override void DoStopRead() => throw new NotSupportedException($"{nameof(TcpServerChannel)}");

        protected override void DoWrite(ChannelOutboundBuffer input) => throw new NotSupportedException($"{nameof(TcpServerChannel)}");
    }
}
