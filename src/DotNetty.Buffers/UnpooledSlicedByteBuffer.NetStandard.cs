// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial class UnpooledSlicedByteBuffer
    {
        protected internal sealed override ReadOnlyMemory<byte> _GetReadableMemory(int index, int count)
        {
            return this.UnwrapCore()._GetReadableMemory(this.Idx(index), count);
        }

        protected internal sealed override ReadOnlySpan<byte> _GetReadableSpan(int index, int count)
        {
            return this.UnwrapCore()._GetReadableSpan(this.Idx(index), count);
        }

        protected internal sealed override ReadOnlySequence<byte> _GetSequence(int index, int count)
        {
            return this.UnwrapCore()._GetSequence(this.Idx(index), count);
        }

        protected internal sealed override Memory<byte> _GetMemory(int index, int count)
        {
            return this.UnwrapCore()._GetMemory(this.Idx(index), count);
        }

        protected internal sealed override Span<byte> _GetSpan(int index, int count)
        {
            return this.UnwrapCore()._GetSpan(this.Idx(index), count);
        }
    }
}
