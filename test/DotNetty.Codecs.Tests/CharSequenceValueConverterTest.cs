namespace DotNetty.Codecs.Tests
{
    using System;
    using Xunit;

    public class CharSequenceValueConverterTest
    {
        private readonly CharSequenceValueConverter converter = CharSequenceValueConverter.Default;

        [Fact]
        public void TestBoolean()
        {
            Assert.True(converter.ConvertToBoolean(converter.ConvertBoolean(true)));
            Assert.False(converter.ConvertToBoolean(converter.ConvertBoolean(false)));
        }

        [Fact]
        public void TestByte()
        {
            Assert.Equal(byte.MaxValue, converter.ConvertToByte(converter.ConvertByte(byte.MaxValue)));
        }

        [Fact]
        public void TestChar()
        {
            Assert.Equal(char.MaxValue, converter.ConvertToChar(converter.ConvertChar(char.MaxValue)));
        }

        //[Fact]
        //public void TestDouble()
        //{
        //    Assert.Equal(double.MaxValue, converter.ConvertToDouble(converter.ConvertDouble(double.MaxValue)), 0);
        //}

        //[Fact]
        //public void TestFloat()
        //{
        //    Assert.Equal(float.MaxValue, converter.ConvertToFloat(converter.ConvertFloat(float.MaxValue)), 0);
        //}

        [Fact]
        public void TestInt()
        {
            Assert.Equal(int.MaxValue, converter.ConvertToInt(converter.ConvertInt(int.MaxValue)));
        }

        [Fact]
        public void TestShort()
        {
            Assert.Equal(short.MaxValue, converter.ConvertToShort(converter.ConvertShort(short.MaxValue)));
        }

        [Fact]
        public void TestLong()
        {
            Assert.Equal(long.MaxValue, converter.ConvertToLong(converter.ConvertLong(long.MaxValue)));
        }

        //[Fact]
        //public void TestTimeMillis()
        //{
        //    // Zero out the millis as this is what the convert is doing as well.
        //    long millis = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        //    Assert.Equal(millis, converter.ConvertToTimeMillis(converter.ConvertTimeMillis(millis)));
        //}
    }
}
