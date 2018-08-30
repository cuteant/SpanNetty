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

    public class WebSocketClientHandler : SimpleChannelInboundHandler<object>
    {
        static readonly ILogger s_logger = TraceLogger.GetLogger<WebSocketClientHandler>();

        readonly WebSocketClientHandshaker handshaker;
        readonly TaskCompletionSource completionSource;

        public WebSocketClientHandler(WebSocketClientHandshaker handshaker)
        {
            this.handshaker = handshaker;
            this.completionSource = new TaskCompletionSource();
        }

        public Task HandshakeCompletion => this.completionSource.Task;

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

        public override void ChannelActive(IChannelHandlerContext ctx) => 
            this.handshaker.HandshakeAsync(ctx.Channel).LinkOutcome(this.completionSource);

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            s_logger.LogInformation("WebSocket Client disconnected!");
        }

        protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
        {
            IChannel ch = ctx.Channel;
            if (!this.handshaker.IsHandshakeComplete)
            {
                try
                {
                    this.handshaker.FinishHandshake(ch, (IFullHttpResponse)msg);
                    s_logger.LogInformation("WebSocket Client connected!");
                    this.completionSource.TryComplete();
                }
                catch (WebSocketHandshakeException e)
                {
                    s_logger.LogInformation("WebSocket Client failed to connect");
                    this.completionSource.TrySetException(e);
                }

                return;
            }


            if (msg is IFullHttpResponse response)
            {
                throw new InvalidOperationException(
                    $"Unexpected FullHttpResponse (getStatus={response.Status}, content={response.Content.ToString(Encoding.UTF8)})");
            }

            if (msg is TextWebSocketFrame textFrame)
            {
                s_logger.LogInformation($"WebSocket Client received message: {textFrame.Text()}");
            }
            else if (msg is PongWebSocketFrame)
            {
                s_logger.LogInformation("WebSocket Client received pong");
            }
            else if (msg is CloseWebSocketFrame)
            {
                s_logger.LogInformation("WebSocket Client received closing");
                ch.CloseAsync();
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
        }
    }
}
