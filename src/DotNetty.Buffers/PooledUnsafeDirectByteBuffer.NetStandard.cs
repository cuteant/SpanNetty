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
        protected internal sealed override ReadOnlyMemory<byte> _GetReadableMemory(int index, int count)
        {
            return MemoryMarshal.CreateFromPinnedArray(this.Memory, this.Idx(index), count);
        }

        protected internal sealed override ReadOnlySpan<byte> _GetReadableSpan(int index, int count)
        {
            return new ReadOnlySpan<byte>(Unsafe.Add<byte>(this.Origin.ToPointer(), this.Idx(index)), count);
        }

        protected internal sealed override ReadOnlySequence<byte> _GetSequence(int index, int count)
        {
            return new ReadOnlySequence<byte>(MemoryMarshal.CreateFromPinnedArray(this.Memory, this.Idx(index), count));
        }

        protected internal sealed override Memory<byte> _GetMemory(int index, int count)
        {
            return MemoryMarshal.CreateFromPinnedArray(this.Memory, this.Idx(index), count);
        }

        protected internal sealed override Span<byte> _GetSpan(int index, int count)
        {
            return new Span<byte>(Unsafe.Add<byte>(this.Origin.ToPointer(), this.Idx(index)), count);
        }
    }
}
#endif