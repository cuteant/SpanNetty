// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial class ArrayPooledByteBuffer
    {
        public override ReadOnlyMemory<byte> GetReadableMemory(int index, int count)
        {
            this.CheckIndex(index, count);
            return new ReadOnlyMemory<byte>(this.Memory, index, count);
        }

        public override ReadOnlySpan<byte> GetReadableSpan(int index, int count)
        {
            this.CheckIndex(index, count);
            return new ReadOnlySpan<byte>(this.Memory, index, count);
        }

        public override ReadOnlySequence<byte> GetSequence(int index, int count)
        {
            return ReadOnlyBufferSegment.Create(new[] { GetReadableMemory(index, count) });
        }

        public override Memory<byte> GetMemory(int index, int count)
        {
            this.CheckIndex(index, count);
            return new Memory<byte>(this.Memory, index, count);
        }

        public override Span<byte> GetSpan(int index, int count)
        {
            this.CheckIndex(index, count);
            return new Span<byte>(this.Memory, index, count);
        }
    }
}
#endif