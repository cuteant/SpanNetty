namespace DotNetty.Codecs.Tests
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class ByteToMessageCodecTest
    {
        [Fact]
        public void TestSharable()
        {
            Assert.Throws<InvalidOperationException>(() => new InvalidByteToMessageCodec());
        }

        [Fact]
        public void TestForwardPendingData()
        {
            var codec = new IntegerByteToMessageCodec();
            IByteBuffer buffer = Unpooled.Buffer();
            buffer.WriteInt(1);
            buffer.WriteByte('0');

            EmbeddedChannel ch = new EmbeddedChannel(codec);
            Assert.True(ch.WriteInbound(buffer));
            ch.Pipeline.Remove(codec);
            Assert.True(ch.Finish());
            Assert.Equal(1, ch.ReadInbound<int>());

            IByteBuffer buf = ch.ReadInbound<IByteBuffer>();
            Assert.Equal(Unpooled.WrappedBuffer(new byte[] { (byte)'0' }), buf);
            buf.Release();
            Assert.Null(ch.ReadInbound<object>());
            Assert.Null(ch.ReadOutbound<object>());
        }

        sealed class IntegerByteToMessageCodec : ByteToMessageCodec<int>
        {
            protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
            {
                if (input.ReadableBytes >= 4)
                {
                    output.Add(input.ReadInt());
                }
            }

            protected override void Encode(IChannelHandlerContext context, int message, IByteBuffer output)
            {
                output.WriteInt(message);
            }
        }

        sealed class InvalidByteToMessageCodec : ByteToMessageCodec<int>
        {
            protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
            {
            }

            protected override void Encode(IChannelHandlerContext context, int message, IByteBuffer output)
            {
            }

            public override bool IsSharable => true;
        }
    }
}
