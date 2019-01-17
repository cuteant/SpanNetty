namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System.Linq;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class WebSocketUtf8FrameValidatorTest
    {
        [Fact]
        public void CorruptedFrameExceptionInFinish()
        {
            var bts = new sbyte[] { -50 };
            AssertCorruptedFrameExceptionHandling(bts.Select(x => (byte)x).ToArray());
        }

        [Fact]
        public void CorruptedFrameExceptionInCheck()
        {
            var bts = new sbyte[] { -8, -120, -128, -128, -128 };
            AssertCorruptedFrameExceptionHandling(bts.Select(x => (byte)x).ToArray());
        }

        private void AssertCorruptedFrameExceptionHandling(byte[] data)
        {
            EmbeddedChannel channel = new EmbeddedChannel(new Utf8FrameValidator());
            Assert.Throws<CorruptedFrameException>(() => channel.WriteInbound(new TextWebSocketFrame(Unpooled.CopiedBuffer(data))));
            Assert.True(channel.Finish());
            var buf = channel.ReadOutbound<IByteBuffer>();
            Assert.NotNull(buf);
            try
            {
                Assert.False(buf.IsReadable());
            }
            finally
            {
                buf.Release();
            }
            Assert.Null(channel.ReadOutbound<object>());
        }
    }
}
