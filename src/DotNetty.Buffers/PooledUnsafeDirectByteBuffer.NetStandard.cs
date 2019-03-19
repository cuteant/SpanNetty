// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    unsafe partial class PooledUnsafeDirectByteBuffer
    {
        protected internal override ReadOnlyMemory<byte> _GetReadableMemory(int index, int count)
        {
            return MemoryMarshal.CreateFromPinnedArray(this.Memory, this.Idx(index), count);
        }

        protected internal override ReadOnlySpan<byte> _GetReadableSpan(int index, int count)
        {
            return new ReadOnlySpan<byte>(Unsafe.AsPointer(ref this.Memory[this.Idx(index)]), count);
        }

        public override ReadOnlySequence<byte> GetSequence(int index, int count)
        {
            return ReadOnlyBufferSegment.Create(new[] { GetReadableMemory(index, count) });
        }

        protected internal override Memory<byte> _GetMemory(int index, int count)
        {
            return MemoryMarshal.CreateFromPinnedArray(this.Memory, this.Idx(index), count);
        }

        protected internal override Span<byte> _GetSpan(int index, int count)
        {
            return new Span<byte>(Unsafe.AsPointer(ref this.Memory[this.Idx(index)]), count);
        }
    }
}
#endif