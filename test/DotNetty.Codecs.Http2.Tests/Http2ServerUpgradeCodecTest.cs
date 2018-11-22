
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class Http2ServerUpgradeCodecTest
    {
        [Fact]
        public void UpgradeToHttp2ConnectionHandler()
        {
            TestUpgrade(new Http2ConnectionHandlerBuilder()
            {
                FrameListener = new Http2FrameAdapter(),
                GracefulShutdownTimeout = TimeSpan.Zero // EmbeddedChannel.Finish 使用 CloseSafe
            }.Build());
        }

        [Fact]
        public void UpgradeToHttp2FrameCodec()
        {
            var b = new Http2FrameCodecBuilder(true);
            b.GracefulShutdownTimeout = TimeSpan.Zero;
            TestUpgrade(b.Build());
        }

        [Fact]
        public void UpgradeToHttp2MultiplexCodec()
        {
            var b = Http2MultiplexCodecBuilder.ForServer(new HttpInboundHandler());
            b.GracefulShutdownTimeout = TimeSpan.Zero;
            TestUpgrade(b.Build());
        }

        private static void TestUpgrade(Http2ConnectionHandler handler)
        {
            IFullHttpRequest request = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Options, "*");
            request.Headers.Set(HttpHeaderNames.Host, "netty.io");
            request.Headers.Set(HttpHeaderNames.Connection, "Upgrade, HTTP2-Settings");
            request.Headers.Set(HttpHeaderNames.Upgrade, "h2c");
            request.Headers.Set((AsciiString)"HTTP2-Settings", "AAMAAABkAAQAAP__");

            EmbeddedChannel channel = new EmbeddedChannel(new ChannelHandlerAdapter());
            IChannelHandlerContext ctx = channel.Pipeline.FirstContext();
            Http2ServerUpgradeCodec codec = new Http2ServerUpgradeCodec("connectionHandler", handler);
            Assert.True(codec.PrepareUpgradeResponse(ctx, request, new DefaultHttpHeaders()));
            codec.UpgradeTo(ctx, request);
            // Flush the channel to ensure we write out all buffered data
            channel.Flush();

            Assert.Same(handler, channel.Pipeline.Remove("connectionHandler"));
            Assert.Null(channel.Pipeline.Get<Http2ConnectionHandler>());
            Assert.True(channel.Finish());

            // Check that the preface was send (a.k.a the settings frame)
            var settingsBuffer = channel.ReadOutbound<IByteBuffer>();
            Assert.NotNull(settingsBuffer);
            settingsBuffer.Release();

            Assert.Null(channel.ReadOutbound<object>());
        }

        sealed class HttpInboundHandler : ChannelHandlerAdapter
        {
            public override bool IsSharable => true;
        }
    }
}
