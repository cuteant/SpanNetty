// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;
    using System.Linq;
    using DotNetty.Common;

    partial class FixedCompositeByteBuf
    {
        public override ReadOnlyMemory<byte> GetReadableMemory(int index, int count)
        {
            switch (this.buffers.Length)
            {
                case 0:
                    return ReadOnlyMemory<byte>.Empty;
                case 1:
                    return this.Buffer(0).GetReadableMemory(index, count);
                default:
                    throw new NotSupportedException();
            }
        }

        public override ReadOnlySpan<byte> GetReadableSpan(int index, int count)
        {
            switch (this.buffers.Length)
            {
                case 0:
                    return ReadOnlySpan<byte>.Empty;
                case 1:
                    return this.Buffer(0).GetReadableSpan(index, count);
                default:
                    throw new NotSupportedException();
            }
        }

        public override ReadOnlySequence<byte> GetSequence(int index, int count)
        {
            this.CheckIndex(index, count);
            if (count == 0) { return ReadOnlySequence<byte>.Empty; }

            var array = ThreadLocalList<ArraySegment<byte>>.NewInstance(this.nioBufferCount);
            try
            {
                var c = this.FindComponent(index);
                int i = c.Index;
                int adjustment = c.Offset;
                var s = c.Buf;
                for (; ; )
                {
                    int localLength = Math.Min(count, s.ReadableBytes - (index - adjustment));
                    switch (s.IoBufferCount)
                    {
                        case 0:
                            ThrowHelper.ThrowNotSupportedException();
                            break;
                        case 1:
                            array.Add(s.GetIoBuffer(index - adjustment, localLength));
                            break;
                        default:
                            array.AddRange(s.GetIoBuffers(index - adjustment, localLength));
                            break;
                    }

                    index += localLength;
                    count -= localLength;
                    adjustment += s.ReadableBytes;
                    if (count <= 0) { break; }
                    s = this.Buffer(++i);
                }

                return ReadOnlyBufferSegment.Create(array.Select(_ => (ReadOnlyMemory<byte>)_));
            }
            finally
            {
                array.Return();
            }
        }

        public override Memory<byte> GetMemory(int index, int count)
        {
            switch (this.buffers.Length)
            {
                case 0:
                    return Memory<byte>.Empty;
                case 1:
                    return this.Buffer(0).GetMemory(index, count);
                default:
                    throw new NotSupportedException();
            }
        }

        public override Span<byte> GetSpan(int index, int count)
        {
            switch (this.buffers.Length)
            {
                case 0:
                    return Span<byte>.Empty;
                case 1:
                    return this.Buffer(0).GetSpan(index, count);
                default:
                    throw new NotSupportedException();
            }
        }
    }
}
#endif