namespace DotNetty.Codecs.Tests
{
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class LineBasedFrameDecoderTest
    {
        [Fact]
        public void DecodeWithStrip()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new LineBasedFrameDecoder(8192, true, false));

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

        [Fact]
        public void DecodeWithoutStrip()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new LineBasedFrameDecoder(8192, false, false));

            ch.WriteInbound(Unpooled.CopiedBuffer("first\r\nsecond\nthird", Encoding.ASCII));

            var buf = ch.ReadInbound<IByteBuffer>();
            Assert.Equal("first\r\n", buf.ToString(Encoding.ASCII));

            var buf2 = ch.ReadInbound<IByteBuffer>();
            Assert.Equal("second\n", buf2.ToString(Encoding.ASCII));
            Assert.Null(ch.ReadInbound<IByteBuffer>());
            ch.Finish();
            ReferenceCountUtil.Release(ch.ReadInbound<IByteBuffer>());

            buf.Release();
            buf2.Release();
        }

        [Fact]
        public void TooLongLine1()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new LineBasedFrameDecoder(16, false, false));

            Assert.Throws<TooLongFrameException>(() => ch.WriteInbound(Unpooled.CopiedBuffer("12345678901234567890\r\nfirst\nsecond", Encoding.ASCII)));

            var buf = ch.ReadInbound<IByteBuffer>();
            var buf2 = Unpooled.CopiedBuffer("first\n", Encoding.ASCII);
            AssertEx.Equal(buf, buf2);
            Assert.False(ch.Finish());

            buf.Release();
            buf2.Release();
        }

        [Fact]
        public void TooLongLine2()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new LineBasedFrameDecoder(16, false, false));

            Assert.False(ch.WriteInbound(Unpooled.CopiedBuffer("12345678901234567", Encoding.ASCII)));
            Assert.Throws<TooLongFrameException>(() => ch.WriteInbound(Unpooled.CopiedBuffer("890\r\nfirst\r\n", Encoding.ASCII)));

            var buf = ch.ReadInbound<IByteBuffer>();
            var buf2 = Unpooled.CopiedBuffer("first\r\n", Encoding.ASCII);
            AssertEx.Equal(buf, buf2);
            Assert.False(ch.Finish());

            buf.Release();
            buf2.Release();
        }

        [Fact]
        public void TooLongLineWithFailFast()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new LineBasedFrameDecoder(16, false, true));

            Assert.Throws<TooLongFrameException>(() => ch.WriteInbound(Unpooled.CopiedBuffer("12345678901234567", Encoding.ASCII)));

            Assert.False(ch.WriteInbound(Unpooled.CopiedBuffer("890", Encoding.ASCII)));
            Assert.True(ch.WriteInbound(Unpooled.CopiedBuffer("123\r\nfirst\r\n", Encoding.ASCII)));

            var buf = ch.ReadInbound<IByteBuffer>();
            var buf2 = Unpooled.CopiedBuffer("first\r\n", Encoding.ASCII);
            AssertEx.Equal(buf, buf2);
            Assert.False(ch.Finish());

            buf.Release();
            buf2.Release();
        }

        [Fact]
        public void DecodeSplitsCorrectly()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new LineBasedFrameDecoder(8192, false, false));

            Assert.True(ch.WriteInbound(Unpooled.CopiedBuffer("line\r\n.\r\n", Encoding.ASCII)));

            var buf = ch.ReadInbound<IByteBuffer>();
            Assert.Equal("line\r\n", buf.ToString(Encoding.ASCII));

            var buf2 = ch.ReadInbound<IByteBuffer>();
            Assert.Equal(".\r\n", buf2.ToString(Encoding.ASCII));
            Assert.False(ch.FinishAndReleaseAll());

            buf.Release();
            buf2.Release();
        }

        [Fact]
        public void FragmentedDecode()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new LineBasedFrameDecoder(8192, false, false));

            Assert.False(ch.WriteInbound(Unpooled.CopiedBuffer("huu", Encoding.ASCII)));
            Assert.Null(ch.ReadInbound<IByteBuffer>());

            Assert.False(ch.WriteInbound(Unpooled.CopiedBuffer("haa\r", Encoding.ASCII)));
            Assert.Null(ch.ReadInbound<IByteBuffer>());

            Assert.True(ch.WriteInbound(Unpooled.CopiedBuffer("\nhuuhaa\r\n", Encoding.ASCII)));
            var buf = ch.ReadInbound<IByteBuffer>();
            Assert.Equal("huuhaa\r\n", buf.ToString(Encoding.ASCII));

            var buf2 = ch.ReadInbound<IByteBuffer>();
            Assert.Equal("huuhaa\r\n", buf2.ToString(Encoding.ASCII));
            Assert.False(ch.FinishAndReleaseAll());

            buf.Release();
            buf2.Release();
        }

        [Fact]
        public void EmptyLine()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new LineBasedFrameDecoder(8192, true, false));

            Assert.True(ch.WriteInbound(Unpooled.CopiedBuffer("\nabcna\r\n", Encoding.ASCII)));

            var buf = ch.ReadInbound<IByteBuffer>();
            Assert.Equal("", buf.ToString(Encoding.ASCII));

            var buf2 = ch.ReadInbound<IByteBuffer>();
            Assert.Equal("abcna", buf2.ToString(Encoding.ASCII));

            Assert.False(ch.FinishAndReleaseAll());

            buf.Release();
            buf2.Release();
        }

        [Fact]
        public void NotFailFast()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new LineBasedFrameDecoder(2, false, false));
            Assert.False(ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { 0, 1, 2 })));
            Assert.False(ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { 3, 4 })));
            Assert.Throws<TooLongFrameException>(() => ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { (byte)'\n' })));

            Assert.False(ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { (byte)'5' })));
            Assert.True(ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { (byte)'\n' })));

            var expected = Unpooled.WrappedBuffer(new byte[] { (byte)'5', (byte)'\n' });
            var buffer = ch.ReadInbound<IByteBuffer>();
            AssertEx.Equal(expected, buffer);
            expected.Release();
            buffer.Release();

            Assert.False(ch.Finish());
        }
    }
}
