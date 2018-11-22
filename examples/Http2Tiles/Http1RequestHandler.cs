
namespace Http2Tiles
{
    using System;
    using System.Threading.Tasks;
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
                ctx.WriteAsync(new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.Continue));
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
                    response.Headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.KeepAlive);
                    ctx.WriteAndFlushAsync(response);
                }
                else
                {
                    ctx.WriteAndFlushAsync(response)
                       .ContinueWith(t => ctx.Channel.CloseAsync(), TaskContinuationOptions.ExecuteSynchronously);
                }
            }, TimeSpan.FromMilliseconds(latency));
        }
    }
}
