// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace WebSockets.Server
{
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using Examples.Common;
    using Microsoft.Extensions.Logging;
    using System.Diagnostics;
    using static DotNetty.Codecs.Http.HttpResponseStatus;

    public sealed class WebSocketServerHttpHandler : SimpleChannelInboundHandler2<IFullHttpRequest>
    {
        static readonly ILogger s_logger = InternalLoggerFactory.DefaultFactory.CreateLogger<WebSocketServerHttpHandler>();

        readonly string websocketPath;

        public WebSocketServerHttpHandler(string websocketPath) => this.websocketPath = websocketPath;

        protected override void ChannelRead0(IChannelHandlerContext context, IFullHttpRequest request)
        {
            // Handle a bad request.
            if (!request.Result.IsSuccess)
            {
                SendHttpResponse(context, request, new DefaultFullHttpResponse(request.ProtocolVersion, BadRequest, context.Allocator.Buffer(0)));
                return;
            }

            // Allow only GET methods.
            if (!HttpMethod.Get.Equals(request.Method))
            {
                SendHttpResponse(context, request, new DefaultFullHttpResponse(request.ProtocolVersion, Forbidden, context.Allocator.Buffer(0)));
                return;
            }

            // Send the demo page and favicon.ico
            switch (request.Uri)
            {
                case "/benchmark":
                    {
                        IByteBuffer content = WebSocketServerBenchmarkPage.GetContent(GetWebSocketLocation(request, this.websocketPath));
                        var res = new DefaultFullHttpResponse(request.ProtocolVersion, OK, content);

                        res.Headers.Set(HttpHeaderNames.ContentType, "text/html; charset=UTF-8");
                        HttpUtil.SetContentLength(res, content.ReadableBytes);

                        SendHttpResponse(context, request, res);
                        return;
                    }
                case "/":
                case "/index.html":
                    {
                        IByteBuffer content = WebSocketServerIndexPage.GetContent(GetWebSocketLocation(request, this.websocketPath));
                        var res = new DefaultFullHttpResponse(request.ProtocolVersion, OK, content);

                        res.Headers.Set(HttpHeaderNames.ContentType, "text/html; charset=UTF-8");
                        HttpUtil.SetContentLength(res, content.ReadableBytes);

                        SendHttpResponse(context, request, res);
                        return;
                    }
                case "/favicon.ico":
                default:
                    {
                        var res = new DefaultFullHttpResponse(request.ProtocolVersion, NotFound, context.Allocator.Buffer(0));
                        SendHttpResponse(context, request, res);
                        return;
                    }
            }
        }

        static void SendHttpResponse(IChannelHandlerContext context, IFullHttpRequest request, IFullHttpResponse response)
        {
            // Generate an error page if response getStatus code is not OK (200).
            HttpResponseStatus responseStatus = response.Status;
            if (responseStatus.Code != 200)
            {
                ByteBufferUtil.WriteUtf8(response.Content, responseStatus.ToString());
                HttpUtil.SetContentLength(response, response.Content.ReadableBytes);
            }

            // Send the response and close the connection if necessary.
            var keepAlive = HttpUtil.IsKeepAlive(request) && responseStatus.Code == 200;
            HttpUtil.SetKeepAlive(response, keepAlive);
            var future = context.WriteAndFlushAsync(response);
            if (!keepAlive)
            {
                future.CloseOnComplete(context.Channel);
            }
        }

        static string GetWebSocketLocation(IFullHttpRequest request, string path)
        {
            bool result = request.Headers.TryGet(HttpHeaderNames.Host, out ICharSequence value);
            Debug.Assert(result, "Host header does not exist.");
            string location = $"{value}{path}";

            if (ServerSettings.IsSsl)
            {
                return $"wss://{location}";
            }
            else
            {
                return $"ws://{location}";
            }
        }
    }
}
