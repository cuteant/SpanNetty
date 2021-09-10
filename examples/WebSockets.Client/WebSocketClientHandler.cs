// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace WebSockets.Client
{
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Handlers.Timeout;
    using DotNetty.Transport.Channels;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Text;
    using System.Threading.Tasks;

    public class WebSocketClientHandler : SimpleChannelInboundHandler<object>
    {
        static readonly ILogger s_logger = InternalLoggerFactory.DefaultFactory.CreateLogger<WebSocketClientHandler>();

        readonly WebSocketClientHandshaker _handshaker;
        IPromise _handshakeFuture;

        public WebSocketClientHandler(WebSocketClientHandshaker handshaker)
        {
            _handshaker = handshaker;
        }

        public Task HandshakeCompletion => _handshakeFuture.Task;

        public override void HandlerAdded(IChannelHandlerContext context)
        {
            _handshakeFuture = context.NewPromise();
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            _handshaker.HandshakeAsync(context.Channel);
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            s_logger.LogInformation("WebSocket Client disconnected!");
        }

        protected override void ChannelRead0(IChannelHandlerContext context, object message)
        {
            IChannel channel = context.Channel;
            if (!_handshaker.IsHandshakeComplete)
            {
                try
                {
                    _handshaker.FinishHandshake(channel, (IFullHttpResponse)message);
                    s_logger.LogInformation("WebSocket Client connected!");
                    _handshakeFuture.TryComplete();
                }
                catch (WebSocketHandshakeException e)
                {
                    s_logger.LogWarning("WebSocket Client failed to connect");
                    _handshakeFuture.TrySetException(e);
                }
                return;
            }

            if (message is IFullHttpResponse response)
            {
                throw new InvalidOperationException(
                        "Unexpected FullHttpResponse (getStatus=" + response.Status +
                                ", content=" + response.Content.ToString(Encoding.UTF8) + ')');
            }

            if (message is TextWebSocketFrame textFrame)
            {
                s_logger.LogInformation($"WebSocket Client received message: {textFrame.Text()}");
            }
            else if (message is PongWebSocketFrame)
            {
                s_logger.LogInformation("WebSocket Client received pong");
            }
            else if (message is CloseWebSocketFrame)
            {
                s_logger.LogInformation("WebSocket Client received closing");
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            s_logger.LogError(exception, $"{nameof(WebSocketClientHandler)} caught exception:");
            _handshakeFuture.TrySetException(exception);
            context.CloseAsync();
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
                _handshakeFuture.TryComplete();
            }
        }
    }
}
