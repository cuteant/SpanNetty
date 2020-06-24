// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class HttpObjectAggregatorTest
    {
        [Fact]
        public void Aggregate()
        {
            var aggregator = new HttpObjectAggregator(1024 * 1024);
            var ch = new EmbeddedChannel(aggregator);

            var message = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Get, "http://localhost");
            message.Headers.Set((AsciiString)"X-Test", true);
            IHttpContent chunk1 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test")));
            IHttpContent chunk2 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test2")));
            IHttpContent chunk3 = new DefaultLastHttpContent(Unpooled.Empty);

            Assert.False(ch.WriteInbound(message));
            Assert.False(ch.WriteInbound(chunk1));
            Assert.False(ch.WriteInbound(chunk2));

            // this should trigger a channelRead event so return true
            Assert.True(ch.WriteInbound(chunk3));
            Assert.True(ch.Finish());
            var aggregatedMessage = ch.ReadInbound<IFullHttpRequest>();
            Assert.NotNull(aggregatedMessage);

            Assert.Equal(chunk1.Content.ReadableBytes + chunk2.Content.ReadableBytes, HttpUtil.GetContentLength(aggregatedMessage));
            Assert.Equal(bool.TrueString, aggregatedMessage.Headers.Get((AsciiString)"X-Test", null)?.ToString());
            CheckContentBuffer(aggregatedMessage);
            var last = ch.ReadInbound();
            Assert.Null(last);
        }

        [Fact]
        public void AggregateWithTrailer()
        {
            var aggregator = new HttpObjectAggregator(1024 * 1024);
            var ch = new EmbeddedChannel(aggregator);
            var message = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Get, "http://localhost");
            message.Headers.Set((AsciiString)"X-Test", true);
            HttpUtil.SetTransferEncodingChunked(message, true);
            var chunk1 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test")));
            var chunk2 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test2")));
            var trailer = new DefaultLastHttpContent();
            trailer.TrailingHeaders.Set((AsciiString)"X-Trailer", true);

            Assert.False(ch.WriteInbound(message));
            Assert.False(ch.WriteInbound(chunk1));
            Assert.False(ch.WriteInbound(chunk2));

            // this should trigger a channelRead event so return true
            Assert.True(ch.WriteInbound(trailer));
            Assert.True(ch.Finish());
            var aggregatedMessage = ch.ReadInbound<IFullHttpRequest>();
            Assert.NotNull(aggregatedMessage);

            Assert.Equal(chunk1.Content.ReadableBytes + chunk2.Content.ReadableBytes, HttpUtil.GetContentLength(aggregatedMessage));
            Assert.Equal(bool.TrueString, aggregatedMessage.Headers.Get((AsciiString)"X-Test", null)?.ToString());
            Assert.Equal(bool.TrueString, aggregatedMessage.TrailingHeaders.Get((AsciiString)"X-Trailer", null)?.ToString());
            CheckContentBuffer(aggregatedMessage);
            var last = ch.ReadInbound();
            Assert.Null(last);
        }

        [Fact]
        public void OversizedRequest()
        {
            var aggregator = new HttpObjectAggregator(4);
            var ch = new EmbeddedChannel(aggregator);
            var message = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Put, "http://localhost");
            var chunk1 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test")));
            var chunk2 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test2")));
            EmptyLastHttpContent chunk3 = EmptyLastHttpContent.Default;

            Assert.False(ch.WriteInbound(message));
            Assert.False(ch.WriteInbound(chunk1));
            Assert.False(ch.WriteInbound(chunk2));

            var response = ch.ReadOutbound<IFullHttpResponse>();
            Assert.Equal(HttpResponseStatus.RequestEntityTooLarge, response.Status);
            Assert.Equal("0", response.Headers.Get(HttpHeaderNames.ContentLength, null));
            Assert.False(ch.IsOpen);

            try
            {
                Assert.False(ch.WriteInbound(chunk3));
                Assert.True(false, "Shoud not get here, expecting exception thrown.");
            }
            catch (Exception e)
            {
                Assert.True(e is ClosedChannelException);
            }

            Assert.False(ch.Finish());
        }

        [Fact]
        public void OversizedRequestWithContentLengthAndDecoder()
        {
            EmbeddedChannel embedder = new EmbeddedChannel(new HttpRequestDecoder(), new HttpObjectAggregator(4, false));
            Assert.False(embedder.WriteInbound(Unpooled.CopiedBuffer(
                    "PUT /upload HTTP/1.1\r\n" +
                            "Content-Length: 5\r\n\r\n", Encoding.ASCII)));

            Assert.Null(embedder.ReadInbound());

            var response = embedder.ReadOutbound<IFullHttpResponse>();
            Assert.Equal(HttpResponseStatus.RequestEntityTooLarge, response.Status);
            Assert.Equal("0", response.Headers.Get(HttpHeaderNames.ContentLength, null));

            Assert.True(embedder.IsOpen);

            Assert.False(embedder.WriteInbound(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4 })));
            Assert.False(embedder.WriteInbound(Unpooled.WrappedBuffer(new byte[] { 5 })));

            Assert.Null(embedder.ReadOutbound());

            Assert.False(embedder.WriteInbound(Unpooled.CopiedBuffer(
                    "PUT /upload HTTP/1.1\r\n" +
                            "Content-Length: 2\r\n\r\n", Encoding.ASCII)));

            Assert.Equal(HttpResponseStatus.RequestEntityTooLarge, response.Status);
            Assert.Equal("0", response.Headers.Get(HttpHeaderNames.ContentLength, null));

            Assert.NotNull(response as ILastHttpContent);
            ReferenceCountUtil.Release(response);

            Assert.True(embedder.IsOpen);

            Assert.False(embedder.WriteInbound(Unpooled.CopiedBuffer(new byte[] { 1 })));
            Assert.Null(embedder.ReadOutbound());
            Assert.True(embedder.WriteInbound(Unpooled.CopiedBuffer(new byte[] { 2 })));
            Assert.Null(embedder.ReadOutbound());

            var request = embedder.ReadInbound<IFullHttpRequest>();
            Assert.Equal(HttpVersion.Http11, request.ProtocolVersion);
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("/upload", request.Uri);
            Assert.Equal(2, HttpUtil.GetContentLength(request));

            byte[] actual = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(actual);
            Assert.Equal(new byte[] { 1, 2 }, actual);
            request.Release();

            Assert.False(embedder.Finish());
        }

        [Fact]
        public void OversizedRequestWithoutKeepAlive()
        {
            // send an HTTP/1.0 request with no keep-alive header
            var message = new DefaultHttpRequest(HttpVersion.Http10, HttpMethod.Put, "http://localhost");
            HttpUtil.SetContentLength(message, 5);
            CheckOversizedRequest(message);
        }

        [Fact]
        public void OversizedRequestWithContentLength()
        {
            var message = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Put, "http://localhost");
            HttpUtil.SetContentLength(message, 5);
            CheckOversizedRequest(message);
        }

        [Fact]
        public void OversizedResponse()
        {
            var aggregator = new HttpObjectAggregator(4);
            var ch = new EmbeddedChannel(aggregator);
            var message = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            var chunk1 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test")));
            var chunk2 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test2")));

            Assert.False(ch.WriteInbound(message));
            Assert.False(ch.WriteInbound(chunk1));
            Assert.Throws<TooLongFrameException>(() => ch.WriteInbound(chunk2));

            Assert.False(ch.IsOpen);
            Assert.False(ch.Finish());
        }

        [Fact]
        public void InvalidConstructorUsage()
        {
            var error = Assert.Throws<ArgumentException>(() => new HttpObjectAggregator(-1));
            Assert.Equal("maxContentLength", error.ParamName);
        }

        [Fact]
        public void InvalidMaxCumulationBufferComponents()
        {
            var aggregator = new HttpObjectAggregator(int.MaxValue);
            Assert.Throws<ArgumentException>(() => aggregator.MaxCumulationBufferComponents = 1);
        }

        [Fact]
        public void SetMaxCumulationBufferComponentsAfterInit()
        {
            var aggregator = new HttpObjectAggregator(int.MaxValue);
            var ch = new EmbeddedChannel(aggregator);
            Assert.Throws<InvalidOperationException>(() => aggregator.MaxCumulationBufferComponents = 10);
            Assert.False(ch.Finish());
        }

        [Fact]
        public void AggregateTransferEncodingChunked()
        {
            var aggregator = new HttpObjectAggregator(1024 * 1024);
            var ch = new EmbeddedChannel(aggregator);

            var message = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Put, "http://localhost");
            message.Headers.Set((AsciiString)"X-Test", true);
            message.Headers.Set((AsciiString)"Transfer-Encoding", (AsciiString)"Chunked");
            var chunk1 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test")));
            var chunk2 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test2")));
            EmptyLastHttpContent chunk3 = EmptyLastHttpContent.Default;
            Assert.False(ch.WriteInbound(message));
            Assert.False(ch.WriteInbound(chunk1));
            Assert.False(ch.WriteInbound(chunk2));

            // this should trigger a channelRead event so return true
            Assert.True(ch.WriteInbound(chunk3));
            Assert.True(ch.Finish());
            var aggregatedMessage = ch.ReadInbound<IFullHttpRequest>();
            Assert.NotNull(aggregatedMessage);

            Assert.Equal(chunk1.Content.ReadableBytes + chunk2.Content.ReadableBytes, HttpUtil.GetContentLength(aggregatedMessage));
            Assert.Equal(bool.TrueString, aggregatedMessage.Headers.Get((AsciiString)"X-Test", null));
            CheckContentBuffer(aggregatedMessage);
            var last = ch.ReadInbound();
            Assert.Null(last);
        }

        [Fact]
        public void BadRequest()
        {
            var ch = new EmbeddedChannel(new HttpRequestDecoder(), new HttpObjectAggregator(1024 * 1024));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("GET / HTTP/1.0 with extra\r\n")));
            var req = ch.ReadInbound<IFullHttpRequest>();
            Assert.NotNull(req);
            Assert.True(req.Result.IsFailure);
            var last = ch.ReadInbound();
            Assert.Null(last);
            ch.Finish();
        }

        [Fact]
        public void BadResponse()
        {
            var ch = new EmbeddedChannel(new HttpResponseDecoder(), new HttpObjectAggregator(1024 * 1024));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("HTTP/1.0 BAD_CODE Bad Server\r\n")));
            var resp = ch.ReadInbound<IFullHttpResponse>();
            Assert.NotNull(resp);
            Assert.True(resp.Result.IsFailure);
            var last = ch.ReadInbound();
            Assert.Null(last);
            ch.Finish();
        }

        [Fact]
        public void OversizedRequestWith100Continue()
        {
            var ch = new EmbeddedChannel(new HttpObjectAggregator(8));

            // Send an oversized request with 100 continue.
            var message = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Put, "http://localhost");
            HttpUtil.Set100ContinueExpected(message, true);
            HttpUtil.SetContentLength(message, 16);

            var chunk1 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("some")));
            var chunk2 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test")));
            EmptyLastHttpContent chunk3 = EmptyLastHttpContent.Default;

            // Send a request with 100-continue + large Content-Length header value.
            Assert.False(ch.WriteInbound(message));

            // The aggregator should respond with '413.'
            var response = ch.ReadOutbound<IFullHttpResponse>();
            Assert.Equal(HttpResponseStatus.RequestEntityTooLarge, response.Status);
            Assert.Equal((AsciiString)"0", response.Headers.Get(HttpHeaderNames.ContentLength, null));

            // An ill-behaving client could continue to send data without a respect, and such data should be discarded.
            Assert.False(ch.WriteInbound(chunk1));

            // The aggregator should not close the connection because keep-alive is on.
            Assert.True(ch.IsOpen);

            // Now send a valid request.
            var message2 = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Put, "http://localhost");

            Assert.False(ch.WriteInbound(message2));
            Assert.False(ch.WriteInbound(chunk2));
            Assert.True(ch.WriteInbound(chunk3));

            var fullMsg = ch.ReadInbound<IFullHttpRequest>();
            Assert.NotNull(fullMsg);

            Assert.Equal(chunk2.Content.ReadableBytes + chunk3.Content.ReadableBytes, HttpUtil.GetContentLength(fullMsg));
            Assert.Equal(HttpUtil.GetContentLength(fullMsg), fullMsg.Content.ReadableBytes);

            fullMsg.Release();
            Assert.False(ch.Finish());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void UnsupportedExpectHeaderExpectation(bool close)
        {
            int maxContentLength = 4;
            var aggregator = new HttpObjectAggregator(maxContentLength, close);
            var ch = new EmbeddedChannel(new HttpRequestDecoder(), aggregator);

            Assert.False(ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(
                "GET / HTTP/1.1\r\n" +
                "Expect: chocolate=yummy\r\n" +
                "Content-Length: 100\r\n\r\n"))));
            var next = ch.ReadInbound();
            Assert.Null(next);

            var response = ch.ReadOutbound<IFullHttpResponse>();
            Assert.Equal(HttpResponseStatus.ExpectationFailed, response.Status);
            Assert.Equal((AsciiString)"0", response.Headers.Get(HttpHeaderNames.ContentLength, null));
            response.Release();

            if (close)
            {
                Assert.False(ch.IsOpen);
            }
            else
            {
                // keep-alive is on by default in HTTP/1.1, so the connection should be still alive
                Assert.True(ch.IsOpen);

                // the decoder should be reset by the aggregator at this point and be able to decode the next request
                Assert.True(ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n\r\n"))));

                var request = ch.ReadInbound<IFullHttpRequest>();
                Assert.NotNull(request);
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal("/", request.Uri);
                Assert.Equal(0, request.Content.ReadableBytes);
                request.Release();
            }

            Assert.False(ch.Finish());
        }

        [Fact]
        public void ValidRequestWith100ContinueAndDecoder()
        {
            EmbeddedChannel embedder = new EmbeddedChannel(new HttpRequestDecoder(), new HttpObjectAggregator(100));
            embedder.WriteInbound(Unpooled.CopiedBuffer(
                "GET /upload HTTP/1.1\r\n" +
                    "Expect: 100-continue\r\n" +
                    "Content-Length: 0\r\n\r\n", Encoding.ASCII));

            var response = embedder.ReadOutbound<IFullHttpResponse>();
            Assert.Equal(HttpResponseStatus.Continue, response.Status);
            var request = embedder.ReadInbound<IFullHttpRequest>();
            Assert.False(request.Headers.Contains(HttpHeaderNames.Expect));
            request.Release();
            response.Release();
            Assert.False(embedder.Finish());
        }

        [Fact]
        public void OversizedRequestWith100ContinueAndDecoder()
        {
            var ch = new EmbeddedChannel(new HttpRequestDecoder(), new HttpObjectAggregator(4));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(
                    "PUT /upload HTTP/1.1\r\n" +
                            "Expect: 100-continue\r\n" +
                            "Content-Length: 100\r\n\r\n")));

            var next = ch.ReadInbound();
            Assert.Null(next);

            var response = ch.ReadOutbound<IFullHttpResponse>();
            Assert.Equal(HttpResponseStatus.RequestEntityTooLarge, response.Status);
            Assert.Equal((AsciiString)"0", response.Headers.Get(HttpHeaderNames.ContentLength, null));

            // Keep-alive is on by default in HTTP/1.1, so the connection should be still alive.
            Assert.True(ch.IsOpen);

            // The decoder should be reset by the aggregator at this point and be able to decode the next request.
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("GET /max-upload-size HTTP/1.1\r\n\r\n")));

            var request = ch.ReadInbound<IFullHttpRequest>();
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/max-upload-size", request.Uri);
            Assert.Equal(0, request.Content.ReadableBytes);
            request.Release();

            Assert.False(ch.Finish());
        }

        [Fact]
        public void OversizedRequestWith100ContinueAndDecoderCloseConnection()
        {
            var ch = new EmbeddedChannel(new HttpRequestDecoder(), new HttpObjectAggregator(4, true));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(
                    "PUT /upload HTTP/1.1\r\n" +
                            "Expect: 100-continue\r\n" +
                            "Content-Length: 100\r\n\r\n")));

            var next = ch.ReadInbound();
            Assert.Null(next);

            var response = ch.ReadOutbound<IFullHttpResponse>();
            Assert.Equal(HttpResponseStatus.RequestEntityTooLarge, response.Status);
            Assert.Equal((AsciiString)"0", response.Headers.Get(HttpHeaderNames.ContentLength, null));

            // We are forcing the connection closed if an expectation is exceeded.
            Assert.False(ch.IsOpen);
            Assert.False(ch.Finish());
        }

        [Fact]
        public void RequestAfterOversized100ContinueAndDecoder()
        {
            var ch = new EmbeddedChannel(new HttpRequestDecoder(), new HttpObjectAggregator(15));

            // Write first request with Expect: 100-continue.
            var message = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Put, "http://localhost");
            HttpUtil.Set100ContinueExpected(message, true);
            HttpUtil.SetContentLength(message, 16);

            var chunk1 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("some")));
            var chunk2 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test")));
            EmptyLastHttpContent chunk3 = EmptyLastHttpContent.Default;

            // Send a request with 100-continue + large Content-Length header value.
            Assert.False(ch.WriteInbound(message));

            // The aggregator should respond with '413'.
            var response = ch.ReadOutbound<IFullHttpResponse>();
            Assert.Equal(HttpResponseStatus.RequestEntityTooLarge, response.Status);
            Assert.Equal((AsciiString)"0", response.Headers.Get(HttpHeaderNames.ContentLength, null));

            // An ill-behaving client could continue to send data without a respect, and such data should be discarded.
            Assert.False(ch.WriteInbound(chunk1));

            // The aggregator should not close the connection because keep-alive is on.
            Assert.True(ch.IsOpen);

            // Now send a valid request.
            var message2 = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Put, "http://localhost");

            Assert.False(ch.WriteInbound(message2));
            Assert.False(ch.WriteInbound(chunk2));
            Assert.True(ch.WriteInbound(chunk3));

            var fullMsg = ch.ReadInbound<IFullHttpRequest>();
            Assert.NotNull(fullMsg);

            Assert.Equal(chunk2.Content.ReadableBytes + chunk3.Content.ReadableBytes, HttpUtil.GetContentLength(fullMsg));
            Assert.Equal(HttpUtil.GetContentLength(fullMsg), fullMsg.Content.ReadableBytes);

            fullMsg.Release();
            Assert.False(ch.Finish());
        }

        [Fact]
        public void ReplaceAggregatedRequest()
        {
            var ch = new EmbeddedChannel(new HttpObjectAggregator(1024 * 1024));

            var boom = new Exception("boom");
            var req = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Get, "http://localhost");
            req.Result = DecoderResult.Failure(boom);

            Assert.True(ch.WriteInbound(req) && ch.Finish());

            var aggregatedReq = ch.ReadInbound<IFullHttpRequest>();
            var replacedReq = (IFullHttpRequest)aggregatedReq.Replace(Unpooled.Empty);

            Assert.Equal(replacedReq.Result, aggregatedReq.Result);
            aggregatedReq.Release();
            replacedReq.Release();
        }

        [Fact]
        public void ReplaceAggregatedResponse()
        {
            var ch = new EmbeddedChannel(new HttpObjectAggregator(1024 * 1024));

            var boom = new Exception("boom");
            var rep = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            rep.Result = DecoderResult.Failure(boom);

            Assert.True(ch.WriteInbound(rep) && ch.Finish());

            var aggregatedRep = ch.ReadInbound<IFullHttpResponse>();
            var replacedRep = (IFullHttpResponse)aggregatedRep.Replace(Unpooled.Empty);

            Assert.Equal(replacedRep.Result, aggregatedRep.Result);
            aggregatedRep.Release();
            replacedRep.Release();
        }

        [Fact]
        public void SelectiveRequestAggregation()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new TestPostHttpObjectAggregator(1024 * 1024));

            try
            {
                // Aggregate: POST
                IHttpRequest request1 = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Post, "/");
                IHttpContent content1 = new DefaultHttpContent(Unpooled.CopiedBuffer("Hello, World!", Encoding.UTF8));
                request1.Headers.Set(HttpHeaderNames.ContentType, HttpHeaderValues.TextPlain);

                Assert.True(channel.WriteInbound(request1, content1, EmptyLastHttpContent.Default));

                // Getting an aggregated response out
                var msg1 = channel.ReadInbound();
                try
                {
                    Assert.True(msg1 is IFullHttpRequest);
                }
                finally
                {
                    ReferenceCountUtil.Release(msg1);
                }

                // Don't aggregate: non-POST
                IHttpRequest request2 = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Put, "/");
                IHttpContent content2 = new DefaultHttpContent(Unpooled.CopiedBuffer("Hello, World!", Encoding.UTF8));
                request2.Headers.Set(HttpHeaderNames.ContentType, HttpHeaderValues.TextPlain);

                try
                {
                    Assert.True(channel.WriteInbound(request2, content2, EmptyLastHttpContent.Default));

                    // Getting the same response objects out
                    Assert.Same(request2, channel.ReadInbound<IHttpRequest>());
                    Assert.Same(content2, channel.ReadInbound<IHttpContent>());
                    Assert.Same(EmptyLastHttpContent.Default, channel.ReadInbound<EmptyLastHttpContent>());
                }
                finally
                {
                    ReferenceCountUtil.Release(request2);
                    ReferenceCountUtil.Release(content2);
                }

                Assert.False(channel.Finish());
            }
            finally
            {
                channel.CloseAsync();
            }
        }

        class TestPostHttpObjectAggregator : HttpObjectAggregator
        {
            public TestPostHttpObjectAggregator(int maxContentLength) : base(maxContentLength) { }

            protected override bool IsStartMessage(IHttpObject msg)
            {
                if (msg is IHttpRequest request)
                {
                    HttpMethod method = request.Method;

                    if (method.Equals(HttpMethod.Post))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        [Fact]
        public void SelectiveResponseAggregation()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new TestTextHttpObjectAggregator(1024 * 1024));

            try
            {
                // Aggregate: text/plain
                IHttpResponse response1 = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
                IHttpContent content1 = new DefaultHttpContent(Unpooled.CopiedBuffer("Hello, World!", Encoding.UTF8));
                response1.Headers.Set(HttpHeaderNames.ContentType, HttpHeaderValues.TextPlain);

                Assert.True(channel.WriteInbound(response1, content1, EmptyLastHttpContent.Default));

                // Getting an aggregated response out
                var msg1 = channel.ReadInbound();
                try
                {
                    Assert.True(msg1 is IFullHttpResponse);
                }
                finally
                {
                    ReferenceCountUtil.Release(msg1);
                }

                // Don't aggregate: application/json
                IHttpResponse response2 = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
                IHttpContent content2 = new DefaultHttpContent(Unpooled.CopiedBuffer("{key: 'value'}", Encoding.UTF8));
                response2.Headers.Set(HttpHeaderNames.ContentType, HttpHeaderValues.ApplicationJson);

                try
                {
                    Assert.True(channel.WriteInbound(response2, content2, EmptyLastHttpContent.Default));

                    // Getting the same response objects out
                    Assert.Same(response2, channel.ReadInbound<IHttpResponse>());
                    Assert.Same(content2, channel.ReadInbound<IHttpContent>());
                    Assert.Same(EmptyLastHttpContent.Default, channel.ReadInbound<EmptyLastHttpContent>());
                }
                finally
                {
                    ReferenceCountUtil.Release(response2);
                    ReferenceCountUtil.Release(content2);
                }

                Assert.False(channel.Finish());
            }
            finally
            {
                channel.CloseAsync();
            }
        }

        class TestTextHttpObjectAggregator : HttpObjectAggregator
        {
            public TestTextHttpObjectAggregator(int maxContentLength) : base(maxContentLength) { }

            protected override bool IsStartMessage(IHttpObject msg)
            {
                if (msg is IHttpResponse response)
                {
                    HttpHeaders headers = response.Headers;

                    var contentType = headers.Get(HttpHeaderNames.ContentType, null);
                    if (AsciiString.ContentEqualsIgnoreCase(contentType, HttpHeaderValues.TextPlain))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        static void CheckOversizedRequest(IHttpRequest message)
        {
            var ch = new EmbeddedChannel(new HttpObjectAggregator(4));

            Assert.False(ch.WriteInbound(message));
            var response = ch.ReadOutbound<IHttpResponse>();
            Assert.Equal(HttpResponseStatus.RequestEntityTooLarge, response.Status);
            Assert.Equal("0", response.Headers.Get(HttpHeaderNames.ContentLength, null));

            Assert.NotNull(response as ILastHttpContent);
            ReferenceCountUtil.Release(response);

            if (ServerShouldCloseConnection(message, response))
            {
                Assert.False(ch.IsOpen);
                try
                {
                    ch.WriteInbound(new DefaultHttpContent(Unpooled.Empty));
                    Assert.False(true);
                }
                catch (Exception exc)
                {
                    Assert.IsType<ClosedChannelException>(exc);
                    // expected
                }
                Assert.False(ch.Finish());
            }
            else
            {
                Assert.True(ch.IsOpen);
                Assert.False(ch.WriteInbound(new DefaultHttpContent(Unpooled.CopiedBuffer(new byte[8]))));
                Assert.False(ch.WriteInbound(new DefaultHttpContent(Unpooled.CopiedBuffer(new byte[8]))));

                // Now start a new message and ensure we will not reject it again.
                IHttpRequest message2 = new DefaultHttpRequest(HttpVersion.Http10, HttpMethod.Put, "http://localhost");
                HttpUtil.SetContentLength(message, 2);

                Assert.False(ch.WriteInbound(message2));
                Assert.Null(ch.ReadOutbound());
                Assert.False(ch.WriteInbound(new DefaultHttpContent(Unpooled.CopiedBuffer(new byte[] { 1 }))));
                Assert.Null(ch.ReadOutbound());
                Assert.True(ch.WriteInbound(new DefaultLastHttpContent(Unpooled.CopiedBuffer(new byte[] { 2 }))));
                Assert.Null(ch.ReadOutbound());

                var request = ch.ReadInbound<IFullHttpRequest>();
                Assert.Equal(message2.ProtocolVersion, request.ProtocolVersion);
                Assert.Equal(message2.Method, request.Method);
                Assert.Equal(message2.Uri, request.Uri);
                Assert.Equal(2, HttpUtil.GetContentLength(request));

                byte[] actual = new byte[request.Content.ReadableBytes];
                request.Content.ReadBytes(actual);
                Assert.Equal(new byte[] { 1, 2 }, actual);
                request.Release();

                Assert.False(ch.Finish());
            }
        }

        static bool ServerShouldCloseConnection(IHttpRequest message, IHttpResponse response)
        {
            // If the response wasn't keep-alive, the server should close the connection.
            if (!HttpUtil.IsKeepAlive(response))
            {
                return true;
            }
            // The connection should only be kept open if Expect: 100-continue is set,
            // or if keep-alive is on.
            if (HttpUtil.Is100ContinueExpected(message))
            {
                return false;
            }
            if (HttpUtil.IsKeepAlive(message))
            {
                return false;
            }

            return true;
        }

        static void CheckContentBuffer(IFullHttpRequest aggregatedMessage)
        {
            var buffer = (CompositeByteBuffer)aggregatedMessage.Content;
            Assert.Equal(2, buffer.NumComponents);
            IList<IByteBuffer> buffers = buffer.Decompose(0, buffer.Capacity);
            Assert.Equal(2, buffers.Count);
            foreach (IByteBuffer buf in buffers)
            {
                // This should be false as we decompose the buffer before to not have deep hierarchy
                Assert.False(buf is CompositeByteBuffer);
            }
            aggregatedMessage.Release();
        }
    }
}
