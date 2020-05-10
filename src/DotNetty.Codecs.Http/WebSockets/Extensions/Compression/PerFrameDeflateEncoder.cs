// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions.Compression
{
    class PerFrameDeflateEncoder : DeflateEncoder
    {
        public PerFrameDeflateEncoder(int compressionLevel, int windowSize, bool noContext)
            : base(compressionLevel, windowSize, noContext)
        {
        }

        public override bool TryAcceptOutboundMessage(object msg, out WebSocketFrame frame)
        {
            frame = msg as WebSocketFrame;
            if (frame is null) { return false; }

            switch (frame.Opcode)
            {
                case Opcode.Text:
                case Opcode.Binary:
                case Opcode.Cont:
                    return frame.Content.ReadableBytes > 0 && 0u >= (uint)(frame.Rsv & WebSocketRsv.Rsv1);
                default:
                    return false;
            }
        }

        protected override int Rsv(WebSocketFrame msg) => msg.Rsv | WebSocketRsv.Rsv1;

        protected override bool RemoveFrameTail(WebSocketFrame msg) => true;
    }
}
