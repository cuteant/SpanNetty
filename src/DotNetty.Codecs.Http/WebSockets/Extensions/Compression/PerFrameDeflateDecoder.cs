// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions.Compression
{
    class PerFrameDeflateDecoder : DeflateDecoder
    {
        public PerFrameDeflateDecoder(bool noContext)
            : base(noContext)
        {
        }

        public override bool TryAcceptInboundMessage(object msg, out WebSocketFrame frame)
        {
            frame = msg as WebSocketFrame;
            if (null == frame) { return false; }

            switch (frame.Opcode)
            {
                case Opcode.Text:
                case Opcode.Binary:
                case Opcode.Cont:
                    return (frame.Rsv & WebSocketRsv.Rsv1) > 0;
                default:
                    return false;
            }
        }

        protected override int NewRsv(WebSocketFrame msg) => msg.Rsv ^ WebSocketRsv.Rsv1;

        protected override bool AppendFrameTail(WebSocketFrame msg) => true;
    }
}
