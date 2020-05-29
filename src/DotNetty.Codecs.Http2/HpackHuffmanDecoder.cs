// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System.Runtime.CompilerServices;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    sealed partial class HpackHuffmanDecoder : IByteProcessor
    {
        internal static readonly Http2Exception s_badEncoding =
            new Http2Exception(Http2Error.CompressionError, "HPACK - Bad Encoding", ShutdownHint.HardShutdown);

        private byte[] _dest;
        private int _k;
        private int _state;

        /// <summary>
        /// Decompresses the given Huffman coded string literal.
        /// </summary>
        /// <param name="buf">the string literal to be decoded</param>
        /// <param name="length"></param>
        /// <returns>the output stream for the compressed data</returns>
        /// <exception cref="Http2Exception">EOS Decoded</exception>
        public AsciiString Decode(IByteBuffer buf, int length)
        {
            if (0u >= (uint)length) { return AsciiString.Empty; }

            _dest = new byte[length * 8 / 5];
            try
            {
                int readerIndex = buf.ReaderIndex;
                // Using ByteProcessor to reduce bounds-checking and reference-count checking during byte-by-byte
                // processing of the ByteBuf.
                int endIndex = buf.ForEachByte(readerIndex, length, this);
                if ((uint)endIndex > SharedConstants.TooBigOrNegative) // == -1
                {
                    // We did consume the requested length
                    buf.SetReaderIndex(readerIndex + length);
                    if ((_state & HUFFMAN_COMPLETE_SHIFT) != HUFFMAN_COMPLETE_SHIFT)
                    {
                        ThrowBadEncoding();
                    }
                    return new AsciiString(_dest, 0, _k, false);
                }

                // The process(...) method returned before the requested length was requested. This means there
                // was a bad encoding detected.
                buf.SetReaderIndex(endIndex);
                throw s_badEncoding;
            }
            finally
            {
                _dest = null;
                _k = 0;
                _state = 0;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowBadEncoding()
        {
            throw s_badEncoding;
        }

        /// <summary>
        /// This should never be called from anything but this class itself!
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public bool Process(byte input)
        {
            return ProcessNibble(input >> 4) && ProcessNibble(input);
        }

        private bool ProcessNibble(int input)
        {
            // The high nibble of the flags byte of each row is always zero
            // (low nibble after shifting row by 12), since there are only 3 flag bits
            int index = _state >> 12 | (input & 0x0F);
            _state = HUFFS[index];
            if ((_state & HUFFMAN_FAIL_SHIFT) != 0)
            {
                return false;
            }
            if ((_state & HUFFMAN_EMIT_SYMBOL_SHIFT) != 0)
            {
                // state is always positive so can cast without mask here
                _dest[_k++] = (byte)_state;
            }
            return true;
        }
    }
}