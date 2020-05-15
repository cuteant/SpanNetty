// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using System.Text;
    using DotNetty.Common.Utilities;
    using Xunit;

    public class ByteBufferUtilTests
    {
        [Fact]
        public void EqualsBufferSubsections()
        {
            var b1 = new byte[128];
            var b2 = new byte[256];
            var rand = new Random();
            rand.NextBytes(b1);
            rand.NextBytes(b2);
            int iB1 = b1.Length / 2;
            int iB2 = iB1 + b1.Length;
            int length = b1.Length - iB1;
            Array.Copy(b1, iB1, b2, iB2, length);
            Assert.True(ByteBufferUtil.Equals(Unpooled.WrappedBuffer(b1), iB1, Unpooled.WrappedBuffer(b2), iB2, length));
        }

        static int GetRandom(Random r, int min, int max) =>  r.Next((max - min) + 1) + min;

        [Fact]
        public void NotEqualsBufferSubsections()
        {
            var b1 = new byte[50];
            var b2 = new byte[256];
            var rand = new Random();
            rand.NextBytes(b1);
            rand.NextBytes(b2);
            int iB1 = b1.Length / 2;
            int iB2 = iB1 + b1.Length;
            int length = b1.Length - iB1;

            Array.Copy(b1, iB1, b2, iB2, length);
            // Randomly pick an index in the range that will be compared and make the value at that index differ between
            // the 2 arrays.
            int diffIndex = GetRandom(rand, iB1, iB1 + length - 1);
            ++b1[diffIndex];
            Assert.False(ByteBufferUtil.Equals(Unpooled.WrappedBuffer(b1), iB1, Unpooled.WrappedBuffer(b2), iB2, length));
        }

        [Fact]
        public void NotEqualsBufferOverflow()
        {
            var b1 = new byte[8];
            var b2 = new byte[16];
            var rand = new Random();
            rand.NextBytes(b1);
            rand.NextBytes(b2);
            int iB1 = b1.Length / 2;
            int iB2 = iB1 + b1.Length;
            int length = b1.Length - iB1;
            Array.Copy(b1, iB1, b2, iB2, length - 1);
            Assert.False(ByteBufferUtil.Equals(Unpooled.WrappedBuffer(b1), iB1, Unpooled.WrappedBuffer(b2), iB2,
                Math.Max(b1.Length, b2.Length) * 2));
        }

        [Fact]
        public void NotEqualsBufferUnderflow()
        {
            var b1 = new byte[8];
            var b2 = new byte[16];
            var rand = new Random();
            rand.NextBytes(b1);
            rand.NextBytes(b2);
            int iB1 = b1.Length / 2;
            int iB2 = iB1 + b1.Length;
            int length = b1.Length - iB1;
            Array.Copy(b1, iB1, b2, iB2, length - 1);
            Assert.Throws<ArgumentException>(() => ByteBufferUtil.Equals(Unpooled.WrappedBuffer(b1), iB1, Unpooled.WrappedBuffer(b2), iB2, -1));
        }


        [Fact]
        public void WriteUsAscii()
        {
            string usAscii = "NettyRocks";
            var buf = Unpooled.Buffer(16);
            buf.WriteBytes(Encoding.ASCII.GetBytes(usAscii));
            var buf2 = Unpooled.Buffer(16);
            ByteBufferUtil.WriteAscii(buf2, usAscii);

            Assert.Equal(buf, buf2);

            buf.Release();
            buf2.Release();
        }

        //[Fact]
        //public void WriteUsAsciiSwapped()
        //{
        //    string usAscii = "NettyRocks";
        //    var buf = Unpooled.Buffer(16);
        //    buf.WriteBytes(Encoding.ASCII.GetBytes(usAscii));
        //    var buf2 = new SwappedByteBuf(Unpooled.Buffer(16));
        //    ByteBufferUtil.WriteAscii(buf2, usAscii);

        //    Assert.Equal(buf, buf2);

        //    buf.Release();
        //    buf2.Release();
        //}

        [Fact]
        public void WriteUsAsciiWrapped()
        {
            string usAscii = "NettyRocks";
            var buf = Unpooled.UnreleasableBuffer(Unpooled.Buffer(16));
            AssertWrapped(buf);
            buf.WriteBytes(Encoding.ASCII.GetBytes(usAscii));
            var buf2 = Unpooled.UnreleasableBuffer(Unpooled.Buffer(16));
            AssertWrapped(buf2);
            ByteBufferUtil.WriteAscii(buf2, usAscii);

            Assert.Equal(buf, buf2);

            buf.Unwrap().Release();
            buf2.Unwrap().Release();
        }

        [Fact]
        public void WriteUsAsciiComposite()
        {
            string usAscii = "NettyRocks";
            var buf = Unpooled.Buffer(16);
            buf.WriteBytes(Encoding.ASCII.GetBytes(usAscii));
            var buf2 = Unpooled.CompositeBuffer().AddComponent(
                    Unpooled.Buffer(8)).AddComponent(Unpooled.Buffer(24));
            // write some byte so we start writing with an offset.
            buf2.WriteByte(1);
            ByteBufferUtil.WriteAscii(buf2, usAscii);

            // Skip the previously written byte.
            Assert.Equal(buf, buf2.SkipBytes(1));

            buf.Release();
            buf2.Release();
        }

        [Fact]
        public void WriteUsAsciiCompositeWrapped()
        {
            string usAscii = "NettyRocks";
            var buf = Unpooled.Buffer(16);
            buf.WriteBytes(Encoding.ASCII.GetBytes(usAscii));
            var buf2 = new WrappedCompositeByteBuffer(Unpooled.CompositeBuffer().AddComponent(
                    Unpooled.Buffer(8)).AddComponent(Unpooled.Buffer(24)));
            // write some byte so we start AddComponent with an offset.
            buf2.WriteByte(1);
            ByteBufferUtil.WriteAscii(buf2, usAscii);

            // Skip the previously written byte.
            Assert.Equal(buf, buf2.SkipBytes(1));

            buf.Release();
            buf2.Release();
        }

        [Fact]
        public void WriteUtf8()
        {
            string usAscii = "Some UTF-8 like äÄ∏ŒŒ";
            var buf = Unpooled.Buffer(16);
            buf.WriteBytes(Encoding.UTF8.GetBytes(usAscii));
            var buf2 = Unpooled.Buffer(16);
            ByteBufferUtil.WriteUtf8(buf2, usAscii);

            Assert.Equal(buf, buf2);

            buf.Release();
            buf2.Release();
        }

        [Fact]
        public void WriteUtf8Composite()
        {
            string utf8 = "Some UTF-8 like äÄ∏ŒŒ";
            var buf = Unpooled.Buffer(16);
            buf.WriteBytes(Encoding.UTF8.GetBytes(utf8));
            var buf2 = Unpooled.CompositeBuffer().AddComponent(
                    Unpooled.Buffer(8)).AddComponent(Unpooled.Buffer(24));
            // write some byte so we start writing with an offset.
            buf2.WriteByte(1);
            ByteBufferUtil.WriteUtf8(buf2, utf8);

            // Skip the previously written byte.
            Assert.Equal(buf, buf2.SkipBytes(1));

            buf.Release();
            buf2.Release();
        }

        [Fact]
        public void WriteUtf8CompositeWrapped()
        {
            string utf8 = "Some UTF-8 like äÄ∏ŒŒ";
            var buf = Unpooled.Buffer(16);
            buf.WriteBytes(Encoding.UTF8.GetBytes(utf8));
            var buf2 = new WrappedCompositeByteBuffer(Unpooled.CompositeBuffer().AddComponent(
                    Unpooled.Buffer(8)).AddComponent(Unpooled.Buffer(24)));
            // write some byte so we start writing with an offset.
            buf2.WriteByte(1);
            ByteBufferUtil.WriteUtf8(buf2, utf8);

            // Skip the previously written byte.
            Assert.Equal(buf, buf2.SkipBytes(1));

            buf.Release();
            buf2.Release();
        }

        [Fact]
        public void WriteUtf8Surrogates()
        {
            // leading surrogate + trailing surrogate
            string surrogateString = new StringBuilder(2)
                                    .Append('a')
                                    .Append('\uD800')
                                    .Append('\uDC00')
                                    .Append('b')
                                    .ToString();
            var buf = Unpooled.Buffer(16);
            buf.WriteBytes(Encoding.UTF8.GetBytes(surrogateString));
            var buf2 = Unpooled.Buffer(16);
            ByteBufferUtil.WriteUtf8(buf2, surrogateString);

            Assert.Equal(buf, buf2);
            Assert.Equal(buf.ReadableBytes, ByteBufferUtil.Utf8Bytes(surrogateString));

            buf.Release();
            buf2.Release();
        }

        [Fact(Skip = "Not Support!")]
        public void WriteUtf8InvalidOnlyTrailingSurrogate()
        {
            string surrogateString = new StringBuilder(2)
                                    .Append('a')
                                    .Append('\uDC00')
                                    .Append('b')
                                    .ToString();
            var buf = Unpooled.Buffer(16);
            buf.WriteBytes(Encoding.UTF8.GetBytes(surrogateString));
            var buf2 = Unpooled.Buffer(16);
            ByteBufferUtil.WriteUtf8(buf2, surrogateString);

            Assert.Equal(buf, buf2);
            Assert.Equal(buf.ReadableBytes, ByteBufferUtil.Utf8Bytes(surrogateString));

            buf.Release();
            buf2.Release();
        }

        [Fact(Skip = "Not Support!")]
        public void WriteUtf8InvalidOnlyLeadingSurrogate()
        {
            string surrogateString = new StringBuilder(2)
                                    .Append('a')
                                    .Append('\uD800')
                                    .Append('b')
                                    .ToString();
            var buf = Unpooled.Buffer(16);
            buf.WriteBytes(Encoding.UTF8.GetBytes(surrogateString));
            var buf2 = Unpooled.Buffer(16);
            ByteBufferUtil.WriteUtf8(buf2, surrogateString);

            Assert.Equal(buf, buf2);
            Assert.Equal(buf.ReadableBytes, ByteBufferUtil.Utf8Bytes(surrogateString));

            buf.Release();
            buf2.Release();
        }

        [Fact(Skip = "Not Support!")]
        public void WriteUtf8InvalidSurrogatesSwitched()
        {
            string surrogateString = new StringBuilder(2)
                                    .Append('a')
                                    .Append('\uDC00')
                                    .Append('\uD800')
                                    .Append('b')
                                    .ToString();
            var buf = Unpooled.Buffer(16);
            buf.WriteBytes(Encoding.UTF8.GetBytes(surrogateString));
            var buf2 = Unpooled.Buffer(16);
            ByteBufferUtil.WriteUtf8(buf2, surrogateString);

            Assert.Equal(buf, buf2);
            Assert.Equal(buf.ReadableBytes, ByteBufferUtil.Utf8Bytes(surrogateString));

            buf.Release();
            buf2.Release();
        }

        [Fact(Skip = "Not Support!")]
        public void WriteUtf8InvalidTwoLeadingSurrogates()
        {
            string surrogateString = new StringBuilder(2)
                                    .Append('a')
                                    .Append('\uD800')
                                    .Append('\uD800')
                                    .Append('b')
                                    .ToString();
            var buf = Unpooled.Buffer(16);
            buf.WriteBytes(Encoding.UTF8.GetBytes(surrogateString));
            var buf2 = Unpooled.Buffer(16);
            ByteBufferUtil.WriteUtf8(buf2, surrogateString);

            Assert.Equal(buf, buf2);
            Assert.Equal(buf.ReadableBytes, ByteBufferUtil.Utf8Bytes(surrogateString));
            buf.Release();
            buf2.Release();
        }

        [Fact(Skip = "Not Support!")]
        public void WriteUtf8InvalidTwoTrailingSurrogates()
        {
            string surrogateString = new StringBuilder(2)
                                    .Append('a')
                                    .Append('\uDC00')
                                    .Append('\uDC00')
                                    .Append('b')
                                    .ToString();
            var buf = Unpooled.Buffer(16);
            buf.WriteBytes(Encoding.UTF8.GetBytes(surrogateString));
            var buf2 = Unpooled.Buffer(16);
            ByteBufferUtil.WriteUtf8(buf2, surrogateString);

            Assert.Equal(buf, buf2);
            Assert.Equal(buf.ReadableBytes, ByteBufferUtil.Utf8Bytes(surrogateString));

            buf.Release();
            buf2.Release();
        }

        [Fact(Skip = "Not Support!")]
        public void WriteUtf8InvalidEndOnLeadingSurrogate()
        {
            string surrogateString = new StringBuilder(2)
                                    .Append('\uD800')
                                    .ToString();
            var buf = Unpooled.Buffer(16);
            buf.WriteBytes(Encoding.UTF8.GetBytes(surrogateString));
            var buf2 = Unpooled.Buffer(16);
            ByteBufferUtil.WriteUtf8(buf2, surrogateString);

            Assert.Equal(buf, buf2);
            Assert.Equal(buf.ReadableBytes, ByteBufferUtil.Utf8Bytes(surrogateString));

            buf.Release();
            buf2.Release();
        }

        [Fact(Skip = "Not Support!")]
        public void WriteUtf8InvalidEndOnTrailingSurrogate()
        {
            string surrogateString = new StringBuilder(2)
                                    .Append('\uDC00')
                                    .ToString();
            var buf = Unpooled.Buffer(16);
            buf.WriteBytes(Encoding.UTF8.GetBytes(surrogateString));
            var buf2 = Unpooled.Buffer(16);
            ByteBufferUtil.WriteUtf8(buf2, surrogateString);

            Assert.Equal(buf, buf2);
            Assert.Equal(buf.ReadableBytes, ByteBufferUtil.Utf8Bytes(surrogateString));

            buf.Release();
            buf2.Release();
        }

        [Fact]
        public void WriteUsAsciiString()
        {
            AsciiString usAscii = new AsciiString("NettyRocks");
            var buf = Unpooled.Buffer(16);
            buf.WriteBytes(Encoding.ASCII.GetBytes(usAscii.ToString()));
            var buf2 = Unpooled.Buffer(16);
            ByteBufferUtil.WriteAscii(buf2, usAscii);

            Assert.Equal(buf, buf2);

            buf.Release();
            buf2.Release();
        }

        [Fact]
        public void WriteUtf8Wrapped()
        {
            string usAscii = "Some UTF-8 like äÄ∏ŒŒ";
            var buf = Unpooled.UnreleasableBuffer(Unpooled.Buffer(16));
            AssertWrapped(buf);
            buf.WriteBytes(Encoding.UTF8.GetBytes(usAscii));
            var buf2 = Unpooled.UnreleasableBuffer(Unpooled.Buffer(16));
            AssertWrapped(buf2);
            ByteBufferUtil.WriteUtf8(buf2, usAscii);

            Assert.Equal(buf, buf2);

            buf.Release();
            buf2.Release();
        }

        private static void AssertWrapped(IByteBuffer buf)
        {
            Assert.True(buf is WrappedByteBuffer);
        }

        [Fact]
        public void DecodeUsAscii()
        {
            DecodeString("This is a test", Encoding.ASCII);
        }

        [Fact]
        public void DecodeUtf8()
        {
            DecodeString("Some UTF-8 like äÄ∏ŒŒ", Encoding.UTF8);
        }

        private static void DecodeString(string text, Encoding charset)
        {
            var buffer = Unpooled.CopiedBuffer(text, charset);
            Assert.Equal(text, ByteBufferUtil.DecodeString(buffer, 0, buffer.ReadableBytes, charset));
            buffer.Release();
        }

        [Fact]
        public void ToStringDoesNotThrowIndexOutOfBounds()
        {
            var buffer = Unpooled.CompositeBuffer();
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes("1234");
                buffer.AddComponent(Unpooled.Buffer(bytes.Length).WriteBytes(bytes));
                buffer.AddComponent(Unpooled.Buffer(bytes.Length).WriteBytes(bytes));
                Assert.Equal("1234", buffer.ToString(bytes.Length, bytes.Length, Encoding.UTF8));
            }
            finally
            {
                buffer.Release();
            }
        }

        [Fact]
        public void IsTextWithUtf8()
        {
            byte[][] validUtf8Bytes = new[] {
                Encoding.UTF8.GetBytes("netty"),
                new[] { (byte) 0x24},
                new[] { (byte) 0xC2, (byte) 0xA2},
                new[] { (byte) 0xE2, (byte) 0x82, (byte) 0xAC},
                new[] { (byte) 0xF0, (byte) 0x90, (byte) 0x8D, (byte) 0x88},
                new [] {(byte) 0x24,
                        (byte) 0xC2, (byte) 0xA2,
                        (byte) 0xE2, (byte) 0x82, (byte) 0xAC,
                        (byte) 0xF0, (byte) 0x90, (byte) 0x8D, (byte) 0x88} // multiple characters
        };
            foreach(byte[] bytes in validUtf8Bytes)
            {
                AssertIsText(bytes, true, Encoding.UTF8);
            }
            byte[][] invalidUtf8Bytes = new [] {
                new [] {(byte) 0x80},
                new [] {(byte) 0xF0, (byte) 0x82, (byte) 0x82, (byte) 0xAC}, // Overlong encodings
                new [] {(byte) 0xC2},                                        // not enough bytes
                new [] {(byte) 0xE2, (byte) 0x82},                           // not enough bytes
                new [] {(byte) 0xF0, (byte) 0x90, (byte) 0x8D},              // not enough bytes
                new [] {(byte) 0xC2, (byte) 0xC0},                           // not correct bytes
                new [] {(byte) 0xE2, (byte) 0x82, (byte) 0xC0},              // not correct bytes
                new [] {(byte) 0xF0, (byte) 0x90, (byte) 0x8D, (byte) 0xC0}, // not correct bytes
                new [] {(byte) 0xC1, (byte) 0x80},                           // out of lower bound
                new [] {(byte) 0xE0, (byte) 0x80, (byte) 0x80},              // out of lower bound
                new [] {(byte) 0xED, (byte) 0xAF, (byte) 0x80}               // out of upper bound
        };
            foreach(byte[] bytes in invalidUtf8Bytes)
            {
                AssertIsText(bytes, false, Encoding.UTF8);
            }
        }

        [Fact(Skip = "Not Support!")]
        public void IsTextWithoutOptimization()
        {
            byte[] validBytes = { (byte)0x01, (byte)0xD8, (byte)0x37, (byte)0xDC };
            byte[] invalidBytes = { (byte)0x01, (byte)0xD8 };

            AssertIsText(validBytes, true, Encoding.Unicode);
            AssertIsText(invalidBytes, false, Encoding.Unicode);
        }

        [Fact]
        public void IsTextWithAscii()
        {
            byte[] validBytes = { (byte)0x00, (byte)0x01, (byte)0x37, (byte)0x7F };
            byte[] invalidBytes = { (byte)0x80, (byte)0xFF };

            AssertIsText(validBytes, true, Encoding.ASCII);
            AssertIsText(invalidBytes, false, Encoding.ASCII);
        }

        [Fact]
        public void IsTextWithInvalidIndexAndLength()
        {
            var buffer = Unpooled.Buffer();
            try
            {
                buffer.WriteBytes(new byte[4]);
                int[][] validIndexLengthPairs =new[] {
                    new [] {4, 0},
                    new [] {0, 4},
                    new [] {1, 3},
                };
                foreach (var pair in validIndexLengthPairs)
                {
                    Assert.True(ByteBufferUtil.IsText(buffer, pair[0], pair[1], Encoding.ASCII));
                }
                int[][] invalidIndexLengthPairs = new [] {
                    new [] {4, 1},
                    new [] {-1, 2},
                    new [] {3, -1},
                    new [] {3, -2},
                    new [] {5, 0},
                    new [] {1, 5},
                };
                foreach (var pair in invalidIndexLengthPairs)
                {
                    Assert.Throws<IndexOutOfRangeException>(() => ByteBufferUtil.IsText(buffer, pair[0], pair[1], Encoding.ASCII));
                }
            }
            finally
            {
                buffer.Release();
            }
        }

        [Fact]
        public void Utf8Bytes()
        {
            var  s = "Some UTF-8 like äÄ∏ŒŒ";
            CheckUtf8Bytes(s);
        }

        [Fact]
        public void Utf8BytesWithSurrogates()
        {
            var  s = "a\uD800\uDC00b";
            CheckUtf8Bytes(s);
        }

        [Fact]
        public void Utf8BytesWithNonSurrogates3Bytes()
        {
            var  s = "a\uE000b";
            CheckUtf8Bytes(s);
        }

        [Fact]
        public void Utf8BytesWithNonSurrogatesNonAscii()
        {
            char nonAscii = (char)0x81;
            var  s = "a" + nonAscii + "b";
            CheckUtf8Bytes(s);
        }

        private static void CheckUtf8Bytes(string charSequence)
        {
            var buf = Unpooled.Buffer(ByteBufferUtil.Utf8MaxBytes(charSequence));
            try
            {
                int writtenBytes = ByteBufferUtil.WriteUtf8(buf, charSequence);
                int utf8Bytes = ByteBufferUtil.Utf8Bytes(charSequence);
                Assert.Equal(writtenBytes, utf8Bytes);
            }
            finally
            {
                buf.Release();
            }
        }

        private static void AssertIsText(byte[] bytes, bool expected, Encoding charset)
        {
            var buffer = Unpooled.Buffer();
            try
            {
                buffer.WriteBytes(bytes);
                Assert.Equal(expected, ByteBufferUtil.IsText(buffer, charset));
            }
            finally
            {
                buffer.Release();
            }
        }
    }
}
