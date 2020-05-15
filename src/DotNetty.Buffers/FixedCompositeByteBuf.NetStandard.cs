// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;
    using System.Diagnostics;
    using DotNetty.Common;

    partial class FixedCompositeByteBuf
    {
        protected internal override ReadOnlyMemory<byte> _GetReadableMemory(int index, int count)
        {
            if (0u >= (uint)count) { return ReadOnlyMemory<byte>.Empty; }

            if (this.buffers.Length == 1)
            {
                var buf = this.Buffer(0);
                if (buf.IsSingleIoBuffer)
                {
                    return buf.GetReadableMemory(index, count);
                }
            }

            var merged = new Memory<byte>(new byte[count]);
            var bufs = this.GetSequence(index, count);

            int offset = 0;
            foreach (var buf in bufs)
            {
                Debug.Assert(merged.Length - offset >= buf.Length);

                buf.CopyTo(merged.Slice(offset));
                offset += buf.Length;
            }

            return merged;
        }

        protected internal override ReadOnlySpan<byte> _GetReadableSpan(int index, int count)
        {
            if (0u >= (uint)count) { return ReadOnlySpan<byte>.Empty; }

            if (this.buffers.Length == 1)
            {
                var buf = this.Buffer(0);
                if (buf.IsSingleIoBuffer)
                {
                    return buf.GetReadableSpan(index, count);
                }
            }

            var merged = new Memory<byte>(new byte[count]);
            var bufs = this.GetSequence(index, count);

            int offset = 0;
            foreach (var buf in bufs)
            {
                Debug.Assert(merged.Length - offset >= buf.Length);

                buf.CopyTo(merged.Slice(offset));
                offset += buf.Length;
            }

            return merged.Span;
        }

        protected internal override ReadOnlySequence<byte> _GetSequence(int index, int count)
        {
            if (0u >= (uint)count) { return ReadOnlySequence<byte>.Empty; }

            var array = ThreadLocalList<ReadOnlyMemory<byte>>.NewInstance(this.nioBufferCount);
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
                            array.Add(s.GetReadableMemory(index - adjustment, localLength));
                            break;
                        default:
                            var sequence = s.GetSequence(index - adjustment, localLength);
                            foreach (var memory in sequence)
                            {
                                array.Add(memory);
                            }
                            break;
                    }

                    index += localLength;
                    count -= localLength;
                    adjustment += s.ReadableBytes;
                    if (count <= 0) { break; }
                    s = this.Buffer(++i);
                }

                return ReadOnlyBufferSegment.Create(array);
            }
            finally
            {
                array.Return();
            }
        }

        public override Memory<byte> GetMemory(int sizeHintt = 0)
        {
            throw new ReadOnlyBufferException();
        }

        protected internal override Memory<byte> _GetMemory(int index, int count)
        {
            throw new ReadOnlyBufferException();
        }

        public override Span<byte> GetSpan(int sizeHintt = 0)
        {
            throw new ReadOnlyBufferException();
        }

        protected internal override Span<byte> _GetSpan(int index, int count)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetBytes(int index, in ReadOnlySpan<byte> src)
        {
            throw new ReadOnlyBufferException();
        }
        public override IByteBuffer SetBytes(int index, in ReadOnlyMemory<byte> src)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer WriteBytes(in ReadOnlySpan<byte> src)
        {
            throw new ReadOnlyBufferException();
        }
        public override IByteBuffer WriteBytes(in ReadOnlyMemory<byte> src)
        {
            throw new ReadOnlyBufferException();
        }
    }
}
