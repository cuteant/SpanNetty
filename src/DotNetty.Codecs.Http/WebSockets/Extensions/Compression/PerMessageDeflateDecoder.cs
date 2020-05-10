// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions.Compression
{
    using System.Collections.Generic;
    using DotNetty.Transport.Channels;

    class PerMessageDeflateDecoder : DeflateDecoder
    {
        bool compressing;

        public PerMessageDeflateDecoder(bool noContext)
            : base(noContext)
        {
        }

        public override bool TryAcceptInboundMessage(object msg, out WebSocketFrame frame)
        {
            frame = msg as WebSocketFrame;
            if (frame is null) { return false; }

            switch (frame.Opcode)
            {
                case Opcode.Text:
                case Opcode.Binary:
                    return (frame.Rsv & WebSocketRsv.Rsv1) > 0;
                case Opcode.Cont:
                    return this.compressing;
                default:
                    return false;
            }
        }

        protected override int NewRsv(WebSocketFrame msg) =>
            (msg.Rsv & WebSocketRsv.Rsv1) > 0 ? msg.Rsv ^ WebSocketRsv.Rsv1 : msg.Rsv;

        protected override bool AppendFrameTail(WebSocketFrame msg) => msg.IsFinalFragment;

        protected override void Decode(IChannelHandlerContext ctx, WebSocketFrame msg, List<object> output)
        {
            base.Decode(ctx, msg, output);

            if (msg.IsFinalFragment)
            {
                this.compressing = false;
            }
            else //if (msg is TextWebSocketFrame || msg is BinaryWebSocketFrame)
            {
                switch (msg.Opcode)
                {
                    case Opcode.Text:
                    case Opcode.Binary:
                        this.compressing = true;
                        break;
                }
            }
        }
    }
}
