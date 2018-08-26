namespace DotNetty.Codecs.Tests.Frame
{
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class DelimiterBasedFrameDecoderTest
    {
        [Fact]
        public void FailSlowTooLongFrameRecovery()
        {
            EmbeddedChannel ch = new EmbeddedChannel(
                    new DelimiterBasedFrameDecoder(1, true, false, Delimiters.NullDelimiter()));

            for (int i = 0; i < 2; i++)
            {
                ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { 1, 2 }));
                Assert.Throws<TooLongFrameException>(() => ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { 0 })));

                ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { (byte)'A', 0 }));
                var buf = ch.ReadInbound<IByteBuffer>();
                Assert.Equal("A", buf.ToString(Encoding.UTF8));

                buf.Release();
            }
        }

        [Fact]
        public void FailFastTooLongFrameRecovery()
        {
            EmbeddedChannel ch = new EmbeddedChannel(
                    new DelimiterBasedFrameDecoder(1, Delimiters.NullDelimiter()));

            for (int i = 0; i < 2; i++)
            {
                Assert.Throws<TooLongFrameException>(() => ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { 1, 2 })));

                ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { 0, (byte)'A', 0 }));
                var buf = ch.ReadInbound<IByteBuffer>();
                Assert.Equal("A", buf.ToString(Encoding.UTF8));

                buf.Release();
            }
        }
    }
}
