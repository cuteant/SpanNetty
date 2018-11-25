
namespace DotNetty.Codecs.Http2.Tests
{
    using DotNetty.Common.Utilities;
    using Xunit;

    public class DefaultHttp2HeadersTest
    {
        [Fact]
        public void NullHeaderNameNotAllowed()
        {
            Assert.Throws<Http2Exception>(() => new DefaultHttp2Headers().Add(null, (AsciiString)"foo"));
        }

        [Fact]
        public void EmptyHeaderNameNotAllowed()
        {
            Assert.Throws<Http2Exception>(() => new DefaultHttp2Headers().Add((AsciiString)StringUtil.EmptyString, (AsciiString)"foo"));
        }

        [Fact]
        public void TestPseudoHeadersMustComeFirstWhenIterating()
        {
            var headers = NewHeaders();

            VerifyPseudoHeadersFirst(headers);
            VerifyAllPseudoHeadersPresent(headers);
        }

        [Fact]
        public void TestPseudoHeadersWithRemovePreservesPseudoIterationOrder()
        {
            var headers = NewHeaders();

            var nonPseudoHeaders = new DefaultHttp2Headers();
            foreach (var entry in headers)
            {
                if (entry.Key.Count == 0 || entry.Key[0] != ':' &&
                    !nonPseudoHeaders.Contains(entry.Key))
                {
                    nonPseudoHeaders.Add(entry.Key, entry.Value);
                }
            }

            Assert.False(nonPseudoHeaders.IsEmpty);

            // Remove all the non-pseudo headers and verify
            foreach (var nonPseudoHeaderEntry in nonPseudoHeaders)
            {
                Assert.True(headers.Remove(nonPseudoHeaderEntry.Key));
                VerifyPseudoHeadersFirst(headers);
                VerifyAllPseudoHeadersPresent(headers);
            }

            // Add back all non-pseudo headers
            foreach (var nonPseudoHeaderEntry in nonPseudoHeaders)
            {
                headers.Add(nonPseudoHeaderEntry.Key, AsciiString.Of("goo"));
                VerifyPseudoHeadersFirst(headers);
                VerifyAllPseudoHeadersPresent(headers);
            }
        }

        [Fact]
        public void TestPseudoHeadersWithClearDoesNotLeak()
        {
            var headers = NewHeaders();

            Assert.False(headers.IsEmpty);
            headers.Clear();
            Assert.True(headers.IsEmpty);

            // Combine 2 headers together, make sure pseudo headers stay up front.
            headers.Add((AsciiString)"name1", (AsciiString)"value1");
            headers.Scheme = (AsciiString)"nothing";
            VerifyPseudoHeadersFirst(headers);

            var other = new DefaultHttp2Headers();
            other.Add((AsciiString)"name2", (AsciiString)"value2");
            other.Authority = (AsciiString)"foo";
            VerifyPseudoHeadersFirst(other);

            headers.Add(other);
            VerifyPseudoHeadersFirst(headers);

            // Make sure the headers are what we expect them to be, and no leaking behind the scenes.
            Assert.Equal(4, headers.Size);
            AssertEx.Equal("value1", headers.Get((AsciiString)"name1", null));
            AssertEx.Equal("value2", headers.Get((AsciiString)"name2", null));
            AssertEx.Equal("nothing", headers.Scheme);
            AssertEx.Equal("foo", headers.Authority);
        }

        [Fact]
        public void TestSetHeadersOrdersPseudoHeadersCorrectly()
        {
            var headers = NewHeaders();
            var other = new DefaultHttp2Headers();
            other.Add((AsciiString)"name2", (AsciiString)"value2");
            other.Authority = (AsciiString)"foo";

            headers.Set(other);
            VerifyPseudoHeadersFirst(headers);
            Assert.Equal(other.Size, headers.Size);
            AssertEx.Equal("foo", headers.Authority);
            AssertEx.Equal("value2", headers.Get((AsciiString)"name2", null));
        }

        [Fact]
        public void TestSetAllOrdersPseudoHeadersCorrectly()
        {
            var headers = NewHeaders();
            var other = new DefaultHttp2Headers();
            other.Add((AsciiString)"name2", (AsciiString)"value2");
            other.Authority = (AsciiString)"foo";

            int headersSizeBefore = headers.Size;
            headers.SetAll(other);
            VerifyPseudoHeadersFirst(headers);
            VerifyAllPseudoHeadersPresent(headers);
            Assert.Equal(headersSizeBefore + 1, headers.Size);
            Assert.Equal((AsciiString)"foo", headers.Authority);
            Assert.Equal((AsciiString)"value2", headers.Get((AsciiString)"name2", null));
        }

        [Fact]
        public void TestHeaderNameValidation()
        {
            var headers = NewHeaders();
            Assert.Throws<Http2Exception>(() => headers.Add(AsciiString.Of("Foo"), AsciiString.Of("foo")));
        }

        [Fact]
        public void TestClearResetsPseudoHeaderDivision()
        {
            DefaultHttp2Headers http2Headers = new DefaultHttp2Headers();
            http2Headers.Method = (AsciiString)"POST";
            http2Headers.Set((AsciiString)"some", (AsciiString)"value");
            http2Headers.Clear();
            http2Headers.Method = (AsciiString)"GET";
            Assert.Equal(1, http2Headers.Names().Count);
        }

        [Fact]
        public void TestContainsNameAndValue()
        {
            var headers = NewHeaders();
            Assert.True(headers.Contains((AsciiString)"name1", (AsciiString)"value2"));
            Assert.False(headers.Contains((AsciiString)"name1", (AsciiString)"Value2"));
            Assert.True(headers.Contains((AsciiString)"2name", (AsciiString)"Value3", true));
            Assert.False(headers.Contains((AsciiString)"2name", (AsciiString)"Value3", false));
        }

        private static void VerifyAllPseudoHeadersPresent(IHttp2Headers headers)
        {
            foreach (PseudoHeaderName pseudoName in PseudoHeaderName.All)
            {
                Assert.NotNull(headers.Get(pseudoName.Value, null));
            }
        }

        internal static void VerifyPseudoHeadersFirst(IHttp2Headers headers)
        {
            ICharSequence lastNonPseudoName = null;
            foreach (var entry in headers)
            {
                if (entry.Key.Count == 0 || entry.Key[0] != ':')
                {
                    lastNonPseudoName = entry.Key;
                }
                else if (lastNonPseudoName != null)
                {
                    Assert.True(false, "All pseudo headers must be fist in iteration. Pseudo header " + entry.Key +
                            " is after a non pseudo header " + lastNonPseudoName);
                }
            }
        }

        private static IHttp2Headers NewHeaders()
        {
            var headers = new DefaultHttp2Headers();
            headers.Add(AsciiString.Of("name1"), new[] { AsciiString.Of("value1"), AsciiString.Of("value2") });
            headers.Method = AsciiString.Of("POST");
            headers.Add(AsciiString.Of("2name"), AsciiString.Of("value3"));
            headers.Path = AsciiString.Of("/index.html");
            headers.Status = AsciiString.Of("200");
            headers.Authority = AsciiString.Of("netty.io");
            headers.Add(AsciiString.Of("name3"), AsciiString.Of("value4"));
            headers.Scheme = AsciiString.Of("https");
            return headers;
        }
    }
}
