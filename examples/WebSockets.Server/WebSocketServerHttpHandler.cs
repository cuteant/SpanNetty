// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace WebSockets.Server
{
    using System.Diagnostics;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using Examples.Common;
    using Microsoft.Extensions.Logging;
    using static DotNetty.Codecs.Http.HttpResponseStatus;
    using static DotNetty.Codecs.Http.HttpVersion;

    public sealed class WebSocketServerHttpHandler : SimpleChannelInboundHandler2<IFullHttpRequest>
    {
        static readonly ILogger s_logger = InternalLoggerFactory.DefaultFactory.CreateLogger<WebSocketServerHttpHandler>();

        readonly string websocketPath;

        public WebSocketServerHttpHandler(string websocketPath) => this.websocketPath = websocketPath;

        protected override void ChannelRead0(IChannelHandlerContext ctx, IFullHttpRequest req)
        {
            // Handle a bad request.
            if (!req.Result.IsSuccess)
            {
                SendHttpResponse(ctx, req, new DefaultFullHttpResponse(req.ProtocolVersion, BadRequest, ctx.Allocator.Buffer(0)));
                return;
            }

            // Allow only GET methods.
            if (!HttpMethod.Get.Equals(req.Method))
            {
                SendHttpResponse(ctx, req, new DefaultFullHttpResponse(req.ProtocolVersion, Forbidden, ctx.Allocator.Buffer(0)));
                return;
            }

            // Send the demo page and favicon.ico
            switch (req.Uri)
            {
                case "/benchmark":
                    {
                        IByteBuffer content = WebSocketServerBenchmarkPage.GetContent(GetWebSocketLocation(req, this.websocketPath));
                        var res = new DefaultFullHttpResponse(req.ProtocolVersion, OK, content);

                        res.Headers.Set(HttpHeaderNames.ContentType, "text/html; charset=UTF-8");
                        HttpUtil.SetContentLength(res, content.ReadableBytes);

                        SendHttpResponse(ctx, req, res);
                        return;
                    }
                case "/":
                case "/index.html":
                    {
                        IByteBuffer content = WebSocketServerIndexPage.GetContent(GetWebSocketLocation(req, this.websocketPath));
                        var res = new DefaultFullHttpResponse(req.ProtocolVersion, OK, content);

                        res.Headers.Set(HttpHeaderNames.ContentType, "text/html; charset=UTF-8");
                        HttpUtil.SetContentLength(res, content.ReadableBytes);

                        SendHttpResponse(ctx, req, res);
                        return;
                    }
                case "/favicon.ico":
                default:
                    {
                        var res = new DefaultFullHttpResponse(req.ProtocolVersion, NotFound, ctx.Allocator.Buffer(0));
                        SendHttpResponse(ctx, req, res);
                        return;
                    }
            }
        }

        static void SendHttpResponse(IChannelHandlerContext ctx, IFullHttpRequest req, IFullHttpResponse res)
        {
            // Generate an error page if response getStatus code is not OK (200).
            HttpResponseStatus responseStatus = res.Status;
            if (responseStatus.Code != 200)
            {
                ByteBufferUtil.WriteUtf8(res.Content, responseStatus.ToString());
                HttpUtil.SetContentLength(res, res.Content.ReadableBytes);
            }

            // Send the response and close the connection if necessary.
            var keepAlive = HttpUtil.IsKeepAlive(req) && responseStatus.Code == 200;
            HttpUtil.SetKeepAlive(res, keepAlive);
            var future = ctx.WriteAndFlushAsync(res);
            if (!keepAlive)
            {
                future.CloseOnComplete(ctx.Channel);
            }
        }

        static string GetWebSocketLocation(IFullHttpRequest req, string path)
        {
            bool result = req.Headers.TryGet(HttpHeaderNames.Host, out ICharSequence value);
            Debug.Assert(result, "Host header does not exist.");
            string location = value.ToString() + path;

            if (ServerSettings.IsSsl)
            {
                return "wss://" + location;
            }
            else
            {
                return "ws://" + location;
            }
        }
    }
}
