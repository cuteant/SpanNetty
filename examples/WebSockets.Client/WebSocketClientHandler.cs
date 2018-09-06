// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace WebSockets.Client
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Handlers.Timeout;
    using DotNetty.Transport.Channels;
    using Microsoft.Extensions.Logging;

    public class WebSocketClientHandler : SimpleChannelInboundHandler2<WebSocketFrame>
    {
        static readonly ILogger s_logger = TraceLogger.GetLogger<WebSocketClientHandler>();

        readonly TaskCompletionSource completionSource;

        public WebSocketClientHandler()
        {
            this.completionSource = new TaskCompletionSource();
        }

        public Task HandshakeCompletion => this.completionSource.Task;

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            s_logger.LogInformation("WebSocket Client disconnected!");
        }

        protected override void ChannelRead0(IChannelHandlerContext ctx, WebSocketFrame msg)
        {
            IChannel ch = ctx.Channel;

            if (msg is TextWebSocketFrame textFrame)
            {
                s_logger.LogInformation($"WebSocket Client received message: {textFrame.Text()}");
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception exception)
        {
            s_logger.LogError(exception, $"{nameof(WebSocketClientHandler)} caught exception:");
            this.completionSource.TrySetException(exception);
            ctx.CloseAsync();
        }

        public override void UserEventTriggered(IChannelHandlerContext context, object evt)
        {
            if (evt is IdleStateEvent stateEvent)
            {
                s_logger.LogWarning($"{nameof(WebSocketClientHandler)} caught idle state: {stateEvent.State}");
                var frame = new PingWebSocketFrame(Unpooled.WrappedBuffer(new byte[] { 8, 1, 8, 1 }));
                context.Channel.WriteAndFlushAsync(frame);
            }
            else if (evt is WebSocketClientProtocolHandler.ClientHandshakeStateEvent handshakeStateEvt && handshakeStateEvt == WebSocketClientProtocolHandler.ClientHandshakeStateEvent.HandshakeComplete)
            {
                completionSource.TryComplete();
            }
        }
    }
}
