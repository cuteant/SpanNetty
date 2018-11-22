
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
            }.Build());
        }

        [Fact]
        public void UpgradeToHttp2FrameCodec()
        {
            var b = Http2FrameCodecBuilder.ForClient();
            b.GracefulShutdownTimeout = TimeSpan.Zero;
            TestUpgrade(b.Build());
        }

        [Fact]
        public void UpgradeToHttp2MultiplexCodec()
        {
            var b = Http2MultiplexCodecBuilder.ForClient(new HttpInboundHandler())
                    .WithUpgradeStreamHandler(new ChannelHandlerAdapter());
            b.GracefulShutdownTimeout = TimeSpan.Zero;
            TestUpgrade(b.Build());
        }

        private static void TestUpgrade(Http2ConnectionHandler handler)
        {
            IFullHttpRequest request = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Options, "*");

            EmbeddedChannel channel = new EmbeddedChannel(new ChannelHandlerAdapter());
            IChannelHandlerContext ctx = channel.Pipeline.FirstContext();
            Http2ClientUpgradeCodec codec = new Http2ClientUpgradeCodec("connectionHandler", handler);
            codec.SetUpgradeHeaders(ctx, request);
            // Flush the channel to ensure we write out all buffered data
            channel.Flush();

            codec.UpgradeTo(ctx, null);
            Assert.NotNull(channel.Pipeline.Get("connectionHandler"));

            Assert.True(channel.FinishAndReleaseAll());
        }

        sealed class HttpInboundHandler : ChannelHandlerAdapter
        {
            public override bool IsSharable => true;
        }
    }
}
