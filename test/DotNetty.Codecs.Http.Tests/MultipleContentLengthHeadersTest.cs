namespace DotNetty.Codecs.Http.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class MultipleContentLengthHeadersTest
    {
        public static IEnumerable<object[]> GetData()
        {
            return new object[][]
            {
                new object[] { false, false, false },
                new object[] { false, false, true },
                new object[] { false, true, false },
                new object[] { false, true, true },
                new object[] { true, false, false },
                new object[] { true, false, true },
                new object[] { true, true, false },
                new object[] { true, true, true }
            };
        }

        [Theory]
        [MemberData(nameof(GetData))]
        public void TestMultipleContentLengthHeadersBehavior(bool allowDuplicateContentLengths, bool sameValue, bool singleField)
        {
            var decoder = new HttpRequestDecoder(
                    HttpObjectDecoder.DefaultMaxInitialLineLength,
                    HttpObjectDecoder.DefaultMaxHeaderSize,
                    HttpObjectDecoder.DefaultMaxChunkSize,
                    HttpObjectDecoder.DefaultValidateHeaders,
                    HttpObjectDecoder.DefaultInitialBufferSize,
                    allowDuplicateContentLengths);
            var channel = new EmbeddedChannel(decoder);

            string requestStr = SetupRequestString(sameValue, singleField);
            Assert.True(channel.WriteInbound(Unpooled.CopiedBuffer(requestStr, Encoding.ASCII)));
            IHttpRequest request = channel.ReadInbound<IHttpRequest>();

            if (allowDuplicateContentLengths)
            {
                if (sameValue)
                {
                    AssertValid(request);
                    var contentLengths = request.Headers.GetAll(HttpHeaderNames.ContentLength);
                    Assert.Contains("1", contentLengths.ToString());
                    ILastHttpContent body = channel.ReadInbound<ILastHttpContent>();
                    Assert.Equal(1, body.Content.ReadableBytes);
                    Assert.Equal("a", body.Content.ReadCharSequence(1, Encoding.ASCII).ToString());
                }
                else
                {
                    AssertInvalid(request);
                }
            }
            else
            {
                AssertInvalid(request);
            }
            Assert.False(channel.Finish());
        }

        private string SetupRequestString(bool sameValue, bool singleField)
        {
            string firstValue = "1";
            string secondValue = sameValue ? firstValue : "2";
            string contentLength;
            if (singleField)
            {
                contentLength = "Content-Length: " + firstValue + ", " + secondValue + "\r\n\r\n";
            }
            else
            {
                contentLength = "Content-Length: " + firstValue + "\r\n" +
                                "Content-Length: " + secondValue + "\r\n\r\n";
            }
            return "PUT /some/path HTTP/1.1\r\n" +
                   contentLength +
                   "ab";
        }

        public static IEnumerable<object[]> GetData0()
        {
            return new object[][]
            {
                new object[] { false },
                new object[] { true },
            };
        }

        [Theory]
        [MemberData(nameof(GetData0))]
        public void TestDanglingComma(bool allowDuplicateContentLengths)
        {
            var decoder = new HttpRequestDecoder(
                    HttpObjectDecoder.DefaultMaxInitialLineLength,
                    HttpObjectDecoder.DefaultMaxHeaderSize,
                    HttpObjectDecoder.DefaultMaxChunkSize,
                    HttpObjectDecoder.DefaultValidateHeaders,
                    HttpObjectDecoder.DefaultInitialBufferSize,
                    allowDuplicateContentLengths);
            var channel = new EmbeddedChannel(decoder);

            string requestStr = "GET /some/path HTTP/1.1\r\n" +
                                "Content-Length: 1,\r\n" +
                                "Connection: close\n\n" +
                                "ab";
            Assert.True(channel.WriteInbound(Unpooled.CopiedBuffer(requestStr, Encoding.ASCII)));
            IHttpRequest request = channel.ReadInbound<IHttpRequest>();
            AssertInvalid(request);
            Assert.False(channel.Finish());
        }

        private static void AssertValid(IHttpRequest request)
        {
            Assert.False(request.Result.IsFailure);
        }

        private static void AssertInvalid(IHttpRequest request)
        {
            Assert.True(request.Result.IsFailure);
            Assert.IsType<ArgumentException>(request.Result.Cause);
            Assert.Contains("Multiple Content-Length values found", request.Result.Cause.Message);
        }
    }
}
