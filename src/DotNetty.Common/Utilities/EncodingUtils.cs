#if !NET40

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Buffers;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    public static class EncodingUtils
    {
        // Encoding Helpers
        const char HighSurrogateStart = '\ud800';
        const char HighSurrogateEnd = '\udbff';
        const char LowSurrogateStart = '\udc00';
        const char LowSurrogateEnd = '\udfff';

        // TODO: Replace this with publicly shipping implementation: https://github.com/dotnet/corefx/issues/34094
        /// <summary>
        /// Converts a span containing a sequence of UTF-16 bytes into UTF-8 bytes.
        ///
        /// This method will consume as many of the input bytes as possible.
        ///
        /// On successful exit, the entire input was consumed and encoded successfully. In this case, <paramref name="bytesConsumed"/> will be
        /// equal to the length of the <paramref name="utf16Source"/> and <paramref name="bytesWritten"/> will equal the total number of bytes written to
        /// the <paramref name="utf8Destination"/>.
        /// </summary>
        /// <param name="utf16Source">A span containing a sequence of UTF-16 bytes.</param>
        /// <param name="utf8Destination">A span to write the UTF-8 bytes into.</param>
        /// <param name="bytesConsumed">On exit, contains the number of bytes that were consumed from the <paramref name="utf16Source"/>.</param>
        /// <param name="bytesWritten">On exit, contains the number of bytes written to <paramref name="utf8Destination"/></param>
        /// <returns>A <see cref="OperationStatus"/> value representing the state of the conversion.</returns>
        public unsafe static OperationStatus ToUtf8(ReadOnlySpan<byte> utf16Source, Span<byte> utf8Destination, out int bytesConsumed, out int bytesWritten)
        {
            fixed (byte* chars = &MemoryMarshal.GetReference(utf16Source))
            fixed (byte* bytes = &MemoryMarshal.GetReference(utf8Destination))
            {
                char* pSrc = (char*)chars;
                byte* pTarget = bytes;

                char* pEnd = (char*)(chars + utf16Source.Length);
                byte* pAllocatedBufferEnd = pTarget + utf8Destination.Length;

                // assume that JIT will enregister pSrc, pTarget and ch

                // Entering the fast encoding loop incurs some overhead that does not get amortized for small
                // number of characters, and the slow encoding loop typically ends up running for the last few
                // characters anyway since the fast encoding loop needs 5 characters on input at least.
                // Thus don't use the fast decoding loop at all if we don't have enough characters. The threashold
                // was choosen based on performance testing.
                // Note that if we don't have enough bytes, pStop will prevent us from entering the fast loop.
                while (pEnd - pSrc > 13)
                {
                    // we need at least 1 byte per character, but Convert might allow us to convert
                    // only part of the input, so try as much as we can.  Reduce charCount if necessary
                    int available = Math.Min(PtrDiff(pEnd, pSrc), PtrDiff(pAllocatedBufferEnd, pTarget));

                    // FASTLOOP:
                    // - optimistic range checks
                    // - fallbacks to the slow loop for all special cases, exception throwing, etc.

                    // To compute the upper bound, assume that all characters are ASCII characters at this point,
                    //  the boundary will be decreased for every non-ASCII character we encounter
                    // Also, we need 5 chars reserve for the unrolled ansi decoding loop and for decoding of surrogates
                    // If there aren't enough bytes for the output, then pStop will be <= pSrc and will bypass the loop.
                    char* pStop = pSrc + available - 5;
                    if (pSrc >= pStop)
                        break;

                    do
                    {
                        int ch = *pSrc;
                        pSrc++;

                        if (ch > 0x7F)
                        {
                            goto LongCode;
                        }
                        *pTarget = (byte)ch;
                        pTarget++;

                        // get pSrc aligned
                        if ((unchecked((int)pSrc) & 0x2) != 0)
                        {
                            ch = *pSrc;
                            pSrc++;
                            if (ch > 0x7F)
                            {
                                goto LongCode;
                            }
                            *pTarget = (byte)ch;
                            pTarget++;
                        }

                        // Run 4 characters at a time!
                        while (pSrc < pStop)
                        {
                            ch = *(int*)pSrc;
                            int chc = *(int*)(pSrc + 2);
                            if (((ch | chc) & unchecked((int)0xFF80FF80)) != 0)
                            {
                                goto LongCodeWithMask;
                            }

                            // Unfortunately, this is endianess sensitive
#if BIGENDIAN
                            *pTarget = (byte)(ch >> 16);
                            *(pTarget + 1) = (byte)ch;
                            pSrc += 4;
                            *(pTarget + 2) = (byte)(chc >> 16);
                            *(pTarget + 3) = (byte)chc;
                            pTarget += 4;
#else // BIGENDIAN
                            *pTarget = (byte)ch;
                            *(pTarget + 1) = (byte)(ch >> 16);
                            pSrc += 4;
                            *(pTarget + 2) = (byte)chc;
                            *(pTarget + 3) = (byte)(chc >> 16);
                            pTarget += 4;
#endif // BIGENDIAN
                        }
                        continue;

                    LongCodeWithMask:
#if BIGENDIAN
                        // be careful about the sign extension
                        ch = (int)(((uint)ch) >> 16);
#else // BIGENDIAN
                        ch = (char)ch;
#endif // BIGENDIAN
                        pSrc++;

                        if (ch > 0x7F)
                        {
                            goto LongCode;
                        }
                        *pTarget = (byte)ch;
                        pTarget++;
                        continue;

                    LongCode:
                        // use separate helper variables for slow and fast loop so that the jit optimizations
                        // won't get confused about the variable lifetimes
                        int chd;
                        if (ch <= 0x7FF)
                        {
                            // 2 byte encoding
                            chd = unchecked((sbyte)0xC0) | (ch >> 6);
                        }
                        else
                        {
                            // if (!IsLowSurrogate(ch) && !IsHighSurrogate(ch))
                            if (!IsInRangeInclusive(ch, HighSurrogateStart, LowSurrogateEnd))
                            {
                                // 3 byte encoding
                                chd = unchecked((sbyte)0xE0) | (ch >> 12);
                            }
                            else
                            {
                                // 4 byte encoding - high surrogate + low surrogate
                                // if (!IsHighSurrogate(ch))
                                if (ch > HighSurrogateEnd)
                                {
                                    // low without high -> bad
                                    goto InvalidData;
                                }

                                chd = *pSrc;

                                // if (!IsLowSurrogate(chd)) {
                                if (!IsInRangeInclusive(chd, LowSurrogateStart, LowSurrogateEnd))
                                {
                                    // high not followed by low -> bad
                                    goto InvalidData;
                                }

                                pSrc++;

                                ch = chd + (ch << 10) +
                                    (0x10000
                                    - LowSurrogateStart
                                    - (HighSurrogateStart << 10));

                                *pTarget = (byte)(unchecked((sbyte)0xF0) | (ch >> 18));
                                // pStop - this byte is compensated by the second surrogate character
                                // 2 input chars require 4 output bytes.  2 have been anticipated already
                                // and 2 more will be accounted for by the 2 pStop-- calls below.
                                pTarget++;

                                chd = unchecked((sbyte)0x80) | (ch >> 12) & 0x3F;
                            }
                            *pTarget = (byte)chd;
                            pStop--;                    // 3 byte sequence for 1 char, so need pStop-- and the one below too.
                            pTarget++;

                            chd = unchecked((sbyte)0x80) | (ch >> 6) & 0x3F;
                        }
                        *pTarget = (byte)chd;
                        pStop--;                        // 2 byte sequence for 1 char so need pStop--.

                        *(pTarget + 1) = (byte)(unchecked((sbyte)0x80) | ch & 0x3F);
                        // pStop - this byte is already included

                        pTarget += 2;
                    }
                    while (pSrc < pStop);

                    Debug.Assert(pTarget <= pAllocatedBufferEnd, "[UTF8Encoding.GetBytes]pTarget <= pAllocatedBufferEnd");
                }

                while (pSrc < pEnd)
                {
                    // SLOWLOOP: does all range checks, handles all special cases, but it is slow

                    // read next char. The JIT optimization seems to be getting confused when
                    // compiling "ch = *pSrc++;", so rather use "ch = *pSrc; pSrc++;" instead
                    int ch = *pSrc;
                    pSrc++;

                    if (ch <= 0x7F)
                    {
                        if (pAllocatedBufferEnd - pTarget <= 0)
                            goto DestinationFull;

                        *pTarget = (byte)ch;
                        pTarget++;
                        continue;
                    }

                    int chd;
                    if (ch <= 0x7FF)
                    {
                        if (pAllocatedBufferEnd - pTarget <= 1)
                            goto DestinationFull;

                        // 2 byte encoding
                        chd = unchecked((sbyte)0xC0) | (ch >> 6);
                    }
                    else
                    {
                        // if (!IsLowSurrogate(ch) && !IsHighSurrogate(ch))
                        if (!IsInRangeInclusive(ch, HighSurrogateStart, LowSurrogateEnd))
                        {
                            if (pAllocatedBufferEnd - pTarget <= 2)
                                goto DestinationFull;

                            // 3 byte encoding
                            chd = unchecked((sbyte)0xE0) | (ch >> 12);
                        }
                        else
                        {
                            if (pAllocatedBufferEnd - pTarget <= 3)
                                goto DestinationFull;

                            // 4 byte encoding - high surrogate + low surrogate
                            // if (!IsHighSurrogate(ch))
                            if (ch > HighSurrogateEnd)
                            {
                                // low without high -> bad
                                goto InvalidData;
                            }

                            if (pSrc >= pEnd)
                                goto NeedMoreData;

                            chd = *pSrc;

                            // if (!IsLowSurrogate(chd)) {
                            if (!IsInRangeInclusive(chd, LowSurrogateStart, LowSurrogateEnd))
                            {
                                // high not followed by low -> bad
                                goto InvalidData;
                            }

                            pSrc++;

                            ch = chd + (ch << 10) +
                                (0x10000
                                - LowSurrogateStart
                                - (HighSurrogateStart << 10));

                            *pTarget = (byte)(unchecked((sbyte)0xF0) | (ch >> 18));
                            pTarget++;

                            chd = unchecked((sbyte)0x80) | (ch >> 12) & 0x3F;
                        }
                        *pTarget = (byte)chd;
                        pTarget++;

                        chd = unchecked((sbyte)0x80) | (ch >> 6) & 0x3F;
                    }

                    *pTarget = (byte)chd;
                    *(pTarget + 1) = (byte)(unchecked((sbyte)0x80) | ch & 0x3F);

                    pTarget += 2;
                }

                bytesConsumed = (int)((byte*)pSrc - chars);
                bytesWritten = (int)(pTarget - bytes);
                return OperationStatus.Done;

            InvalidData:
                bytesConsumed = (int)((byte*)(pSrc - 1) - chars);
                bytesWritten = (int)(pTarget - bytes);
                return OperationStatus.InvalidData;

            DestinationFull:
                bytesConsumed = (int)((byte*)(pSrc - 1) - chars);
                bytesWritten = (int)(pTarget - bytes);
                return OperationStatus.DestinationTooSmall;

            NeedMoreData:
                bytesConsumed = (int)((byte*)(pSrc - 1) - chars);
                bytesWritten = (int)(pTarget - bytes);
                return OperationStatus.NeedMoreData;
            }
        }

        /// <summary>Converts a span containing a sequence of UTF-8 bytes into UTF-16 bytes.
        ///
        /// This method will consume as many of the input bytes as possible.
        ///
        /// On successful exit, the entire input was consumed and encoded successfully. In this case, <paramref name="bytesConsumed"/> will be
        /// equal to the length of the <paramref name="utf8Source"/> and <paramref name="bytesWritten"/> will equal the total number of bytes written to
        /// the <paramref name="utf16Destination"/>.</summary>
        /// <param name="utf8Source">A span containing a sequence of UTF-8 bytes.</param>
        /// <param name="utf16Destination">A span to write the UTF-16 bytes into.</param>
        /// <param name="bytesConsumed">On exit, contains the number of bytes that were consumed from the <paramref name="utf8Source"/>.</param>
        /// <param name="bytesWritten">On exit, contains the number of bytes written to <paramref name="utf16Destination"/></param>
        /// <returns>A <see cref="OperationStatus"/> value representing the state of the conversion.</returns>
        public unsafe static OperationStatus ToUtf16(ReadOnlySpan<byte> utf8Source, Span<byte> utf16Destination, out int bytesConsumed, out int bytesWritten)
        {
            fixed (byte* pUtf8 = &MemoryMarshal.GetReference(utf8Source))
            fixed (byte* pUtf16 = &MemoryMarshal.GetReference(utf16Destination))
            {
                byte* pSrc = pUtf8;
                byte* pSrcEnd = pSrc + utf8Source.Length;
                char* pDst = (char*)pUtf16;
                char* pDstEnd = pDst + (utf16Destination.Length >> 1);   // Conversion from bytes to chars - div by sizeof(char)

                int ch = 0;
                while (pSrc < pSrcEnd && pDst < pDstEnd)
                {
                    // we may need as many as 1 character per byte, so reduce the byte count if necessary.
                    // If availableChars is too small, pStop will be before pTarget and we won't do fast loop.
                    int availableChars = PtrDiff(pDstEnd, pDst);
                    int availableBytes = PtrDiff(pSrcEnd, pSrc);

                    if (availableChars < availableBytes)
                        availableBytes = availableChars;

                    // don't fall into the fast decoding loop if we don't have enough bytes
                    if (availableBytes <= 13)
                    {
                        // try to get over the remainder of the ascii characters fast though
                        byte* pLocalEnd = pSrc + availableBytes;
                        while (pSrc < pLocalEnd)
                        {
                            ch = *pSrc;
                            pSrc++;

                            if (ch > 0x7F)
                                goto LongCodeSlow;

                            *pDst = (char)ch;
                            pDst++;
                        }

                        // we are done
                        break;
                    }

                    // To compute the upper bound, assume that all characters are ASCII characters at this point,
                    //  the boundary will be decreased for every non-ASCII character we encounter
                    // Also, we need 7 chars reserve for the unrolled ansi decoding loop and for decoding of multibyte sequences
                    char* pStop = pDst + availableBytes - 7;

                    // Fast loop
                    while (pDst < pStop)
                    {
                        ch = *pSrc;
                        pSrc++;

                        if (ch > 0x7F)
                            goto LongCode;

                        *pDst = (char)ch;
                        pDst++;

                        // 2-byte align
                        if ((unchecked((int)pSrc) & 0x1) != 0)
                        {
                            ch = *pSrc;
                            pSrc++;

                            if (ch > 0x7F)
                                goto LongCode;

                            *pDst = (char)ch;
                            pDst++;
                        }

                        // 4-byte align
                        if ((unchecked((int)pSrc) & 0x2) != 0)
                        {
                            ch = *(ushort*)pSrc;
                            if ((ch & 0x8080) != 0)
                                goto LongCodeWithMask16;

                            // Unfortunately, endianness sensitive
#if BIGENDIAN
                                *pDst = (char)((ch >> 8) & 0x7F);
                                pSrc += 2;
                                *(pDst + 1) = (char)(ch & 0x7F);
                                pDst += 2;
#else // BIGENDIAN
                            *pDst = (char)(ch & 0x7F);
                            pSrc += 2;
                            *(pDst + 1) = (char)((ch >> 8) & 0x7F);
                            pDst += 2;
#endif // BIGENDIAN
                        }

                        // Run 8 characters at a time!
                        while (pDst < pStop)
                        {
                            ch = *(int*)pSrc;
                            int chb = *(int*)(pSrc + 4);
                            if (((ch | chb) & unchecked((int)0x80808080)) != 0)
                                goto LongCodeWithMask32;

                            // Unfortunately, endianness sensitive
#if BIGENDIAN
                                *pDst = (char)((ch >> 24) & 0x7F);
                                *(pDst+1) = (char)((ch >> 16) & 0x7F);
                                *(pDst+2) = (char)((ch >> 8) & 0x7F);
                                *(pDst+3) = (char)(ch & 0x7F);
                                pSrc += 8;
                                *(pDst+4) = (char)((chb >> 24) & 0x7F);
                                *(pDst+5) = (char)((chb >> 16) & 0x7F);
                                *(pDst+6) = (char)((chb >> 8) & 0x7F);
                                *(pDst+7) = (char)(chb & 0x7F);
                                pDst += 8;
#else // BIGENDIAN
                            *pDst = (char)(ch & 0x7F);
                            *(pDst + 1) = (char)((ch >> 8) & 0x7F);
                            *(pDst + 2) = (char)((ch >> 16) & 0x7F);
                            *(pDst + 3) = (char)((ch >> 24) & 0x7F);
                            pSrc += 8;
                            *(pDst + 4) = (char)(chb & 0x7F);
                            *(pDst + 5) = (char)((chb >> 8) & 0x7F);
                            *(pDst + 6) = (char)((chb >> 16) & 0x7F);
                            *(pDst + 7) = (char)((chb >> 24) & 0x7F);
                            pDst += 8;
#endif // BIGENDIAN
                        }

                        break;

#if BIGENDIAN
                            LongCodeWithMask32:
                                // be careful about the sign extension
                                ch = (int)(((uint)ch) >> 16);
                            LongCodeWithMask16:
                                ch = (int)(((uint)ch) >> 8);
#else // BIGENDIAN
                    LongCodeWithMask32:
                    LongCodeWithMask16:
                        ch &= 0xFF;
#endif // BIGENDIAN
                        pSrc++;
                        if (ch <= 0x7F)
                        {
                            *pDst = (char)ch;
                            pDst++;
                            continue;
                        }

                    LongCode:
                        int chc = *pSrc;
                        pSrc++;

                        // Bit 6 should be 0, and trailing byte should be 10vvvvvv
                        if ((ch & 0x40) == 0 || (chc & unchecked((sbyte)0xC0)) != 0x80)
                            goto InvalidData;

                        chc &= 0x3F;

                        if ((ch & 0x20) != 0)
                        {
                            // Handle 3 or 4 byte encoding.

                            // Fold the first 2 bytes together
                            chc |= (ch & 0x0F) << 6;

                            if ((ch & 0x10) != 0)
                            {
                                // 4 byte - surrogate pair
                                ch = *pSrc;

                                // Bit 4 should be zero + the surrogate should be in the range 0x000000 - 0x10FFFF
                                // and the trailing byte should be 10vvvvvv
                                if (!IsInRangeInclusive(chc >> 4, 0x01, 0x10) || (ch & unchecked((sbyte)0xC0)) != 0x80)
                                    goto InvalidData;

                                // Merge 3rd byte then read the last byte
                                chc = (chc << 6) | (ch & 0x3F);
                                ch = *(pSrc + 1);

                                // The last trailing byte still holds the form 10vvvvvv
                                if ((ch & unchecked((sbyte)0xC0)) != 0x80)
                                    goto InvalidData;

                                pSrc += 2;
                                ch = (chc << 6) | (ch & 0x3F);

                                *pDst = (char)(((ch >> 10) & 0x7FF) + unchecked((short)(HighSurrogateStart - (0x10000 >> 10))));
                                pDst++;

                                ch = (ch & 0x3FF) + unchecked((short)(LowSurrogateStart));
                            }
                            else
                            {
                                // 3 byte encoding
                                ch = *pSrc;

                                // Check for non-shortest form of 3 byte sequence
                                // No surrogates
                                // Trailing byte must be in the form 10vvvvvv
                                if ((chc & (0x1F << 5)) == 0 ||
                                    (chc & (0xF800 >> 6)) == (0xD800 >> 6) ||
                                    (ch & unchecked((sbyte)0xC0)) != 0x80)
                                    goto InvalidData;

                                pSrc++;
                                ch = (chc << 6) | (ch & 0x3F);
                            }

                            // extra byte, we're already planning 2 chars for 2 of these bytes,
                            // but the big loop is testing the target against pStop, so we need
                            // to subtract 2 more or we risk overrunning the input.  Subtract
                            // one here and one below.
                            pStop--;
                        }
                        else
                        {
                            // 2 byte encoding
                            ch &= 0x1F;

                            // Check for non-shortest form
                            if (ch <= 1)
                                goto InvalidData;

                            ch = (ch << 6) | chc;
                        }

                        *pDst = (char)ch;
                        pDst++;

                        // extra byte, we're only expecting 1 char for each of these 2 bytes,
                        // but the loop is testing the target (not source) against pStop.
                        // subtract an extra count from pStop so that we don't overrun the input.
                        pStop--;
                    }

                    continue;

                LongCodeSlow:
                    if (pSrc >= pSrcEnd)
                    {
                        // This is a special case where hit the end of the buffer but are in the middle
                        // of decoding a long code. The error exit thinks we have read 2 extra bytes already,
                        // so we add +1 to pSrc to get the count correct for the bytes consumed value.
                        pSrc++;
                        goto NeedMoreData;
                    }

                    int chd = *pSrc;
                    pSrc++;

                    // Bit 6 should be 0, and trailing byte should be 10vvvvvv
                    if ((ch & 0x40) == 0 || (chd & unchecked((sbyte)0xC0)) != 0x80)
                        goto InvalidData;

                    chd &= 0x3F;

                    if ((ch & 0x20) != 0)
                    {
                        // Handle 3 or 4 byte encoding.

                        // Fold the first 2 bytes together
                        chd |= (ch & 0x0F) << 6;

                        if ((ch & 0x10) != 0)
                        {
                            // 4 byte - surrogate pair
                            // We need 2 more bytes
                            if (pSrc >= pSrcEnd - 1)
                                goto NeedMoreData;

                            ch = *pSrc;

                            // Bit 4 should be zero + the surrogate should be in the range 0x000000 - 0x10FFFF
                            // and the trailing byte should be 10vvvvvv
                            if (!IsInRangeInclusive(chd >> 4, 0x01, 0x10) || (ch & unchecked((sbyte)0xC0)) != 0x80)
                                goto InvalidData;

                            // Merge 3rd byte then read the last byte
                            chd = (chd << 6) | (ch & 0x3F);
                            ch = *(pSrc + 1);

                            // The last trailing byte still holds the form 10vvvvvv
                            // We only know for sure we have room for one more char, but we need an extra now.
                            if ((ch & unchecked((sbyte)0xC0)) != 0x80)
                                goto InvalidData;

                            if (PtrDiff(pDstEnd, pDst) < 2)
                                goto DestinationFull;

                            pSrc += 2;
                            ch = (chd << 6) | (ch & 0x3F);

                            *pDst = (char)(((ch >> 10) & 0x7FF) + unchecked((short)(HighSurrogateStart - (0x10000 >> 10))));
                            pDst++;

                            ch = (ch & 0x3FF) + unchecked((short)(LowSurrogateStart));
                        }
                        else
                        {
                            // 3 byte encoding
                            if (pSrc >= pSrcEnd)
                                goto NeedMoreData;

                            ch = *pSrc;

                            // Check for non-shortest form of 3 byte sequence
                            // No surrogates
                            // Trailing byte must be in the form 10vvvvvv
                            if ((chd & (0x1F << 5)) == 0 ||
                                (chd & (0xF800 >> 6)) == (0xD800 >> 6) ||
                                (ch & unchecked((sbyte)0xC0)) != 0x80)
                                goto InvalidData;

                            pSrc++;
                            ch = (chd << 6) | (ch & 0x3F);
                        }
                    }
                    else
                    {
                        // 2 byte encoding
                        ch &= 0x1F;

                        // Check for non-shortest form
                        if (ch <= 1)
                            goto InvalidData;

                        ch = (ch << 6) | chd;
                    }

                    *pDst = (char)ch;
                    pDst++;
                }

            DestinationFull:
                bytesConsumed = PtrDiff(pSrc, pUtf8);
                bytesWritten = PtrDiff((byte*)pDst, pUtf16);
                return PtrDiff(pSrcEnd, pSrc) == 0 ? OperationStatus.Done : OperationStatus.DestinationTooSmall;

            NeedMoreData:
                bytesConsumed = PtrDiff(pSrc - 2, pUtf8);
                bytesWritten = PtrDiff((byte*)pDst, pUtf16);
                return OperationStatus.NeedMoreData;

            InvalidData:
                bytesConsumed = PtrDiff(pSrc - 2, pUtf8);
                bytesWritten = PtrDiff((byte*)pDst, pUtf16);
                return OperationStatus.InvalidData;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static int PtrDiff(char* a, char* b)
        {
            return (int)(((uint)((byte*)a - (byte*)b)) >> 1);
        }

        // byte* flavor just for parity
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static int PtrDiff(byte* a, byte* b)
        {
            return (int)(a - b);
        }

        /// <summary>Returns <see langword="true"/> iff <paramref name="value"/> is between
        /// <paramref name="lowerBound"/> and <paramref name="upperBound"/>, inclusive.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInRangeInclusive(int value, int lowerBound, int upperBound)
            => (uint)(value - lowerBound) <= (uint)(upperBound - lowerBound);
    }
}

#endif