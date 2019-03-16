// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;
    using System.Linq;
    using DotNetty.Common;

    public partial class CompositeByteBuffer
    {
        public override ReadOnlyMemory<byte> GetReadableMemory(int index, int count)
        {
            switch (this.componentCount)
            {
                case 0:
                    return ReadOnlyMemory<byte>.Empty;
                case 1:
                    ComponentEntry c = this.components[0];
                    return c.Buffer.GetReadableMemory(index, count);
                default:
                    throw new NotSupportedException();
            }
        }

        public override ReadOnlySpan<byte> GetReadableSpan(int index, int count)
        {
            switch (this.componentCount)
            {
                case 0:
                    return ReadOnlySpan<byte>.Empty;
                case 1:
                    ComponentEntry c = this.components[0];
                    return c.Buffer.GetReadableSpan(index, count);
                default:
                    throw new NotSupportedException();
            }
        }

        public override ReadOnlySequence<byte> GetSequence(int index, int count)
        {
            this.CheckIndex(index, count);
            if (count == 0) { return ReadOnlySequence<byte>.Empty; }

            var buffers = ThreadLocalList<ArraySegment<byte>>.NewInstance(this.componentCount);
            try
            {
                int i = this.ToComponentIndex0(index);
                while (count > 0)
                {
                    ComponentEntry c = this.components[i];
                    IByteBuffer s = c.Buffer;
                    int localLength = Math.Min(count, c.EndOffset - index);
                    switch (s.IoBufferCount)
                    {
                        case 0:
                            ThrowHelper.ThrowNotSupportedException();
                            break;
                        case 1:
                            buffers.Add(s.GetIoBuffer(c.Idx(index), localLength));
                            break;
                        default:
                            buffers.AddRange(s.GetIoBuffers(c.Idx(index), localLength));
                            break;
                    }

                    index += localLength;
                    count -= localLength;
                    i++;
                }

                return ReadOnlyBufferSegment.Create(buffers.Select(_ => (ReadOnlyMemory<byte>)_));
            }
            finally
            {
                buffers.Return();
            }
        }

        public override Memory<byte> GetMemory(int index, int count)
        {
            switch (this.componentCount)
            {
                case 0:
                    return Memory<byte>.Empty;
                case 1:
                    ComponentEntry c = this.components[0];
                    return c.Buffer.GetMemory(index, count);
                default:
                    throw new NotSupportedException();
            }
        }

        public override Span<byte> GetSpan(int index, int count)
        {
            switch (this.componentCount)
            {
                case 0:
                    return Span<byte>.Empty;
                case 1:
                    ComponentEntry c = this.components[0];
                    return c.Buffer.GetSpan(index, count);
                default:
                    throw new NotSupportedException();
            }
        }
    }
}
#endif
