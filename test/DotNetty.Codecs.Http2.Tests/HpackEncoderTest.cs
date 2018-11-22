
namespace DotNetty.Codecs.Http2.Tests
{
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using Moq;
    using Xunit;

    public class HpackEncoderTest
    {
        private HpackDecoder hpackDecoder;
        private HpackEncoder hpackEncoder;
        private Mock<IHttp2Headers> mockHeaders;

        public HpackEncoderTest()
        {
            hpackEncoder = new HpackEncoder();
            hpackDecoder = new HpackDecoder(Http2CodecUtil.DefaultHeaderListSize, 32);
            mockHeaders = new Mock<IHttp2Headers>(MockBehavior.Strict);
        }

        [Fact]
        public void TestSetMaxHeaderTableSizeToMaxValue()
        {
            var buf = Unpooled.Buffer();
            hpackEncoder.SetMaxHeaderTableSize(buf, Http2CodecUtil.MaxHeaderTableSize);
            hpackDecoder.SetMaxHeaderTableSize(Http2CodecUtil.MaxHeaderTableSize); // TODO sometimes throw OutOfMemoryException
            hpackDecoder.Decode(0, buf, mockHeaders.Object, true);
            Assert.Equal(Http2CodecUtil.MaxHeaderTableSize, hpackDecoder.GetMaxHeaderTableSize());
            buf.Release();
        }

        [Fact]
        public void TestSetMaxHeaderTableSizeOverflow()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                var buf = Unpooled.Buffer();
                try
                {
                    hpackEncoder.SetMaxHeaderTableSize(buf, Http2CodecUtil.MaxHeaderTableSize + 1);
                }
                finally
                {
                    buf.Release();
                }
            });
        }

        /**
         * The encoder should not impose an arbitrary limit on the header size if
         * the server has not specified any limit.
         * @throws Http2Exception
         */
        [Fact]
        public void TestWillEncode16MBHeaderByDefault()
        {
            var buf = Unpooled.Buffer();
            var bigHeaderName = "x-big-header";
            int bigHeaderSize = 1024 * 1024 * 16;
            var bigHeaderVal = new string(new char[bigHeaderSize]).Replace('\0', 'X');
            IHttp2Headers headersIn = new DefaultHttp2Headers();
            headersIn.Add((AsciiString)"x-big-header", (AsciiString)bigHeaderVal);
            IHttp2Headers headersOut = new DefaultHttp2Headers();

            try
            {
                hpackEncoder.EncodeHeaders(0, buf, headersIn, NeverSensitiveDetector.Instance);
                hpackDecoder.SetMaxHeaderListSize(bigHeaderSize + 1024);
                hpackDecoder.Decode(0, buf, headersOut, false);
            }
            finally
            {
                buf.Release();
            }
            Assert.Equal(bigHeaderVal, headersOut.Get((AsciiString)bigHeaderName, null).ToString());
        }

        [Fact]
        public void TestSetMaxHeaderListSizeEnforcedAfterSet()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                var buf = Unpooled.Buffer();
                IHttp2Headers headers = new DefaultHttp2Headers();
                headers.Add(
                        (AsciiString)"x-big-header",
                        (AsciiString)new string(new char[1024 * 16]).Replace('\0', 'X')
                );

                hpackEncoder.SetMaxHeaderListSize(1000);

                try
                {
                    hpackEncoder.EncodeHeaders(0, buf, headers, NeverSensitiveDetector.Instance);
                }
                finally
                {
                    buf.Release();
                }
            });
        }
    }
}
