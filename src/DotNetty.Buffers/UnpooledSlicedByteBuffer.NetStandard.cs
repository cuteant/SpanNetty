// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Buffers
{
    using System;

    partial class UnpooledSlicedByteBuffer
    {
        protected internal override ReadOnlyMemory<byte> _GetReadableMemory(int index, int count)
        {
            return this.UnwrapCore()._GetReadableMemory(this.Idx(index), count);
        }

        protected internal override ReadOnlySpan<byte> _GetReadableSpan(int index, int count)
        {
            return this.UnwrapCore()._GetReadableSpan(this.Idx(index), count);
        }

        protected internal override Memory<byte> _GetMemory(int index, int count)
        {
            return this.UnwrapCore()._GetMemory(this.Idx(index), count);
        }

        protected internal override Span<byte> _GetSpan(int index, int count)
        {
            return this.UnwrapCore()._GetSpan(this.Idx(index), count);
        }
    }
}
#endif