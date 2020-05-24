// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions.Compression
{
    using System.Collections.Generic;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Per-message implementation of deflate compressor.
    /// </summary>
    sealed class PerMessageDeflateEncoder : DeflateEncoder
    {
        private bool _compressing;

        /// <summary>Constructor</summary>
        /// <param name="compressionLevel">compression level of the compressor.</param>
        /// <param name="windowSize">maximum size of the window compressor buffer.</param>
        /// <param name="noContext">true to disable context takeover.</param>
        public PerMessageDeflateEncoder(int compressionLevel, int windowSize, bool noContext)
            : base(compressionLevel, windowSize, noContext, NeverSkipWebSocketExtensionFilter.Instance)
        {
        }

        /// <summary>Constructor</summary>
        /// <param name="compressionLevel">compression level of the compressor.</param>
        /// <param name="windowSize">maximum size of the window compressor buffer.</param>
        /// <param name="noContext">true to disable context takeover.</param>
        /// <param name="extensionEncoderFilter">extension filter for per message deflate encoder.</param>
        public PerMessageDeflateEncoder(int compressionLevel, int windowSize, bool noContext, IWebSocketExtensionFilter extensionEncoderFilter)
            : base(compressionLevel, windowSize, noContext, extensionEncoderFilter)
        {
        }

        /// <inheritdoc />
        public override bool AcceptOutboundMessage(object msg)
        {
            if (!(msg is WebSocketFrame wsFrame)) { return false; }

            if (ExtensionEncoderFilter.MustSkip(wsFrame))
            {
                if (_compressing)
                {
                    ThrowHelper.ThrowInvalidOperationException_Cannot_skip_per_message_deflate_encoder();
                }
                return false;
            }

            switch (wsFrame.Opcode)
            {
                case Opcode.Text:
                case Opcode.Binary:
                    return 0u >= (uint)(wsFrame.Rsv & WebSocketRsv.Rsv1);
                case Opcode.Cont:
                    return _compressing;
                default:
                    return false;
            }
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        protected override bool RemoveFrameTail(WebSocketFrame msg) => msg.IsFinalFragment;

        /// <inheritdoc />
        protected override void Encode(IChannelHandlerContext ctx, WebSocketFrame msg, List<object> output)
        {
            base.Encode(ctx, msg, output);

            if (msg.IsFinalFragment)
            {
                _compressing = false;
            }
            else //if (msg is TextWebSocketFrame || msg is BinaryWebSocketFrame)
            {
                switch (msg.Opcode)
                {
                    case Opcode.Text:
                    case Opcode.Binary:
                        _compressing = true;
                        break;
                }
            }
        }
    }
}
