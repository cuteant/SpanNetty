namespace DotNetty.Buffers
{
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Internal;

    partial class AbstractByteBuffer
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CheckIndexBounds(int readerIndex, int writerIndex)
        {
            if (readerIndex < 0 || readerIndex > writerIndex)
            {
                ThrowHelper.ThrowIndexOutOfRangeException_ReaderIndex(readerIndex, writerIndex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CheckIndexBounds(int readerIndex, int writerIndex, int capacity)
        {
            if (readerIndex < 0 || readerIndex > writerIndex || writerIndex > capacity)
            {
                ThrowHelper.ThrowIndexOutOfRangeException_CheckIndexBounds(readerIndex, writerIndex, capacity);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CheckReadableBounds(IByteBuffer src, int length)
        {
            if (length > src.ReadableBytes)
            {
                ThrowHelper.ThrowIndexOutOfRangeException_ReadableBytes(length, src);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CheckMinReadableBounds(int minimumReadableBytes, int readerIndex, int writerIndex, AbstractByteBuffer buf)
        {
            if (CheckBounds && (readerIndex > writerIndex - minimumReadableBytes))
            {
                ThrowHelper.ThrowIndexOutOfRangeException_ReaderIndex(minimumReadableBytes, readerIndex, writerIndex, buf);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CheckWritableBounds(IByteBuffer dst, int length)
        {
            if (length > dst.WritableBytes)
            {
                ThrowHelper.ThrowIndexOutOfRangeException_WritableBytes(length, dst);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CheckMinWritableBounds(int minWritableBytes, int writerIndex, int maxCapacity, AbstractByteBuffer buf)
        {
            if (minWritableBytes > maxCapacity - writerIndex)
            {
                ThrowHelper.ThrowIndexOutOfRangeException_WriterIndex(minWritableBytes, writerIndex, maxCapacity, buf);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CheckRangeBounds(int index, int fieldLength, int capacity)
        {
            if (MathUtil.IsOutOfBounds(index, fieldLength, capacity))
            {
                ThrowHelper.ThrowIndexOutOfRangeException_Index(index, fieldLength, capacity);
            }
        }
    }
}
