namespace Http2Helloworld.Server
{
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http2;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using Microsoft.Extensions.Logging;
    using System;

    public class HelloWorldHttp2Handler : Http2ConnectionHandler, IHttp2FrameListener
    {
        static readonly ILogger s_logger = InternalLoggerFactory.DefaultFactory.CreateLogger<HelloWorldHttp2Handler>();


        public HelloWorldHttp2Handler(IHttp2ConnectionDecoder decoder, IHttp2ConnectionEncoder encoder, Http2Settings initialSettings)
            : base(decoder, encoder, initialSettings)
        {
        }

        static IHttp2Headers Http1HeadersToHttp2Headers(IFullHttpRequest request)
        {
            IHttp2Headers http2Headers = new DefaultHttp2Headers()
            {
                Method = HttpMethod.Get.AsciiName,
                Path = AsciiString.Of(request.Uri),
                Scheme = HttpScheme.Http.Name
            };
            if (request.Headers.TryGet(HttpHeaderNames.Host, out var host))
            {
                http2Headers.Authority = host;
            }
            return http2Headers;
        }

        /**
         * Handles the cleartext HTTP upgrade event. If an upgrade occurred, sends a simple response via HTTP/2
         * on stream 1 (the stream specifically reserved for cleartext HTTP upgrade).
         */
        public override void UserEventTriggered(IChannelHandlerContext context, object evt)
        {
            if (evt is HttpServerUpgradeHandler.UpgradeEvent upgradeEvent)
            {
                this.OnHeadersRead(context, 1, Http1HeadersToHttp2Headers(upgradeEvent.UpgradeRequest), 0, true);
            }
            base.UserEventTriggered(context, evt);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            base.ExceptionCaught(context, exception);
            s_logger.LogError($"{exception}");
            context.CloseAsync();
        }

        /**
         * Sends a "Hello World" DATA frame to the client.
         */
        void SendResponse(IChannelHandlerContext context, int streamId, IByteBuffer payload)
        {
            // Send a frame for the response status
            IHttp2Headers headers = new DefaultHttp2Headers() { Status = HttpResponseStatus.OK.CodeAsText };
            this.Encoder.WriteHeadersAsync(context, streamId, headers, 0, false, context.NewPromise());
            this.Encoder.WriteDataAsync(context, streamId, payload, 0, true, context.NewPromise());

            // no need to call flush as channelReadComplete(...) will take care of it.
        }

        public int OnDataRead(IChannelHandlerContext context, int streamId, IByteBuffer data, int padding, bool endOfStream)
        {
            int processed = data.ReadableBytes + padding;
            if (endOfStream)
            {
                this.SendResponse(context, streamId, (IByteBuffer)data.Retain());
            }
            return processed;
        }

        public void OnHeadersRead(IChannelHandlerContext context, int streamId, IHttp2Headers headers, int padding, bool endOfStream)
        {
            if (endOfStream)
            {
                var content = context.Allocator.Buffer();
                content.WriteBytes(HelloWorldHttp1Handler.RESPONSE_BYTES.Duplicate());
                ByteBufferUtil.WriteAscii(content, " - via HTTP/2");
                this.SendResponse(context, streamId, content);
            }
        }

        public void OnHeadersRead(IChannelHandlerContext context, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream)
        {
            this.OnHeadersRead(context, streamId, headers, padding, endOfStream);
        }

        public void OnPriorityRead(IChannelHandlerContext context, int streamId, int streamDependency, short weight, bool exclusive)
        {
        }

        public void OnRstStreamRead(IChannelHandlerContext context, int streamId, Http2Error errorCode)
        {
        }

        public void OnSettingsAckRead(IChannelHandlerContext context)
        {
        }

        public void OnSettingsRead(IChannelHandlerContext context, Http2Settings settings)
        {
        }

        public void OnPingAckRead(IChannelHandlerContext context, long data)
        {
        }

        public void OnPingRead(IChannelHandlerContext context, long data)
        {
        }

        public void OnGoAwayRead(IChannelHandlerContext context, int lastStreamId, Http2Error errorCode, IByteBuffer debugData)
        {
        }

        public void OnPushPromiseRead(IChannelHandlerContext context, int streamId, int promisedStreamId, IHttp2Headers headers, int padding)
        {
        }

        public void OnUnknownFrame(IChannelHandlerContext context, Http2FrameTypes frameType, int streamId, Http2Flags flags, IByteBuffer payload)
        {
        }

        public void OnWindowUpdateRead(IChannelHandlerContext context, int streamId, int windowSizeIncrement)
        {
        }
    }
}
