// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    public sealed class ReadOnlyByteBufferStream : ByteBufferStream
    {
        public ReadOnlyByteBufferStream(IByteBuffer buffer)
            : base(buffer, false, false) { }

        public ReadOnlyByteBufferStream(IByteBuffer buffer, bool releaseReferenceOnClosure)
            : base(buffer, false, releaseReferenceOnClosure) { }
    }
}
