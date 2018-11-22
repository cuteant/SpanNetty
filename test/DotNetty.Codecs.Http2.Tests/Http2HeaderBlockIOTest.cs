
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using Xunit;

    /**
     * Tests for encoding/decoding HTTP2 header blocks.
     */
    public class Http2HeaderBlockIOTest : IDisposable
    {
        private DefaultHttp2HeadersDecoder decoder;
        private DefaultHttp2HeadersEncoder encoder;
        private IByteBuffer buffer;

        public Http2HeaderBlockIOTest()
        {
            this.encoder = new DefaultHttp2HeadersEncoder();
            this.decoder = new DefaultHttp2HeadersDecoder(false);
            this.buffer = Unpooled.Buffer();
        }

        public void Dispose()
        {
            this.buffer.Release();
        }

        [Fact]
        public void RoundtripShouldBeSuccessful()
        {
            var input = Headers();
            this.AssertRoundtripSuccessful(input);
        }

        [Fact]
        public void SuccessiveCallsShouldSucceed()
        {
            var input = new DefaultHttp2Headers();
            input.Method = new AsciiString("GET");
            input.Scheme = new AsciiString("https");
            input.Authority = new AsciiString("example.org");
            input.Path = new AsciiString("/some/path");
            input.Add(new AsciiString("accept"), new AsciiString("*/*"));
            this.AssertRoundtripSuccessful(input);

            input = new DefaultHttp2Headers();
            input.Method = new AsciiString("GET");
            input.Scheme = new AsciiString("https");
            input.Authority = new AsciiString("example.org");
            input.Path = new AsciiString("/some/path/resource1");
            input.Add(new AsciiString("accept"), new AsciiString("image/jpeg"));
            input.Add(new AsciiString("cache-control"), new AsciiString("no-cache"));
            this.AssertRoundtripSuccessful(input);

            input = new DefaultHttp2Headers();
            input.Method = new AsciiString("GET");
            input.Scheme = new AsciiString("https");
            input.Authority = new AsciiString("example.org");
            input.Path = new AsciiString("/some/path/resource2");
            input.Add(new AsciiString("accept"), new AsciiString("image/png"));
            input.Add(new AsciiString("cache-control"), new AsciiString("no-cache"));
            this.AssertRoundtripSuccessful(input);
        }

        [Fact]
        public void SetMaxHeaderSizeShouldBeSuccessful()
        {
            encoder.SetMaxHeaderTableSize(10);
            var input = Headers();
            this.AssertRoundtripSuccessful(input);
            Assert.Equal(10, decoder.MaxHeaderTableSize);
        }

        private void AssertRoundtripSuccessful(IHttp2Headers input)
        {
            encoder.EncodeHeaders(3 /* randomly chosen */, input, buffer);

            IHttp2Headers output = decoder.DecodeHeaders(0, buffer);
            Assert.Equal(input, output);
        }

        private static IHttp2Headers Headers()
        {
            var headers = new DefaultHttp2Headers(false);
            headers.Method = new AsciiString("GET");
            headers.Scheme = new AsciiString("https");
            headers.Authority = new AsciiString("example.org");
            headers.Path = new AsciiString("/some/path/resource2");
            headers.Add(new AsciiString("accept"), new AsciiString("image/png"));
            headers.Add(new AsciiString("cache-control"), new AsciiString("no-cache"));
            headers.Add(new AsciiString("custom"), new AsciiString("value1"));
            headers.Add(new AsciiString("custom"), new AsciiString("value2"));
            headers.Add(new AsciiString("custom"), new AsciiString("value3"));
            headers.Add(new AsciiString("custom"), new AsciiString("custom4"));
            headers.Add(Http2TestUtil.RandomString(), Http2TestUtil.RandomString());
            return headers;
        }
    }
}
