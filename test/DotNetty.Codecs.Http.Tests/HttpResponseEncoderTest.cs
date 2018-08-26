// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class HttpResponseEncoderTest
    {
        const long IntegerOverflow = (long)int.MaxValue + 1;
        static readonly IFileRegion FileRegion = new DummyLongFileRegion();

        [Fact]
        public void LargeFileRegionChunked()
        {
            var channel = new EmbeddedChannel(new HttpResponseEncoder());
            IHttpResponse response = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            response.Headers.Set(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);

            Assert.True(channel.WriteOutbound(response));

            var buffer = channel.ReadOutbound<IByteBuffer>();
            Assert.Equal(
                "HTTP/1.1 200 OK\r\n" + HttpHeaderNames.TransferEncoding + ": " +
                HttpHeaderValues.Chunked + "\r\n\r\n",
                buffer.ToString(Encoding.ASCII));
            buffer.Release();

            Assert.True(channel.WriteOutbound(FileRegion));
            buffer = channel.ReadOutbound<IByteBuffer>();
            Assert.Equal("80000000\r\n", buffer.ToString(Encoding.ASCII));
            buffer.Release();

            var region = channel.ReadOutbound<IFileRegion>();
            Assert.Same(FileRegion, region);
            region.Release();
            buffer = channel.ReadOutbound<IByteBuffer>();
            Assert.Equal("\r\n", buffer.ToString(Encoding.ASCII));
            buffer.Release();

            Assert.True(channel.WriteOutbound(EmptyLastHttpContent.Default));
            buffer = channel.ReadOutbound<IByteBuffer>();
            Assert.Equal("0\r\n\r\n", buffer.ToString(Encoding.ASCII));
            buffer.Release();

            Assert.False(channel.Finish());
        }

        class DummyLongFileRegion : IFileRegion
        {
            public int ReferenceCount => 1;

            public IReferenceCounted Retain() => this;

            public IReferenceCounted Retain(int increment) => this;

            public IReferenceCounted Touch() => this;

            public IReferenceCounted Touch(object hint) => this;

            public bool Release() => false;

            public bool Release(int decrement) => false;

            public long Position => 0;

            public long Transferred => 0;

            public long Count => IntegerOverflow;

            public long TransferTo(Stream target, long position)
            {
                throw new NotSupportedException();
            }
        }

        [Fact]
        public void EmptyBufferBypass()
        {
            var channel = new EmbeddedChannel(new HttpResponseEncoder());

            // Test writing an empty buffer works when the encoder is at ST_INIT.
            channel.WriteOutbound(Unpooled.Empty);
            var buffer = channel.ReadOutbound<IByteBuffer>();
            Assert.Same(buffer, Unpooled.Empty);

            // Leave the ST_INIT state.
            IHttpResponse response = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            Assert.True(channel.WriteOutbound(response));
            buffer = channel.ReadOutbound<IByteBuffer>();
            Assert.Equal("HTTP/1.1 200 OK\r\n\r\n", buffer.ToString(Encoding.ASCII));
            buffer.Release();

            // Test writing an empty buffer works when the encoder is not at ST_INIT.
            channel.WriteOutbound(Unpooled.Empty);
            buffer = channel.ReadOutbound<IByteBuffer>();
            Assert.Same(buffer, Unpooled.Empty);

            Assert.False(channel.Finish());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void EmptyContent(bool chunked)
        {
            const string Content = "netty rocks";
            IByteBuffer contentBuffer = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(Content));
            int length = contentBuffer.ReadableBytes;

            var channel = new EmbeddedChannel(new HttpResponseEncoder());
            IHttpResponse response = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            if (!chunked)
            {
                HttpUtil.SetContentLength(response, length);
            }
            Assert.True(channel.WriteOutbound(response));
            Assert.True(channel.WriteOutbound(new DefaultHttpContent(Unpooled.Empty)));
            Assert.True(channel.WriteOutbound(new DefaultLastHttpContent(contentBuffer)));

            var buffer = channel.ReadOutbound<IByteBuffer>();
            if (!chunked)
            {
                Assert.Equal(
                    "HTTP/1.1 200 OK\r\ncontent-length: " + length + "\r\n\r\n",
                    buffer.ToString(Encoding.ASCII));
            }
            else
            {
                Assert.Equal("HTTP/1.1 200 OK\r\n\r\n", buffer.ToString(Encoding.ASCII));
            }
            buffer.Release();

            // Test writing an empty buffer works when the encoder is not at ST_INIT.
            buffer = channel.ReadOutbound<IByteBuffer>();
            Assert.Equal(0, buffer.ReadableBytes);
            buffer.Release();

            buffer = channel.ReadOutbound<IByteBuffer>();
            Assert.Equal(length, buffer.ReadableBytes);
            buffer.Release();

            Assert.False(channel.Finish());
        }

        [Fact]
        public void StatusNoContent()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new HttpResponseEncoder());
            AssertEmptyResponse(channel, HttpResponseStatus.NoContent, null, false);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void StatusNoContentContentLength()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new HttpResponseEncoder());
            AssertEmptyResponse(channel, HttpResponseStatus.NoContent, HttpHeaderNames.ContentLength, true);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void StatusNoContentTransferEncoding()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new HttpResponseEncoder());
            AssertEmptyResponse(channel, HttpResponseStatus.NoContent, HttpHeaderNames.TransferEncoding, true);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void StatusNotModified()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new HttpResponseEncoder());
            AssertEmptyResponse(channel, HttpResponseStatus.NotModified, null, false);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void StatusNotModifiedContentLength()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new HttpResponseEncoder());
            AssertEmptyResponse(channel, HttpResponseStatus.NotModified, HttpHeaderNames.ContentLength, false);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void StatusNotModifiedTransferEncoding()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new HttpResponseEncoder());
            AssertEmptyResponse(channel, HttpResponseStatus.NotModified, HttpHeaderNames.TransferEncoding, false);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void StatusInformational()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new HttpResponseEncoder());
            for (int code = 100; code < 200; code++)
            {
                HttpResponseStatus status = HttpResponseStatus.ValueOf(code);
                AssertEmptyResponse(channel, status, null, false);
            }
            Assert.False(channel.Finish());
        }

        [Fact]
        public void StatusInformationalContentLength()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new HttpResponseEncoder());
            for (int code = 100; code < 200; code++)
            {
                HttpResponseStatus status = HttpResponseStatus.ValueOf(code);
                AssertEmptyResponse(channel, status, HttpHeaderNames.ContentLength, code != 101);
            }
            Assert.False(channel.Finish());
        }

        [Fact]
        public void StatusInformationalTransferEncoding()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new HttpResponseEncoder());
            for (int code = 100; code < 200; code++)
            {
                HttpResponseStatus status = HttpResponseStatus.ValueOf(code);
                AssertEmptyResponse(channel, status, HttpHeaderNames.TransferEncoding, code != 101);
            }
            Assert.False(channel.Finish());
        }

        private static void AssertEmptyResponse(EmbeddedChannel channel, HttpResponseStatus status,
                                                AsciiString headerName, bool headerStripped)
        {
            var response = new DefaultHttpResponse(HttpVersion.Http11, status);
            if (HttpHeaderNames.ContentLength.ContentEquals(headerName))
            {
                response.Headers.Set(headerName, "0");
            }
            else if (HttpHeaderNames.TransferEncoding.ContentEquals(headerName))
            {
                response.Headers.Set(headerName, HttpHeaderValues.Chunked);
            }

            Assert.True(channel.WriteOutbound(response));
            Assert.True(channel.WriteOutbound(EmptyLastHttpContent.Default));

            var buffer = channel.ReadOutbound<IByteBuffer>();
            StringBuilder responseText = new StringBuilder();
            responseText.Append(HttpVersion.Http11.ToString()).Append(' ').Append(status.ToString()).Append("\r\n");
            if (!headerStripped && headerName != null)
            {
                responseText.Append(headerName).Append(": ");

                if (HttpHeaderNames.ContentLength.ContentEquals(headerName))
                {
                    responseText.Append('0');
                }
                else
                {
                    responseText.Append(HttpHeaderValues.Chunked.ToString());
                }
                responseText.Append("\r\n");
            }
            responseText.Append("\r\n");

            Assert.Equal(responseText.ToString(), buffer.ToString(Encoding.ASCII));

            buffer.Release();

            buffer = channel.ReadOutbound<IByteBuffer>();
            buffer.Release();
        }

        [Fact]
        public void EmptyContentsChunked()
        {
            EmptyContents(true, false);
        }

        [Fact]
        public void EmptyContentsChunkedWithTrailers()
        {
            EmptyContents(true, true);
        }

        [Fact]
        public void EmptyContentsNotChunked()
        {
            EmptyContents(false, false);
        }

        [Fact]
        public void EmptyContentNotsChunkedWithTrailers()
        {
            EmptyContents(false, true);
        }

        private void EmptyContents(bool chunked, bool trailers)
        {
            HttpResponseEncoder encoder = new HttpResponseEncoder();
            EmbeddedChannel channel = new EmbeddedChannel(encoder);
            var request = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
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

        [Fact]
        public void StatusResetContentTransferContentLength()
        {
            StatusResetContentTransferContentLength0(HttpHeaderNames.ContentLength, Unpooled.Buffer().WriteLong(8));
        }

        [Fact]
        public void StatusResetContentTransferEncoding()
        {
            StatusResetContentTransferContentLength0(HttpHeaderNames.TransferEncoding, Unpooled.Buffer().WriteLong(8));
        }

        private static void StatusResetContentTransferContentLength0(AsciiString headerName, IByteBuffer content)
        {
            EmbeddedChannel channel = new EmbeddedChannel(new HttpResponseEncoder());

            var response = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.ResetContent);
            if (HttpHeaderNames.ContentLength.ContentEqualsIgnoreCase(headerName))
            {
                response.Headers.Set(HttpHeaderNames.ContentLength, content.ReadableBytes);
            }
            else
            {
                response.Headers.Set(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);
            }

            Assert.True(channel.WriteOutbound(response));
            Assert.True(channel.WriteOutbound(new DefaultHttpContent(content)));
            Assert.True(channel.WriteOutbound(EmptyLastHttpContent.Default));

            StringBuilder responseText = new StringBuilder();
            responseText.Append(HttpVersion.Http11.ToString()).Append(' ')
                    .Append(HttpResponseStatus.ResetContent.ToString()).Append("\r\n");
            responseText.Append(HttpHeaderNames.ContentLength).Append(": 0\r\n");
            responseText.Append("\r\n");

            StringBuilder written = new StringBuilder();
            for (; ; )
            {
                var buffer = channel.ReadOutbound<IByteBuffer>();
                if (buffer == null)
                {
                    break;
                }
                written.Append(buffer.ToString(Encoding.ASCII));
                buffer.Release();
            }

            Assert.Equal(responseText.ToString(), written.ToString());
            Assert.False(channel.Finish());
        }
    }
}
