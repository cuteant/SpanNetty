// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Http2Helloworld.MultiplexServer
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http2;
    using DotNetty.Transport.Channels;
    using Microsoft.Extensions.Logging;

    /**
     * A simple handler that responds with the message "Hello World!".
     *
     * This example is making use of the "multiplexing" http2 API, where streams are mapped to child
     * Channels. This API is very experimental and incomplete.
     */
    public class HelloWorldHttp2Handler : ChannelDuplexHandler
    {
        static readonly ILogger s_logger = TraceLogger.GetLogger<HelloWorldHttp2Handler>();

        public override bool IsSharable => true;

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            base.ExceptionCaught(ctx, cause);
            s_logger.LogError(cause.ToString());
            ctx.CloseAsync();
        }

        public override void ChannelRead(IChannelHandlerContext ctx, object msg)
        {
            if (msg is IHttp2HeadersFrame headersFrame)
            {
                OnHeadersRead(ctx, headersFrame);
            }
            else if (msg is IHttp2DataFrame dataFrame)
            {
                OnDataRead(ctx, dataFrame);
            }
            else
            {
                base.ChannelRead(ctx, msg);
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            context.Flush();
        }

        /**
         * If receive a frame with end-of-stream set, send a pre-canned response.
         */
        private static void OnDataRead(IChannelHandlerContext ctx, IHttp2DataFrame data)
        {
            if (data.IsEndStream)
            {
                SendResponse(ctx, data.Content);
            }
            else
            {
                // We do not send back the response to the remote-peer, so we need to release it.
                data.Release();
            }
        }

        /**
         * If receive a frame with end-of-stream set, send a pre-canned response.
         */
        private static void OnHeadersRead(IChannelHandlerContext ctx, IHttp2HeadersFrame headers)
        {
            if (headers.IsEndStream)
            {
                var content = ctx.Allocator.Buffer();
                content.WriteBytes(Http2Helloworld.Server.HelloWorldHttp1Handler.RESPONSE_BYTES.Duplicate());
                ByteBufferUtil.WriteAscii(content, " - via HTTP/2");
                SendResponse(ctx, content);
            }
        }

        /**
         * Sends a "Hello World" DATA frame to the client.
         */
        private static void SendResponse(IChannelHandlerContext ctx, IByteBuffer payload)
        {
            // Send a frame for the response status
            IHttp2Headers headers = new DefaultHttp2Headers() { Status = HttpResponseStatus.OK.CodeAsText };
            ctx.WriteAsync(new DefaultHttp2HeadersFrame(headers));
            ctx.WriteAsync(new DefaultHttp2DataFrame(payload, true));
        }
    }
}
