
namespace DotNetty.Common.Tests.Internal
{
    using DotNetty.Common.Internal;
    using Xunit;

    public class MathUtilTest
    {
        [Fact]
        public void FindNextPositivePowerOfTwo()
        {
            Assert.Equal(1, MathUtil.FindNextPositivePowerOfTwo(0));
            Assert.Equal(1, MathUtil.FindNextPositivePowerOfTwo(1));
            Assert.Equal(1024, MathUtil.FindNextPositivePowerOfTwo(1000));
            Assert.Equal(1024, MathUtil.FindNextPositivePowerOfTwo(1023));
            Assert.Equal(2048, MathUtil.FindNextPositivePowerOfTwo(2048));
            Assert.Equal(1 << 30, MathUtil.FindNextPositivePowerOfTwo((1 << 30) - 1));
            Assert.Equal(1, MathUtil.FindNextPositivePowerOfTwo(-1));
            Assert.Equal(1, MathUtil.FindNextPositivePowerOfTwo(-10000));
        }

        [Fact]
        public void SafeFindNextPositivePowerOfTwo()
        {
            Assert.Equal(1, MathUtil.SafeFindNextPositivePowerOfTwo(0));
            Assert.Equal(1, MathUtil.SafeFindNextPositivePowerOfTwo(1));
            Assert.Equal(1024, MathUtil.SafeFindNextPositivePowerOfTwo(1000));
            Assert.Equal(1024, MathUtil.SafeFindNextPositivePowerOfTwo(1023));
            Assert.Equal(2048, MathUtil.SafeFindNextPositivePowerOfTwo(2048));
            Assert.Equal(1 << 30, MathUtil.SafeFindNextPositivePowerOfTwo((1 << 30) - 1));
            Assert.Equal(1, MathUtil.SafeFindNextPositivePowerOfTwo(-1));
            Assert.Equal(1, MathUtil.SafeFindNextPositivePowerOfTwo(-10000));
            Assert.Equal(1 << 30, MathUtil.SafeFindNextPositivePowerOfTwo(int.MaxValue));
            Assert.Equal(1 << 30, MathUtil.SafeFindNextPositivePowerOfTwo((1 << 30) + 1));
            //Assert.Equal(1, MathUtil.SafeFindNextPositivePowerOfTwo(int.MinValue)); // 采用 (uint)(value - 1) > SharedConstants.TooBigOrNegative 判断 <= 0
            Assert.Equal(1, MathUtil.SafeFindNextPositivePowerOfTwo(int.MinValue + 1));
        }

        [Fact]
        public void IsOutOfBounds()
        {
            Assert.False(MathUtil.IsOutOfBounds(0, 0, 0));
            Assert.False(MathUtil.IsOutOfBounds(0, 0, 1));
            Assert.False(MathUtil.IsOutOfBounds(0, 1, 1));
            Assert.True(MathUtil.IsOutOfBounds(1, 1, 1));
            Assert.True(MathUtil.IsOutOfBounds(int.MaxValue, 1, 1));
            Assert.True(MathUtil.IsOutOfBounds(int.MaxValue, int.MaxValue, 1));
            Assert.True(MathUtil.IsOutOfBounds(int.MaxValue, int.MaxValue, int.MaxValue));
            Assert.False(MathUtil.IsOutOfBounds(0, int.MaxValue, int.MaxValue));
            Assert.False(MathUtil.IsOutOfBounds(0, int.MaxValue - 1, int.MaxValue));
            Assert.True(MathUtil.IsOutOfBounds(0, int.MaxValue, int.MaxValue - 1));
            Assert.False(MathUtil.IsOutOfBounds(int.MaxValue - 1, 1, int.MaxValue));
            Assert.True(MathUtil.IsOutOfBounds(int.MaxValue - 1, 1, int.MaxValue - 1));
            Assert.True(MathUtil.IsOutOfBounds(int.MaxValue - 1, 2, int.MaxValue));
            Assert.True(MathUtil.IsOutOfBounds(1, int.MaxValue, int.MaxValue));
        }

        [Fact]
        public void Compare()
        {
            Assert.Equal(-1, MathUtil.Compare(0, 1));
            Assert.Equal(-1, MathUtil.Compare(0L, 1L));
            Assert.Equal(-1, MathUtil.Compare(0, int.MaxValue));
            Assert.Equal(-1, MathUtil.Compare(0L, long.MaxValue));
            Assert.Equal(0, MathUtil.Compare(0, 0));
            Assert.Equal(0, MathUtil.Compare(0L, 0L));
            Assert.Equal(0, MathUtil.Compare(int.MinValue, int.MinValue));
            Assert.Equal(0, MathUtil.Compare(long.MinValue, long.MinValue));
            Assert.Equal(1, MathUtil.Compare(int.MaxValue, 0));
            Assert.Equal(1, MathUtil.Compare(int.MaxValue, int.MaxValue - 1));
            Assert.Equal(1, MathUtil.Compare(long.MaxValue, 0L));
            Assert.Equal(1, MathUtil.Compare(long.MaxValue, long.MaxValue - 1));
        }
    }
}
