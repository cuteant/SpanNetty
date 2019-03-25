#if !NET40

namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;
    using System.Runtime.CompilerServices;

    public static partial class IByteBufferExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyMemory<byte> GetReadableMemory(this IByteBuffer buf) => buf.UnreadMemory;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<byte> GetReadableSpan(this IByteBuffer buf) => buf.UnreadSpan;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySequence<byte> GetSequence(this IByteBuffer buf) => buf.UnreadSequence;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindIndex(this IByteBuffer buf, Predicate<byte> match)
        {
            return buf.FindIndex(buf.ReaderIndex, buf.ReadableBytes, match);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindLastIndex(this IByteBuffer buf, Predicate<byte> match)
        {
            return buf.FindLastIndex(buf.ReaderIndex, buf.ReadableBytes, match);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOf(this IByteBuffer buf, ReadOnlySpan<byte> values)
        {
            return buf.IndexOf(buf.ReaderIndex, buf.WriterIndex, values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOf(this IByteBuffer buf, IByteBuffer value)
        {
            return buf.IndexOf(buf.ReaderIndex, buf.WriterIndex, value.GetReadableSpan());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOf(this IByteBuffer buf, IByteBuffer value, int valueIndex, int valueLength)
        {
            return buf.IndexOf(buf.ReaderIndex, buf.WriterIndex, value.GetReadableSpan(valueIndex, valueLength));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOfAny(this IByteBuffer buf, byte value0, byte value1)
        {
            return buf.IndexOfAny(buf.ReaderIndex, buf.WriterIndex, value0, value1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOfAny(this IByteBuffer buf, byte value0, byte value1, byte value2)
        {
            return buf.IndexOfAny(buf.ReaderIndex, buf.WriterIndex, value0, value1, value2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOfAny(this IByteBuffer buf, ReadOnlySpan<byte> values)
        {
            return buf.IndexOfAny(buf.ReaderIndex, buf.WriterIndex, values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOfAny(this IByteBuffer buf, IByteBuffer value)
        {
            return buf.IndexOfAny(buf.ReaderIndex, buf.WriterIndex, value.GetReadableSpan());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOfAny(this IByteBuffer buf, IByteBuffer value, int valueIndex, int valueLength)
        {
            return buf.IndexOfAny(buf.ReaderIndex, buf.WriterIndex, value.GetReadableSpan(valueIndex, valueLength));
        }
    }
}

#endif