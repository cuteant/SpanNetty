// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Transport.Channels;

    public abstract class WebSocketProtocolHandler : MessageToMessageDecoder<WebSocketFrame>
    {
        internal readonly bool DropPongFrames;

        /// <summary>
        /// Creates a new <see cref="WebSocketProtocolHandler"/> that will <i>drop</i> <see cref="PongWebSocketFrame"/>s.
        /// </summary>
        internal WebSocketProtocolHandler() : this(true)
        {
        }

        /// <summary>
        /// Creates a new <see cref="WebSocketProtocolHandler"/>, given a parameter that determines whether or not to drop
        /// <see cref="PongWebSocketFrame"/>s.
        /// </summary>
        /// <param name="dropPongFrames"></param>
        internal WebSocketProtocolHandler(bool dropPongFrames)
        {
            DropPongFrames = dropPongFrames;
        }

        protected override void Decode(IChannelHandlerContext ctx, WebSocketFrame frame, List<object> output)
        {
            // 须同时修改 WebSocketServerProtocolHandler & WebSocketClientProtocolHandler
            switch (frame.Opcode)
            {
                case Opcode.Ping:
                    var contect = frame.Content;
                    contect.Retain();
                    ctx.Channel.WriteAndFlushAsync(new PongWebSocketFrame(contect));
                    ReadIfNeeded(ctx);
                    return;

                case Opcode.Pong when DropPongFrames:
                    // Pong frames need to get ignored
                    ReadIfNeeded(ctx);
                    return;

                default:
                    output.Add(frame.Retain());
                    break;
            }
        }

        protected static void ReadIfNeeded(IChannelHandlerContext ctx)
        {
            if (!ctx.Channel.Configuration.AutoRead)
            {
                ctx.Read();
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            ctx.FireExceptionCaught(cause);
            ctx.CloseAsync();
        }
    }
}
