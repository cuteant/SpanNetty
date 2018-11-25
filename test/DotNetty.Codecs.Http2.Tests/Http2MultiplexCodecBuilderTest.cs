
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Threading;
    using DotNetty.Buffers;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Local;
    using Xunit;

    [Collection("BootstrapEnv")]
    public class Http2MultiplexCodecBuilderTest : IClassFixture<EventLoopGroupFixture>, IDisposable
    {
        private readonly EventLoopGroupFixture fixture;

        private IChannel serverChannel;
        private volatile IChannel serverConnectedChannel;
        private IChannel clientChannel;
        private LastInboundHandler serverLastInboundHandler;

        public Http2MultiplexCodecBuilderTest(EventLoopGroupFixture fixture)
        {
            this.fixture = fixture;

            var serverChannelLatch = new CountdownEvent(1);
            LocalAddress serverAddress = new LocalAddress(this.GetType().Name);
            this.serverLastInboundHandler = new SharableLastInboundHandler();
            ServerBootstrap sb = new ServerBootstrap()
                .Channel<LocalServerChannel>()
                .Group(fixture.Group)
                .ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    this.serverConnectedChannel = ch;
                    ch.Pipeline.AddLast(new Http2MultiplexCodecBuilder(true, new ActionChannelInitializer<IChannel>(ch0 =>
                    {
                        ch0.Pipeline.AddLast(new TestServerChannelHandler());
                        ch0.Pipeline.AddLast(this.serverLastInboundHandler);
                    })).Build());
                    serverChannelLatch.SafeSignal();
                }));
            this.serverChannel = sb.BindAsync(serverAddress).GetAwaiter().GetResult();
            Bootstrap cb = new Bootstrap()
                .Channel<LocalChannel>()
                .Group(fixture.Group)
                .Handler(new Http2MultiplexCodecBuilder(false, new ActionChannelInitializer<IChannel>(ch =>
                {
                    Assert.False(true, "Should not be called for outbound streams");
                })).Build());
            this.clientChannel = cb.ConnectAsync(serverAddress).GetAwaiter().GetResult();
            Assert.True(serverChannelLatch.Wait(TimeSpan.FromSeconds(5)));
        }

        public void Dispose()
        {
            if (this.clientChannel != null)
            {
                this.clientChannel.CloseAsync().GetAwaiter().GetResult();
                this.clientChannel = null;
            }
            if (this.serverChannel != null)
            {
                this.serverChannel.CloseAsync().GetAwaiter().GetResult();
                this.serverChannel = null;
            }
            var serverConnectedChannel = this.serverConnectedChannel;
            if (serverConnectedChannel != null)
            {
                serverConnectedChannel.CloseAsync().GetAwaiter().GetResult();
                this.serverConnectedChannel = null;
            }
        }

        [Fact]
        public void MultipleOutboundStreams()
        {
            IHttp2StreamChannel childChannel1 = this.NewOutboundStream(new TestChannelInitializer());
            Assert.True(childChannel1.Active);
            Assert.False(Http2CodecUtil.IsStreamIdValid(childChannel1.Stream.Id));
            IHttp2StreamChannel childChannel2 = this.NewOutboundStream(new TestChannelInitializer());
            Assert.True(childChannel2.Active);
            Assert.False(Http2CodecUtil.IsStreamIdValid(childChannel2.Stream.Id));

            IHttp2Headers headers1 = new DefaultHttp2Headers();
            IHttp2Headers headers2 = new DefaultHttp2Headers();
            // Test that streams can be made active (headers sent) in different order than the corresponding channels
            // have been created.
            childChannel2.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(headers2));
            childChannel1.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(headers1));

            IHttp2HeadersFrame headersFrame2 = serverLastInboundHandler.BlockingReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(headersFrame2);
            Assert.Equal(3, headersFrame2.Stream.Id);

            IHttp2HeadersFrame headersFrame1 = serverLastInboundHandler.BlockingReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(headersFrame1);
            Assert.Equal(5, headersFrame1.Stream.Id);

            Assert.Equal(3, childChannel2.Stream.Id);
            Assert.Equal(5, childChannel1.Stream.Id);

            childChannel1.CloseAsync();
            childChannel2.CloseAsync();

            serverLastInboundHandler.CheckException();
        }

        [Fact]
        public void CreateOutboundStream()
        {
            IChannel childChannel = this.NewOutboundStream(new TestChannelInitializer());
            Assert.True(childChannel.Registered);
            Assert.True(childChannel.Active);

            IHttp2Headers headers = new DefaultHttp2Headers();
            childChannel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(headers));
            IByteBuffer data = Unpooled.Buffer(100).WriteZero(100);
            childChannel.WriteAndFlushAsync(new DefaultHttp2DataFrame(data, true));

            IHttp2HeadersFrame headersFrame = serverLastInboundHandler.BlockingReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(headersFrame);
            Assert.Equal(3, headersFrame.Stream.Id);
            Assert.Equal(headers, headersFrame.Headers);

            IHttp2DataFrame dataFrame = serverLastInboundHandler.BlockingReadInbound<IHttp2DataFrame>();
            Assert.NotNull(dataFrame);
            Assert.Equal(3, dataFrame.Stream.Id);
            AssertEx.Equal(data.ResetReaderIndex(), dataFrame.Content);
            Assert.True(dataFrame.IsEndStream);
            dataFrame.Release();

            childChannel.CloseAsync();

            IHttp2ResetFrame rstFrame = serverLastInboundHandler.BlockingReadInbound<IHttp2ResetFrame>();
            Assert.NotNull(rstFrame);
            Assert.Equal(3, rstFrame.Stream.Id);

            serverLastInboundHandler.CheckException();
        }

        [Fact]
        public void TestSharableCheck()
        {
            Assert.NotNull(Http2MultiplexCodecBuilder.ForServer(new SharableChannelHandler1()));
            Assert.NotNull(Http2MultiplexCodecBuilder.ForServer(new SharableChannelHandler2()));
        }

        [Fact]
        public void TestUnsharableHandler()
        {
            Assert.Throws<ArgumentException>(() => Http2MultiplexCodecBuilder.ForServer(new UnsharableChannelHandler()));
        }

        private IHttp2StreamChannel NewOutboundStream(IChannelHandler handler)
        {
            return new Http2StreamChannelBootstrap(clientChannel).Handler(handler).OpenAsync().GetAwaiter().GetResult();
        }

        sealed class TestServerChannelHandler : ChannelHandlerAdapter
        {
            private bool writable;

            public override void ChannelActive(IChannelHandlerContext context)
            {
                this.writable |= context.Channel.IsWritable;
                base.ChannelActive(context);
            }

            public override void ChannelWritabilityChanged(IChannelHandlerContext context)
            {
                this.writable |= context.Channel.IsWritable;
                base.ChannelWritabilityChanged(context);
            }

            public override void ChannelInactive(IChannelHandlerContext context)
            {
                Assert.True(this.writable);
                base.ChannelInactive(context);
            }
        }

        sealed class SharableLastInboundHandler : LastInboundHandler
        {
            public override bool IsSharable => true;

            public override void ChannelActive(IChannelHandlerContext context)
            {
                context.FireChannelActive();
            }

            public override void ChannelInactive(IChannelHandlerContext context)
            {
                context.FireChannelInactive();
            }
        }

        sealed class SharableChannelHandler1 : ChannelHandlerAdapter
        {
            public override bool IsSharable => true;
        }

        sealed class SharableChannelHandler2 : ChannelHandlerAdapter
        {
            public override bool IsSharable => true;
        }

        sealed class UnsharableChannelHandler : ChannelHandlerAdapter
        {
            public override bool IsSharable => false;
        }
    }
}
