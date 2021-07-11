namespace Http2Tiles
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http2;
    using DotNetty.Transport.Channels;

    /**
     * Handles all the requests for data. It receives a {@link IFullHttpRequest},
     * which has been converted by a {@link InboundHttp2ToHttpAdapter} before it
     * arrived here. For further details, check {@link Http2OrHttpHandler} where the
     * pipeline is setup.
     */
    public class Http2RequestHandler : SimpleChannelInboundHandler2<IFullHttpRequest>
    {
        private static readonly string LATENCY_FIELD_NAME = "latency";
        private static readonly int MIN_LATENCY = 0;
        private static readonly int MAX_LATENCY = 1000;
        private static readonly string IMAGE_COORDINATE_Y = "y";
        private static readonly string IMAGE_COORDINATE_X = "x";

        protected override void ChannelRead0(IChannelHandlerContext ctx, IFullHttpRequest request)
        {
            QueryStringDecoder queryString = new QueryStringDecoder(request.Uri);
            string streamId = StreamId(request);
            int latency = Http2ExampleUtil.ToInt(Http2ExampleUtil.FirstValue(queryString, LATENCY_FIELD_NAME), 0);
            if (latency < MIN_LATENCY || latency > MAX_LATENCY)
            {
                SendBadRequest(ctx, streamId);
                return;
            }
            string x = Http2ExampleUtil.FirstValue(queryString, IMAGE_COORDINATE_X);
            string y = Http2ExampleUtil.FirstValue(queryString, IMAGE_COORDINATE_Y);
            if (x == null || y == null)
            {
                HandlePage(ctx, streamId, latency, request);
            }
            else
            {
                HandleImage(x, y, ctx, streamId, latency, request);
            }
        }

        private static void SendBadRequest(IChannelHandlerContext ctx, string streamId)
        {
            IFullHttpResponse response = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.BadRequest, Unpooled.Empty);
            StreamId(response, streamId);
            ctx.WriteAndFlushAsync(response);
        }

        private void HandleImage(string x, string y, IChannelHandlerContext ctx, string streamId, int latency, IFullHttpRequest request)
        {
            var image = ImageCache.Image(int.Parse(x), int.Parse(y));
            IFullHttpResponse response = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK, image.Duplicate());
            response.Headers.Set(HttpHeaderNames.ContentType, "image/jpeg");
            SendResponse(ctx, streamId, latency, response, request);
        }

        private void HandlePage(IChannelHandlerContext ctx, string streamId, int latency, IFullHttpRequest request)
        {
            byte[] body = Html.Body(latency);
            IByteBuffer content = ctx.Allocator.Buffer(Html.HEADER.Length + body.Length + Html.FOOTER.Length);
            content.WriteBytes(Html.HEADER);
            content.WriteBytes(body);
            content.WriteBytes(Html.FOOTER);
            IFullHttpResponse response = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK, content);
            response.Headers.Set(HttpHeaderNames.ContentType, "text/html; charset=UTF-8");
            SendResponse(ctx, streamId, latency, response, request);
        }

        protected virtual void SendResponse(IChannelHandlerContext ctx, string streamId, int latency,
            IFullHttpResponse response, IFullHttpRequest request)
        {
            HttpUtil.SetContentLength(response, response.Content.ReadableBytes);
            StreamId(response, streamId);

            ctx.Executor.Schedule(() =>
            {
                ctx.WriteAndFlushAsync(response);
            }, TimeSpan.FromMilliseconds(latency));
        }

        private static string StreamId(IFullHttpRequest request)
        {
            return request.Headers.GetAsString(HttpConversionUtil.ExtensionHeaderNames.StreamId);
        }

        private static void StreamId(IFullHttpResponse response, string streamId)
        {
            response.Headers.Set(HttpConversionUtil.ExtensionHeaderNames.StreamId, streamId);
        }

    }
}
