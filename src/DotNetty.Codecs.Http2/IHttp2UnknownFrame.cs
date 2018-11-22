// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;

    public interface IHttp2UnknownFrame : IHttp2StreamFrame, IByteBufferHolder
    {
        Http2FrameTypes FrameType { get; }

        Http2Flags Flags { get; }
    }
}
