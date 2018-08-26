namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using DotNetty.Codecs.Http.WebSockets;
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
    }
}
