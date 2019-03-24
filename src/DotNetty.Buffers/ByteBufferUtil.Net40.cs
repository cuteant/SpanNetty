// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Diagnostics;
    using System.Text;
    using DotNetty.Common.Utilities;

    partial class ByteBufferUtil
    {
        /// <summary>
        ///     Returns the reader index of needle in haystack, or -1 if needle is not in haystack.
        /// </summary>
        public static int IndexOf(IByteBuffer needle, IByteBuffer haystack)
        {
            // TODO: maybe use Boyer Moore for efficiency.
            int attempts = haystack.ReadableBytes - needle.ReadableBytes + 1;
            for (int i = 0; i < attempts; i++)
            {
                if (Equals(needle, needle.ReaderIndex, haystack, haystack.ReaderIndex + i, needle.ReadableBytes))
                {
                    return haystack.ReaderIndex + i;
                }
            }

            return -1;
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

            int longCount = unchecked((int)((uint)length >> 3));
            int byteCount = length & 7;

            for (int i = longCount; i > 0; i--)
            {
                if (a.GetLong(aStartIndex) != b.GetLong(bStartIndex))
                {
                    return false;
                }
                aStartIndex += 8;
                bStartIndex += 8;
            }

            for (int i = byteCount; i > 0; i--)
            {
                if (a.GetByte(aStartIndex) != b.GetByte(bStartIndex))
                {
                    return false;
                }
                aStartIndex++;
                bStartIndex++;
            }

            return true;
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
        public static int Compare(IByteBuffer bufferA, IByteBuffer bufferB)
        {
            int aLen = bufferA.ReadableBytes;
            int bLen = bufferB.ReadableBytes;
            int minLength = Math.Min(aLen, bLen);
            int uintCount = minLength.RightUShift(2);
            int byteCount = minLength & 3;

            int aIndex = bufferA.ReaderIndex;
            int bIndex = bufferB.ReaderIndex;

            if (uintCount > 0)
            {
                int uintCountIncrement = uintCount << 2;
                int res = CompareUint(bufferA, bufferB, aIndex, bIndex, uintCountIncrement);
                if (res != 0)
                {
                    return res;
                }

                aIndex += uintCountIncrement;
                bIndex += uintCountIncrement;
            }

            for (int aEnd = aIndex + byteCount; aIndex < aEnd; ++aIndex, ++bIndex)
            {
                int comp = bufferA.GetByte(aIndex) - bufferB.GetByte(bIndex);
                if (comp != 0)
                {
                    return comp;
                }
            }

            return aLen - bLen;
        }

        static int CompareUint(IByteBuffer bufferA, IByteBuffer bufferB, int aIndex, int bIndex, int uintCountIncrement)
        {
            for (int aEnd = aIndex + uintCountIncrement; aIndex < aEnd; aIndex += 4, bIndex += 4)
            {
                long va = bufferA.GetUnsignedInt(aIndex);
                long vb = bufferB.GetUnsignedInt(bIndex);
                if (va > vb)
                {
                    return 1;
                }
                if (va < vb)
                {
                    return -1;
                }
            }
            return 0;
        }

        /// <summary>
        /// The default implementation of <see cref="IByteBufferExtensions.IndexOf(IByteBuffer, int, int, byte)"/>.
        /// This method is useful when implementing a new buffer type.
        /// </summary>
        public static int IndexOf(IByteBuffer buffer, int fromIndex, int toIndex, byte value)
        {
            if (fromIndex <= toIndex)
            {
                return FirstIndexOf(buffer, fromIndex, toIndex, value);
            }
            else
            {
                return LastIndexOf(buffer, fromIndex, toIndex, value);
            }
        }

        static int FirstIndexOf(IByteBuffer buffer, int fromIndex, int toIndex, byte value)
        {
            fromIndex = Math.Max(fromIndex, 0);
            if (fromIndex >= toIndex || buffer.Capacity == 0)
            {
                return -1;
            }

            return buffer.ForEachByte(fromIndex, toIndex - fromIndex, new IndexOfProcessor(value));
        }

        static int LastIndexOf(IByteBuffer buffer, int fromIndex, int toIndex, byte value)
        {
            int capacity = buffer.Capacity;
            fromIndex = Math.Min(fromIndex, capacity);
            if (fromIndex < 0 || capacity == 0)
            {
                return -1;
            }

            return buffer.ForEachByteDesc(toIndex, fromIndex - toIndex, new IndexOfProcessor(value));
        }

        // Fast-Path implementation
        internal static int WriteUtf8(AbstractByteBuffer buffer, int writerIndex, ICharSequence value)
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
            var len = seq.Count;
            // We can use the _set methods as these not need to do any index checks and reference checks.
            // This is possible as we called ensureWritable(...) before.
            for (int i = 0; i < len; i++)
            {
                buffer._SetByte(writerIndex++, AsciiString.CharToByte(seq[i]));
            }
            return len;
        }

        internal static int WriteAscii(AbstractByteBuffer buffer, int writerIndex, string value)
        {
            var len = value.Length;
            // We can use the _set methods as these not need to do any index checks and reference checks.
            // This is possible as we called ensureWritable(...) before.
            for (int i = 0; i < len; i++)
            {
                buffer._SetByte(writerIndex++, (byte)value[i]);
            }
            return len;
        }

        internal static IByteBuffer EncodeString0(IByteBufferAllocator alloc, bool enforceHeap, string src, Encoding encoding, int extraCapacity)
        {
            int length = encoding.GetMaxByteCount(src.Length) + extraCapacity;
            bool release = true;

            IByteBuffer dst = enforceHeap ? alloc.HeapBuffer(length) : alloc.Buffer(length);
            Debug.Assert(dst.HasArray, "Operation expects allocator to operate array-based buffers.");

            try
            {
                int written = encoding.GetBytes(src, 0, src.Length, dst.Array, dst.ArrayOffset + dst.WriterIndex);
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
            if (len == 0)
            {
                return string.Empty;
            }

            if (src.IoBufferCount == 1)
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
        }
    }
}
#endif