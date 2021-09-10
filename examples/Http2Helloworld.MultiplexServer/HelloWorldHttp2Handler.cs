﻿namespace Http2Helloworld.MultiplexServer
{
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http2;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Channels;
    using Microsoft.Extensions.Logging;
    using System;

    /**
     * A simple handler that responds with the message "Hello World!".
     *
     * This example is making use of the "multiplexing" http2 API, where streams are mapped to child
     * Channels. This API is very experimental and incomplete.
     */
    public class HelloWorldHttp2Handler : ChannelDuplexHandler
    {
        static readonly ILogger s_logger = InternalLoggerFactory.DefaultFactory.CreateLogger<HelloWorldHttp2Handler>();

        public override bool IsSharable => true;

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            base.ExceptionCaught(context, exception);
            s_logger.LogError($"{exception}");
            context.CloseAsync();
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            if (message is IHttp2HeadersFrame headersFrame)
            {
                OnHeadersRead(context, headersFrame);
            }
            else if (message is IHttp2DataFrame dataFrame)
            {
                OnDataRead(context, dataFrame);
            }
            else
            {
                base.ChannelRead(context, message);
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            context.Flush();
        }

        /**
         * If receive a frame with end-of-stream set, send a pre-canned response.
         */
        private static void OnDataRead(IChannelHandlerContext context, IHttp2DataFrame data)
        {
            if (data.IsEndStream)
            {
                SendResponse(context, data.Content);
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
        private static void OnHeadersRead(IChannelHandlerContext context, IHttp2HeadersFrame headers)
        {
            if (headers.IsEndStream)
            {
                var content = context.Allocator.Buffer();
                content.WriteBytes(Server.HelloWorldHttp1Handler.RESPONSE_BYTES.Duplicate());
                ByteBufferUtil.WriteAscii(content, " - via HTTP/2");
                SendResponse(context, content);
            }
        }

        /**
         * Sends a "Hello World" DATA frame to the client.
         */
        private static void SendResponse(IChannelHandlerContext context, IByteBuffer payload)
        {
            // Send a frame for the response status
            IHttp2Headers headers = new DefaultHttp2Headers() { Status = HttpResponseStatus.OK.CodeAsText };
            context.WriteAsync(new DefaultHttp2HeadersFrame(headers));
            context.WriteAsync(new DefaultHttp2DataFrame(payload, true));
        }
    }
}
