namespace DotNetty.Transport.Tests.Channel
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using Xunit;

    [Collection("Transport Tests")]
    public class DefaultChannelPipelineTailTest : IDisposable
    {
        private readonly IEventLoopGroup _group;

        public DefaultChannelPipelineTailTest()
        {
            _group = new MultithreadEventLoopGroup(1);
        }

        public void Dispose()
        {
            _group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
        }

        [Fact]
        public async Task TestOnUnhandledInboundChannelActive()
        {
            CountdownEvent latch = new CountdownEvent(1);
            var myChannel = new MyChannel0(latch);

            Bootstrap bootstrap = new Bootstrap()
                    .ChannelFactory(() => myChannel)
                    .Group(_group)
                    .Handler(new ChannelHandlerAdapter())
                    .RemoteAddress(new IPEndPoint(IPAddress.IPv6Any, IPEndPoint.MinPort));
            var channel = await bootstrap.ConnectAsync();

            try
            {
                Assert.True(latch.Wait(TimeSpan.FromSeconds(1)));
            }
            finally
            {
                channel.CloseAsync().Ignore();
            }
        }

        class MyChannel0 : MyChannel
        {
            private readonly CountdownEvent _latch;

            public MyChannel0(CountdownEvent latch) => _latch = latch;

            public override void OnUnhandledInboundChannelActive()
            {
                _latch.Signal();
            }
        }

        [Fact]
        public async Task TestOnUnhandledInboundChannelInactive()
        {
            CountdownEvent latch = new CountdownEvent(1);
            var myChannel = new MyChannel1(latch);

            Bootstrap bootstrap = new Bootstrap()
                    .ChannelFactory(() => myChannel)
                    .Group(_group)
                    .Handler(new ChannelHandlerAdapter())
                    .RemoteAddress(new IPEndPoint(IPAddress.IPv6Any, IPEndPoint.MinPort));
            var channel = await bootstrap.ConnectAsync();

            await channel.CloseAsync();
            Assert.True(latch.Wait(TimeSpan.FromSeconds(1)));
        }

        class MyChannel1 : MyChannel
        {
            private readonly CountdownEvent _latch;

            public MyChannel1(CountdownEvent latch) => _latch = latch;

            public override void OnUnhandledInboundChannelInactive()
            {
                _latch.Signal();
            }
        }

        [Fact]
        public async Task TestOnUnhandledInboundException()
        {
            AtomicReference<Exception> causeRef = new AtomicReference<Exception>();
            CountdownEvent latch = new CountdownEvent(1);
            var myChannel = new MyChannel2(causeRef, latch);

            Bootstrap bootstrap = new Bootstrap()
                    .ChannelFactory(() => myChannel)
                    .Group(_group)
                    .Handler(new ChannelHandlerAdapter())
                    .RemoteAddress(new IPEndPoint(IPAddress.IPv6Any, IPEndPoint.MinPort));
            var channel = await bootstrap.ConnectAsync();

            try
            {
                IOException ex = new IOException("testOnUnhandledInboundException");
                channel.Pipeline.FireExceptionCaught(ex);
                Assert.True(latch.Wait(TimeSpan.FromSeconds(1)));
                Assert.Same(ex, causeRef.Value);
            }
            finally
            {
                channel.CloseAsync().Ignore();
            }
        }

        class MyChannel2 : MyChannel
        {
            private readonly AtomicReference<Exception> _causeRef;
            private readonly CountdownEvent _latch;

            public MyChannel2(AtomicReference<Exception> causeRef, CountdownEvent latch)
            {
                _causeRef = causeRef;
                _latch = latch;
            }

            public override void OnUnhandledInboundException(Exception cause)
            {
                _causeRef.Value = cause;
                _latch.Signal();
            }
        }

        [Fact]
        public async Task TestOnUnhandledInboundMessage()
        {
            CountdownEvent latch = new CountdownEvent(1);
            var myChannel = new MyChannel3(latch);

            Bootstrap bootstrap = new Bootstrap()
                    .ChannelFactory(() => myChannel)
                    .Group(_group)
                    .Handler(new ChannelHandlerAdapter())
                    .RemoteAddress(new IPEndPoint(IPAddress.IPv6Any, IPEndPoint.MinPort));
            var channel = await bootstrap.ConnectAsync();

            try
            {
                channel.Pipeline.FireChannelRead("testOnUnhandledInboundMessage");
                Assert.True(latch.Wait(TimeSpan.FromSeconds(1)));
            }
            finally
            {
                channel.CloseAsync().Ignore();
            }
        }

        class MyChannel3 : MyChannel
        {
            private readonly CountdownEvent _latch;

            public MyChannel3(CountdownEvent latch) => _latch = latch;

            public override void OnUnhandledInboundMessage(object msg)
            {
                _latch.Signal();
            }
        }

        [Fact]
        public async Task TestOnUnhandledInboundReadComplete()
        {
            CountdownEvent latch = new CountdownEvent(1);
            var myChannel = new MyChannel4(latch);

            Bootstrap bootstrap = new Bootstrap()
                    .ChannelFactory(() => myChannel)
                    .Group(_group)
                    .Handler(new ChannelHandlerAdapter())
                    .RemoteAddress(new IPEndPoint(IPAddress.IPv6Any, IPEndPoint.MinPort));
            var channel = await bootstrap.ConnectAsync();

            try
            {
                channel.Pipeline.FireChannelReadComplete();
                Assert.True(latch.Wait(TimeSpan.FromSeconds(1)));
            }
            finally
            {
                channel.CloseAsync().Ignore();
            }
        }

        class MyChannel4 : MyChannel
        {
            private readonly CountdownEvent _latch;

            public MyChannel4(CountdownEvent latch) => _latch = latch;

            public override void OnUnhandledInboundChannelReadComplete()
            {
                _latch.Signal();
            }
        }

        [Fact]
        public async Task TestOnUnhandledInboundUserEventTriggered()
        {
            CountdownEvent latch = new CountdownEvent(1);
            var myChannel = new MyChannel5(latch);

            Bootstrap bootstrap = new Bootstrap()
                    .ChannelFactory(() => myChannel)
                    .Group(_group)
                    .Handler(new ChannelHandlerAdapter())
                    .RemoteAddress(new IPEndPoint(IPAddress.IPv6Any, IPEndPoint.MinPort));
            var channel = await bootstrap.ConnectAsync();

            try
            {
                channel.Pipeline.FireUserEventTriggered("testOnUnhandledInboundUserEventTriggered");
                Assert.True(latch.Wait(TimeSpan.FromSeconds(1)));
            }
            finally
            {
                channel.CloseAsync().Ignore();
            }
        }

        class MyChannel5 : MyChannel
        {
            private readonly CountdownEvent _latch;

            public MyChannel5(CountdownEvent latch) => _latch = latch;

            public override void OnUnhandledInboundUserEventTriggered(object evt)
            {
                _latch.Signal();
            }
        }

        [Fact]
        public async Task TestOnUnhandledInboundWritabilityChanged()
        {
            CountdownEvent latch = new CountdownEvent(1);
            var myChannel = new MyChannel6(latch);

            Bootstrap bootstrap = new Bootstrap()
                    .ChannelFactory(() => myChannel)
                    .Group(_group)
                    .Handler(new ChannelHandlerAdapter())
                    .RemoteAddress(new IPEndPoint(IPAddress.IPv6Any, IPEndPoint.MinPort));
            var channel = await bootstrap.ConnectAsync();

            try
            {
                channel.Pipeline.FireChannelWritabilityChanged();
                Assert.True(latch.Wait(TimeSpan.FromSeconds(1)));
            }
            finally
            {
                channel.CloseAsync().Ignore();
            }
        }

        class MyChannel6 : MyChannel
        {
            private readonly CountdownEvent _latch;

            public MyChannel6(CountdownEvent latch) => _latch = latch;

            public override void OnUnhandledChannelWritabilityChanged()
            {
                _latch.Signal();
            }
        }

        class MyChannel : AbstractChannel<MyChannel, MyChannel.MyUnsafe>
        {
            private static readonly ChannelMetadata TEST_METADATA = new ChannelMetadata(false);

            private readonly IChannelConfiguration _config;

            private bool _active;
            private bool _closed;

            public MyChannel()
                : base(null)
            {
                _config = new DefaultChannelConfiguration(this);
            }

            protected override DefaultChannelPipeline NewChannelPipeline()
            {
                return new MyChannelPipeline(this);
            }

            public override IChannelConfiguration Configuration => _config;

            public override bool IsOpen => !_closed;

            public override bool IsActive => IsOpen && _active;

            public override ChannelMetadata Metadata => TEST_METADATA;

            protected override EndPoint LocalAddressInternal => null;

            protected override EndPoint RemoteAddressInternal => null;

            protected override void DoBeginRead()
            {
            }

            protected override void DoBind(EndPoint localAddress)
            {
            }

            protected override void DoClose()
            {
                _closed = true;
            }

            protected override void DoDisconnect()
            {
            }

            protected override void DoWrite(ChannelOutboundBuffer input)
            {
                throw new IOException();
            }

            protected override bool IsCompatible(IEventLoop eventLoop) => true;

            public virtual void OnUnhandledInboundChannelActive()
            {
            }

            public virtual void OnUnhandledInboundChannelInactive()
            {
            }

            public virtual void OnUnhandledInboundException(Exception cause)
            {
            }

            public virtual void OnUnhandledInboundMessage(object msg)
            {
            }

            public virtual void OnUnhandledInboundChannelReadComplete()
            {
            }

            public virtual void OnUnhandledInboundUserEventTriggered(object evt)
            {
            }

            public virtual void OnUnhandledChannelWritabilityChanged()
            {
            }

            public class MyUnsafe : AbstractUnsafe
            {
                public override Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
                {
                    if (!EnsureOpen()) { return TaskUtil.Completed; }


                    if (!_channel._active)
                    {
                        _channel._active = true;
                        _channel.Pipeline.FireChannelActive();
                    }

                    return TaskUtil.Completed;
                }
            }
        }

        class MyChannelPipeline : DefaultChannelPipeline
        {
            private readonly MyChannel _channel;

            public MyChannelPipeline(MyChannel channel)
                : base(channel)
            {
                _channel = channel;
            }

            protected override void OnUnhandledInboundChannelActive()
            {
                _channel.OnUnhandledInboundChannelActive();
            }

            protected override void OnUnhandledInboundChannelInactive()
            {
                _channel.OnUnhandledInboundChannelInactive();
            }

            protected override void OnUnhandledInboundException(Exception cause)
            {
                _channel.OnUnhandledInboundException(cause);
            }

            protected override void OnUnhandledInboundMessage(object msg)
            {
                _channel.OnUnhandledInboundMessage(msg);
            }

            protected override void OnUnhandledInboundChannelReadComplete()
            {
                _channel.OnUnhandledInboundChannelReadComplete();
            }

            protected override void OnUnhandledInboundUserEventTriggered(object evt)
            {
                _channel.OnUnhandledInboundUserEventTriggered(evt);
            }

            protected override void OnUnhandledChannelWritabilityChanged()
            {
                _channel.OnUnhandledChannelWritabilityChanged();
            }
        }
    }
}