// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Text;
    using CuteAnt.Pool;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    public static partial class ByteBufferUtil
    {
        const int IndexNotFound = -1;
        const char WriteUtfUnknown = '?';
        static readonly int MaxBytesPerCharUtf8 = Encoding.UTF8.GetMaxByteCount(1);

        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance(typeof(ByteBufferUtil));

        public static readonly IByteBufferAllocator DefaultAllocator;

        static ByteBufferUtil()
        {
            string allocType = SystemPropertyUtil.Get("io.netty.allocator.type", "pooled");
            allocType = allocType.Trim();

            IByteBufferAllocator alloc;
            if ("unpooled".Equals(allocType, StringComparison.OrdinalIgnoreCase))
            {
                alloc = UnpooledByteBufferAllocator.Default;
                Logger.Debug("-Dio.netty.allocator.type: {}", allocType);
            }
            else if ("pooled".Equals(allocType, StringComparison.OrdinalIgnoreCase))
            {
                alloc = PooledByteBufferAllocator.Default;
                Logger.Debug("-Dio.netty.allocator.type: {}", allocType);
            }
            else if ("arraypooled".Equals(allocType, StringComparison.OrdinalIgnoreCase))
            {
                alloc = ArrayPooledByteBufferAllocator.Default;
                Logger.Debug("-Dio.netty.allocator.type: {}", allocType);
            }
            else
            {
                alloc = PooledByteBufferAllocator.Default;
                Logger.Debug("-Dio.netty.allocator.type: pooled (unknown: {})", allocType);
            }

            DefaultAllocator = alloc;
        }

        /// <summary>
        ///     Returns a <a href="http://en.wikipedia.org/wiki/Hex_dump">hex dump</a>
        ///     of the specified buffer's sub-region.
        /// </summary>
        public static string HexDump(IByteBuffer buffer) => HexDump(buffer, buffer.ReaderIndex, buffer.ReadableBytes);

        /// <summary>
        ///     Returns a <a href="http://en.wikipedia.org/wiki/Hex_dump">hex dump</a>
        ///     of the specified buffer's sub-region.
        /// </summary>
        public static string HexDump(IByteBuffer buffer, int fromIndex, int length) => HexUtil.DoHexDump(buffer, fromIndex, length);

        /// <summary>
        ///     Returns a <a href="http://en.wikipedia.org/wiki/Hex_dump">hex dump</a>
        ///     of the specified buffer's sub-region.
        /// </summary>
        public static string HexDump(byte[] array) => HexDump(array, 0, array.Length);

        /// <summary>
        ///     Returns a <a href="http://en.wikipedia.org/wiki/Hex_dump">hex dump</a>
        ///     of the specified buffer's sub-region.
        /// </summary>
        public static string HexDump(byte[] array, int fromIndex, int length) => HexUtil.DoHexDump(array, fromIndex, length);

        public static bool EnsureWritableSuccess(int ensureWritableResult)
        {
            var nresult = (uint)ensureWritableResult;
            return 0u >= nresult || 2u == nresult;
        }

        /// <summary>
        ///     Calculates the hash code of the specified buffer.  This method is
        ///     useful when implementing a new buffer type.
        /// </summary>
        public static int HashCode(IByteBuffer buffer)
        {
            int aLen = buffer.ReadableBytes;
            int intCount = aLen.RightUShift(2);
            int byteCount = aLen & 3;

            int hashCode = EmptyByteBuffer.EmptyByteBufferHashCode;
            int arrayIndex = buffer.ReaderIndex;
            for (int i = intCount; i > 0; i--)
            {
                hashCode = 31 * hashCode + buffer.GetInt(arrayIndex);
                arrayIndex += 4;
            }

            for (int i = byteCount; i > 0; i--)
            {
                hashCode = 31 * hashCode + buffer.GetByte(arrayIndex++);
            }

            if (0u >= (uint)hashCode)
            {
                hashCode = 1;
            }

            return hashCode;
        }

        /// <summary>
        ///     Read the given amount of bytes into a new <see cref="IByteBuffer"/> that is allocated from the <see cref="IByteBufferAllocator"/>.
        /// </summary>
        public static IByteBuffer ReadBytes(IByteBufferAllocator alloc, IByteBuffer buffer, int length)
        {
            bool release = true;
            IByteBuffer dst = alloc.Buffer(length);
            try
            {
                buffer.ReadBytes(dst);
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

        public static IByteBuffer WriteUtf8(IByteBufferAllocator alloc, ICharSequence seq)
        {
            // UTF-8 uses max. 3 bytes per char, so calculate the worst case.
            var maxByteCount = Utf8MaxBytes(seq);
            IByteBuffer buf = alloc.Buffer(maxByteCount);
            ReserveAndWriteUtf8(buf, seq, maxByteCount);
            return buf;
        }

        public static int WriteUtf8(IByteBuffer buf, ICharSequence seq) => ReserveAndWriteUtf8(buf, seq, Utf8MaxBytes(seq));

        public static int ReserveAndWriteUtf8(IByteBuffer buf, ICharSequence seq, int reserveBytes)
        {
            while (true)
            {
                switch (buf)
                {
                    case WrappedCompositeByteBuffer _:
                        // WrappedCompositeByteBuf is a sub-class of AbstractByteBuf so it needs special handling.
                        buf = buf.Unwrap();
                        break;

                    case AbstractByteBuffer byteBuf:
                        byteBuf.EnsureWritable0(reserveBytes);
                        int written = WriteUtf8(byteBuf, byteBuf.WriterIndex, seq);
                        byteBuf.SetWriterIndex(byteBuf.WriterIndex + written);
                        return written;

                    case WrappedByteBuffer _:
                        // Unwrap as the wrapped buffer may be an AbstractByteBuf and so we can use fast-path.
                        buf = buf.Unwrap();
                        break;

                    default:
                        byte[] bytes = Encoding.UTF8.GetBytes(seq.ToString());
                        buf.WriteBytes(bytes);
                        return bytes.Length;
                }
            }
        }

        public static IByteBuffer WriteUtf8(IByteBufferAllocator alloc, string value)
        {
            // UTF-8 uses max. 3 bytes per char, so calculate the worst case.
            var maxByteCount = Utf8MaxBytes(value);
            IByteBuffer buf = alloc.Buffer(maxByteCount);
            ReserveAndWriteUtf8(buf, value, maxByteCount);
            return buf;
        }

        public static int WriteUtf8(IByteBuffer buf, string seq) => ReserveAndWriteUtf8(buf, seq, Utf8MaxBytes(seq));

        ///<summary>
        /// Encode a string in http://en.wikipedia.org/wiki/UTF-8 and write it into reserveBytes of 
        /// a byte buffer. The reserveBytes must be computed (ie eagerly using {@link #utf8MaxBytes(string)}
        /// or exactly with #utf8Bytes(string)}) to ensure this method not to not: for performance reasons
        /// the index checks will be performed using just reserveBytes.
        /// </summary>
        /// <returns> This method returns the actual number of bytes written.</returns>
        public static int ReserveAndWriteUtf8(IByteBuffer buf, string value, int reserveBytes)
        {
            while (true)
            {
                switch (buf)
                {
                    case WrappedCompositeByteBuffer _:
                        // WrappedCompositeByteBuf is a sub-class of AbstractByteBuf so it needs special handling.
                        buf = buf.Unwrap();
                        break;

                    case AbstractByteBuffer byteBuf:
                        byteBuf.EnsureWritable0(reserveBytes);
                        int written = WriteUtf8(byteBuf, byteBuf.WriterIndex, value);
                        byteBuf.SetWriterIndex(byteBuf.WriterIndex + written);
                        return written;

                    case WrappedByteBuffer _:
                        // Unwrap as the wrapped buffer may be an AbstractByteBuf and so we can use fast-path.
                        buf = buf.Unwrap();
                        break;

                    default:
                        byte[] bytes = Encoding.UTF8.GetBytes(value);
                        buf.WriteBytes(bytes);
                        return bytes.Length;
                }
            }
        }

        public static int Utf8MaxBytes(ICharSequence seq) => seq.Count * MaxBytesPerCharUtf8;

        public static int Utf8MaxBytes(string seq) => seq.Length * MaxBytesPerCharUtf8;

        public static int Utf8MaxBytes(int seqLength) => seqLength * MaxBytesPerCharUtf8;

        public static int Utf8Bytes(string seq)
        {
            int seqLength = seq.Length;
            int i = 0;
            // ASCII fast path
            while (i < seqLength && seq[i] < 0x80)
            {
                ++i;
            }
            // !ASCII is packed in a separate method to let the ASCII case be smaller
            return i < seqLength ? i + Utf8Bytes(seq, i, seqLength) : i;
        }

        static int Utf8Bytes(string seq, int start, int length)
        {
            int encodedLength = 0;
            for (int i = start; i < length; i++)
            {
                char c = seq[i];
                // making it 100% branchless isn't rewarding due to the many bit operations necessary!
                if (c < 0x800)
                {
                    // branchless version of: (c <= 127 ? 0:1) + 1
                    encodedLength += ((0x7f - c).RightUShift(31)) + 1;
                }
                else if (char.IsSurrogate(c))
                {
                    if (!char.IsHighSurrogate(c))
                    {
                        encodedLength++;
                        // WRITE_UTF_UNKNOWN
                        continue;
                    }
                    char c2;
                    try
                    {
                        // Surrogate Pair consumes 2 characters. Optimistically try to get the next character to avoid
                        // duplicate bounds checking with charAt.
                        c2 = seq[++i];
                    }
                    catch (IndexOutOfRangeException)
                    {
                        encodedLength++;
                        // WRITE_UTF_UNKNOWN
                        break;
                    }
                    if (!char.IsLowSurrogate(c2))
                    {
                        // WRITE_UTF_UNKNOWN + (Character.isHighSurrogate(c2) ? WRITE_UTF_UNKNOWN : c2)
                        encodedLength += 2;
                        continue;
                    }
                    // See http://www.unicode.org/versions/Unicode7.0.0/ch03.pdf#G2630.
                    encodedLength += 4;
                }
                else
                {
                    encodedLength += 3;
                }
            }
            return encodedLength;
        }

        public static IByteBuffer WriteAscii(IByteBufferAllocator alloc, ICharSequence seq)
        {
            // ASCII uses 1 byte per char
            IByteBuffer buf = alloc.Buffer(seq.Count);
            WriteAscii(buf, seq);
            return buf;
        }

        public static int WriteAscii(IByteBuffer buf, ICharSequence seq)
        {
            // ASCII uses 1 byte per char
            int len = seq.Count;
            if (seq is AsciiString asciiString)
            {
                buf.WriteBytes(asciiString.Array, asciiString.Offset, len);
            }
            else
            {
                while (true)
                {
                    switch (buf)
                    {
                        case WrappedCompositeByteBuffer _:
                            // WrappedCompositeByteBuf is a sub-class of AbstractByteBuf so it needs special handling.
                            buf = buf.Unwrap();
                            break;

                        case AbstractByteBuffer byteBuf:
                            byteBuf.EnsureWritable0(len);
                            int written = WriteAscii(byteBuf, byteBuf.WriterIndex, seq);
                            byteBuf.SetWriterIndex(byteBuf.WriterIndex + written);
                            return written;

                        case WrappedByteBuffer _:
                            // Unwrap as the wrapped buffer may be an AbstractByteBuf and so we can use fast-path.
                            buf = buf.Unwrap();
                            break;

                        default:
                            byte[] bytes = Encoding.ASCII.GetBytes(seq.ToString());
                            buf.WriteBytes(bytes);
                            return bytes.Length;
                    }
                }
            }
            return len;
        }

        public static IByteBuffer WriteAscii(IByteBufferAllocator alloc, string value)
        {
            // ASCII uses 1 byte per char
            IByteBuffer buf = alloc.Buffer(value.Length);
            WriteAscii(buf, value);
            return buf;
        }

        public static int WriteAscii(IByteBuffer buf, string value)
        {
            // ASCII uses 1 byte per char
            int len = value.Length;
            while (true)
            {
                switch (buf)
                {
                    case WrappedCompositeByteBuffer _:
                        // WrappedCompositeByteBuf is a sub-class of AbstractByteBuf so it needs special handling.
                        buf = buf.Unwrap();
                        break;

                    case AbstractByteBuffer byteBuf:
                        byteBuf.EnsureWritable0(len);
                        int written = WriteAscii(byteBuf, byteBuf.WriterIndex, value);
                        byteBuf.SetWriterIndex(byteBuf.WriterIndex + written);
                        return written;

                    case WrappedByteBuffer _:
                        // Unwrap as the wrapped buffer may be an AbstractByteBuf and so we can use fast-path.
                        buf = buf.Unwrap();
                        break;

                    default:
                        byte[] bytes = Encoding.ASCII.GetBytes(value);
                        buf.WriteBytes(bytes);
                        return bytes.Length;
                }
            }
        }

        /// <summary>
        ///     Encode the given <see cref="string" /> using the given <see cref="Encoding" /> into a new
        ///     <see cref="IByteBuffer" /> which
        ///     is allocated via the <see cref="IByteBufferAllocator" />.
        /// </summary>
        /// <param name="alloc">The <see cref="IByteBufferAllocator" /> to allocate {@link IByteBuffer}.</param>
        /// <param name="src">src The <see cref="string" /> to encode.</param>
        /// <param name="encoding">charset The specified <see cref="Encoding" /></param>
        public static IByteBuffer EncodeString(IByteBufferAllocator alloc, string src, Encoding encoding) => EncodeString0(alloc, false, src, encoding, 0);

        /// <summary>
        ///     Encode the given <see cref="string" /> using the given <see cref="Encoding" /> into a new
        ///     <see cref="IByteBuffer" /> which
        ///     is allocated via the <see cref="IByteBufferAllocator" />.
        /// </summary>
        /// <param name="alloc">The <see cref="IByteBufferAllocator" /> to allocate {@link IByteBuffer}.</param>
        /// <param name="src">src The <see cref="string" /> to encode.</param>
        /// <param name="encoding">charset The specified <see cref="Encoding" /></param>
        /// <param name="extraCapacity">the extra capacity to alloc except the space for decoding.</param>
        public static IByteBuffer EncodeString(IByteBufferAllocator alloc, string src, Encoding encoding, int extraCapacity) => EncodeString0(alloc, false, src, encoding, extraCapacity);

        /// <summary>
        /// Create a copy of the underlying storage from <paramref name="buf"/> into a byte array.
        /// The copy will start at <see cref="IByteBuffer.ReaderIndex"/> and copy <see cref="IByteBuffer.ReadableBytes"/> bytes.
        /// </summary>
        /// <param name="buf"></param>
        /// <returns></returns>
        public static byte[] GetBytes(IByteBuffer buf)
        {
            return GetBytes(buf, buf.ReaderIndex, buf.ReadableBytes, true);
        }

        /// <summary>
        /// Create a copy of the underlying storage from <paramref name="buf"/> into a byte array.
        /// The copy will start at <paramref name="start"/> and copy <paramref name="length"/> bytes.
        /// </summary>
        /// <param name="buf"></param>
        /// <param name="start"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static byte[] GetBytes(IByteBuffer buf, int start, int length)
        {
            return GetBytes(buf, start, length, true);
        }

        /// <summary>
        /// Return an array of the underlying storage from <paramref name="buf"/> into a byte array.
        /// The copy will start at {@code start} and copy {@code length} bytes.
        /// If <paramref name="copy"/> is true a copy will be made of the memory.
        /// If <paramref name="copy"/> is false the underlying storage will be shared, if possible.
        /// </summary>
        /// <param name="buf"></param>
        /// <param name="start"></param>
        /// <param name="length"></param>
        /// <param name="copy"></param>
        /// <returns></returns>
        public static byte[] GetBytes(IByteBuffer buf, int start, int length, bool copy)
        {
            var capacity = buf.Capacity;
            if (MathUtil.IsOutOfBounds(start, length, capacity))
            {
                ThrowHelper.ThrowIndexOutOfRangeException_Expected(start, length, capacity);
            }

            if (buf.HasArray)
            {
                if (copy || start != 0 || length != capacity)
                {
                    int baseOffset = buf.ArrayOffset + start;
                    var bytes = new byte[length];
                    PlatformDependent.CopyMemory(buf.Array, baseOffset, bytes, 0, length);
                    return bytes;
                }
                else
                {
                    return buf.Array;
                }
            }

            byte[] v = new byte[length];
            buf.GetBytes(start, v);
            return v;
        }

        public static void Copy(AsciiString src, IByteBuffer dst) => Copy(src, 0, dst, src.Count);

        [MethodImpl(InlineMethod.AggressiveInlining)]
        public static void Copy(AsciiString src, int srcIdx, IByteBuffer dst, int dstIdx, int length)
        {
            if (MathUtil.IsOutOfBounds(srcIdx, length, src.Count))
            {
                ThrowHelper.ThrowIndexOutOfRangeException_Src(srcIdx, length, src.Count);
            }
            if (dst is null)
            {
                ThrowHelper.ThrowArgumentNullException_Dst();
            }
            // ReSharper disable once PossibleNullReferenceException
            dst.SetBytes(dstIdx, src.Array, srcIdx + src.Offset, length);
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        public static void Copy(AsciiString src, int srcIdx, IByteBuffer dst, int length)
        {
            if (MathUtil.IsOutOfBounds(srcIdx, length, src.Count))
            {
                ThrowHelper.ThrowIndexOutOfRangeException_Src(srcIdx, length, src.Count);
            }
            if (dst is null)
            {
                ThrowHelper.ThrowArgumentNullException_Dst();
            }
            // ReSharper disable once PossibleNullReferenceException
            dst.WriteBytes(src.Array, srcIdx + src.Offset, length);
        }

        /// <summary>
        ///     Returns a multi-line hexadecimal dump of the specified {@link ByteBuf} that is easy to read by humans.
        /// </summary>
        public static string PrettyHexDump(IByteBuffer buffer) => PrettyHexDump(buffer, buffer.ReaderIndex, buffer.ReadableBytes);

        /// <summary>
        ///     Returns a multi-line hexadecimal dump of the specified {@link ByteBuf} that is easy to read by humans,
        ///     starting at the given {@code offset} using the given {@code length}.
        /// </summary>
        public static string PrettyHexDump(IByteBuffer buffer, int offset, int length) => HexUtil.DoPrettyHexDump(buffer, offset, length);

        /// <summary>
        ///     Appends the prettified multi-line hexadecimal dump of the specified {@link ByteBuf} to the specified
        ///     {@link StringBuilder} that is easy to read by humans.
        /// </summary>
        public static void AppendPrettyHexDump(StringBuilder dump, IByteBuffer buf) => AppendPrettyHexDump(dump, buf, buf.ReaderIndex, buf.ReadableBytes);

        /// <summary>
        ///     Appends the prettified multi-line hexadecimal dump of the specified {@link ByteBuf} to the specified
        ///     {@link StringBuilder} that is easy to read by humans, starting at the given {@code offset} using
        ///     the given {@code length}.
        /// </summary>
        public static void AppendPrettyHexDump(StringBuilder dump, IByteBuffer buf, int offset, int length) => HexUtil.DoAppendPrettyHexDump(dump, buf, offset, length);

        static class HexUtil
        {
            static readonly char[] HexdumpTable = new char[256 * 4];
            static readonly string Newline = StringUtil.Newline;
            static readonly string[] Byte2Hex = new string[256];
            static readonly string[] HexPadding = new string[16];
            static readonly string[] BytePadding = new string[16];
            static readonly char[] Byte2Char = new char[256];
            static readonly string[] HexDumpRowPrefixes = new string[65536.RightUShift(4)];

            static HexUtil()
            {
                char[] digits = "0123456789abcdef".ToCharArray();
                for (int i = 0; i < 256; i++)
                {
                    HexdumpTable[i << 1] = digits[i.RightUShift(4) & 0x0F];
                    HexdumpTable[(i << 1) + 1] = digits[i & 0x0F];
                }

                // Generate the lookup table for byte-to-hex-dump conversion
                for (int i = 0; i < Byte2Hex.Length; i++)
                {
                    Byte2Hex[i] = ' ' + StringUtil.ByteToHexStringPadded(i);
                }

                // Generate the lookup table for hex dump paddings
                for (int i = 0; i < HexPadding.Length; i++)
                {
                    int padding = HexPadding.Length - i;
                    var buf = StringBuilderManager.Allocate(padding * 3);
                    for (int j = 0; j < padding; j++)
                    {
                        buf.Append("   ");
                    }
                    HexPadding[i] = StringBuilderManager.ReturnAndFree(buf);
                }

                // Generate the lookup table for byte dump paddings
                for (int i = 0; i < BytePadding.Length; i++)
                {
                    int padding = BytePadding.Length - i;
                    var buf = StringBuilderManager.Allocate(padding);
                    for (int j = 0; j < padding; j++)
                    {
                        buf.Append(' ');
                    }
                    BytePadding[i] = StringBuilderManager.ReturnAndFree(buf);
                }

                // Generate the lookup table for byte-to-char conversion
                for (int i = 0; i < Byte2Char.Length; i++)
                {
                    if (i <= 0x1f || i >= 0x7f)
                    {
                        Byte2Char[i] = '.';
                    }
                    else
                    {
                        Byte2Char[i] = (char)i;
                    }
                }

                // Generate the lookup table for the start-offset header in each row (up to 64KiB).
                for (int i = 0; i < HexDumpRowPrefixes.Length; i++)
                {
                    var buf = StringBuilderManager.Allocate(); // new StringBuilder(12);
                    buf.Append(Environment.NewLine);
                    buf.Append((i << 4 & 0xFFFFFFFFL | 0x100000000L).ToString("X2"));
                    buf.Insert(buf.Length - 9, '|');
                    buf.Append('|');
                    HexDumpRowPrefixes[i] = StringBuilderManager.ReturnAndFree(buf);
                }
            }

            public static string DoHexDump(IByteBuffer buffer, int fromIndex, int length)
            {
                if (length < 0) { ThrowHelper.ThrowArgumentException_PositiveOrZero(length, ExceptionArgument.length); }
                if (0u >= (uint)length)
                {
                    return "";
                }
                int endIndex = fromIndex + length;
                var buf = new char[length << 1];

                int srcIdx = fromIndex;
                int dstIdx = 0;
                for (; srcIdx < endIndex; srcIdx++, dstIdx += 2)
                {
                    Array.Copy(
                        HexdumpTable, buffer.GetByte(srcIdx) << 1,
                        buf, dstIdx, 2);
                }

                return new string(buf);
            }

            public static string DoHexDump(byte[] array, int fromIndex, int length)
            {
                if (length < 0) { ThrowHelper.ThrowArgumentException_PositiveOrZero(length, ExceptionArgument.length); }

                if (0u >= (uint)length)
                {
                    return "";
                }

                int endIndex = fromIndex + length;
                var buf = new char[length << 1];

                int srcIdx = fromIndex;
                int dstIdx = 0;
                for (; srcIdx < endIndex; srcIdx++, dstIdx += 2)
                {
                    Array.Copy(HexdumpTable, (array[srcIdx] & 0xFF) << 1, buf, dstIdx, 2);
                }

                return new string(buf);
            }

            public static string DoPrettyHexDump(IByteBuffer buffer, int offset, int length)
            {
                if (0u >= (uint)length)
                {
                    return string.Empty;
                }
                else
                {
                    int rows = length / 16 + (0u >= (uint)(length % 15) ? 0 : 1) + 4;
                    var buf = StringBuilderManager.Allocate(rows * 80);
                    AppendPrettyHexDump(buf, buffer, offset, length);
                    return StringBuilderManager.ReturnAndFree(buf);
                }
            }

            public static void DoAppendPrettyHexDump(StringBuilder dump, IByteBuffer buf, int offset, int length)
            {
                if (MathUtil.IsOutOfBounds(offset, length, buf.Capacity))
                {
                    ThrowHelper.ThrowIndexOutOfRangeException_Expected(offset, length, buf.Capacity);
                }
                if (0u >= (uint)length)
                {
                    return;
                }
                dump.Append(
                    "         +-------------------------------------------------+" +
                    Newline + "         |  0  1  2  3  4  5  6  7  8  9  a  b  c  d  e  f |" +
                    Newline + "+--------+-------------------------------------------------+----------------+");

                int startIndex = offset;
                int fullRows = length.RightUShift(4);
                int remainder = length & 0xF;

                // Dump the rows which have 16 bytes.
                for (int row = 0; row < fullRows; row++)
                {
                    int rowStartIndex = (row << 4) + startIndex;

                    // Per-row prefix.
                    AppendHexDumpRowPrefix(dump, row, rowStartIndex);

                    // Hex dump
                    int rowEndIndex = rowStartIndex + 16;
                    for (int j = rowStartIndex; j < rowEndIndex; j++)
                    {
                        dump.Append(Byte2Hex[buf.GetByte(j)]);
                    }
                    dump.Append(" |");

                    // ASCII dump
                    for (int j = rowStartIndex; j < rowEndIndex; j++)
                    {
                        dump.Append(Byte2Char[buf.GetByte(j)]);
                    }
                    dump.Append('|');
                }

                // Dump the last row which has less than 16 bytes.
                if (remainder != 0)
                {
                    int rowStartIndex = (fullRows << 4) + startIndex;
                    AppendHexDumpRowPrefix(dump, fullRows, rowStartIndex);

                    // Hex dump
                    int rowEndIndex = rowStartIndex + remainder;
                    for (int j = rowStartIndex; j < rowEndIndex; j++)
                    {
                        dump.Append(Byte2Hex[buf.GetByte(j)]);
                    }
                    dump.Append(HexPadding[remainder]);
                    dump.Append(" |");

                    // Ascii dump
                    for (int j = rowStartIndex; j < rowEndIndex; j++)
                    {
                        dump.Append(Byte2Char[buf.GetByte(j)]);
                    }
                    dump.Append(BytePadding[remainder]);
                    dump.Append('|');
                }

                dump.Append(Newline + "+--------+-------------------------------------------------+----------------+");
            }

            static void AppendHexDumpRowPrefix(StringBuilder dump, int row, int rowStartIndex)
            {
                if (row < HexDumpRowPrefixes.Length)
                {
                    dump.Append(HexDumpRowPrefixes[row]);
                }
                else
                {
                    dump.Append(Environment.NewLine);
                    dump.Append((rowStartIndex & 0xFFFFFFFFL | 0x100000000L).ToString("X2"));
                    dump.Insert(dump.Length - 9, '|');
                    dump.Append('|');
                }
            }
        }

        public static bool IsText(IByteBuffer buf, Encoding charset) => IsText(buf, buf.ReaderIndex, buf.ReadableBytes, charset);

        public static bool IsText(IByteBuffer buf, int index, int length, Encoding encoding)
        {
            if (buf is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buf); }
            if (encoding is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.encoding); }

            int maxIndex = buf.ReaderIndex + buf.ReadableBytes;
            if (index < 0 || length < 0 || index > maxIndex - length)
            {
                ThrowHelper.ThrowIndexOutOfRangeException_IsText(index, length);
            }
            switch (encoding.CodePage)
            {
                case SharedConstants.UTF8CodePage:
                    return IsUtf8(buf, index, length);

                case SharedConstants.ASCIICodePage:
                    return IsAscii(buf, index, length);

                default:
                    try
                    {
                        if (buf.IsSingleIoBuffer)
                        {
                            ArraySegment<byte> segment = buf.GetIoBuffer();
                            encoding.GetChars(segment.Array, segment.Offset, segment.Count);
                        }
                        else
                        {
                            IByteBuffer heapBuffer = buf.Allocator.HeapBuffer(length);
                            try
                            {
                                heapBuffer.WriteBytes(buf, index, length);
                                ArraySegment<byte> segment = heapBuffer.GetIoBuffer();
                                encoding.GetChars(segment.Array, segment.Offset, segment.Count);
                            }
                            finally
                            {
                                heapBuffer.Release();
                            }
                        }
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
            }
        }

        static readonly FindNonAscii AsciiByteProcessor = new FindNonAscii();

        sealed class FindNonAscii : IByteProcessor
        {
            public bool Process(byte value) => value < 0x80;
        }

        static bool IsAscii(IByteBuffer buf, int index, int length) => buf.ForEachByte(index, length, AsciiByteProcessor) == -1;

        static bool IsUtf8(IByteBuffer buf, int index, int length)
        {
            int endIndex = index + length;
            while (index < endIndex)
            {
                byte b1 = buf.GetByte(index++);
                byte b2, b3;
                if (0u >= (uint)(b1 & 0x80))
                {
                    // 1 byte
                    continue;
                }
                if ((b1 & 0xE0) == 0xC0)
                {
                    // 2 bytes
                    //
                    // Bit/Byte pattern
                    // 110xxxxx    10xxxxxx
                    // C2..DF      80..BF
                    if (index >= endIndex)
                    { // no enough bytes
                        return false;
                    }
                    b2 = buf.GetByte(index++);
                    if ((b2 & 0xC0) != 0x80)
                    { // 2nd byte not starts with 10
                        return false;
                    }
                    if ((b1 & 0xFF) < 0xC2)
                    { // out of lower bound
                        return false;
                    }
                }
                else if ((b1 & 0xF0) == 0xE0)
                {
                    // 3 bytes
                    //
                    // Bit/Byte pattern
                    // 1110xxxx    10xxxxxx    10xxxxxx
                    // E0          A0..BF      80..BF
                    // E1..EC      80..BF      80..BF
                    // ED          80..9F      80..BF
                    // E1..EF      80..BF      80..BF
                    if (index > endIndex - 2)
                    { // no enough bytes
                        return false;
                    }
                    b2 = buf.GetByte(index++);
                    b3 = buf.GetByte(index++);
                    if ((b2 & 0xC0) != 0x80 || (b3 & 0xC0) != 0x80)
                    { // 2nd or 3rd bytes not start with 10
                        return false;
                    }
                    if ((b1 & 0x0F) == 0x00 && (b2 & 0xFF) < 0xA0)
                    { // out of lower bound
                        return false;
                    }
                    if ((b1 & 0x0F) == 0x0D && (b2 & 0xFF) > 0x9F)
                    { // out of upper bound
                        return false;
                    }
                }
                else if ((b1 & 0xF8) == 0xF0)
                {
                    // 4 bytes
                    //
                    // Bit/Byte pattern
                    // 11110xxx    10xxxxxx    10xxxxxx    10xxxxxx
                    // F0          90..BF      80..BF      80..BF
                    // F1..F3      80..BF      80..BF      80..BF
                    // F4          80..8F      80..BF      80..BF
                    if (index > endIndex - 3)
                    { // no enough bytes
                        return false;
                    }
                    b2 = buf.GetByte(index++);
                    b3 = buf.GetByte(index++);
                    byte b4 = buf.GetByte(index++);
                    if ((b2 & 0xC0) != 0x80 || (b3 & 0xC0) != 0x80 || (b4 & 0xC0) != 0x80)
                    {
                        // 2nd, 3rd or 4th bytes not start with 10
                        return false;
                    }
                    if ((b1 & 0xFF) > 0xF4 // b1 invalid
                        || (b1 & 0xFF) == 0xF0 && (b2 & 0xFF) < 0x90    // b2 out of lower bound
                        || (b1 & 0xFF) == 0xF4 && (b2 & 0xFF) > 0x8F)
                    { // b2 out of upper bound
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        public static unsafe int SingleToInt32Bits(float value)
        {
            return *(int*)(&value);
        }

        public static unsafe float Int32BitsToSingle(int value)
        {
            return *(float*)(&value);
        }

        /// <summary>
        ///     Toggles the endianness of the specified 64-bit long integer.
        /// </summary>
        public static long SwapLong(long value)
            => ((SwapInt((int)value) & 0xFFFFFFFF) << 32)
                | (SwapInt((int)(value >> 32)) & 0xFFFFFFFF);

        /// <summary>
        ///     Toggles the endianness of the specified 32-bit integer.
        /// </summary>
        public static int SwapInt(int value)
            => ((SwapShort((short)value) & 0xFFFF) << 16)
                | (SwapShort((short)(value >> 16)) & 0xFFFF);

        /// <summary>
        ///     Toggles the endianness of the specified 16-bit integer.
        /// </summary>
        public static short SwapShort(short value) => (short)(((value & 0xFF) << 8) | (value >> 8) & 0xFF);

    }
}
