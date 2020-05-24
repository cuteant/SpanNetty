// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System.Text;
    using DotNetty.Buffers;

    /// <summary>
    /// Web Socket continuation frame containing continuation text or binary data. This is used for
    /// fragmented messages where the contents of a messages is contained more than 1 frame.
    /// </summary>
    public class ContinuationWebSocketFrame : WebSocketFrame
    {
        /// <summary>
        /// Creates a new empty continuation frame.
        /// </summary>
        public ContinuationWebSocketFrame()
            : base(true, 0, Opcode.Cont, ArrayPooled.Buffer(0))
        {
        }

        /// <summary>
        /// Creates a new continuation frame with the specified binary data. The final fragment flag is
        /// set to true.
        /// </summary>
        /// <param name="binaryData">the content of the frame.</param>
        public ContinuationWebSocketFrame(IByteBuffer binaryData)
            : base(true, 0, Opcode.Cont, binaryData)
        {
        }

        /// <summary>
        /// Creates a new continuation frame with the specified binary data.
        /// </summary>
        /// <param name="finalFragment">flag indicating if this frame is the final fragment</param>
        /// <param name="binaryData">the content of the frame.</param>
        public ContinuationWebSocketFrame(bool finalFragment, IByteBuffer binaryData)
            : base(finalFragment, 0, Opcode.Cont, binaryData)
        {
        }

        /// <summary>
        /// Creates a new continuation frame with the specified text data
        /// </summary>
        /// <param name="finalFragment">flag indicating if this frame is the final fragment</param>
        /// <param name="text">text content of the frame.</param>
        public ContinuationWebSocketFrame(bool finalFragment, string text)
            : this(finalFragment, 0, FromText(text))
        {
        }

        /// <summary>
        /// Creates a new continuation frame with the specified binary data.
        /// </summary>
        /// <param name="finalFragment">flag indicating if this frame is the final fragment</param>
        /// <param name="rsv">reserved bits used for protocol extensions</param>
        /// <param name="binaryData">the content of the frame.</param>
        public ContinuationWebSocketFrame(bool finalFragment, int rsv, IByteBuffer binaryData)
            : base(finalFragment, rsv, Opcode.Cont, binaryData)
        {
        }

        /// <summary>
        /// Creates a new continuation frame with the specified text data
        /// </summary>
        /// <param name="finalFragment">flag indicating if this frame is the final fragment</param>
        /// <param name="rsv">reserved bits used for protocol extensions</param>
        /// <param name="text">text content of the frame.</param>
        public ContinuationWebSocketFrame(bool finalFragment, int rsv, string text)
            : this(finalFragment, rsv, FromText(text))
        {
        }

        /// <summary>
        /// Returns the text data in this frame.
        /// </summary>
        /// <returns></returns>
        public string Text() => this.Content.ToString(Encoding.UTF8);

        /// <summary>
        /// Sets the string for this frame.
        /// </summary>
        /// <param name="text">text to store.</param>
        /// <returns></returns>
        static IByteBuffer FromText(string text) => string.IsNullOrEmpty(text)
            ? Unpooled.Empty : ArrayPooled.EncodeString(text, Encoding.UTF8);

        /// <inheritdoc />
        public override IByteBufferHolder Replace(IByteBuffer content) => new ContinuationWebSocketFrame(this.IsFinalFragment, this.Rsv, content);
    }
}
