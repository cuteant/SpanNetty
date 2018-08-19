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
            switch (msg)
            {
                case TextWebSocketFrame textFrame when textFrame.Content.ReadableBytes > 0 && (textFrame.Rsv & WebSocketRsv.Rsv1) == 0:
                    frame = textFrame;
                    return true;
                case BinaryWebSocketFrame binFrame when binFrame.Content.ReadableBytes > 0 && (binFrame.Rsv & WebSocketRsv.Rsv1) == 0:
                    frame = binFrame;
                    return true;
                case ContinuationWebSocketFrame conFrame when conFrame.Content.ReadableBytes > 0 && (conFrame.Rsv & WebSocketRsv.Rsv1) == 0:
                    frame = conFrame;
                    return true;
                default:
                    frame = null;
                    return false;
            }
            //return (msg is TextWebSocketFrame || msg is BinaryWebSocketFrame || msg is ContinuationWebSocketFrame)
            //&& ((WebSocketFrame)msg).Content.ReadableBytes > 0
            //&& (((WebSocketFrame)msg).Rsv & WebSocketRsv.Rsv1) == 0;
        }

        protected override int Rsv(WebSocketFrame msg) => msg.Rsv | WebSocketRsv.Rsv1;

        protected override bool RemoveFrameTail(WebSocketFrame msg) => true;
    }
}
