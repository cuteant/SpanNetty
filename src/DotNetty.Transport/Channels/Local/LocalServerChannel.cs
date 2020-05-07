// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Local
{
    using System;
    using System.Net;
    using System.Threading;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;

    /// <summary>
    /// A <see cref="IServerChannel"/> for the local transport which allows in VM communication.
    /// </summary>
    public class LocalServerChannel : AbstractServerChannel<LocalServerChannel, LocalServerChannel.LocalServerUnsafe>
    {
        readonly IQueue<object> inboundBuffer = PlatformDependent.NewMpscQueue<object>();

        int state; // 0 - open, 1 - active, 2 - closed
        LocalAddress localAddress;
        int acceptInProgress;

        readonly Action shutdownHook;

        public LocalServerChannel()
        {
            this.shutdownHook = () => this.Unsafe.Close(this.Unsafe.VoidPromise());
            var config = new DefaultChannelConfiguration(this);
            config.Allocator = new PreferHeapByteBufAllocator(config.Allocator);
            this.Configuration = config;
        }

        public override IChannelConfiguration Configuration { get; }

        public override bool Open => Volatile.Read(ref this.state) < 2;

        public override bool Active => Volatile.Read(ref this.state) == 1;

        protected override EndPoint LocalAddressInternal => Volatile.Read(ref this.localAddress);

        protected override bool IsCompatible(IEventLoop eventLoop) => eventLoop is SingleThreadEventLoop;

        public new LocalAddress LocalAddress => (LocalAddress)base.LocalAddress;

        public new LocalAddress RemoteAddress => (LocalAddress)base.RemoteAddress;

        protected override void DoRegister() =>
            ((SingleThreadEventExecutor)this.EventLoop).AddShutdownHook(this.shutdownHook);

        protected override void DoBind(EndPoint localAddress)
        {
            Interlocked.Exchange(ref this.localAddress, LocalChannelRegistry.Register(this, Volatile.Read(ref this.localAddress), localAddress));
            Interlocked.Exchange(ref this.state, 1);
        }

        protected override void DoClose()
        {
            if (Volatile.Read(ref this.state) <= 1)
            {
                // Update all internal state before the closeFuture is notified.
                var thisLocalAddr = Volatile.Read(ref this.localAddress);
                if (thisLocalAddr != null)
                {
                    LocalChannelRegistry.Unregister(thisLocalAddr);
                    Interlocked.Exchange(ref this.localAddress, null);
                }
                Interlocked.Exchange(ref this.state, 2);
            }
        }

        protected override void DoDeregister()
            => ((SingleThreadEventExecutor)this.EventLoop).RemoveShutdownHook(this.shutdownHook);

        protected override void DoBeginRead()
        {
            if (SharedConstants.True == Volatile.Read(ref this.acceptInProgress))
            {
                return;
            }

            if (this.inboundBuffer.IsEmpty)
            {
                Interlocked.Exchange(ref this.acceptInProgress, SharedConstants.True);
                return;
            }

            this.ReadInbound();
        }

        public LocalChannel Serve(LocalChannel peer)
        {
            LocalChannel child = this.NewLocalChannel(peer);
            if (this.EventLoop.InEventLoop)
            {
                this.Serve0(child);
            }
            else
            {
                this.EventLoop.Execute(() => this.Serve0(child));
            }
            return child;
        }

        private void ReadInbound()
        {
            // TODO Respect MAX_MESSAGES_PER_READ in LocalChannel / LocalServerChannel.
            //var handle = this.Unsafe.RecvBufAllocHandle;
            //handle.Reset(this.Configuration);
            var pipeline = this.Pipeline;
            var inboundBuffer = this.inboundBuffer;

            //do
            //{
            //    if (!inboundBuffer.TryDequeue(out object received)) { break; }
            //    pipeline.FireChannelRead(received);
            //} while (handle.ContinueReading());
            while (inboundBuffer.TryDequeue(out object m))
            {
                pipeline.FireChannelRead(m);
            }

            pipeline.FireChannelReadComplete();
        }

        /// <summary>
        /// A factory method for <see cref="LocalChannel"/>s. Users may override it to create custom instances of <see cref="LocalChannel"/>s.
        /// </summary>
        /// <param name="peer">An existing <see cref="LocalChannel"/> that will act as a peer for the new channel.</param>
        /// <returns>The newly created <see cref="LocalChannel"/> instance.</returns>
        protected LocalChannel NewLocalChannel(LocalChannel peer) => new LocalChannel(this, peer);

        void Serve0(LocalChannel child)
        {
            this.inboundBuffer.TryEnqueue(child);

            if (SharedConstants.True == Volatile.Read(ref this.acceptInProgress))
            {
                Interlocked.Exchange(ref this.acceptInProgress, SharedConstants.False);
                this.ReadInbound();
            }
        }

        public class LocalServerUnsafe : DefaultServerUnsafe { }
    }
}
