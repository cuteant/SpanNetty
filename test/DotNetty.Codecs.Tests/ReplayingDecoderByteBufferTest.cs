namespace DotNetty.Codecs.Tests
{
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Common.Utilities;
    using Xunit;

    public class ReplayingDecoderByteBufferTest
    {
        [Fact]
        public void TestGetByte()
        {
            IByteBuffer buf = Unpooled.CopiedBuffer("TestBuffer", Encoding.ASCII);
            ReplayingDecoderByteBuffer buffer = new ReplayingDecoderByteBuffer(buf);

            bool error;
            int i = 0;
            try
            {
                for (; ; )
                {
                    buffer.GetByte(i);
                    i++;
                }
            }
            catch (Signal)
            {
                error = true;
            }

            Assert.True(error);
            Assert.Equal(10, i);

            buf.Release();
        }

        [Fact]
        public void TestGetBoolean()
        {
            IByteBuffer buf = Unpooled.Buffer(10);
            while (buf.IsWritable())
            {
                buf.WriteBoolean(true);
            }
            ReplayingDecoderByteBuffer buffer = new ReplayingDecoderByteBuffer(buf);

            bool error;
            int i = 0;
            try
            {
                for (; ; )
                {
                    buffer.GetBoolean(i);
                    i++;
                }
            }
            catch (Signal)
            {
                error = true;
            }

            Assert.True(error);
            Assert.Equal(10, i);

            buf.Release();
        }
    }
}
