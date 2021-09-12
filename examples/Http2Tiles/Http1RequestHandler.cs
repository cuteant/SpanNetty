namespace Http2Tiles
{
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Transport.Channels;
    using System;

    /**
     * Handles the requests for the tiled image using HTTP 1.x as a protocol.
     */
    public class Http1RequestHandler : Http2RequestHandler
    {
        protected override void ChannelRead0(IChannelHandlerContext context, IFullHttpRequest request)
        {
            if (HttpUtil.Is100ContinueExpected(request))
            {
                context.WriteAsync(new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.Continue, Unpooled.Empty));
            }
            base.ChannelRead0(context, request);
        }

        protected override void SendResponse(IChannelHandlerContext context, string streamId, int latency, IFullHttpResponse response, IFullHttpRequest request)
        {
            HttpUtil.SetContentLength(response, response.Content.ReadableBytes);
            context.Executor.Schedule(() =>
            {
                if (HttpUtil.IsKeepAlive(request))
                {
                    if (request.ProtocolVersion.Equals(HttpVersion.Http10))
                    {
                        response.Headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.KeepAlive);
                    }
                    context.WriteAndFlushAsync(response);
                }
                else
                {
                    // Tell the client we're going to close the connection.
                    response.Headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.Close);
                    context.WriteAndFlushAsync(response).CloseOnComplete(context.Channel);
                }
            }, TimeSpan.FromMilliseconds(latency));
        }
    }
}
