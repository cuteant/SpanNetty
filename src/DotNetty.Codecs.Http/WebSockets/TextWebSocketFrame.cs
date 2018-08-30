// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System.Text;
    using DotNetty.Buffers;

    public class TextWebSocketFrame : WebSocketFrame
    {
        public TextWebSocketFrame()
            : base(true, 0, Opcode.Text, ArrayPooled.Buffer(0))
        {
        }

        public TextWebSocketFrame(string text)
            : base(true, 0, Opcode.Text, FromText(text))
        {
        }

        public TextWebSocketFrame(IByteBuffer binaryData)
            : base(true, 0, Opcode.Text, binaryData)
        {
        }

        public TextWebSocketFrame(bool finalFragment, string text)
            : base(finalFragment, 0, Opcode.Text, FromText(text))
        {
        }

        public TextWebSocketFrame(bool finalFragment, IByteBuffer binaryData)
            : base(finalFragment, 0, Opcode.Text, binaryData)
        {
        }

        public TextWebSocketFrame(bool finalFragment, int rsv, string text)
            : base(finalFragment, rsv, Opcode.Text, FromText(text))
        {
        }

        public TextWebSocketFrame(bool finalFragment, int rsv, IByteBuffer binaryData)
            : base(finalFragment, rsv, Opcode.Text, binaryData)
        {
        }

        static IByteBuffer FromText(string text) => string.IsNullOrEmpty(text)
            ? Unpooled.Empty : Unpooled.CopiedBuffer(text, Encoding.UTF8);

        public string Text() => this.Content.ToString(Encoding.UTF8);

        public override IByteBufferHolder Replace(IByteBuffer content) => new TextWebSocketFrame(this.IsFinalFragment, this.Rsv, content);
    }
}
