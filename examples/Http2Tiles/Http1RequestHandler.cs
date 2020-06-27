namespace Http2Tiles
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Transport.Channels;

    /**
     * Handles the requests for the tiled image using HTTP 1.x as a protocol.
     */
    public class Http1RequestHandler : Http2RequestHandler
    {
        protected override void ChannelRead0(IChannelHandlerContext ctx, IFullHttpRequest request)
        {
            if (HttpUtil.Is100ContinueExpected(request))
            {
                ctx.WriteAsync(new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.Continue, Unpooled.Empty));
            }
            base.ChannelRead0(ctx, request);
        }

        protected override void SendResponse(IChannelHandlerContext ctx, string streamId, int latency, IFullHttpResponse response, IFullHttpRequest request)
        {
            HttpUtil.SetContentLength(response, response.Content.ReadableBytes);
            ctx.Executor.Schedule(() =>
            {
                if (HttpUtil.IsKeepAlive(request))
                {
                    if (request.ProtocolVersion.Equals(HttpVersion.Http10))
                    {
                        response.Headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.KeepAlive);
                    }
                    ctx.WriteAndFlushAsync(response);
                }
                else
                {
                    // Tell the client we're going to close the connection.
                    response.Headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.Close);
                    ctx.WriteAndFlushAsync(response).CloseOnComplete(ctx.Channel);
                }
            }, TimeSpan.FromMilliseconds(latency));
        }
    }
}
