// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial class UnpooledUnsafeDirectByteBuffer
    {
        protected internal sealed override ReadOnlyMemory<byte> _GetReadableMemory(int index, int count)
        {
            return new ReadOnlyMemory<byte>(this.buffer, index, count);
        }

        protected internal sealed override ReadOnlySpan<byte> _GetReadableSpan(int index, int count)
        {
            return new ReadOnlySpan<byte>(this.buffer, index, count);
        }

        protected internal sealed override ReadOnlySequence<byte> _GetSequence(int index, int count)
        {
            return new ReadOnlySequence<byte>(this.buffer, index, count);
        }

        protected internal sealed override Memory<byte> _GetMemory(int index, int count)
        {
            return new Memory<byte>(this.buffer, index, count);
        }

        protected internal sealed override Span<byte> _GetSpan(int index, int count)
        {
            return new Span<byte>(this.buffer, index, count);
        }
    }
}
#endif