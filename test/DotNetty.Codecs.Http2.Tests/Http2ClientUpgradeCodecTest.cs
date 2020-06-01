
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using DotNetty.Codecs.Http;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class Http2ClientUpgradeCodecTest
    {
        [Fact]
        public void UpgradeToHttp2ConnectionHandler()
        {
            TestUpgrade(new Http2ConnectionHandlerBuilder()
            {
                IsServer = false,
                FrameListener = new Http2FrameAdapter(),
                GracefulShutdownTimeout = TimeSpan.Zero // EmbeddedChannel.Finish 使用 CloseSafe
            }.Build(), null);
        }

        [Fact]
        public void UpgradeToHttp2FrameCodec()
        {
            TestUpgrade(Http2FrameCodecBuilder.ForClient().Build(), null);
        }

        [Fact]
        public void UpgradeToHttp2MultiplexCodec()
        {
            TestUpgrade(Http2MultiplexCodecBuilder.ForClient(new HttpInboundHandler())
                    .WithUpgradeStreamHandler(new ChannelHandlerAdapter()).Build(), null);
        }

        [Fact]
        public void UpgradeToHttp2FrameCodecWithMultiplexer()
        {
            TestUpgrade(
                Http2FrameCodecBuilder.ForClient().Build(),
                new Http2MultiplexHandler(new HttpInboundHandler(), new HttpInboundHandler()));
        }

        private static void TestUpgrade(Http2ConnectionHandler handler, Http2MultiplexHandler multiplexer)
        {
            IFullHttpRequest request = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Options, "*");

            EmbeddedChannel channel = new EmbeddedChannel(new ChannelHandlerAdapter());
            IChannelHandlerContext ctx = channel.Pipeline.FirstContext();

            Http2ClientUpgradeCodec codec;

            if (multiplexer == null)
            {
                codec = new Http2ClientUpgradeCodec("connectionHandler", handler);
            }
            else
            {
                codec = new Http2ClientUpgradeCodec("connectionHandler", handler, multiplexer);
            }

            codec.SetUpgradeHeaders(ctx, request);
            // Flush the channel to ensure we write out all buffered data
            channel.Flush();

            codec.UpgradeTo(ctx, null);
            Assert.NotNull(channel.Pipeline.Get("connectionHandler"));

            if (multiplexer != null)
            {
                Assert.NotNull(channel.Pipeline.Get<Http2MultiplexHandler>());
            }

            Assert.True(channel.FinishAndReleaseAll());
        }

        sealed class HttpInboundHandler : ChannelHandlerAdapter
        {
            public override bool IsSharable => true;
        }
    }
}
