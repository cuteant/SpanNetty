// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using DotNetty.Buffers;

    public class PongWebSocketFrame : WebSocketFrame
    {
        public PongWebSocketFrame()
            : base(true, 0, Opcode.Pong, ArrayPooled.Buffer(0))
        {
        }

        public PongWebSocketFrame(IByteBuffer binaryData)
            : base(true, 0, Opcode.Pong, binaryData)
        {
        }

        public PongWebSocketFrame(bool finalFragment, int rsv, IByteBuffer binaryData)
            : base(finalFragment, rsv, Opcode.Pong, binaryData)
        {
        }

        public override IByteBufferHolder Replace(IByteBuffer content) => new PongWebSocketFrame(this.IsFinalFragment, this.Rsv, content);
    }
}
