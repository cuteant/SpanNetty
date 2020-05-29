
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Moq;
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
            }.Build(), null);
        }

        [Fact]
        public void UpgradeToHttp2FrameCodec()
        {
            var b = new Http2FrameCodecBuilder(true);
            b.GracefulShutdownTimeout = TimeSpan.Zero;
            TestUpgrade(b.Build(), null);
        }

        [Fact]
        public void UpgradeToHttp2MultiplexCodec()
        {
            var b = Http2MultiplexCodecBuilder.ForServer(new HttpInboundHandler());
            b.GracefulShutdownTimeout = TimeSpan.Zero;
            TestUpgrade(b.Build(), null);
        }

        [Fact]
        public void UpgradeToHttp2FrameCodecWithMultiplexer()
        {
            TestUpgrade(new Http2FrameCodecBuilder(true).Build(),
                   new Http2MultiplexHandler(new HttpInboundHandler()));
        }

        private static void TestUpgrade(Http2ConnectionHandler handler, IChannelHandler multiplexer)
        {
            IFullHttpRequest request = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Options, "*");
            request.Headers.Set(HttpHeaderNames.Host, "netty.io");
            request.Headers.Set(HttpHeaderNames.Connection, "Upgrade, HTTP2-Settings");
            request.Headers.Set(HttpHeaderNames.Upgrade, "h2c");
            request.Headers.Set((AsciiString)"HTTP2-Settings", "AAMAAABkAAQAAP__");

            var parent = new Mock<IServerChannel>();
            EmbeddedChannel channel = new EmbeddedChannel(parent.Object, DefaultChannelId.NewInstance(), false, true, new ChannelHandlerAdapter());
            IChannelHandlerContext ctx = channel.Pipeline.FirstContext();
            Http2ServerUpgradeCodec codec;
            if (multiplexer == null)
            {
                codec = new Http2ServerUpgradeCodec(handler);
            }
            else
            {
                codec = new Http2ServerUpgradeCodec((Http2FrameCodec)handler, multiplexer);
            }
            Assert.True(codec.PrepareUpgradeResponse(ctx, request, new DefaultHttpHeaders()));
            codec.UpgradeTo(ctx, request);
            // Flush the channel to ensure we write out all buffered data
            channel.Flush();

            channel.WriteInbound(Http2CodecUtil.ConnectionPrefaceBuf());
            Http2FrameInboundWriter writer = new Http2FrameInboundWriter(channel);
            writer.WriteInboundSettings(new Http2Settings());
            writer.WriteInboundRstStream(Http2CodecUtil.HttpUpgradeStreamId, Http2Error.Cancel);

            Assert.Same(handler, channel.Pipeline.Remove<Http2ConnectionHandler>());
            Assert.Null(channel.Pipeline.Get<Http2ConnectionHandler>());
            Assert.True(channel.Finish());

            // Check that the preface was send (a.k.a the settings frame)
            var settingsBuffer = channel.ReadOutbound<IByteBuffer>();
            Assert.NotNull(settingsBuffer);
            settingsBuffer.Release();

            var buf = channel.ReadOutbound<IByteBuffer>();
            Assert.NotNull(buf);
            buf.Release();

            Assert.Null(channel.ReadOutbound<object>());
        }

        sealed class HttpInboundHandler : ChannelHandlerAdapter
        {
            public override bool IsSharable => true;
        }
    }
}
