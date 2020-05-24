// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions.Compression
{
    /// <summary>
    /// Per-frame implementation of deflate decompressor.
    /// </summary>
    sealed class PerFrameDeflateDecoder : DeflateDecoder
    {
        /// <summary>Constructor</summary>
        /// <param name="noContext">true to disable context takeover.</param>
        public PerFrameDeflateDecoder(bool noContext)
            : base(noContext, NeverSkipWebSocketExtensionFilter.Instance)
        {
        }

        /// <summary>Constructor</summary>
        /// <param name="noContext">true to disable context takeover.</param>
        /// <param name="extensionDecoderFilter">extension decoder filter for per frame deflate decoder.</param>
        public PerFrameDeflateDecoder(bool noContext, IWebSocketExtensionFilter extensionDecoderFilter)
            : base(noContext, extensionDecoderFilter)
        {
        }

        /// <inheritdoc />
        public override bool AcceptInboundMessage(object msg)
        {
            if (!(msg is WebSocketFrame wsFrame)) { return false; }

            if (ExtensionDecoderFilter.MustSkip(wsFrame)) { return false; }

            switch (wsFrame.Opcode)
            {
                case Opcode.Text:
                case Opcode.Binary:
                case Opcode.Cont:
                    return (wsFrame.Rsv & WebSocketRsv.Rsv1) > 0;
                default:
                    return false;
            }
        }

        /// <inheritdoc />
        protected override int NewRsv(WebSocketFrame msg) => msg.Rsv ^ WebSocketRsv.Rsv1;

        /// <inheritdoc />
        protected override bool AppendFrameTail(WebSocketFrame msg) => true;
    }
}
