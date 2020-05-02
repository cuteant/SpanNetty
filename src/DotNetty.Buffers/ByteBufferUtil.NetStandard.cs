// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if !NET40

namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using CuteAnt.Buffers;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;

    partial class ByteBufferUtil
    {
        /// <summary>
        ///     Returns the reader index of needle in haystack, or -1 if needle is not in haystack.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOf(IByteBuffer needle, IByteBuffer haystack)
        {
            return haystack.IndexOf(needle);
        }

        /// <summary>
        ///     Returns {@code true} if and only if the two specified buffers are
        ///     identical to each other for {@code length} bytes starting at {@code aStartIndex}
        ///     index for the {@code a} buffer and {@code bStartIndex} index for the {@code b} buffer.
        ///     A more compact way to express this is:
        ///     <p />
        ///     {@code a[aStartIndex : aStartIndex + length] == b[bStartIndex : bStartIndex + length]}
        /// </summary>
        public static bool Equals(IByteBuffer a, int aStartIndex, IByteBuffer b, int bStartIndex, int length)
        {
            if (aStartIndex < 0 || bStartIndex < 0 || length < 0)
            {
                ThrowHelper.ThrowArgumentException_NonNegative();
            }
            if (a.WriterIndex - length < aStartIndex || b.WriterIndex - length < bStartIndex)
            {
                return false;
            }

            var spanA = a.GetReadableSpan(aStartIndex, length);
            var spanB = b.GetReadableSpan(bStartIndex, length);
            return spanA.SequenceEqual(spanB);
        }

        /// <summary>
        ///     Returns {@code true} if and only if the two specified buffers are
        ///     identical to each other as described in {@link ByteBuf#equals(Object)}.
        ///     This method is useful when implementing a new buffer type.
        /// </summary>
        public static bool Equals(IByteBuffer bufferA, IByteBuffer bufferB)
        {
            int aLen = bufferA.ReadableBytes;
            if (aLen != bufferB.ReadableBytes)
            {
                return false;
            }

            return Equals(bufferA, bufferA.ReaderIndex, bufferB, bufferB.ReaderIndex, aLen);
        }

        /// <summary>
        ///     Compares the two specified buffers as described in {@link ByteBuf#compareTo(ByteBuf)}.
        ///     This method is useful when implementing a new buffer type.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(IByteBuffer bufferA, IByteBuffer bufferB)
        {
            return bufferA.GetReadableSpan().SequenceCompareTo(bufferB.GetReadableSpan());
        }

        // Fast-Path implementation
        internal static int WriteUtf8(AbstractByteBuffer buffer, int writerIndex, ICharSequence value)
        {
            if (value is IHasUtf16Span hasUtf16)
            {
                if (buffer.IsSingleIoBuffer)
                {
                    var bufSpan = buffer.GetSpan(writerIndex, buffer.Capacity - writerIndex);
                    var status = TextEncodings.Utf16.ToUtf8(hasUtf16.Utf16Span, bufSpan, out _, out var written);
                    if (status == OperationStatus.Done) { return written; }
                }
                else
                {
                    if (TryWriteUtf8Composite(buffer, writerIndex, hasUtf16.Utf16Span, out var written)) { return written; }
                }
            }
            return WriteUtf80(buffer, writerIndex, value);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryWriteUtf8Composite(AbstractByteBuffer buffer, int writerIndex, ReadOnlySpan<char> utf16Span, out int written)
        {
            var memory = BufferManager.Shared.Rent(buffer.Capacity);
            try
            {
                var status = TextEncodings.Utf16.ToUtf8(utf16Span, memory.AsSpan(), out _, out written);
                if (status == OperationStatus.Done)
                {
                    buffer.SetBytes(writerIndex, memory, 0, written);
                    return true;
                }
            }
            finally
            {
                BufferManager.Shared.Return(memory);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int WriteUtf80(AbstractByteBuffer buffer, int writerIndex, ICharSequence value)
        {
            int oldWriterIndex = writerIndex;
            var len = value.Count;

            // We can use the _set methods as these not need to do any index checks and reference checks.
            // This is possible as we called ensureWritable(...) before.
            for (int i = 0; i < len; i++)
            {
                char c = value[i];
                if (c < 0x80)
                {
                    buffer._SetByte(writerIndex++, (byte)c);
                }
                else if (c < 0x800)
                {
                    buffer._SetByte(writerIndex++, (byte)(0xc0 | (c >> 6)));
                    buffer._SetByte(writerIndex++, (byte)(0x80 | (c & 0x3f)));
                }
                else if (char.IsSurrogate(c))
                {
                    if (!char.IsHighSurrogate(c))
                    {
                        buffer._SetByte(writerIndex++, WriteUtfUnknown);
                        continue;
                    }
                    char c2;
                    try
                    {
                        // Surrogate Pair consumes 2 characters. Optimistically try to get the next character to avoid
                        // duplicate bounds checking with charAt. If an IndexOutOfBoundsException is thrown we will
                        // re-throw a more informative exception describing the problem.
                        c2 = value[++i];
                    }
                    catch (IndexOutOfRangeException)
                    {
                        buffer._SetByte(writerIndex++, WriteUtfUnknown);
                        break;
                    }
                    if (!char.IsLowSurrogate(c2))
                    {
                        buffer._SetByte(writerIndex++, WriteUtfUnknown);
                        buffer._SetByte(writerIndex++, char.IsHighSurrogate(c2) ? WriteUtfUnknown : c2);
                        continue;
                    }
                    int codePoint = CharUtil.ToCodePoint(c, c2);
                    // See http://www.unicode.org/versions/Unicode7.0.0/ch03.pdf#G2630.
                    buffer._SetByte(writerIndex++, (byte)(0xf0 | (codePoint >> 18)));
                    buffer._SetByte(writerIndex++, (byte)(0x80 | ((codePoint >> 12) & 0x3f)));
                    buffer._SetByte(writerIndex++, (byte)(0x80 | ((codePoint >> 6) & 0x3f)));
                    buffer._SetByte(writerIndex++, (byte)(0x80 | (codePoint & 0x3f)));
                }
                else
                {
                    buffer._SetByte(writerIndex++, (byte)(0xe0 | (c >> 12)));
                    buffer._SetByte(writerIndex++, (byte)(0x80 | ((c >> 6) & 0x3f)));
                    buffer._SetByte(writerIndex++, (byte)(0x80 | (c & 0x3f)));
                }
            }

            return writerIndex - oldWriterIndex;
        }

        // Fast-Path implementation
        internal static int WriteUtf8(AbstractByteBuffer buffer, int writerIndex, string value)
        {
            if (buffer.IsSingleIoBuffer)
            {
                var bufSpan = buffer.GetSpan(writerIndex, buffer.Capacity - writerIndex);
                var status = TextEncodings.Utf16.ToUtf8(value.AsSpan(), bufSpan, out _, out var written);
                if (status == OperationStatus.Done) { return written; }
            }
            else
            {
                if (TryWriteUtf8Composite(buffer, writerIndex, value.AsSpan(), out var written)) { return written; }
            }
            return WriteUtf80(buffer, writerIndex, value);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int WriteUtf80(AbstractByteBuffer buffer, int writerIndex, string value)
        {
            int oldWriterIndex = writerIndex;
            var len = value.Length;

            // We can use the _set methods as these not need to do any index checks and reference checks.
            // This is possible as we called ensureWritable(...) before.
            for (int i = 0; i < len; i++)
            {
                char c = value[i];
                if (c < 0x80)
                {
                    buffer._SetByte(writerIndex++, (byte)c);
                }
                else if (c < 0x800)
                {
                    buffer._SetByte(writerIndex++, (byte)(0xc0 | (c >> 6)));
                    buffer._SetByte(writerIndex++, (byte)(0x80 | (c & 0x3f)));
                }
                else if (char.IsSurrogate(c))
                {
                    if (!char.IsHighSurrogate(c))
                    {
                        buffer._SetByte(writerIndex++, WriteUtfUnknown);
                        continue;
                    }
                    char c2;
                    try
                    {
                        // Surrogate Pair consumes 2 characters. Optimistically try to get the next character to avoid
                        // duplicate bounds checking with charAt. If an IndexOutOfBoundsException is thrown we will
                        // re-throw a more informative exception describing the problem.
                        c2 = value[++i];
                    }
                    catch (IndexOutOfRangeException)
                    {
                        buffer._SetByte(writerIndex++, WriteUtfUnknown);
                        break;
                    }
                    // Extra method to allow inlining the rest of writeUtf8 which is the most likely code path.
                    writerIndex = WriteUtf8Surrogate(buffer, writerIndex, c, c2);
                }
                else
                {
                    buffer._SetByte(writerIndex++, (byte)(0xe0 | (c >> 12)));
                    buffer._SetByte(writerIndex++, (byte)(0x80 | ((c >> 6) & 0x3f)));
                    buffer._SetByte(writerIndex++, (byte)(0x80 | (c & 0x3f)));
                }
            }

            return writerIndex - oldWriterIndex;
        }

        static int WriteUtf8Surrogate(AbstractByteBuffer buffer, int writerIndex, char c, char c2)
        {
            if (!char.IsLowSurrogate(c2))
            {
                buffer._SetByte(writerIndex++, WriteUtfUnknown);
                buffer._SetByte(writerIndex++, char.IsHighSurrogate(c2) ? WriteUtfUnknown : c2);
                return writerIndex;
            }
            int codePoint = CharUtil.ToCodePoint(c, c2);
            // See http://www.unicode.org/versions/Unicode7.0.0/ch03.pdf#G2630.
            buffer._SetByte(writerIndex++, (byte)(0xf0 | (codePoint >> 18)));
            buffer._SetByte(writerIndex++, (byte)(0x80 | ((codePoint >> 12) & 0x3f)));
            buffer._SetByte(writerIndex++, (byte)(0x80 | ((codePoint >> 6) & 0x3f)));
            buffer._SetByte(writerIndex++, (byte)(0x80 | (codePoint & 0x3f)));
            return writerIndex;
        }

        // Fast-Path implementation
        internal static int WriteAscii(AbstractByteBuffer buffer, int writerIndex, ICharSequence seq)
        {
            if (seq is IHasAsciiSpan hasAscii)
            {
                buffer.SetBytes(writerIndex, hasAscii.AsciiSpan);
                return seq.Count;
            }
            if (seq is IHasUtf16Span hasUtf16)
            {
                return WriteAscii0(buffer, writerIndex, hasUtf16.Utf16Span);
            }

            return WriteAscii0(buffer, writerIndex, seq);
        }

        internal static int WriteAscii(AbstractByteBuffer buffer, int writerIndex, string value)
        {
            return WriteAscii0(buffer, writerIndex, value.AsSpan());
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int WriteAscii0(AbstractByteBuffer buffer, int writerIndex, ICharSequence seq)
        {
            var len = seq.Count;
            // We can use the _set methods as these not need to do any index checks and reference checks.
            // This is possible as we called ensureWritable(...) before.
            for (int i = 0; i < len; i++)
            {
                buffer._SetByte(writerIndex++, AsciiString.CharToByte(seq[i]));
            }
            return len;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteAscii0(AbstractByteBuffer buffer, int writerIndex, ReadOnlySpan<char> utf16Source)
        {
            var charCount = utf16Source.Length;
            if (buffer.IsSingleIoBuffer)
            {
                var asciiDestination = buffer.GetSpan(writerIndex, buffer.Capacity - writerIndex);
                WriteAscii0(utf16Source, asciiDestination, charCount);
            }
            else
            {
                WriteAsciiComposite(buffer, writerIndex, utf16Source, charCount);
            }
            return charCount;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteAsciiComposite(AbstractByteBuffer buffer, int writerIndex, ReadOnlySpan<char> utf16Source, int length)
        {
            var memory = BufferManager.Shared.Rent(length);
            try
            {
                WriteAscii0(utf16Source, memory.AsSpan(), length);
                buffer.SetBytes(writerIndex, memory, 0, length);
            }
            finally
            {
                BufferManager.Shared.Return(memory);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteAscii0(ReadOnlySpan<char> utf16Source, Span<byte> asciiDestination, int length)
        {
#if NETCOREAPP
            System.Text.Encoding.ASCII.GetBytes(utf16Source, asciiDestination);
#else
            unsafe
            {
                fixed (char* chars = &MemoryMarshal.GetReference(utf16Source))
                {
                    fixed (byte* bytes = &MemoryMarshal.GetReference(asciiDestination))
                    {
                        System.Text.Encoding.ASCII.GetBytes(chars, length, bytes, length);
                    }
                }
            }
#endif
        }

        internal static IByteBuffer EncodeString0(IByteBufferAllocator alloc, bool enforceHeap, string src, Encoding encoding, int extraCapacity)
        {
            int length = encoding.GetMaxByteCount(src.Length) + extraCapacity;
            bool release = true;

            IByteBuffer dst = enforceHeap ? alloc.HeapBuffer(length) : alloc.Buffer(length);
            Debug.Assert(dst.HasArray, "Operation expects allocator to operate array-based buffers.");

            try
            {
#if NETCOREAPP
                int written = encoding.GetBytes(src.AsSpan(), dst.FreeSpan);
#else
                int written = encoding.GetBytes(src, 0, src.Length, dst.Array, dst.ArrayOffset + dst.WriterIndex);
#endif
                dst.SetWriterIndex(dst.WriterIndex + written);
                release = false;

                return dst;
            }
            finally
            {
                if (release)
                {
                    dst.Release();
                }
            }
        }

        public static string DecodeString(IByteBuffer src, int readerIndex, int len, Encoding encoding)
        {
            if (0u >= (uint)len) { return string.Empty; }

#if NET451
            if (src.IsSingleIoBuffer)
            {
                ArraySegment<byte> ioBuf = src.GetIoBuffer(readerIndex, len);
                return encoding.GetString(ioBuf.Array, ioBuf.Offset, ioBuf.Count);
            }
            else
            {
                int maxLength = encoding.GetMaxCharCount(len);
                IByteBuffer buffer = src.Allocator.HeapBuffer(maxLength);
                try
                {
                    buffer.WriteBytes(src, readerIndex, len);
                    ArraySegment<byte> ioBuf = buffer.GetIoBuffer();
                    return encoding.GetString(ioBuf.Array, ioBuf.Offset, ioBuf.Count);
                }
                finally
                {
                    // Release the temporary buffer again.
                    buffer.Release();
                }
            }
#else
            var source = src.GetReadableSpan(readerIndex, len);
#if NETCOREAPP
            return encoding.GetString(source);
#else
            unsafe
            {
                fixed (byte* bytes = &MemoryMarshal.GetReference(source))
                {
                    return encoding.GetString(bytes, source.Length);
                }
            }
#endif
#endif
        }
    }
}

#endif