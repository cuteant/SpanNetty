
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Utilities;
    using Xunit;

    public class HttpConversionUtilTest
    {
        [Fact]
        public void SetHttp2AuthorityWithoutUserInfo()
        {
            var headers = new DefaultHttp2Headers();

            HttpConversionUtil.SetHttp2Authority("foo", headers);
            Assert.Equal(new AsciiString("foo"), headers.Authority);
        }

        [Fact]
        public void SetHttp2AuthorityWithUserInfo()
        {
            var headers = new DefaultHttp2Headers();

            HttpConversionUtil.SetHttp2Authority("info@foo", headers);
            Assert.Equal(new AsciiString("foo"), headers.Authority);

            HttpConversionUtil.SetHttp2Authority("@foo.bar", headers);
            Assert.Equal(new AsciiString("foo.bar"), headers.Authority);
        }

        [Fact]
        public void SetHttp2AuthorityNullOrEmpty()
        {
            var headers = new DefaultHttp2Headers();

            HttpConversionUtil.SetHttp2Authority(null, headers);
            Assert.Null(headers.Authority);

            HttpConversionUtil.SetHttp2Authority("", headers);
            Assert.Same(AsciiString.Empty, headers.Authority);
        }

        [Fact]
        public void SetHttp2AuthorityWithEmptyAuthority()
        {
            Assert.Throws<ArgumentException>(() => HttpConversionUtil.SetHttp2Authority("info@", new DefaultHttp2Headers()));
        }

        [Fact]
        public void StripTEHeaders()
        {
            HttpHeaders inHeaders = new DefaultHttpHeaders();
            inHeaders.Add(HttpHeaderNames.Te, HttpHeaderValues.Gzip);
            var output = new DefaultHttp2Headers();
            HttpConversionUtil.ToHttp2Headers(inHeaders, output);
            Assert.True(output.IsEmpty);
        }

        [Fact]
        public void StripTEHeadersExcludingTrailers()
        {
            HttpHeaders inHeaders = new DefaultHttpHeaders();
            inHeaders.Add(HttpHeaderNames.Te, HttpHeaderValues.Gzip);
            inHeaders.Add(HttpHeaderNames.Te, HttpHeaderValues.Trailers);
            var output = new DefaultHttp2Headers();
            HttpConversionUtil.ToHttp2Headers(inHeaders, output);
            Assert.Same(HttpHeaderValues.Trailers, output.Get(HttpHeaderNames.Te, null));
        }

        [Fact]
        public void StripTEHeadersCsvSeparatedExcludingTrailers()
        {
            HttpHeaders inHeaders = new DefaultHttpHeaders();
            inHeaders.Add(HttpHeaderNames.Te, HttpHeaderValues.Gzip + "," + HttpHeaderValues.Trailers);
            var output = new DefaultHttp2Headers();
            HttpConversionUtil.ToHttp2Headers(inHeaders, output);
            Assert.Same(HttpHeaderValues.Trailers, output.Get(HttpHeaderNames.Te, null));
        }

        [Fact]
        public void StripTEHeadersCsvSeparatedAccountsForValueSimilarToTrailers()
        {
            HttpHeaders inHeaders = new DefaultHttpHeaders();
            inHeaders.Add(HttpHeaderNames.Te, HttpHeaderValues.Gzip + "," + HttpHeaderValues.Trailers + "foo");
            var output = new DefaultHttp2Headers();
            HttpConversionUtil.ToHttp2Headers(inHeaders, output);
            Assert.False(output.Contains(HttpHeaderNames.Te));
        }

        [Fact]
        public void StripTEHeadersAccountsForValueSimilarToTrailers()
        {
            HttpHeaders inHeaders = new DefaultHttpHeaders();
            inHeaders.Add(HttpHeaderNames.Te, HttpHeaderValues.Trailers + "foo");
            var output = new DefaultHttp2Headers();
            HttpConversionUtil.ToHttp2Headers(inHeaders, output);
            Assert.False(output.Contains(HttpHeaderNames.Te));
        }

        [Fact]
        public void StripTEHeadersAccountsForOWS()
        {
            HttpHeaders inHeaders = new DefaultHttpHeaders();
            inHeaders.Add(HttpHeaderNames.Te, " " + HttpHeaderValues.Trailers + " ");
            var output = new DefaultHttp2Headers();
            HttpConversionUtil.ToHttp2Headers(inHeaders, output);
            Assert.Same(HttpHeaderValues.Trailers, output.Get(HttpHeaderNames.Te, null));
        }

        [Fact]
        public void StripConnectionHeadersAndNominees()
        {
            HttpHeaders inHeaders = new DefaultHttpHeaders();
            inHeaders.Add(HttpHeaderNames.Connection, "foo");
            inHeaders.Add((AsciiString)"foo", "bar");
            var output = new DefaultHttp2Headers();
            HttpConversionUtil.ToHttp2Headers(inHeaders, output);
            Assert.True(output.IsEmpty);
        }

        [Fact]
        public void StripConnectionNomineesWithCsv()
        {
            HttpHeaders inHeaders = new DefaultHttpHeaders();
            inHeaders.Add(HttpHeaderNames.Connection, "foo,  bar");
            inHeaders.Add((AsciiString)"foo", "baz");
            inHeaders.Add((AsciiString)"bar", "qux");
            inHeaders.Add((AsciiString)"hello", "world");
            var output = new DefaultHttp2Headers();
            HttpConversionUtil.ToHttp2Headers(inHeaders, output);
            Assert.Equal(1, output.Size);
            Assert.Same("world", output.Get((AsciiString)"hello", null).ToString());
        }
    }
}
