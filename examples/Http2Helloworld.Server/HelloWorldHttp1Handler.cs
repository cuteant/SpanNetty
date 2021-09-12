namespace Http2Helloworld.Server
{
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Channels;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Text;

    /// <summary>
    /// HTTP handler that responds with a "Hello World"
    /// </summary>
    public class HelloWorldHttp1Handler : SimpleChannelInboundHandler2<IFullHttpRequest>
    {
        static readonly ILogger s_logger = InternalLoggerFactory.DefaultFactory.CreateLogger<HelloWorldHttp1Handler>();

        internal static readonly IByteBuffer RESPONSE_BYTES = Unpooled.UnreleasableBuffer(Unpooled.CopiedBuffer("Hello World", Encoding.UTF8));

        readonly string establishApproach;

        public HelloWorldHttp1Handler(string establishApproach)
        {
            this.establishApproach = establishApproach ?? throw new ArgumentNullException(nameof(establishApproach));
        }

        protected override void ChannelRead0(IChannelHandlerContext context, IFullHttpRequest request)
        {
            if (HttpUtil.Is100ContinueExpected(request))
            {
                context.WriteAsync(new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.Continue, Unpooled.Empty));
            }
            var keepAlive = HttpUtil.IsKeepAlive(request);

            var content = context.Allocator.Buffer();
            content.WriteBytes(RESPONSE_BYTES.Duplicate());
            ByteBufferUtil.WriteAscii(content, " - via " + request.ProtocolVersion + " (" + this.establishApproach + ")");

            IFullHttpResponse response = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK, content);
            response.Headers.Set(HttpHeaderNames.ContentType, "text/plain; charset=UTF-8");
            response.Headers.SetInt(HttpHeaderNames.ContentLength, response.Content.ReadableBytes);

            if (keepAlive)
            {
                if (request.ProtocolVersion.Equals(HttpVersion.Http10))
                {
                    response.Headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.KeepAlive);
                }
                context.WriteAsync(response);
            }
            else
            {
                // Tell the client we're going to close the connection.
                response.Headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.Close);
                context.WriteAsync(response).CloseOnComplete(context.Channel);
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            context.Flush();
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            s_logger.LogError($"{exception}");
            context.CloseAsync();
        }
    }
}
