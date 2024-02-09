namespace Http2Tiles
{
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http2;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Channels;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Text;
    using System.Threading.Tasks;

    /**
     * Handles the exceptional case where HTTP 1.x was negotiated under TLS.
     */
    public class FallbackRequestHandler : SimpleChannelInboundHandler2<IHttpRequest>
    {
        static readonly ILogger s_logger = InternalLoggerFactory.DefaultFactory.CreateLogger<FallbackRequestHandler>();

        private static readonly IByteBuffer Response = Unpooled.UnreleasableBuffer(Unpooled.CopiedBuffer("<!DOCTYPE html>"
                + "<html><body><h2>To view the example you need a browser that supports HTTP/2 ("
                + Http2CodecUtil.TlsUpgradeProtocolName
                + ")</h2></body></html>", Encoding.UTF8));

        protected override void ChannelRead0(IChannelHandlerContext context, IHttpRequest request)
        {
            if (HttpUtil.Is100ContinueExpected(request))
            {
                context.WriteAsync(new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.Continue, Unpooled.Empty));
            }

            IByteBuffer content = context.Allocator.Buffer();
            content.WriteBytes(Response.Duplicate());

            IFullHttpResponse response = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK, content);
            response.Headers.Set(HttpHeaderNames.ContentType, "text/html; charset=UTF-8");
            response.Headers.SetInt(HttpHeaderNames.ContentLength, response.Content.ReadableBytes);

            context.WriteAsync(response)
               .ContinueWith(t => context.CloseAsync(), TaskContinuationOptions.ExecuteSynchronously);
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
