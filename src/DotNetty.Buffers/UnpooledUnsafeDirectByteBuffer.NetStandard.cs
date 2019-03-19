// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial class UnpooledUnsafeDirectByteBuffer
    {
        protected internal override ReadOnlyMemory<byte> _GetReadableMemory(int index, int count)
        {
            return new ReadOnlyMemory<byte>(this.buffer, index, count);
        }

        protected internal override ReadOnlySpan<byte> _GetReadableSpan(int index, int count)
        {
            return new ReadOnlySpan<byte>(this.buffer, index, count);
        }

        public override ReadOnlySequence<byte> GetSequence(int index, int count)
        {
            return ReadOnlyBufferSegment.Create(new[] { GetReadableMemory(index, count) });
        }

        protected internal override Memory<byte> _GetMemory(int index, int count)
        {
            return new Memory<byte>(this.buffer, index, count);
        }

        protected internal override Span<byte> _GetSpan(int index, int count)
        {
            return new Span<byte>(this.buffer, index, count);
        }
    }
}
#endif