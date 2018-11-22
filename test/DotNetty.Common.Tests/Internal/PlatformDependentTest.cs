
namespace DotNetty.Common.Tests.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Common.Internal;
    using Xunit;

    public class PlatformDependentTest
    {
        interface IEqualityChecker
        {
            bool Equals(byte[] bytes1, int startPos1, byte[] bytes2, int startPos2, int length);
        }

        sealed class ByteArrayEqualityChecker : IEqualityChecker
        {
            public bool Equals(byte[] bytes1, int startPos1, byte[] bytes2, int startPos2, int length)
            {
                return PlatformDependent.ByteArrayEquals(bytes1, startPos1, bytes2, startPos2, length);
            }
        }

        sealed class ConstantTimeEqualityChecker : IEqualityChecker
        {
            public bool Equals(byte[] bytes1, int startPos1, byte[] bytes2, int startPos2, int length)
            {
                return PlatformDependent.ByteArrayEqualsConstantTime(bytes1, startPos1, bytes2, startPos2, length) != 0;
            }
        }

        [Fact]
        public void TestEqualsConsistentTime()
        {
            TestEquals0(new ConstantTimeEqualityChecker());
        }

        [Fact]
        public void TestEquals()
        {
            TestEquals0(new ByteArrayEqualityChecker());
        }

        private static void TestEquals0(IEqualityChecker equalsChecker)
        {
            byte[] bytes1 = { (byte)'H', (byte)'e', (byte)'l', (byte)'l', (byte)'o', (byte)' ', (byte)'W', (byte)'o', (byte)'r', (byte)'l', (byte)'d' };
            byte[] bytes2 = { (byte)'H', (byte)'e', (byte)'l', (byte)'l', (byte)'o', (byte)' ', (byte)'W', (byte)'o', (byte)'r', (byte)'l', (byte)'d' };
            Assert.NotSame(bytes1, bytes2);
            Assert.True(equalsChecker.Equals(bytes1, 0, bytes2, 0, bytes1.Length));
            Assert.True(equalsChecker.Equals(bytes1, 2, bytes2, 2, bytes1.Length - 2));

            bytes1 = new byte[] { 1, 2, 3, 4, 5, 6 };
            bytes2 = new byte[] { 1, 2, 3, 4, 5, 6, 7 };
            Assert.NotSame(bytes1, bytes2);
            Assert.False(equalsChecker.Equals(bytes1, 0, bytes2, 1, bytes1.Length));
            Assert.True(equalsChecker.Equals(bytes2, 0, bytes1, 0, bytes1.Length));

            bytes1 = new byte[] { 1, 2, 3, 4 };
            bytes2 = new byte[] { 1, 2, 3, 5 };
            Assert.False(equalsChecker.Equals(bytes1, 0, bytes2, 0, bytes1.Length));
            Assert.True(equalsChecker.Equals(bytes1, 0, bytes2, 0, 3));

            bytes1 = new byte[] { 1, 2, 3, 4 };
            bytes2 = new byte[] { 1, 3, 3, 4 };
            Assert.False(equalsChecker.Equals(bytes1, 0, bytes2, 0, bytes1.Length));
            Assert.True(equalsChecker.Equals(bytes1, 2, bytes2, 2, bytes1.Length - 2));

            bytes1 = new byte[0];
            bytes2 = new byte[0];
            Assert.NotSame(bytes1, bytes2);
            Assert.True(equalsChecker.Equals(bytes1, 0, bytes2, 0, 0));

            bytes1 = new byte[100];
            bytes2 = new byte[100];
            for (int i = 0; i < 100; i++)
            {
                bytes1[i] = (byte)i;
                bytes2[i] = (byte)i;
            }
            Assert.True(equalsChecker.Equals(bytes1, 0, bytes2, 0, bytes1.Length));
            bytes1[50] = 0;
            Assert.False(equalsChecker.Equals(bytes1, 0, bytes2, 0, bytes1.Length));
            Assert.True(equalsChecker.Equals(bytes1, 51, bytes2, 51, bytes1.Length - 51));
            Assert.True(equalsChecker.Equals(bytes1, 0, bytes2, 0, 50));

            bytes1 = new byte[] { 1, 2, 3, 4, 5 };
            bytes2 = new byte[] { 3, 4, 5 };
            Assert.False(equalsChecker.Equals(bytes1, 0, bytes2, 0, bytes2.Length));
            Assert.True(equalsChecker.Equals(bytes1, 2, bytes2, 0, bytes2.Length));
            Assert.True(equalsChecker.Equals(bytes2, 0, bytes1, 2, bytes2.Length));

            var r = new Random();
            for (int i = 0; i < 1000; ++i)
            {
                bytes1 = new byte[i];
                r.NextBytes(bytes1);
                bytes2 = new byte[bytes1.Length];
                PlatformDependent.CopyMemory(bytes1, 0, bytes2, 0, bytes1.Length);
                Assert.True(equalsChecker.Equals(bytes1, 0, bytes2, 0, bytes1.Length));
            }

            Assert.True(equalsChecker.Equals(bytes1, 0, bytes2, 0, 0));
            Assert.True(equalsChecker.Equals(bytes1, 0, bytes2, 0, -1));
        }
    }
}
