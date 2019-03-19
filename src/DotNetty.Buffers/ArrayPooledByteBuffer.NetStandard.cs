// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial class ArrayPooledByteBuffer
    {
        protected internal override ReadOnlyMemory<byte> _GetReadableMemory(int index, int count)
        {
            return new ReadOnlyMemory<byte>(this.Memory, index, count);
        }

        protected internal override ReadOnlySpan<byte> _GetReadableSpan(int index, int count)
        {
            return new ReadOnlySpan<byte>(this.Memory, index, count);
        }

        public override ReadOnlySequence<byte> GetSequence(int index, int count)
        {
            return ReadOnlyBufferSegment.Create(new[] { GetReadableMemory(index, count) });
        }

        protected internal override Memory<byte> _GetMemory(int index, int count)
        {
            return new Memory<byte>(this.Memory, index, count);
        }

        protected internal override Span<byte> _GetSpan(int index, int count)
        {
            return new Span<byte>(this.Memory, index, count);
        }
    }
}
#endif