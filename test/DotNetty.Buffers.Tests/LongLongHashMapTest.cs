namespace DotNetty.Buffers.Tests
{
    using System.Collections.Generic;
    using Xunit;

    public class LongLongHashMapTest
    {
        [Fact]
        public void ZeroPutGetAndRemove()
        {
            LongLongHashMap map = new LongLongHashMap(-1);
            Assert.Equal(-1, map.Put(0, 42));
            Assert.Equal(42, map.Get(0));
            Assert.Equal(42, map.Put(0, 24));
            Assert.Equal(24, map.Get(0));
            map.Remove(0);
            Assert.Equal(-1, map.Get(0));
        }

        [Fact]
        public void MustHandleCollisions()
        {
            LongLongHashMap map = new LongLongHashMap(-1);
            var set = new HashSet<long>();
            long v = 1;
            for (int i = 0; i < 63; i++)
            {
                Assert.Equal(-1, map.Put(v, v));
                set.Add(v);
                v <<= 1;
            }
            foreach (long value in set)
            {
                Assert.Equal(value, map.Get(value));
                Assert.Equal(value, map.Put(value, -value));
                Assert.Equal(-value, map.Get(value));
                map.Remove(value);
                Assert.Equal(-1, map.Get(value));
            }
        }
    }
}
