namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Internal;
    using Xunit;

    public class WebSocketUtilTest
    {
        // how many times do we want to run each random variable checker
        private const int NUM_ITERATIONS = 1000;

        private static void AssertRandomWithinBoundaries(int min, int max)
        {
            int r = WebSocketUtil.RandomNumber(min, max);
            Assert.True(min <= r && r <= max);
        }

        [Fact]
        public void RandomNumberGenerator()
        {
            int iteration = 0;
            while (++iteration < NUM_ITERATIONS)
            {
                AssertRandomWithinBoundaries(0, 1);
                AssertRandomWithinBoundaries(0, 1);
                AssertRandomWithinBoundaries(-1, 1);
                AssertRandomWithinBoundaries(-1, 0);
            }
        }

        [Fact]
        public void Base64()
        {
            string base64 = WebSocketUtil.Base64String(EmptyArrays.EmptyBytes);
            Assert.NotNull(base64);
            Assert.Equal(0, base64.Length);

            base64 = WebSocketUtil.Base64String(Encoding.UTF8.GetBytes("foo"));
            Assert.Equal("Zm9v", base64);

            base64 = WebSocketUtil.Base64String(Encoding.UTF8.GetBytes("bar"));
            IByteBuffer src = Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes(base64));
            try
            {
                IByteBuffer dst = DotNetty.Codecs.Base64.Base64.Decode(src);
                try
                {
                    Assert.Equal("bar", Encoding.UTF8.GetString(ByteBufferUtil.GetBytes(dst)));
                }
                finally
                {
                    dst.Release();
                }
            }
            finally
            {
                src.Release();
            }
        }
    }
}
