// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System.Text;
    using DotNetty.Buffers;

    public class ContinuationWebSocketFrame : WebSocketFrame
    {
        public ContinuationWebSocketFrame()
            : base(true, 0, Opcode.Cont, ArrayPooled.Buffer(0))
        {
        }

        public ContinuationWebSocketFrame(IByteBuffer binaryData)
            : base(true, 0, Opcode.Cont, binaryData)
        {
        }

        public ContinuationWebSocketFrame(bool finalFragment, IByteBuffer binaryData)
            : base(finalFragment, 0, Opcode.Cont, binaryData)
        {
        }

        public ContinuationWebSocketFrame(bool finalFragment, string text)
            : this(finalFragment, 0, FromText(text))
        {
        }

        public ContinuationWebSocketFrame(bool finalFragment, int rsv, IByteBuffer binaryData)
            : base(finalFragment, rsv, Opcode.Cont, binaryData)
        {
        }

        public ContinuationWebSocketFrame(bool finalFragment, int rsv, string text)
            : this(finalFragment, rsv, FromText(text))
        {
        }

        public string Text() => this.Content.ToString(Encoding.UTF8);

        static IByteBuffer FromText(string text) => string.IsNullOrEmpty(text)
            ? Unpooled.Empty : ArrayPooled.CopiedBuffer(text, Encoding.UTF8);

        public override IByteBufferHolder Replace(IByteBuffer content) => new ContinuationWebSocketFrame(this.IsFinalFragment, this.Rsv, content);
    }
}
