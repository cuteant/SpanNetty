// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using DotNetty.Buffers;

    /// <summary>
    /// Web Socket frame containing binary data.
    /// </summary>
    public class PingWebSocketFrame : WebSocketFrame
    {
        public PingWebSocketFrame()
            : base(true, 0, Opcode.Ping, ArrayPooled.Buffer(0))
        {
        }

        /// <summary>
        /// Creates a new ping frame with the specified binary data.
        /// </summary>
        /// <param name="binaryData">the content of the frame.</param>
        public PingWebSocketFrame(IByteBuffer binaryData)
            : base(true, 0, Opcode.Ping, binaryData)
        {
        }

        /// <summary>
        /// Creates a new ping frame with the specified binary data.
        /// </summary>
        /// <param name="finalFragment">flag indicating if this frame is the final fragment</param>
        /// <param name="rsv">reserved bits used for protocol extensions</param>
        /// <param name="binaryData">the content of the frame.</param>
        public PingWebSocketFrame(bool finalFragment, int rsv, IByteBuffer binaryData)
            : base(finalFragment, rsv, Opcode.Ping, binaryData)
        {
        }

        /// <inheritdoc />
        public override IByteBufferHolder Replace(IByteBuffer content) => new PingWebSocketFrame(this.IsFinalFragment, this.Rsv, content);
    }
}
