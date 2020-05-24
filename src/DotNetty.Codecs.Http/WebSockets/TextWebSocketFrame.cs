// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System.Text;
    using DotNetty.Buffers;

    /// <summary>
    /// Web Socket text frame.
    /// </summary>
    public class TextWebSocketFrame : WebSocketFrame
    {
        /// <summary>
        /// Creates a new empty text frame.
        /// </summary>
        public TextWebSocketFrame()
            : base(true, 0, Opcode.Text, ArrayPooled.Buffer(0))
        {
        }

        /// <summary>
        /// Creates a new text frame with the specified text string. The final fragment flag is set to true.
        /// </summary>
        /// <param name="text">String to put in the frame.</param>
        public TextWebSocketFrame(string text)
            : base(true, 0, Opcode.Text, FromText(text))
        {
        }

        /// <summary>
        /// Creates a new text frame with the specified binary data. The final fragment flag is set to true.
        /// </summary>
        /// <param name="binaryData">the content of the frame.</param>
        public TextWebSocketFrame(IByteBuffer binaryData)
            : base(true, 0, Opcode.Text, binaryData)
        {
        }

        /// <summary>
        /// Creates a new text frame with the specified text string. The final fragment flag is set to true.
        /// </summary>
        /// <param name="finalFragment">flag indicating if this frame is the final fragment</param>
        /// <param name="text">String to put in the frame.</param>
        public TextWebSocketFrame(bool finalFragment, string text)
            : base(finalFragment, 0, Opcode.Text, FromText(text))
        {
        }

        /// <summary>
        /// Creates a new text frame with the specified binary data and the final fragment flag.
        /// </summary>
        /// <param name="finalFragment">flag indicating if this frame is the final fragment</param>
        /// <param name="binaryData">the content of the frame.</param>
        public TextWebSocketFrame(bool finalFragment, IByteBuffer binaryData)
            : base(finalFragment, 0, Opcode.Text, binaryData)
        {
        }

        /// <summary>
        /// Creates a new text frame with the specified text string. The final fragment flag is set to true.
        /// </summary>
        /// <param name="finalFragment">flag indicating if this frame is the final fragment</param>
        /// <param name="rsv">reserved bits used for protocol extensions</param>
        /// <param name="text">String to put in the frame.</param>
        public TextWebSocketFrame(bool finalFragment, int rsv, string text)
            : base(finalFragment, rsv, Opcode.Text, FromText(text))
        {
        }

        /// <summary>
        /// Creates a new text frame with the specified binary data and the final fragment flag.
        /// </summary>
        /// <param name="finalFragment">flag indicating if this frame is the final fragment</param>
        /// <param name="rsv">reserved bits used for protocol extensions</param>
        /// <param name="binaryData">the content of the frame.</param>
        public TextWebSocketFrame(bool finalFragment, int rsv, IByteBuffer binaryData)
            : base(finalFragment, rsv, Opcode.Text, binaryData)
        {
        }

        static IByteBuffer FromText(string text) => string.IsNullOrEmpty(text)
            ? Unpooled.Empty : ArrayPooled.EncodeString(text, Encoding.UTF8);

        /// <summary>
        /// Returns the text data in this frame.
        /// </summary>
        /// <returns></returns>
        public string Text() => this.Content.ToString(Encoding.UTF8);

        /// <inheritdoc />
        public override IByteBufferHolder Replace(IByteBuffer content) => new TextWebSocketFrame(this.IsFinalFragment, this.Rsv, content);
    }
}
