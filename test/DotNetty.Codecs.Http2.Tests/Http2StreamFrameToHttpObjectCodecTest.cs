
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Handlers.Tls;
    using DotNetty.Tests.Common;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class Http2StreamFrameToHttpObjectCodecTest
    {
        [Fact]
        public void TestUpgradeEmptyFullResponse()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(true));
            Assert.True(ch.WriteOutbound(new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK)));

            var headersFrame = ch.ReadOutbound<IHttp2HeadersFrame>();
            Assert.Equal("200", headersFrame.Headers.Status.ToString());
            Assert.True(headersFrame.IsEndStream);
            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void Encode100ContinueAsHttp2HeadersFrameThatIsNotEndStream()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(true));
            Assert.True(ch.WriteOutbound(new DefaultFullHttpResponse(
                    HttpVersion.Http11, HttpResponseStatus.Continue)));

            var headersFrame = ch.ReadOutbound<IHttp2HeadersFrame>();
            Assert.Equal("100", headersFrame.Headers.Status.ToString());
            Assert.False(headersFrame.IsEndStream);
            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void EncodeNonFullHttpResponse100ContinueIsRejected()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(true));
            Assert.Throws<EncoderException>(() =>
            {
                try
                {
                    ch.WriteOutbound(new DefaultHttpResponse(
                            HttpVersion.Http11, HttpResponseStatus.Continue));
                }
                catch (AggregateException e)
                {
                    throw e.InnerException;
                }
                finally
                {
                    ch.FinishAndReleaseAll();
                }
            });
        }

        [Fact]
        public void TestUpgradeNonEmptyFullResponse()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(true));
            var hello = Unpooled.CopiedBuffer("hello world", Encoding.UTF8);
            Assert.True(ch.WriteOutbound(new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK, hello)));

            var headersFrame = ch.ReadOutbound<IHttp2HeadersFrame>();
            Assert.Equal("200", headersFrame.Headers.Status.ToString());
            Assert.False(headersFrame.IsEndStream);

            var dataFrame = ch.ReadOutbound<IHttp2DataFrame>();
            try
            {
                Assert.Equal("hello world", dataFrame.Content.ToString(Encoding.UTF8));
                Assert.True(dataFrame.IsEndStream);
            }
            finally
            {
                dataFrame.Release();
            }

            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestUpgradeEmptyFullResponseWithTrailers()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(true));
            var response = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            HttpHeaders trailers = response.TrailingHeaders;
            trailers.Set((AsciiString)"key", "value");
            Assert.True(ch.WriteOutbound(response));

            var headersFrame = ch.ReadOutbound<IHttp2HeadersFrame>();
            Assert.Equal("200", headersFrame.Headers.Status.ToString());
            Assert.False(headersFrame.IsEndStream);

            var trailersFrame = ch.ReadOutbound<IHttp2HeadersFrame>();
            Assert.Equal("value", trailersFrame.Headers.Get((AsciiString)"key", null));
            Assert.True(trailersFrame.IsEndStream);

            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestUpgradeNonEmptyFullResponseWithTrailers()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(true));
            var hello = Unpooled.CopiedBuffer("hello world", Encoding.UTF8);
            var response = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK, hello);
            HttpHeaders trailers = response.TrailingHeaders;
            trailers.Set((AsciiString)"key", "value");
            Assert.True(ch.WriteOutbound(response));

            var headersFrame = ch.ReadOutbound<IHttp2HeadersFrame>();
            Assert.Equal("200", headersFrame.Headers.Status.ToString());
            Assert.False(headersFrame.IsEndStream);

            var dataFrame = ch.ReadOutbound<IHttp2DataFrame>();
            try
            {
                Assert.Equal("hello world", dataFrame.Content.ToString(Encoding.UTF8));
                Assert.False(dataFrame.IsEndStream);
            }
            finally
            {
                dataFrame.Release();
            }

            var trailersFrame = ch.ReadOutbound<IHttp2HeadersFrame>();
            Assert.Equal("value", trailersFrame.Headers.Get((AsciiString)"key", null));
            Assert.True(trailersFrame.IsEndStream);

            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestUpgradeHeaders()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(true));
            var response = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            Assert.True(ch.WriteOutbound(response));

            var headersFrame = ch.ReadOutbound<IHttp2HeadersFrame>();
            Assert.Equal("200", headersFrame.Headers.Status.ToString());
            Assert.False(headersFrame.IsEndStream);

            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestUpgradeChunk()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(true));
            var hello = Unpooled.CopiedBuffer("hello world", Encoding.UTF8);
            var content = new DefaultHttpContent(hello);
            Assert.True(ch.WriteOutbound(content));

            var dataFrame = ch.ReadOutbound<IHttp2DataFrame>();
            try
            {
                Assert.Equal("hello world", dataFrame.Content.ToString(Encoding.UTF8));
                Assert.False(dataFrame.IsEndStream);
            }
            finally
            {
                dataFrame.Release();
            }

            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestUpgradeEmptyEnd()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(true));
            var end = EmptyLastHttpContent.Default;
            Assert.True(ch.WriteOutbound(end));

            var emptyFrame = ch.ReadOutbound<IHttp2DataFrame>();
            try
            {
                Assert.Equal(0, emptyFrame.Content.ReadableBytes);
                Assert.True(emptyFrame.IsEndStream);
            }
            finally
            {
                emptyFrame.Release();
            }

            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestUpgradeDataEnd()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(true));
            var hello = Unpooled.CopiedBuffer("hello world", Encoding.UTF8);
            var end = new DefaultLastHttpContent(hello, true);
            Assert.True(ch.WriteOutbound(end));

            var dataFrame = ch.ReadOutbound<IHttp2DataFrame>();
            try
            {
                Assert.Equal("hello world", dataFrame.Content.ToString(Encoding.UTF8));
                Assert.True(dataFrame.IsEndStream);
            }
            finally
            {
                dataFrame.Release();
            }

            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestUpgradeTrailers()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(true));
            var trailers = new DefaultLastHttpContent(Unpooled.Empty, true);
            HttpHeaders headers = trailers.TrailingHeaders;
            headers.Set((AsciiString)"key", "value");
            Assert.True(ch.WriteOutbound(trailers));

            var headerFrame = ch.ReadOutbound<IHttp2HeadersFrame>();
            Assert.Equal("value", headerFrame.Headers.Get((AsciiString)"key", null));
            Assert.True(headerFrame.IsEndStream);

            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestUpgradeDataEndWithTrailers()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(true));
            var hello = Unpooled.CopiedBuffer("hello world", Encoding.UTF8);
            var trailers = new DefaultLastHttpContent(hello, true);
            HttpHeaders headers = trailers.TrailingHeaders;
            headers.Set((AsciiString)"key", "value");
            Assert.True(ch.WriteOutbound(trailers));

            var dataFrame = ch.ReadOutbound<IHttp2DataFrame>();
            try
            {
                Assert.Equal("hello world", dataFrame.Content.ToString(Encoding.UTF8));
                Assert.False(dataFrame.IsEndStream);
            }
            finally
            {
                dataFrame.Release();
            }

            var headerFrame = ch.ReadOutbound<IHttp2HeadersFrame>();
            Assert.Equal("value", headerFrame.Headers.Get((AsciiString)"key", null));
            Assert.True(headerFrame.IsEndStream);

            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestDowngradeHeaders()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(true));
            IHttp2Headers headers = new DefaultHttp2Headers
            {
                Path = (AsciiString)"/",
                Method = (AsciiString)"GET"
            };

            Assert.True(ch.WriteInbound(new DefaultHttp2HeadersFrame(headers)));

            var request = ch.ReadInbound<IHttpRequest>();
            Assert.Equal("/", request.Uri);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(HttpVersion.Http11, request.ProtocolVersion);
            Assert.False(request is IFullHttpRequest);
            Assert.True(HttpUtil.IsTransferEncodingChunked(request));

            Assert.Null(ch.ReadInbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestDowngradeHeadersWithContentLength()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(true));
            IHttp2Headers headers = new DefaultHttp2Headers
            {
                Path = (AsciiString)"/",
                Method = (AsciiString)"GET"
            };
            headers.SetInt((AsciiString)"content-length", 0);

            Assert.True(ch.WriteInbound(new DefaultHttp2HeadersFrame(headers)));

            var request = ch.ReadInbound<IHttpRequest>();
            Assert.Equal("/", request.Uri);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(HttpVersion.Http11, request.ProtocolVersion);
            Assert.False(request is IFullHttpRequest);
            Assert.False(HttpUtil.IsTransferEncodingChunked(request));

            Assert.Null(ch.ReadInbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestDowngradeFullHeaders()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(true));
            IHttp2Headers headers = new DefaultHttp2Headers
            {
                Path = (AsciiString)"/",
                Method = (AsciiString)"GET"
            };

            Assert.True(ch.WriteInbound(new DefaultHttp2HeadersFrame(headers, true)));

            var request = ch.ReadInbound<IFullHttpRequest>();
            try
            {
                Assert.Equal("/", request.Uri);
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal(HttpVersion.Http11, request.ProtocolVersion);
                Assert.Equal(0, request.Content.ReadableBytes);
                Assert.True(request.TrailingHeaders.IsEmpty);
                Assert.False(HttpUtil.IsTransferEncodingChunked(request));
            }
            finally
            {
                request.Release();
            }

            Assert.Null(ch.ReadInbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestDowngradeTrailers()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(true));
            IHttp2Headers headers = new DefaultHttp2Headers();
            headers.Set((AsciiString)"key", (AsciiString)"value");
            Assert.True(ch.WriteInbound(new DefaultHttp2HeadersFrame(headers, true)));

            var trailers = ch.ReadInbound<ILastHttpContent>();
            try
            {
                Assert.Equal(0, trailers.Content.ReadableBytes);
                Assert.Equal("value", trailers.TrailingHeaders.Get((AsciiString)"key", null));
                Assert.False(trailers is IFullHttpRequest);
            }
            finally
            {
                trailers.Release();
            }

            Assert.Null(ch.ReadInbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestDowngradeData()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(true));
            var hello = Unpooled.CopiedBuffer("hello world", Encoding.UTF8);
            Assert.True(ch.WriteInbound(new DefaultHttp2DataFrame(hello)));

            var content = ch.ReadInbound<IHttpContent>();
            try
            {
                Assert.Equal("hello world", content.Content.ToString(Encoding.UTF8));
                Assert.False(content is ILastHttpContent);
            }
            finally
            {
                content.Release();
            }

            Assert.Null(ch.ReadInbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestDowngradeEndData()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(true));
            var hello = Unpooled.CopiedBuffer("hello world", Encoding.UTF8);
            Assert.True(ch.WriteInbound(new DefaultHttp2DataFrame(hello, true)));

            var content = ch.ReadInbound<ILastHttpContent>();
            try
            {
                Assert.Equal("hello world", content.Content.ToString(Encoding.UTF8));
                Assert.True(content.TrailingHeaders.IsEmpty);
            }
            finally
            {
                content.Release();
            }

            Assert.Null(ch.ReadInbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestPassThroughOther()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(true));
            IHttp2ResetFrame reset = new DefaultHttp2ResetFrame(Http2Error.NoError);
            IHttp2GoAwayFrame goaway = new DefaultHttp2GoAwayFrame(Http2Error.NoError);
            Assert.True(ch.WriteInbound(reset));
            Assert.True(ch.WriteInbound(goaway.Retain()));

            Assert.Equal(reset, ch.ReadInbound<IHttp2ResetFrame>());

            var frame = ch.ReadInbound<IHttp2GoAwayFrame>();
            try
            {
                Assert.Equal(goaway, frame);
                Assert.Null(ch.ReadInbound());
                Assert.False(ch.Finish());
            }
            finally
            {
                goaway.Release();
                frame.Release();
            }
        }

        // client-specific tests
        [Fact]
        public void TestEncodeEmptyFullRequest()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(false));
            Assert.True(ch.WriteOutbound(new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/hello/world")));

            var headersFrame = ch.ReadOutbound<IHttp2HeadersFrame>();
            IHttp2Headers headers = headersFrame.Headers;

            Assert.Equal("http", headers.Scheme.ToString());
            Assert.Equal("GET", headers.Method.ToString());
            Assert.Equal("/hello/world", headers.Path.ToString());
            Assert.True(headersFrame.IsEndStream);

            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        sealed class TestChannelOutboundHandlerAdapter : ChannelHandlerAdapter
        {
            readonly ConcurrentQueue<IHttp2StreamFrame> frames;

            public TestChannelOutboundHandlerAdapter(ConcurrentQueue<IHttp2StreamFrame> frames)
            {
                this.frames = frames;
            }

            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                if (message is IHttp2StreamFrame streamFrame)
                {
                    frames.Enqueue(streamFrame);
                    context.WriteAsync(Unpooled.Empty, promise);
                }
                else
                {
                    context.WriteAsync(message, promise);
                }
            }
        }

        [Fact]
        public void TestEncodeHttpsSchemeWhenSslHandlerExists()
        {
            var frames = new ConcurrentQueue<IHttp2StreamFrame>();
            var ch = new EmbeddedChannel(new TestChannelOutboundHandlerAdapter(frames),
                                         new Http2StreamFrameToHttpObjectCodec(false));

            try
            {
                var req = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/hello/world");
                Assert.True(ch.WriteOutbound(req));

                ch.FinishAndReleaseAll();

                frames.TryDequeue(out var streamFrame);
                var headersFrame = (IHttp2HeadersFrame)streamFrame;
                var headers = headersFrame.Headers;
                Assert.Equal("http", headers.Scheme);
                Assert.Equal("GET", headers.Method);
                Assert.Equal("/hello/world", headers.Path);
                Assert.True(headersFrame.IsEndStream);
                Assert.False(frames.TryDequeue(out _));
            }
            finally
            {
                ch.FinishAndReleaseAll();
            }
        }

        [Fact]
        public void TestEncodeNonEmptyFullRequest()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(false));
            IByteBuffer hello = Unpooled.CopiedBuffer("hello world", Encoding.UTF8);
            Assert.True(ch.WriteOutbound(new DefaultFullHttpRequest(
                    HttpVersion.Http11, HttpMethod.Put, "/hello/world", hello)));

            var headersFrame = ch.ReadOutbound<IHttp2HeadersFrame>();
            IHttp2Headers headers = headersFrame.Headers;

            Assert.Equal("http", headers.Scheme);
            Assert.Equal("PUT", headers.Method);
            Assert.Equal("/hello/world", headers.Path);
            Assert.False(headersFrame.IsEndStream);

            var dataFrame = ch.ReadOutbound<IHttp2DataFrame>();
            try
            {
                Assert.Equal("hello world", dataFrame.Content.ToString(Encoding.UTF8));
                Assert.True(dataFrame.IsEndStream);
            }
            finally
            {
                dataFrame.Release();
            }

            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestEncodeEmptyFullRequestWithTrailers()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(false));
            IFullHttpRequest request = new DefaultFullHttpRequest(
                    HttpVersion.Http11, HttpMethod.Put, "/hello/world");

            HttpHeaders trailers = request.TrailingHeaders;
            trailers.Set((AsciiString)"key", "value");
            Assert.True(ch.WriteOutbound(request));

            var headersFrame = ch.ReadOutbound<IHttp2HeadersFrame>();
            IHttp2Headers headers = headersFrame.Headers;

            Assert.Equal("http", headers.Scheme);
            Assert.Equal("PUT", headers.Method);
            Assert.Equal("/hello/world", headers.Path);
            Assert.False(headersFrame.IsEndStream);

            IHttp2HeadersFrame trailersFrame = ch.ReadOutbound<IHttp2HeadersFrame>();
            Assert.Equal("value", trailersFrame.Headers.Get((AsciiString)"key", null));
            Assert.True(trailersFrame.IsEndStream);

            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestEncodeNonEmptyFullRequestWithTrailers()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(false));
            IByteBuffer hello = Unpooled.CopiedBuffer("hello world", Encoding.UTF8);
            IFullHttpRequest request = new DefaultFullHttpRequest(
                    HttpVersion.Http11, HttpMethod.Put, "/hello/world", hello);

            HttpHeaders trailers = request.TrailingHeaders;
            trailers.Set((AsciiString)"key", "value");
            Assert.True(ch.WriteOutbound(request));

            var headersFrame = ch.ReadOutbound<IHttp2HeadersFrame>();
            IHttp2Headers headers = headersFrame.Headers;

            Assert.Equal("http", headers.Scheme);
            Assert.Equal("PUT", headers.Method);
            Assert.Equal("/hello/world", headers.Path);
            Assert.False(headersFrame.IsEndStream);

            var dataFrame = ch.ReadOutbound<IHttp2DataFrame>();
            try
            {
                Assert.Equal("hello world", dataFrame.Content.ToString(Encoding.UTF8));
                Assert.False(dataFrame.IsEndStream);
            }
            finally
            {
                dataFrame.Release();
            }

            IHttp2HeadersFrame trailersFrame = ch.ReadOutbound<IHttp2HeadersFrame>();
            Assert.Equal("value", trailersFrame.Headers.Get((AsciiString)"key", null));
            Assert.True(trailersFrame.IsEndStream);

            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestEncodeRequestHeaders()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(false));
            IHttpRequest request = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/hello/world");
            Assert.True(ch.WriteOutbound(request));

            var headersFrame = ch.ReadOutbound<IHttp2HeadersFrame>();
            IHttp2Headers headers = headersFrame.Headers;

            Assert.Equal("http", headers.Scheme);
            Assert.Equal("GET", headers.Method);
            Assert.Equal("/hello/world", headers.Path);
            Assert.False(headersFrame.IsEndStream);

            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestEncodeChunkAsClient()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(false));
            IByteBuffer hello = Unpooled.CopiedBuffer("hello world", Encoding.UTF8);
            IHttpContent content = new DefaultHttpContent(hello);
            Assert.True(ch.WriteOutbound(content));

            var dataFrame = ch.ReadOutbound<IHttp2DataFrame>();
            try
            {
                Assert.Equal("hello world", dataFrame.Content.ToString(Encoding.UTF8));
                Assert.False(dataFrame.IsEndStream);
            }
            finally
            {
                dataFrame.Release();
            }

            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestEncodeEmptyEndAsClient()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(false));
            ILastHttpContent end = EmptyLastHttpContent.Default;
            Assert.True(ch.WriteOutbound(end));

            IHttp2DataFrame emptyFrame = ch.ReadOutbound<IHttp2DataFrame>();
            try
            {
                Assert.Equal(0, emptyFrame.Content.ReadableBytes);
                Assert.True(emptyFrame.IsEndStream);
            }
            finally
            {
                emptyFrame.Release();
            }

            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestEncodeDataEndAsClient()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(false));
            IByteBuffer hello = Unpooled.CopiedBuffer("hello world", Encoding.UTF8);
            ILastHttpContent end = new DefaultLastHttpContent(hello, true);
            Assert.True(ch.WriteOutbound(end));

            var dataFrame = ch.ReadOutbound<IHttp2DataFrame>();
            try
            {
                Assert.Equal("hello world", dataFrame.Content.ToString(Encoding.UTF8));
                Assert.True(dataFrame.IsEndStream);
            }
            finally
            {
                dataFrame.Release();
            }

            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestEncodeTrailersAsClient()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(false));
            ILastHttpContent trailers = new DefaultLastHttpContent(Unpooled.Empty, true);
            HttpHeaders headers = trailers.TrailingHeaders;
            headers.Set((AsciiString)"key", "value");
            Assert.True(ch.WriteOutbound(trailers));

            IHttp2HeadersFrame headerFrame = ch.ReadOutbound<IHttp2HeadersFrame>();
            Assert.Equal("value", headerFrame.Headers.Get((AsciiString)"key", null));
            Assert.True(headerFrame.IsEndStream);

            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestEncodeDataEndWithTrailersAsClient()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(false));
            IByteBuffer hello = Unpooled.CopiedBuffer("hello world", Encoding.UTF8);
            ILastHttpContent trailers = new DefaultLastHttpContent(hello, true);
            HttpHeaders headers = trailers.TrailingHeaders;
            headers.Set((AsciiString)"key", "value");
            Assert.True(ch.WriteOutbound(trailers));

            var dataFrame = ch.ReadOutbound<IHttp2DataFrame>();
            try
            {
                Assert.Equal("hello world", dataFrame.Content.ToString(Encoding.UTF8));
                Assert.False(dataFrame.IsEndStream);
            }
            finally
            {
                dataFrame.Release();
            }

            IHttp2HeadersFrame headerFrame = ch.ReadOutbound<IHttp2HeadersFrame>();
            Assert.Equal("value", headerFrame.Headers.Get((AsciiString)"key", null));
            Assert.True(headerFrame.IsEndStream);

            Assert.Null(ch.ReadOutbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void Decode100ContinueHttp2HeadersAsFullHttpResponse()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(false));
            IHttp2Headers headers = new DefaultHttp2Headers();
            headers.Scheme = HttpScheme.Http.Name;
            headers.Status = HttpResponseStatus.Continue.CodeAsText;

            Assert.True(ch.WriteInbound(new DefaultHttp2HeadersFrame(headers, false)));

            IFullHttpResponse response = ch.ReadInbound<IFullHttpResponse>();
            try
            {
                Assert.Equal(HttpResponseStatus.Continue, response.Status);
                Assert.Equal(HttpVersion.Http11, response.ProtocolVersion);
            }
            finally
            {
                response.Release();
            }

            Assert.Null(ch.ReadInbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestDecodeResponseHeaders()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(false));
            IHttp2Headers headers = new DefaultHttp2Headers();
            headers.Scheme = HttpScheme.Http.Name;
            headers.Status = HttpResponseStatus.OK.CodeAsText;

            Assert.True(ch.WriteInbound(new DefaultHttp2HeadersFrame(headers)));

            IHttpResponse response = ch.ReadInbound<IHttpResponse>();
            Assert.Equal(HttpResponseStatus.OK, response.Status);
            Assert.Equal(HttpVersion.Http11, response.ProtocolVersion);
            Assert.False(response is IFullHttpResponse);
            Assert.True(HttpUtil.IsTransferEncodingChunked(response));

            Assert.Null(ch.ReadInbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestDecodeResponseHeadersWithContentLength()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(false));
            IHttp2Headers headers = new DefaultHttp2Headers();
            headers.Scheme = HttpScheme.Http.Name;
            headers.Status = HttpResponseStatus.OK.CodeAsText;
            headers.SetInt((AsciiString)"content-length", 0);

            Assert.True(ch.WriteInbound(new DefaultHttp2HeadersFrame(headers)));

            IHttpResponse response = ch.ReadInbound<IHttpResponse>();
            Assert.Equal(HttpResponseStatus.OK, response.Status);
            Assert.Equal(HttpVersion.Http11, response.ProtocolVersion);
            Assert.False(response is IFullHttpResponse);
            Assert.False(HttpUtil.IsTransferEncodingChunked(response));

            Assert.Null(ch.ReadInbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestDecodeFullResponseHeaders()
        {
            TestDecodeFullResponseHeaders0(false);
        }

        [Fact]
        public void TestDecodeFullResponseHeadersWithStreamID()
        {
            TestDecodeFullResponseHeaders0(true);
        }

        sealed class TestHttp2FrameStream : IHttp2FrameStream
        {
            public int Id => 1;

            public Http2StreamState State => throw new NotImplementedException();

            public bool Equals(IHttp2FrameStream other)
            {
                return ReferenceEquals(this, other);
            }
        }
        private void TestDecodeFullResponseHeaders0(bool withStreamId)
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(false));
            IHttp2Headers headers = new DefaultHttp2Headers();
            headers.Scheme = HttpScheme.Http.Name;
            headers.Status = HttpResponseStatus.OK.CodeAsText;

            IHttp2HeadersFrame frame = new DefaultHttp2HeadersFrame(headers, true);
            if (withStreamId)
            {
                frame.Stream = new TestHttp2FrameStream();
            }

            Assert.True(ch.WriteInbound(frame));

            IFullHttpResponse response = ch.ReadInbound<IFullHttpResponse>();
            try
            {
                Assert.Equal(HttpResponseStatus.OK, response.Status);
                Assert.Equal(HttpVersion.Http11, response.ProtocolVersion);
                Assert.Equal(0, response.Content.ReadableBytes);
                Assert.True(response.TrailingHeaders.IsEmpty);
                Assert.False(HttpUtil.IsTransferEncodingChunked(response));
                if (withStreamId)
                {
                    Assert.Equal(1, response.Headers.GetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 0));
                }
            }
            finally
            {
                response.Release();
            }

            Assert.Null(ch.ReadInbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestDecodeResponseTrailersAsClient()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(false));
            IHttp2Headers headers = new DefaultHttp2Headers();
            headers.Set((AsciiString)"key", (AsciiString)"value");
            Assert.True(ch.WriteInbound(new DefaultHttp2HeadersFrame(headers, true)));

            ILastHttpContent trailers = ch.ReadInbound<ILastHttpContent>();
            try
            {
                Assert.Equal(0, trailers.Content.ReadableBytes);
                Assert.Equal("value", trailers.TrailingHeaders.Get((AsciiString)"key", null));
                Assert.False(trailers is IFullHttpRequest);
            }
            finally
            {
                trailers.Release();
            }

            Assert.Null(ch.ReadInbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestDecodeDataAsClient()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(false));
            IByteBuffer hello = Unpooled.CopiedBuffer("hello world", Encoding.UTF8);
            Assert.True(ch.WriteInbound(new DefaultHttp2DataFrame(hello)));

            IHttpContent content = ch.ReadInbound<IHttpContent>();
            try
            {
                Assert.Equal("hello world", content.Content.ToString(Encoding.UTF8));
                Assert.False(content is ILastHttpContent);
            }
            finally
            {
                content.Release();
            }

            Assert.Null(ch.ReadInbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestDecodeEndDataAsClient()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(false));
            IByteBuffer hello = Unpooled.CopiedBuffer("hello world", Encoding.UTF8);
            Assert.True(ch.WriteInbound(new DefaultHttp2DataFrame(hello, true)));

            ILastHttpContent content = ch.ReadInbound<ILastHttpContent>();
            try
            {
                Assert.Equal("hello world", content.Content.ToString(Encoding.UTF8));
                Assert.True(content.TrailingHeaders.IsEmpty);
            }
            finally
            {
                content.Release();
            }

            Assert.Null(ch.ReadInbound());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestPassThroughOtherAsClient()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new Http2StreamFrameToHttpObjectCodec(false));
            IHttp2ResetFrame reset = new DefaultHttp2ResetFrame(0);
            IHttp2GoAwayFrame goaway = new DefaultHttp2GoAwayFrame(0);
            Assert.True(ch.WriteInbound(reset));
            Assert.True(ch.WriteInbound(goaway.Retain()));

            Assert.Equal(reset, ch.ReadInbound<IHttp2ResetFrame>());

            IHttp2GoAwayFrame frame = ch.ReadInbound<IHttp2GoAwayFrame>();
            try
            {
                Assert.Equal(goaway, frame);
                Assert.Null(ch.ReadInbound());
                Assert.False(ch.Finish());
            }
            finally
            {
                goaway.Release();
                frame.Release();
            }
        }

        sealed class TestChannelOutboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            readonly ConcurrentQueue<IHttp2StreamFrame> frames;

            public TestChannelOutboundHandlerAdapter0(ConcurrentQueue<IHttp2StreamFrame> frames) => this.frames = frames;

            public override void Write(IChannelHandlerContext ctx, object msg, IPromise promise)
            {
                if (msg is IHttp2StreamFrame http2StreamFrame)
                {
                    frames.Enqueue(http2StreamFrame);
                    promise.Complete();
                }
                else
                {
                    ctx.WriteAsync(msg, promise);
                }
            }
        }

        [Fact]
        public void TestIsSharableBetweenChannels()
        {
            var frames = new ConcurrentQueue<IHttp2StreamFrame>();
            IChannelHandler sharedHandler = new Http2StreamFrameToHttpObjectCodec(false);

            X509Certificate2 tlsCertificate = TestResourceHelper.GetTestCertificate();
            string targetHost = tlsCertificate.GetNameInfo(X509NameType.DnsName, false);
            TlsHandler tlsHandler = new TlsHandler(new ClientTlsSettings(targetHost).AllowAnyServerCertificate());
            EmbeddedChannel tlsCh = new EmbeddedChannel(tlsHandler, new TestChannelOutboundHandlerAdapter0(frames), sharedHandler);
            EmbeddedChannel plaintextCh = new EmbeddedChannel(new TestChannelOutboundHandlerAdapter0(frames), sharedHandler);

            var req = new DefaultFullHttpRequest(
                HttpVersion.Http11, HttpMethod.Get, "/hello/world");
            Assert.True(tlsCh.WriteOutbound(req));
            Assert.True(tlsCh.FinishAndReleaseAll());

            frames.TryDequeue(out var headersFrame);
            var headers = ((IHttp2HeadersFrame)headersFrame).Headers;

            Assert.Equal("https", headers.Scheme.ToString());
            Assert.Equal("GET", headers.Method.ToString());
            Assert.Equal("/hello/world", headers.Path.ToString());
            Assert.True(((IHttp2HeadersFrame)headersFrame).IsEndStream);
            Assert.False(frames.TryDequeue(out _));

            // Run the plaintext channel
            req = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/hello/world");
            Assert.False(plaintextCh.WriteOutbound(req));
            Assert.False(plaintextCh.FinishAndReleaseAll());

            frames.TryDequeue(out headersFrame);
            headers = ((IHttp2HeadersFrame)headersFrame).Headers;

            Assert.Equal("http", headers.Scheme.ToString());
            Assert.Equal("GET", headers.Method.ToString());
            Assert.Equal("/hello/world", headers.Path.ToString());
            Assert.True(((IHttp2HeadersFrame)headersFrame).IsEndStream);
            Assert.False(frames.TryDequeue(out _));
        }
    }
}
