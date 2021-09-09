// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace WebSockets.Server
{
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Handlers.Timeout;
    using DotNetty.Transport.Channels;
    using Microsoft.Extensions.Logging;
    using System;

    public sealed class WebSocketServerFrameHandler : SimpleChannelInboundHandler<WebSocketFrame>
    {
        static readonly ILogger s_logger = InternalLoggerFactory.DefaultFactory.CreateLogger<WebSocketServerFrameHandler>();

        protected override void ChannelRead0(IChannelHandlerContext context, WebSocketFrame frame)
        {
            if (frame is TextWebSocketFrame textFrame)
            {
                var msg = textFrame.Text();
                if (msg.StartsWith("throw ", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception(msg.Substring(6, msg.Length - 6));
                }
                // Echo the frame
                context.Channel.WriteAndFlushAsync(new TextWebSocketFrame(msg));
                return;
            }

            if (frame is BinaryWebSocketFrame binaryFrame)
            {
                // Echo the frame
                context.Channel.WriteAndFlushAsync(new BinaryWebSocketFrame(binaryFrame.Content.RetainedDuplicate()));
            }
        }

        //public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            s_logger.LogError(exception, $"{nameof(WebSocketServerFrameHandler)} caught exception:");
            context.CloseAsync();
        }

        public override void UserEventTriggered(IChannelHandlerContext context, object evt)
        {
            switch (evt)
            {
                case IdleStateEvent stateEvent:
                    s_logger.LogWarning($"{nameof(WebSocketServerFrameHandler)} caught idle state: {stateEvent.State}");
                    break;

                case WebSocketServerProtocolHandler.HandshakeComplete handshakeComplete:
                    if (context.Pipeline.Get<WebSocketServerHttpHandler>() != null) { context.Pipeline.Remove<WebSocketServerHttpHandler>(); }
                    s_logger.LogInformation($"RequestUri: {handshakeComplete.RequestUri}, \r\nHeaders:{handshakeComplete.RequestHeaders}, \r\nSubprotocol: {handshakeComplete.SelectedSubprotocol}");
                    break;

                default:
                    break;
            }
        }
    }
}
