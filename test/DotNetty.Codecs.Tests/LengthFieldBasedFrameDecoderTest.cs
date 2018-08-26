namespace DotNetty.Codecs.Tests
{
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class LengthFieldBasedFrameDecoderTest
    {
        [Fact]
        public void DiscardTooLongFrame1()
        {
            var buf = Unpooled.Buffer();
            buf.WriteInt(32);
            for (int i = 0; i < 32; i++)
            {
                buf.WriteByte(i);
            }
            buf.WriteInt(1);
            buf.WriteByte('a');
            EmbeddedChannel channel = new EmbeddedChannel(new LengthFieldBasedFrameDecoder(16, 0, 4));
            Assert.Throws<TooLongFrameException>(() => channel.WriteInbound(buf));
            Assert.True(channel.Finish());

            var b = channel.ReadInbound<IByteBuffer>();
            Assert.Equal(5, b.ReadableBytes);
            Assert.Equal(1, b.ReadInt());
            Assert.Equal('a', (char)b.ReadByte());
            b.Release();

            Assert.Null(channel.ReadInbound<IByteBuffer>());
            channel.Finish();
        }

        [Fact]
        public void DiscardTooLongFrame2()
        {
            var buf = Unpooled.Buffer();
            buf.WriteInt(32);
            for (int i = 0; i < 32; i++)
            {
                buf.WriteByte(i);
            }
            buf.WriteInt(1);
            buf.WriteByte('a');
            EmbeddedChannel channel = new EmbeddedChannel(new LengthFieldBasedFrameDecoder(16, 0, 4));
            Assert.Throws<TooLongFrameException>(() => channel.WriteInbound(buf.ReadRetainedSlice(14)));
            Assert.True(channel.WriteInbound(buf.ReadRetainedSlice(buf.ReadableBytes)));

            Assert.True(channel.Finish());

            var b = channel.ReadInbound<IByteBuffer>();
            Assert.Equal(5, b.ReadableBytes);
            Assert.Equal(1, b.ReadInt());
            Assert.Equal('a', (char)b.ReadByte());
            b.Release();

            Assert.Null(channel.ReadInbound<IByteBuffer>());
            channel.Finish();

            buf.Release();
        }
    }
}
