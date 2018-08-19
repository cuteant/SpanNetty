// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using DotNetty.Buffers;

    partial class ByteToMessageDecoder
    {
        private static IByteBuffer MergeCumulatorInternal(IByteBufferAllocator alloc, IByteBuffer cumulation, IByteBuffer input)
        {
            IByteBuffer buffer;
            if (cumulation.WriterIndex > cumulation.MaxCapacity - input.ReadableBytes || cumulation.ReferenceCount > 1)
            {
                // Expand cumulation (by replace it) when either there is not more room in the buffer
                // or if the refCnt is greater then 1 which may happen when the user use Slice().Retain() or
                // Duplicate().Retain().
                //
                // See:
                // - https://github.com/netty/netty/issues/2327
                // - https://github.com/netty/netty/issues/1764
                buffer = ExpandCumulation(alloc, cumulation, input.ReadableBytes);
            }
            else
            {
                buffer = cumulation;
            }
            buffer.WriteBytes(input);
            input.Release();
            return buffer;
        }

        private static IByteBuffer CompositionCumulationInternal(IByteBufferAllocator alloc, IByteBuffer cumulation, IByteBuffer input)
        {
            IByteBuffer buffer;
            if (cumulation.ReferenceCount > 1)
            {
                // Expand cumulation (by replace it) when the refCnt is greater then 1 which may happen when the user
                // use slice().retain() or duplicate().retain().
                //
                // See:
                // - https://github.com/netty/netty/issues/2327
                // - https://github.com/netty/netty/issues/1764
                buffer = ExpandCumulation(alloc, cumulation, input.ReadableBytes);
                buffer.WriteBytes(input);
                input.Release();
            }
            else
            {
                CompositeByteBuffer composite;
                var asComposite = cumulation as CompositeByteBuffer;
                if (asComposite != null)
                {
                    composite = asComposite;
                }
                else
                {
                    int readable = cumulation.ReadableBytes;
                    composite = alloc.CompositeBuffer();
                    composite.AddComponent(cumulation).SetWriterIndex(readable);
                }
                composite.AddComponent(input).SetWriterIndex(composite.WriterIndex + input.ReadableBytes);
                buffer = composite;
            }
            return buffer;
        }
    }
}