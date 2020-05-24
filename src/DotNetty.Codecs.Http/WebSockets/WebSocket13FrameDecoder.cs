// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    /// <summary>
    /// Decodes a web socket frame from wire protocol version 13 format. V13 is essentially the same as V8.
    /// </summary>
    public class WebSocket13FrameDecoder : WebSocket08FrameDecoder
    {
        /// <summary>Constructor</summary>
        /// <param name="expectMaskedFrames">Web socket servers must set this to true processed incoming masked payload. Client implementations
        /// must set this to false.</param>
        /// <param name="allowExtensions">Flag to allow reserved extension bits to be used or not</param>
        /// <param name="maxFramePayloadLength">Maximum length of a frame's payload. Setting this to an appropriate value for you application
        /// helps check for denial of services attacks.</param>
        public WebSocket13FrameDecoder(bool expectMaskedFrames, bool allowExtensions, int maxFramePayloadLength)
            : this(expectMaskedFrames, allowExtensions, maxFramePayloadLength, false)
        {
        }

        /// <summary>Constructor</summary>
        /// <param name="expectMaskedFrames">Web socket servers must set this to true processed incoming masked payload. Client implementations
        /// must set this to false.</param>
        /// <param name="allowExtensions">Flag to allow reserved extension bits to be used or not</param>
        /// <param name="maxFramePayloadLength">Maximum length of a frame's payload. Setting this to an appropriate value for you application
        /// helps check for denial of services attacks.</param>
        /// <param name="allowMaskMismatch">When set to true, frames which are not masked properly according to the standard will still be
        /// accepted.</param>
        public WebSocket13FrameDecoder(bool expectMaskedFrames, bool allowExtensions, int maxFramePayloadLength, bool allowMaskMismatch)
            : this(WebSocketDecoderConfig.NewBuilder()
                .ExpectMaskedFrames(expectMaskedFrames)
                .AllowExtensions(allowExtensions)
                .MaxFramePayloadLength(maxFramePayloadLength)
                .AllowMaskMismatch(allowMaskMismatch)
                .Build())
        {
        }

        /// <summary>Constructor</summary>
        /// <param name="decoderConfig">Frames decoder configuration.</param>
        public WebSocket13FrameDecoder(WebSocketDecoderConfig decoderConfig)
            : base(decoderConfig)
        {
        }
    }
}
