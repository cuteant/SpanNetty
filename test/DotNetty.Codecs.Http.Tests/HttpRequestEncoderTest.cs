// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class HttpRequestEncoderTest
    {
        [Fact]
        public void UriWithoutPath()
        {
            var encoder = new HttpRequestEncoder();
            IByteBuffer buffer = Unpooled.Buffer(64);
            encoder.EncodeInitialLine(
                buffer,
                new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Get, "http://localhost"));
            string req = buffer.ToString(Encoding.ASCII);
            Assert.Equal("GET http://localhost/ HTTP/1.1\r\n", req);
        }

        [Fact]
        public void UriWithoutPath2()
        {
            var encoder = new HttpRequestEncoder();
            IByteBuffer buffer = Unpooled.Buffer(64);
            encoder.EncodeInitialLine(
                buffer,
                new DefaultHttpRequest(
                    HttpVersion.Http11,
                    HttpMethod.Get,
                    "http://localhost:9999?p1=v1"));
            string req = buffer.ToString(Encoding.ASCII);
            Assert.Equal("GET http://localhost:9999/?p1=v1 HTTP/1.1\r\n", req);
        }

        [Fact]
        public void UriWithPath()
        {
            var encoder = new HttpRequestEncoder();
            IByteBuffer buffer = Unpooled.Buffer(64);
            encoder.EncodeInitialLine(
                buffer,
                new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Get, "http://localhost/"));
            string req = buffer.ToString(Encoding.ASCII);
            Assert.Equal("GET http://localhost/ HTTP/1.1\r\n", req);
        }

        [Fact]
        public void AbsPath()
        {
            var encoder = new HttpRequestEncoder();
            IByteBuffer buffer = Unpooled.Buffer(64);
            encoder.EncodeInitialLine(
                buffer,
                new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/"));
            string req = buffer.ToString(Encoding.ASCII);
            Assert.Equal("GET / HTTP/1.1\r\n", req);
        }

        [Fact]
        public void EmptyAbsPath()
        {
            var encoder = new HttpRequestEncoder();
            IByteBuffer buffer = Unpooled.Buffer(64);
            encoder.EncodeInitialLine(
                buffer,
                new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Get, ""));
            string req = buffer.ToString(Encoding.ASCII);
            Assert.Equal("GET / HTTP/1.1\r\n", req);
        }

        [Fact]
        public void QueryStringPath()
        {
            var encoder = new HttpRequestEncoder();
            IByteBuffer buffer = Unpooled.Buffer(64);
            encoder.EncodeInitialLine(
                buffer,
                new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/?url=http://example.com"));
            string req = buffer.ToString(Encoding.ASCII);
            Assert.Equal("GET /?url=http://example.com HTTP/1.1\r\n", req);
        }

        [Fact]
        public void EmptyReleasedBufferShouldNotWriteEmptyBufferToChannel()
        {
            var encoder = new HttpRequestEncoder();
            var channel = new EmbeddedChannel(encoder);
            IByteBuffer buf = Unpooled.Buffer();
            buf.Release();
            var exception = Assert.Throws<AggregateException>(() => channel.WriteAndFlushAsync(buf).Wait());
            Assert.Single(exception.InnerExceptions);
            Assert.IsType<EncoderException>(exception.InnerExceptions[0]);
            Assert.IsType<IllegalReferenceCountException>(exception.InnerExceptions[0].InnerException);
            channel.FinishAndReleaseAll();
        }

        [Fact]
        public void EmptydBufferShouldPassThrough()
        {
            var encoder = new HttpRequestEncoder();
            var channel = new EmbeddedChannel(encoder);
            IByteBuffer buffer = Unpooled.Buffer();
            channel.WriteAndFlushAsync(buffer).Wait();
            channel.FinishAndReleaseAll();
            Assert.Equal(0, buffer.ReferenceCount);
        }

        [Fact]
        public void EmptyContentsChunked()
        {
            EmptyContent(true, false);
        }

        [Fact]
        public void EmptyContentsChunkedWithTrailers()
        {
            EmptyContent(true, true);
        }

        [Fact]
        public void EmptyContentsNotChunked()
        {
            EmptyContent(false, false);
        }

        [Fact]
        public void EmptyContentNotsChunkedWithTrailers()
        {
            EmptyContent(false, true);
        }

        private void EmptyContent(bool chunked, bool trailers)
        {
            HttpRequestEncoder encoder = new HttpRequestEncoder();
            EmbeddedChannel channel = new EmbeddedChannel(encoder);
            var request = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Post, "/");
            if (chunked)
            {
                HttpUtil.SetTransferEncodingChunked(request, true);
            }
            Assert.True(channel.WriteOutbound(request));

            var contentBuffer = Unpooled.Buffer();
            Assert.True(channel.WriteOutbound(new DefaultHttpContent(contentBuffer)));

            var lastContentBuffer = Unpooled.Buffer();
            var last = new DefaultLastHttpContent(lastContentBuffer);
            if (trailers)
            {
                last.TrailingHeaders.Set((AsciiString)"X-Netty-Test", "true");
            }
            Assert.True(channel.WriteOutbound(last));

            // Ensure we only produce ByteBuf instances.
            var head = channel.ReadOutbound<IByteBuffer>();
            Assert.True(head.Release());

            var content = channel.ReadOutbound<IByteBuffer>();
            content.Release();

            var lastContent = channel.ReadOutbound<IByteBuffer>();
            lastContent.Release();
            Assert.False(channel.Finish());
        }

        /**
         * A test that checks for a NPE that would occur if when processing {@link LastHttpContent#EMPTY_LAST_CONTENT}
         * when a certain initialization order of {@link EmptyHttpHeaders} would occur.
         */
        [Fact]
        public void ForChunkedRequestNpe()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new HttpRequestEncoder());
            Assert.True(channel.WriteOutbound(new CustomHttpRequest()));
            Assert.True(channel.WriteOutbound(new DefaultHttpContent(Unpooled.CopiedBuffer("test", Encoding.ASCII))));
            Assert.True(channel.WriteOutbound(EmptyLastHttpContent.Default));
            Assert.True(channel.FinishAndReleaseAll());
        }

        class CustomHttpRequest : DefaultHttpRequest
        {
            public CustomHttpRequest()
                :base(HttpVersion.Http11, HttpMethod.Post, @"/")
            {
                this.Result = DecoderResult.Success;
                this.Headers.Add(HttpHeaderNames.TransferEncoding, "chunked");
            }
        }
    }
}
