// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using DotNetty.Buffers;

    partial class WebSocketFrame
    {
        internal readonly Opcode Opcode;

        internal WebSocketFrame(bool finalFragment, int rsv, Opcode opcode, IByteBuffer binaryData)
            : base(binaryData)
        {
            this.finalFragment = finalFragment;
            this.rsv = rsv;
            this.Opcode = opcode;
        }
    }
}
