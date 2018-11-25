
namespace DotNetty.Codecs.Http2.Tests
{
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using Xunit;

    public class DefaultHttp2HeadersDecoderTest
    {
        private DefaultHttp2HeadersDecoder decoder;

        public DefaultHttp2HeadersDecoderTest()
        {
            this.decoder = new DefaultHttp2HeadersDecoder(false);
        }

        [Fact]
        public void DecodeShouldSucceed()
        {
            var buf = Encode(B(":method"), B("GET"), B("akey"), B("avalue"), Http2TestUtil.RandomBytes(), Http2TestUtil.RandomBytes());
            try
            {
                var headers = decoder.DecodeHeaders(0, buf);
                Assert.Equal(3, headers.Size);
                AssertEx.Equal("GET", headers.Method);
                AssertEx.Equal("avalue", headers.Get(new AsciiString("akey"), null));
            }
            finally
            {
                buf.Release();
            }
        }

        [Fact]
        public void TestExceedHeaderSize()
        {
            int maxListSize = 100;
            decoder.Configuration.SetMaxHeaderListSize(maxListSize, maxListSize);
            var buf = Encode(Http2TestUtil.RandomBytes(maxListSize), Http2TestUtil.RandomBytes(1));
            Assert.Throws<Http2Exception>(() => decoder.DecodeHeaders(0, buf));
            buf.Release();
        }

        [Fact]
        public void DecodeLargerThanHeaderListSizeButLessThanGoAway()
        {
            decoder.SetMaxHeaderListSize(Http2CodecUtil.MinHeaderListSize, Http2CodecUtil.MaxHeaderListSize);
            var buf = Encode(B(":method"), B("GET"));
            int streamId = 1;
            var e = Assert.Throws<HeaderListSizeException>(() => decoder.DecodeHeaders(streamId, buf));
            Assert.Equal(streamId, e.StreamId);
            buf.Release();
        }

        [Fact]
        public void DecodeLargerThanHeaderListSizeButLessThanGoAwayWithInitialDecoderSettings()
        {
            var buf = Encode(B(":method"), B("GET"), B("test_header"),
                B("A".PadRight(9000, 'A')));
            int streamId = 1;
            var e = Assert.Throws<HeaderListSizeException>(() => decoder.DecodeHeaders(streamId, buf));
            Assert.Equal(streamId, e.StreamId);
            buf.Release();
        }

        [Fact]
        public void DecodeLargerThanHeaderListSizeGoAway()
        {
            decoder.SetMaxHeaderListSize(Http2CodecUtil.MinHeaderListSize, Http2CodecUtil.MaxHeaderListSize);
            var buf = Encode(B(":method"), B("GET"));
            int streamId = 1;
            var e = Assert.Throws<HeaderListSizeException>(() => decoder.DecodeHeaders(streamId, buf));
            Assert.Equal(Http2Error.ProtocolError, e.Error);
            buf.Release();
        }

        private static byte[] B(string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        private static IByteBuffer Encode(params byte[][] entries)
        {
            HpackEncoder hpackEncoder = Http2TestUtil.NewTestEncoder();
            var output = Unpooled.Buffer();
            var http2Headers = new DefaultHttp2Headers(false);
            for (int ix = 0; ix < entries.Length;)
            {
                http2Headers.Add(new AsciiString(entries[ix++], false), new AsciiString(entries[ix++], false));
            }
            hpackEncoder.EncodeHeaders(3 /* randomly chosen */, output, http2Headers, NeverSensitiveDetector.Instance);
            return output;
        }

    }
}
