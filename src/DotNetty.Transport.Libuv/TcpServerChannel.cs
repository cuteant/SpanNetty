// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    public sealed class TcpServerChannel : TcpServerChannel<TcpServerChannel, TcpChannelFactory>
    {
        public TcpServerChannel() : base() { }
    }


    public class TcpServerChannel<TServerChannel, TChannelFactory> : NativeChannel<TServerChannel, TcpServerChannel<TServerChannel, TChannelFactory>.TcpServerChannelUnsafe>, IServerChannel
        where TServerChannel : TcpServerChannel<TServerChannel, TChannelFactory>
        where TChannelFactory : ITcpChannelFactory, new()
    {
        // ## 苦竹 屏蔽 ##
        //static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<TcpServerChannel>();
        static readonly ChannelMetadata TcpServerMetadata = new ChannelMetadata(false, 16);

        readonly TcpServerChannelConfig config;
        TcpListener tcpListener;

        private readonly TChannelFactory _channelFactory;

        public TcpServerChannel() : base(null)
        {
            this.config = new TcpServerChannelConfig(this);
            _channelFactory = new TChannelFactory();
        }

        public sealed override IChannelConfiguration Configuration => this.config;

        public sealed override ChannelMetadata Metadata => TcpServerMetadata;

        protected sealed override EndPoint LocalAddressInternal => this.tcpListener?.GetLocalEndPoint();

        protected sealed override EndPoint RemoteAddressInternal => null;

        protected sealed override void DoBind(EndPoint localAddress)
        {
            if (!this.Open)
            {
                return;
            }

            if (!this.IsInState(StateFlags.Active))
            {
                this.tcpListener.Listen((IPEndPoint)localAddress, this.Unsafe, this.config.Backlog);
                this.CacheLocalAddress();
                this.SetState(StateFlags.Active);
            }
        }

        // ## 苦竹 屏蔽 ##
        //protected override IChannelUnsafe NewUnsafe() => new TcpServerChannelUnsafe(this);

        protected sealed override void DoRegister()
        {
            Debug.Assert(this.tcpListener == null);

            var loopExecutor = (ILoopExecutor)this.EventLoop;
            Loop loop = loopExecutor.UnsafeLoop;

            this.tcpListener = new TcpListener(loop);
            this.config.SetOptions(this.tcpListener);

            var dispatcher = loopExecutor as DispatcherEventLoop;
            dispatcher?.Register(this.Unsafe);
        }

        internal override unsafe IntPtr GetLoopHandle()
        {
            if (this.tcpListener == null)
            {
                throw new InvalidOperationException("tcpListener handle not intialized");
            }

            return ((uv_stream_t*)this.tcpListener.Handle)->loop;
        }

        protected sealed override void DoClose()
        {
            if (this.TryResetState(StateFlags.Open | StateFlags.Active))
            {
                this.tcpListener?.CloseHandle();
                this.tcpListener = null;
            }
        }

        protected sealed override void DoBeginRead()
        {
            if (!this.Open)
            {
                return;
            }

            if (!this.IsInState(StateFlags.ReadScheduled))
            {
                this.SetState(StateFlags.ReadScheduled);
            }
        }

        public sealed class TcpServerChannelUnsafe : NativeChannelUnsafe, IServerNativeUnsafe
        {
            public TcpServerChannelUnsafe() { }
            //public TcpServerChannelUnsafe(TcpServerChannel channel) : base(channel)
            //{
            //}

            public override IntPtr UnsafeHandle => this.channel.tcpListener.Handle;

            void IServerNativeUnsafe.Accept(RemoteConnection connection)
            {
                var ch = this.channel;
                if (ch.EventLoop.InEventLoop)
                {
                    this.Accept(connection);
                }
                else
                {
                    ch.EventLoop.Execute(AcceptCallbackAction, this, connection);
                }
            }

            static readonly Action<object, object> AcceptCallbackAction = (u, e) => ((TcpServerChannelUnsafe)u).Accept((RemoteConnection)e);

            void Accept(RemoteConnection connection)
            {
                var ch = this.channel;
                NativeHandle client = connection.Client;

                if (connection.Error != null)
                {
                    Logger.Warn("Client connection failed.", connection.Error);
                    try
                    {
                        client?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Failed to dispose a client connection.", ex);
                    }

                    return;
                }

                if (client == null)
                {
                    return;
                }

                if (ch.EventLoop is DispatcherEventLoop dispatcher)
                {
                    dispatcher.Dispatch(client);
                }
                else
                {
                    this.Accept((Tcp)client);
                }
            }

            void IServerNativeUnsafe.Accept(NativeHandle handle)
            {
                this.Accept((Tcp)handle);
            }

            void Accept(Tcp tcp)
            {
                var ch = this.channel;
                // ## 苦竹 修改 ##
                //var tcpChannel = new TcpChannel(ch, tcp);
                var tcpChannel = ch._channelFactory.CreateChannel(ch, tcp);
                ch.Pipeline.FireChannelRead(tcpChannel);
                ch.Pipeline.FireChannelReadComplete();
            }
        }

        protected sealed override void DoDisconnect()
        {
            throw new NotSupportedException();
        }

        protected sealed override void DoScheduleRead()
        {
            throw new NotSupportedException();
        }

        protected sealed override void DoWrite(ChannelOutboundBuffer input)
        {
            throw new NotSupportedException();
        }
    }
}
