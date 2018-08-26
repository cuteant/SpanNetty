namespace DotNetty.Codecs.Tests
{
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class DelimiterBasedFrameDecoderTest
    {
        [Fact]
        public void MultipleLinesStrippedDelimiters()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new DelimiterBasedFrameDecoder(8192, true,
                    Delimiters.LineDelimiter()));
            ch.WriteInbound(Unpooled.CopiedBuffer("TestLine\r\ng\r\n", Encoding.UTF8));

            var buf = ch.ReadInbound<IByteBuffer>();
            Assert.Equal("TestLine", buf.ToString(Encoding.UTF8));

            var buf2 = ch.ReadInbound<IByteBuffer>();
            Assert.Equal("g", buf2.ToString(Encoding.UTF8));
            Assert.Null(ch.ReadInbound<IByteBuffer>());
            ch.Finish();

            buf.Release();
            buf2.Release();
        }

        [Fact]
        public void IncompleteLinesStrippedDelimiters()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new DelimiterBasedFrameDecoder(8192, true,
                    Delimiters.LineDelimiter()));
            ch.WriteInbound(Unpooled.CopiedBuffer("Test", Encoding.UTF8));
            Assert.Null(ch.ReadInbound<IByteBuffer>());
            ch.WriteInbound(Unpooled.CopiedBuffer("Line\r\ng\r\n", Encoding.UTF8));

            var buf = ch.ReadInbound<IByteBuffer>();
            Assert.Equal("TestLine", buf.ToString(Encoding.UTF8));

            var buf2 = ch.ReadInbound<IByteBuffer>();
            Assert.Equal("g", buf2.ToString(Encoding.UTF8));
            Assert.Null(ch.ReadInbound<IByteBuffer>());
            ch.Finish();

            buf.Release();
            buf2.Release();
        }

        [Fact]
        public void MultipleLines()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new DelimiterBasedFrameDecoder(8192, false,
                    Delimiters.LineDelimiter()));
            ch.WriteInbound(Unpooled.CopiedBuffer("TestLine\r\ng\r\n", Encoding.UTF8));

            var buf = ch.ReadInbound<IByteBuffer>();
            Assert.Equal("TestLine\r\n", buf.ToString(Encoding.UTF8));

            var buf2 = ch.ReadInbound<IByteBuffer>();
            Assert.Equal("g\r\n", buf2.ToString(Encoding.UTF8));
            Assert.Null(ch.ReadInbound<IByteBuffer>());
            ch.Finish();

            buf.Release();
            buf2.Release();
        }

        [Fact]
        public void IncompleteLines()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new DelimiterBasedFrameDecoder(8192, false,
                    Delimiters.LineDelimiter()));
            ch.WriteInbound(Unpooled.CopiedBuffer("Test", Encoding.UTF8));
            Assert.Null(ch.ReadInbound<IByteBuffer>());
            ch.WriteInbound(Unpooled.CopiedBuffer("Line\r\ng\r\n", Encoding.UTF8));

            var buf = ch.ReadInbound<IByteBuffer>();
            Assert.Equal("TestLine\r\n", buf.ToString(Encoding.UTF8));

            var buf2 = ch.ReadInbound<IByteBuffer>();
            Assert.Equal("g\r\n", buf2.ToString(Encoding.UTF8));
            Assert.Null(ch.ReadInbound<IByteBuffer>());
            ch.Finish();

            buf.Release();
            buf2.Release();
        }

        [Fact]
        public void Decode()
        {
            EmbeddedChannel ch = new EmbeddedChannel(
                    new DelimiterBasedFrameDecoder(8192, true, Delimiters.LineDelimiter()));

            ch.WriteInbound(Unpooled.CopiedBuffer("first\r\nsecond\nthird", Encoding.ASCII));

            var buf = ch.ReadInbound<IByteBuffer>();
            Assert.Equal("first", buf.ToString(Encoding.ASCII));

            var buf2 = ch.ReadInbound<IByteBuffer>();
            Assert.Equal("second", buf2.ToString(Encoding.ASCII));
            Assert.Null(ch.ReadInbound<IByteBuffer>());
            ch.Finish();

            ReferenceCountUtil.Release(ch.ReadInbound<IByteBuffer>());

            buf.Release();
            buf2.Release();
        }
    }
}
