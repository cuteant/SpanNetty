
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using Moq;
    using Xunit;

    public class HpackDecoderTest
    {

        private HpackDecoder hpackDecoder;
        private Mock<IHttp2Headers> mockHeaders;

        private static string Hex(string s)
        {
            return StringUtil.ToHexString(Encoding.UTF8.GetBytes(s));
        }

        private void Decode(string encoded)
        {
            byte[] b = StringUtil.DecodeHexDump(encoded);
            var input = Unpooled.WrappedBuffer(b);
            try
            {
                this.hpackDecoder.Decode(0, input, this.mockHeaders.Object, true);
            }
            finally
            {
                input.Release();
            }
        }

        public HpackDecoderTest()
        {
            this.hpackDecoder = new HpackDecoder(8192, 32);
            this.mockHeaders = new Mock<IHttp2Headers>(MockBehavior.Default);
        }

        [Fact]
        public void TestDecodeULE128IntMax()
        {
            byte[] bytes = { (byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0x07 };
            var input = Unpooled.WrappedBuffer(bytes);
            try
            {
                Assert.Equal(int.MaxValue, HpackDecoder.DecodeULE128(input, 0));
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void TestDecodeULE128IntOverflow1()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                byte[] bytes = { (byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0x07 };
                IByteBuffer input = Unpooled.WrappedBuffer(bytes);
                int readerIndex = input.ReaderIndex;
                try
                {
                    HpackDecoder.DecodeULE128(input, 1);
                }
                finally
                {
                    Assert.Equal(readerIndex, input.ReaderIndex);
                    input.Release();
                }
            });
        }

        [Fact]
        public void TestDecodeULE128IntOverflow2()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                byte[] bytes = { (byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0x08 };
                IByteBuffer input = Unpooled.WrappedBuffer(bytes);
                int readerIndex = input.ReaderIndex;
                try
                {
                    HpackDecoder.DecodeULE128(input, 0);
                }
                finally
                {
                    Assert.Equal(readerIndex, input.ReaderIndex);
                    input.Release();
                }
            });
        }

        [Fact]
        public void TestDecodeULE128LongMax()
        {
            byte[] bytes = {(byte) 0xFF, (byte) 0xFF, (byte) 0xFF, (byte) 0xFF, (byte) 0xFF, (byte) 0xFF, (byte) 0xFF,
                        (byte) 0xFF, (byte) 0x7F};
            IByteBuffer input = Unpooled.WrappedBuffer(bytes);
            try
            {
                Assert.Equal(long.MaxValue, HpackDecoder.DecodeULE128(input, 0L));
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void TestDecodeULE128LongOverflow1()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                byte[] bytes = {(byte) 0xFF, (byte) 0xFF, (byte) 0xFF, (byte) 0xFF, (byte) 0xFF, (byte) 0xFF, (byte) 0xFF,
                        (byte) 0xFF, (byte) 0xFF};
                IByteBuffer input = Unpooled.WrappedBuffer(bytes);
                int readerIndex = input.ReaderIndex;
                try
                {
                    HpackDecoder.DecodeULE128(input, 0L);
                }
                finally
                {
                    Assert.Equal(readerIndex, input.ReaderIndex);
                    input.Release();
                }
            });
        }

        [Fact]
        public void TestDecodeULE128LongOverflow2()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                byte[] bytes = {(byte) 0xFF, (byte) 0xFF, (byte) 0xFF, (byte) 0xFF, (byte) 0xFF, (byte) 0xFF, (byte) 0xFF,
                        (byte) 0xFF, (byte) 0x7F};
                IByteBuffer input = Unpooled.WrappedBuffer(bytes);
                int readerIndex = input.ReaderIndex;
                try
                {
                    HpackDecoder.DecodeULE128(input, 1L);
                }
                finally
                {
                    Assert.Equal(readerIndex, input.ReaderIndex);
                    input.Release();
                }
            });
        }

        [Fact]
        public void TestSetTableSizeWithMaxUnsigned32BitValueSucceeds()
        {
            byte[] bytes = { (byte)0x3F, (byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0x0E };
            IByteBuffer input = Unpooled.WrappedBuffer(bytes);
            try
            {
                long expectedHeaderSize = 4026531870L; // based on the bytes above
                hpackDecoder.SetMaxHeaderTableSize(expectedHeaderSize);
                hpackDecoder.Decode(0, input, mockHeaders.Object, true);
                Assert.Equal(expectedHeaderSize, hpackDecoder.GetMaxHeaderTableSize());
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void TestSetTableSizeOverLimitFails()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                byte[] bytes = { (byte)0x3F, (byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0x0E };
                IByteBuffer input = Unpooled.WrappedBuffer(bytes);
                try
                {
                    hpackDecoder.SetMaxHeaderTableSize(4026531870L - 1); // based on the bytes above ... 1 less than is above.
                    hpackDecoder.Decode(0, input, mockHeaders.Object, true);
                }
                finally
                {
                    input.Release();
                }
            });
        }

        [Fact]
        public void TestLiteralHuffmanEncodedWithEmptyNameAndValue()
        {
            byte[] bytes = { 0, (byte)0x80, 0 };
            IByteBuffer input = Unpooled.WrappedBuffer(bytes);
            try
            {
                hpackDecoder.Decode(0, input, mockHeaders.Object, true);
                mockHeaders.Verify(x => x.Add(AsciiString.Empty, AsciiString.Empty), Times.Once);
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void TestLiteralHuffmanEncodedWithPaddingGreaterThan7Throws()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                var v1 = -1;
                byte[] bytes = { 0, (byte)0x81, (byte)v1 };
                IByteBuffer input = Unpooled.WrappedBuffer(bytes);
                try
                {
                    hpackDecoder.Decode(0, input, mockHeaders.Object, true);
                }
                finally
                {
                    input.Release();
                }
            });
        }

        [Fact]
        public void TestLiteralHuffmanEncodedWithDecodingEOSThrows()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                byte[] bytes = { 0, (byte)0x84, (byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0xFF };
                IByteBuffer input = Unpooled.WrappedBuffer(bytes);
                try
                {
                    hpackDecoder.Decode(0, input, mockHeaders.Object, true);
                }
                finally
                {
                    input.Release();
                }
            });
        }

        [Fact]
        public void TestLiteralHuffmanEncodedWithPaddingNotCorrespondingToMSBThrows()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                byte[] bytes = { 0, (byte)0x81, 0 };
                IByteBuffer input = Unpooled.WrappedBuffer(bytes);
                try
                {
                    hpackDecoder.Decode(0, input, mockHeaders.Object, true);
                }
                finally
                {
                    input.Release();
                }
            });
        }

        [Fact]
        public void TestIncompleteIndex()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                byte[] compressed = StringUtil.DecodeHexDump("FFF0");
                IByteBuffer input = Unpooled.WrappedBuffer(compressed);
                try
                {
                    hpackDecoder.Decode(0, input, mockHeaders.Object, true);
                    Assert.Equal(1, input.ReadableBytes);
                    hpackDecoder.Decode(0, input, mockHeaders.Object, true);
                }
                finally
                {
                    input.Release();
                }
            });
        }

        [Fact]
        public void TestUnusedIndex()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                // Index 0 is not used
                this.Decode("80");
            });
        }

        [Fact]
        public void TestIllegalIndex()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                // Index larger than the header table
                this.Decode("FF00");
            });
        }

        [Fact]
        public void TestInsidiousIndex()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                // Insidious index so the last shift causes sign overflow
                this.Decode("FF8080808007");
            });
        }

        [Fact]
        public void TestDynamicTableSizeUpdate()
        {
            this.Decode("20");
            Assert.Equal(0, hpackDecoder.GetMaxHeaderTableSize());
            this.Decode("3FE11F");
            Assert.Equal(4096, hpackDecoder.GetMaxHeaderTableSize());
        }

        [Fact]
        public void TestDynamicTableSizeUpdateRequired()
        {
            hpackDecoder.SetMaxHeaderTableSize(32);
            this.Decode("3F00");
            Assert.Equal(31, hpackDecoder.GetMaxHeaderTableSize());
        }

        [Fact]
        public void TestIllegalDynamicTableSizeUpdate()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                // max header table size = MAX_HEADER_TABLE_SIZE + 1
                this.Decode("3FE21F");
            });
        }

        [Fact]
        public void TestInsidiousMaxDynamicTableSize()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                hpackDecoder.SetMaxHeaderTableSize(int.MaxValue);
                // max header table size sign overflow
                this.Decode("3FE1FFFFFF07");
            });
        }

        [Fact]
        public void TestMaxValidDynamicTableSize()
        {
            hpackDecoder.SetMaxHeaderTableSize(int.MaxValue);
            string baseValue = "3FE1FFFFFF0";
            for (int i = 0; i < 7; ++i)
            {
                this.Decode(baseValue + i);
            }
        }

        [Fact]
        public void TestReduceMaxDynamicTableSize()
        {
            hpackDecoder.SetMaxHeaderTableSize(0);
            Assert.Equal(0, hpackDecoder.GetMaxHeaderTableSize());
            this.Decode("2081");
        }

        [Fact]
        public void TestTooLargeDynamicTableSizeUpdate()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                hpackDecoder.SetMaxHeaderTableSize(0);
                Assert.Equal(0, hpackDecoder.GetMaxHeaderTableSize());
                this.Decode("21"); // encoder max header table size not small enough
            });
        }

        [Fact]
        public void TestMissingDynamicTableSizeUpdate()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                hpackDecoder.SetMaxHeaderTableSize(0);
                Assert.Equal(0, hpackDecoder.GetMaxHeaderTableSize());
                this.Decode("81");
            });
        }

        [Fact]
        public void TestLiteralWithIncrementalIndexingWithEmptyName()
        {
            this.Decode("400005" + Hex("value"));
            mockHeaders.Verify(x => x.Add(AsciiString.Empty, AsciiString.Of("value")), Times.Once);
        }

        [Fact]
        public void TestLiteralWithIncrementalIndexingCompleteEviction()
        {
            // Verify indexed host header
            this.Decode("4004" + Hex("name") + "05" + Hex("value"));
            mockHeaders.Verify(x => x.Add(AsciiString.Of("name"), AsciiString.Of("value")));
            mockHeaders.VerifyNoOtherCalls();
            mockHeaders.Reset();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 4096; i++)
            {
                sb.Append('a');
            }
            var value = sb.ToString();
            sb = new StringBuilder();
            sb.Append("417F811F");
            for (int i = 0; i < 4096; i++)
            {
                sb.Append("61"); // 'a'
            }
            this.Decode(sb.ToString());
            mockHeaders.Verify(x => x.Add(AsciiString.Of(":authority"), AsciiString.Of(value)));
            mockHeaders.VerifyNoOtherCalls();
            mockHeaders.Reset();

            // Verify next header is inserted at index 62
            this.Decode("4004" + Hex("name") + "05" + Hex("value") + "BE");
            mockHeaders.Verify(x => x.Add(AsciiString.Of("name"), AsciiString.Of("value")), Times.AtLeast(2));
            mockHeaders.VerifyNoOtherCalls();
        }

        [Fact]
        public void TestLiteralWithIncrementalIndexingWithLargeValue()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                // Ignore header that exceeds max header size
                StringBuilder sb = new StringBuilder();
                sb.Append("4004");
                sb.Append(Hex("name"));
                sb.Append("7F813F");
                for (int i = 0; i < 8192; i++)
                {
                    sb.Append("61"); // 'a'
                }
                this.Decode(sb.ToString());
            });
        }

        [Fact]
        public void TestLiteralWithoutIndexingWithEmptyName()
        {
            this.Decode("000005" + Hex("value"));
            mockHeaders.Verify(x => x.Add(AsciiString.Empty, AsciiString.Of("value")), Times.Once);
        }

        [Fact]
        public void TestLiteralWithoutIndexingWithLargeName()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                // Ignore header name that exceeds max header size
                StringBuilder sb = new StringBuilder();
                sb.Append("007F817F");
                for (int i = 0; i < 16384; i++)
                {
                    sb.Append("61"); // 'a'
                }
                sb.Append("00");
                this.Decode(sb.ToString());
            });
        }

        [Fact]
        public void TestLiteralWithoutIndexingWithLargeValue()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                // Ignore header that exceeds max header size
                StringBuilder sb = new StringBuilder();
                sb.Append("0004");
                sb.Append(Hex("name"));
                sb.Append("7F813F");
                for (int i = 0; i < 8192; i++)
                {
                    sb.Append("61"); // 'a'
                }
                this.Decode(sb.ToString());
            });
        }

        [Fact]
        public void TestLiteralNeverIndexedWithEmptyName()
        {
            this.Decode("100005" + Hex("value"));
            mockHeaders.Verify(x => x.Add(AsciiString.Empty, AsciiString.Of("value")), Times.Once);
        }

        [Fact]
        public void TestLiteralNeverIndexedWithLargeName()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                // Ignore header name that exceeds max header size
                StringBuilder sb = new StringBuilder();
                sb.Append("107F817F");
                for (int i = 0; i < 16384; i++)
                {
                    sb.Append("61"); // 'a'
                }
                sb.Append("00");
                this.Decode(sb.ToString());
            });
        }

        [Fact]
        public void TestLiteralNeverIndexedWithLargeValue()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                // Ignore header that exceeds max header size
                StringBuilder sb = new StringBuilder();
                sb.Append("1004");
                sb.Append(Hex("name"));
                sb.Append("7F813F");
                for (int i = 0; i < 8192; i++)
                {
                    sb.Append("61"); // 'a'
                }
                this.Decode(sb.ToString());
            });
        }

        [Fact]
        public void TestDecodeLargerThanMaxHeaderListSizeUpdatesDynamicTable()
        {
            IByteBuffer input = Unpooled.Buffer(300);
            try
            {
                hpackDecoder.SetMaxHeaderListSize(200);
                HpackEncoder hpackEncoder = new HpackEncoder(true);

                // encode headers that are slightly larger than maxHeaderListSize
                IHttp2Headers toEncode = new DefaultHttp2Headers();
                toEncode.Add((AsciiString)"test_1", (AsciiString)"1");
                toEncode.Add((AsciiString)"test_2", (AsciiString)"2");
                toEncode.Add((AsciiString)"long", (AsciiString)"A".PadRight(100, 'A')); //string.Format("{0,0100:d}", 0).Replace('0', 'A')
                toEncode.Add((AsciiString)"test_3", (AsciiString)"3");
                hpackEncoder.EncodeHeaders(1, input, toEncode, NeverSensitiveDetector.Instance);

                // decode the headers, we should get an exception
                IHttp2Headers decoded = new DefaultHttp2Headers();
                Assert.Throws<HeaderListSizeException>(() => hpackDecoder.Decode(1, input, decoded, true));

                // but the dynamic table should have been updated, so that later blocks
                // can refer to earlier headers
                input.Clear();
                // 0x80, "indexed header field representation"
                // index 62, the first (most recent) dynamic table entry
                input.WriteByte(0x80 | 62);
                IHttp2Headers decoded2 = new DefaultHttp2Headers();
                hpackDecoder.Decode(1, input, decoded2, true);

                IHttp2Headers golden = new DefaultHttp2Headers();
                golden.Add((AsciiString)"test_3", (AsciiString)"3");
                Assert.Equal(golden, decoded2);
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void TestDecodeCountsNamesOnlyOnce()
        {
            IByteBuffer input = Unpooled.Buffer(200);
            try
            {
                hpackDecoder.SetMaxHeaderListSize(3500);
                HpackEncoder hpackEncoder = new HpackEncoder(true);

                // encode headers that are slightly larger than maxHeaderListSize
                IHttp2Headers toEncode = new DefaultHttp2Headers();
                toEncode.Add((AsciiString)("0".PadRight(3000, '0').Replace('0', 'f')), (AsciiString)"value");
                toEncode.Add((AsciiString)"accept", (AsciiString)"value");
                hpackEncoder.EncodeHeaders(1, input, toEncode, NeverSensitiveDetector.Instance);

                IHttp2Headers decoded = new DefaultHttp2Headers();
                hpackDecoder.Decode(1, input, decoded, true);
                Assert.Equal(2, decoded.Size);
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void TestAccountForHeaderOverhead()
        {
            IByteBuffer input = Unpooled.Buffer(100);
            try
            {
                string headerName = "12345";
                string headerValue = "56789";
                long headerSize = headerName.Length + headerValue.Length;
                hpackDecoder.SetMaxHeaderListSize(headerSize);
                HpackEncoder hpackEncoder = new HpackEncoder(true);

                IHttp2Headers toEncode = new DefaultHttp2Headers();
                toEncode.Add((AsciiString)headerName, (AsciiString)headerValue);
                hpackEncoder.EncodeHeaders(1, input, toEncode, NeverSensitiveDetector.Instance);

                IHttp2Headers decoded = new DefaultHttp2Headers();

                // SETTINGS_MAX_HEADER_LIST_SIZE is big enough for the header to fit...
                Assert.True(hpackDecoder.GetMaxHeaderListSize() >= headerSize);

                // ... but decode should fail because we add some overhead for each header entry
                Assert.Throws<HeaderListSizeException>(() => hpackDecoder.Decode(1, input, decoded, true));
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void TestIncompleteHeaderFieldRepresentation()
        {
            // Incomplete Literal Header Field with Incremental Indexing
            byte[] bytes = { (byte)0x40 };
            IByteBuffer input = Unpooled.WrappedBuffer(bytes);
            try
            {
                Assert.Throws<Http2Exception>(() => hpackDecoder.Decode(0, input, mockHeaders.Object, true));
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void UnknownPseudoHeader()
        {
            IByteBuffer input = Unpooled.Buffer(200);
            try
            {
                HpackEncoder hpackEncoder = new HpackEncoder(true);

                IHttp2Headers toEncode = new DefaultHttp2Headers();
                toEncode.Add((AsciiString)":test", (AsciiString)"1");
                hpackEncoder.EncodeHeaders(1, input, toEncode, NeverSensitiveDetector.Instance);

                IHttp2Headers decoded = new DefaultHttp2Headers();

                Assert.Throws<StreamException>(() => hpackDecoder.Decode(1, input, decoded, true));
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void DisableHeaderValidation()
        {
            IByteBuffer input = Unpooled.Buffer(200);
            try
            {
                HpackEncoder hpackEncoder = new HpackEncoder(true);

                IHttp2Headers toEncode = new DefaultHttp2Headers();
                toEncode.Add((AsciiString)":test", (AsciiString)"1");
                toEncode.Add((AsciiString)":status", (AsciiString)"200");
                toEncode.Add((AsciiString)":method", (AsciiString)"GET");
                hpackEncoder.EncodeHeaders(1, input, toEncode, NeverSensitiveDetector.Instance);

                IHttp2Headers decoded = new DefaultHttp2Headers();

                hpackDecoder.Decode(1, input, decoded, false);

                Assert.Equal("1", decoded.GetAll((AsciiString)":test")[0]);
                Assert.Equal("200", decoded.Status);
                Assert.Equal("GET", decoded.Method);
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void RequestPseudoHeaderInResponse()
        {
            IByteBuffer input = Unpooled.Buffer(200);
            try
            {
                HpackEncoder hpackEncoder = new HpackEncoder(true);

                IHttp2Headers toEncode = new DefaultHttp2Headers();
                toEncode.Add((AsciiString)":status", (AsciiString)"200");
                toEncode.Add((AsciiString)":method", (AsciiString)"GET");
                hpackEncoder.EncodeHeaders(1, input, toEncode, NeverSensitiveDetector.Instance);

                IHttp2Headers decoded = new DefaultHttp2Headers();

                Assert.Throws<StreamException>(()=> hpackDecoder.Decode(1, input, decoded, true));
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void ResponsePseudoHeaderInRequest()
        {
            IByteBuffer input = Unpooled.Buffer(200);
            try
            {
                HpackEncoder hpackEncoder = new HpackEncoder(true);

                IHttp2Headers toEncode = new DefaultHttp2Headers();
                toEncode.Add((AsciiString)":method", (AsciiString)"GET");
                toEncode.Add((AsciiString)":status", (AsciiString)"200");
                hpackEncoder.EncodeHeaders(1, input, toEncode, NeverSensitiveDetector.Instance);

                IHttp2Headers decoded = new DefaultHttp2Headers();

                Assert.Throws<StreamException>(() => hpackDecoder.Decode(1, input, decoded, true));
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void PseudoHeaderAfterRegularHeader()
        {
            IByteBuffer input = Unpooled.Buffer(200);
            try
            {
                HpackEncoder hpackEncoder = new HpackEncoder(true);

                IHttp2Headers toEncode = new InOrderHttp2Headers();
                toEncode.Add((AsciiString)"test", (AsciiString)"1");
                toEncode.Add((AsciiString)":method", (AsciiString)"GET");
                hpackEncoder.EncodeHeaders(1, input, toEncode, NeverSensitiveDetector.Instance);

                IHttp2Headers decoded = new DefaultHttp2Headers();

                Assert.Throws<StreamException>(() => hpackDecoder.Decode(1, input, decoded, true));
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void FailedValidationDoesntCorruptHpack()
        {
            IByteBuffer in1 = Unpooled.Buffer(200);
            IByteBuffer in2 = Unpooled.Buffer(200);
            try
            {
                HpackEncoder hpackEncoder = new HpackEncoder(true);

                IHttp2Headers toEncode = new DefaultHttp2Headers();
                toEncode.Add((AsciiString)":method", (AsciiString)"GET");
                toEncode.Add((AsciiString)":status", (AsciiString)"200");
                toEncode.Add((AsciiString)"foo", (AsciiString)"bar");
                hpackEncoder.EncodeHeaders(1, in1, toEncode, NeverSensitiveDetector.Instance);

                IHttp2Headers decoded = new DefaultHttp2Headers();

                var expected = Assert.Throws<StreamException>(() => hpackDecoder.Decode(1, in1, decoded, true));
                Assert.Equal(1, expected.StreamId);

                // Do it again, this time without validation, to make sure the HPACK state is still sane.
                decoded.Clear();
                hpackEncoder.EncodeHeaders(1, in2, toEncode, NeverSensitiveDetector.Instance);
                hpackDecoder.Decode(1, in2, decoded, false);

                Assert.Equal(3, decoded.Size);
                Assert.Equal("GET", decoded.Method);
                Assert.Equal("200", decoded.Status);
                Assert.Equal("bar", decoded.Get((AsciiString)"foo", null));
            }
            finally
            {
                in1.Release();
                in2.Release();
            }
        }
    }
}
