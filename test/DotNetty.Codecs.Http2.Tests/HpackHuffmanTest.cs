
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using Xunit;

    public class HpackHuffmanTest
    {
        [Fact]
        public void TestHuffman()
        {
            string s = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            for (int i = 0; i < s.Length; i++)
            {
                RoundTrip(s.Substring(0, i));
            }

            Random random = new Random();
            byte[] buf = new byte[4096];
            random.NextBytes(buf);
            RoundTrip(buf);
        }

        [Fact]
        public void TestDecodeEOS()
        {
            byte[] buf = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                buf[i] = (byte)0xFF;
            }
            Assert.Throws<Http2Exception>(() => Decode(NewHuffmanDecoder(), buf));
        }

        [Fact]
        public void TestDecodeIllegalPadding()
        {
            byte[] buf = new byte[1];
            buf[0] = 0x00; // '0', invalid padding
            Assert.Throws<Http2Exception>(() => Decode(NewHuffmanDecoder(), buf));
        }

        [Fact]
        public void TestDecodeExtraPadding()
        {
            byte[] buf = MakeBuf(0x0f, 0xFF); // '1', 'EOS'
            Assert.Throws<Http2Exception>(() => Decode(NewHuffmanDecoder(), buf));
        }

        [Fact]
        public void TestDecodeExtraPadding1byte()
        {
            byte[] buf = MakeBuf(0xFF);
            Assert.Throws<Http2Exception>(() => Decode(NewHuffmanDecoder(), buf));
        }

        [Fact]
        public void TestDecodeExtraPadding2byte()
        {
            byte[] buf = MakeBuf(0x1F, 0xFF); // 'a'
            Assert.Throws<Http2Exception>(() => Decode(NewHuffmanDecoder(), buf));
        }

        [Fact]
        public void TestDecodeExtraPadding3byte()
        {
            byte[] buf = MakeBuf(0x1F, 0xFF, 0xFF); // 'a'
            Assert.Throws<Http2Exception>(() => Decode(NewHuffmanDecoder(), buf));
        }

        [Fact]
        public void TestDecodeExtraPadding4byte()
        {
            byte[] buf = MakeBuf(0x1F, 0xFF, 0xFF, 0xFF); // 'a'
            Assert.Throws<Http2Exception>(() => Decode(NewHuffmanDecoder(), buf));
        }

        [Fact]
        public void TestDecodeExtraPadding29bit()
        {
            byte[] buf = MakeBuf(0xFF, 0x9F, 0xFF, 0xFF, 0xFF);  // '|'
            Assert.Throws<Http2Exception>(() => Decode(NewHuffmanDecoder(), buf));
        }

        [Fact]
        public void TestDecodePartialSymbol()
        {
            byte[] buf = MakeBuf(0x52, 0xBC, 0x30, 0xFF, 0xFF, 0xFF, 0xFF); // " pFA\x00", 31 bits of padding, a.k.a. EOS
            Assert.Throws<Http2Exception>(() => Decode(NewHuffmanDecoder(), buf));
        }

        private static byte[] MakeBuf(params int[] bytes)
        {
            byte[] buf = new byte[bytes.Length];
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] = (byte)bytes[i];
            }
            return buf;
        }

        private static void RoundTrip(string s)
        {
            RoundTrip(new HpackHuffmanEncoder(), NewHuffmanDecoder(), s);
        }

        private static void RoundTrip(HpackHuffmanEncoder encoder, HpackHuffmanDecoder decoder, string s)
        {
            RoundTrip(encoder, decoder, Encoding.UTF8.GetBytes(s));
        }

        private static void RoundTrip(byte[] buf)
        {
            RoundTrip(new HpackHuffmanEncoder(), NewHuffmanDecoder(), buf);
        }

        private static void RoundTrip(HpackHuffmanEncoder encoder, HpackHuffmanDecoder decoder, byte[] buf)
        {
            var buffer = Unpooled.Buffer();
            try
            {
                encoder.Encode(buffer, new AsciiString(buf, false));
                byte[] bytes = new byte[buffer.ReadableBytes];
                buffer.ReadBytes(bytes);

                byte[] actualBytes = Decode(decoder, bytes);

                Assert.Equal(buf, actualBytes);
            }
            finally
            {
                buffer.Release();
            }
        }

        private static byte[] Decode(HpackHuffmanDecoder decoder, byte[] bytes)
        {
            var buffer = Unpooled.WrappedBuffer(bytes);
            try
            {
                AsciiString decoded = decoder.Decode(buffer, buffer.ReadableBytes);
                Assert.False(buffer.IsReadable());
                return decoded.ToByteArray();
            }
            finally
            {
                buffer.Release();
            }
        }

        private static HpackHuffmanDecoder NewHuffmanDecoder()
        {
            return new HpackHuffmanDecoder(32);
        }
    }
}
