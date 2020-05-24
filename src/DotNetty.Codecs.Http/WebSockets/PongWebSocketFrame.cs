// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using DotNetty.Buffers;

    /// <summary>
    /// Web Socket frame containing binary data.
    /// </summary>
    public class PongWebSocketFrame : WebSocketFrame
    {
        /// <summary>
        /// Creates a new empty pong frame.
        /// </summary>
        public PongWebSocketFrame()
            : base(true, 0, Opcode.Pong, ArrayPooled.Buffer(0))
        {
        }

        /// <summary>
        /// Creates a new pong frame with the specified binary data.
        /// </summary>
        /// <param name="binaryData">the content of the frame.</param>
        public PongWebSocketFrame(IByteBuffer binaryData)
            : base(true, 0, Opcode.Pong, binaryData)
        {
        }

        /// <summary>
        /// Creates a new pong frame with the specified binary data
        /// </summary>
        /// <param name="finalFragment">flag indicating if this frame is the final fragment</param>
        /// <param name="rsv">reserved bits used for protocol extensions</param>
        /// <param name="binaryData">the content of the frame.</param>
        public PongWebSocketFrame(bool finalFragment, int rsv, IByteBuffer binaryData)
            : base(finalFragment, rsv, Opcode.Pong, binaryData)
        {
        }

        /// <inheritdoc />
        public override IByteBufferHolder Replace(IByteBuffer content) => new PongWebSocketFrame(this.IsFinalFragment, this.Rsv, content);
    }
}
