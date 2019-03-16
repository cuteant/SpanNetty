// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial class PooledSlicedByteBuffer
    {
        public override ReadOnlyMemory<byte> GetReadableMemory(int index, int count)
        {
            this.CheckIndex0(index, count);
            return this.Unwrap().GetReadableMemory(this.Idx(index), count);
        }

        public override ReadOnlySpan<byte> GetReadableSpan(int index, int count)
        {
            this.CheckIndex0(index, count);
            return this.Unwrap().GetReadableSpan(this.Idx(index), count);
        }

        public override ReadOnlySequence<byte> GetSequence(int index, int count)
        {
            this.CheckIndex0(index, count);
            return this.Unwrap().GetSequence(this.Idx(index), count);
        }

        public override Memory<byte> GetMemory(int index, int count)
        {
            this.CheckIndex0(index, count);
            return this.Unwrap().GetMemory(this.Idx(index), count);
        }

        public override Span<byte> GetSpan(int index, int count)
        {
            this.CheckIndex0(index, count);
            return this.Unwrap().GetSpan(this.Idx(index), count);
        }
    }
}
#endif