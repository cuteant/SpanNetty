// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using DotNetty.Buffers;

    /// <summary>
    /// Web Socket frame containing binary data.
    /// </summary>
    public class BinaryWebSocketFrame : WebSocketFrame
    {
        /// <summary>
        /// Creates a new empty binary frame.
        /// </summary>
        public BinaryWebSocketFrame() 
            : base(true, 0, Opcode.Binary, ArrayPooled.Buffer(0))
        {
        }

        /// <summary>
        /// Creates a new binary frame with the specified binary data. The final fragment flag is set to true.
        /// </summary>
        /// <param name="binaryData">the content of the frame.</param>
        public BinaryWebSocketFrame(IByteBuffer binaryData)
            : base(true, 0, Opcode.Binary, binaryData)
        {
        }

        /// <summary>
        /// Creates a new binary frame with the specified binary data and the final fragment flag.
        /// </summary>
        /// <param name="finalFragment">flag indicating if this frame is the final fragment</param>
        /// <param name="binaryData">the content of the frame.</param>
        public BinaryWebSocketFrame(bool finalFragment, IByteBuffer binaryData)
            : base(finalFragment, 0, Opcode.Binary, binaryData)
        {
        }

        /// <summary>
        /// Creates a new binary frame with the specified binary data and the final fragment flag.
        /// </summary>
        /// <param name="finalFragment">flag indicating if this frame is the final fragment</param>
        /// <param name="rsv">reserved bits used for protocol extensions</param>
        /// <param name="binaryData">the content of the frame.</param>
        public BinaryWebSocketFrame(bool finalFragment, int rsv, IByteBuffer binaryData)
            : base(finalFragment, rsv, Opcode.Binary, binaryData)
        {
        }

        /// <inheritdoc />
        public override IByteBufferHolder Replace(IByteBuffer content) => new BinaryWebSocketFrame(this.IsFinalFragment, this.Rsv, content);
    }
}
