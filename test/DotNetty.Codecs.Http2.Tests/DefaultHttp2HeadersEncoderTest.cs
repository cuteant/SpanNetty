
namespace DotNetty.Codecs.Http2.Tests
{
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using Xunit;

    /**
     * Tests for {@link DefaultHttp2HeadersEncoder}.
     */
    public class DefaultHttp2HeadersEncoderTest
    {

        private DefaultHttp2HeadersEncoder encoder;

        public DefaultHttp2HeadersEncoderTest()
        {
            encoder = new DefaultHttp2HeadersEncoder(NeverSensitiveDetector.Instance, Http2TestUtil.NewTestEncoder());
        }

        [Fact]
        public void EncodeShouldSucceed()
        {
            var headers = Headers();
            var buf = Unpooled.Buffer();
            try
            {
                encoder.EncodeHeaders(3 /* randomly chosen */, headers, buf);
                Assert.True(buf.WriterIndex > 0);
            }
            finally
            {
                buf.Release();
            }
        }

        [Fact]
        public void HeadersExceedMaxSetSizeShouldFail()
        {
            Assert.Throws<HeaderListSizeException>(() =>
            {
                IHttp2Headers headers = Headers();
                encoder.SetMaxHeaderListSize(2);
                encoder.EncodeHeaders(3 /* randomly chosen */, headers, Unpooled.Buffer());
            });
        }

        private static IHttp2Headers Headers()
        {
            var headers = new DefaultHttp2Headers();
            headers.Method = new AsciiString("GET");
            headers.Add(new AsciiString("a"), new AsciiString("1"))
                   .Add(new AsciiString("a"), new AsciiString("2"));
            return headers;
        }
    }
}
