// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions.Compression
{
    using System.Collections.Generic;
    using DotNetty.Transport.Channels;

    class PerMessageDeflateEncoder : DeflateEncoder
    {
        bool compressing;

        public PerMessageDeflateEncoder(int compressionLevel, int windowSize, bool noContext)
            : base(compressionLevel, windowSize, noContext)
        {
        }

        public override bool TryAcceptOutboundMessage(object msg, out WebSocketFrame frame)
        {
            frame = msg as WebSocketFrame;
            if (null == frame) { return false; }

            switch (frame.Opcode)
            {
                case Opcode.Text:
                case Opcode.Binary:
                    return (frame.Rsv & WebSocketRsv.Rsv1) == 0;
                case Opcode.Cont:
                    return this.compressing;
                default:
                    return false;
            }
        }

        protected override int Rsv(WebSocketFrame msg)
        {
            switch (msg.Opcode)
            {
                case Opcode.Text:
                case Opcode.Binary:
                    return msg.Rsv | WebSocketRsv.Rsv1;
                default:
                    return msg.Rsv;
            }
        }

        protected override bool RemoveFrameTail(WebSocketFrame msg) => msg.IsFinalFragment;

        protected override void Encode(IChannelHandlerContext ctx, WebSocketFrame msg, List<object> output)
        {
            base.Encode(ctx, msg, output);

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
