// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    sealed class HpackHuffmanEncoder
    {
        readonly int[] codes;
        readonly byte[] lengths;
        readonly EncodedLengthProcessor encodedLengthProcessor;
        readonly EncodeProcessor encodeProcessor;

        internal HpackHuffmanEncoder()
            : this(HpackUtil.HuffmanCodes, HpackUtil.HuffmanCodeLengths)
        {
        }

        /// <summary>
        /// Creates a new Huffman encoder with the specified Huffman coding.
        /// </summary>
        /// <param name="codes">the Huffman codes indexed by symbol</param>
        /// <param name="lengths">the length of each Huffman code</param>
        internal HpackHuffmanEncoder(int[] codes, byte[] lengths)
        {
            this.encodedLengthProcessor = new EncodedLengthProcessor(lengths);
            this.encodeProcessor = new EncodeProcessor(codes, lengths);
            this.codes = codes;
            this.lengths = lengths;
        }

        /// <summary>
        /// Compresses the input string literal using the Huffman coding.
        /// </summary>
        /// <param name="ouput">the output stream for the compressed data</param>
        /// <param name="data">the string literal to be Huffman encoded</param>
        public void Encode(IByteBuffer ouput, ICharSequence data)
        {
            if (null == ouput) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.ouput); }

            if (data is AsciiString str)
            {
                try
                {
                    this.encodeProcessor.ouput = ouput;
                    str.ForEachByte(this.encodeProcessor);
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    this.encodeProcessor.End();
                }
            }
            else
            {
                this.EncodeSlowPath(ouput, data);
            }
        }

        void EncodeSlowPath(IByteBuffer ouput, ICharSequence data)
        {
            long current = 0;
            int n = 0;

            for (int i = 0; i < data.Count; i++)
            {
                int b = data[i] & 0xFF;
                int code = this.codes[b];
                int nbits = this.lengths[b];

                current <<= nbits;
                current |= (uint)code;
                n += nbits;

                while (n >= 8)
                {
                    n -= 8;
                    ouput.WriteByte((int)(current >> n));
                }
            }

            if (n > 0)
            {
                current <<= 8 - n;
                current |= (uint)0xFF >> n; // this should be EOS symbol
                ouput.WriteByte((int)current);
            }
        }

        /// <summary>
        /// Returns the number of bytes required to Huffman encode the input string literal.
        /// </summary>
        /// <param name="data">the string literal to be Huffman encoded</param>
        /// <returns>the number of bytes required to Huffman encode <paramref name="data"/>.</returns>
        internal int GetEncodedLength(ICharSequence data)
        {
            if (data is AsciiString str)
            {
                try
                {
                    this.encodedLengthProcessor.Reset();
                    str.ForEachByte(this.encodedLengthProcessor);
                    return this.encodedLengthProcessor.Count;
                }
                catch (Exception)
                {
                    throw;
                    //return -1;
                }
            }
            else
            {
                return this.GetEncodedLengthSlowPath(data);
            }
        }

        int GetEncodedLengthSlowPath(ICharSequence data)
        {
            long len = 0;
            for (int i = 0; i < data.Count; i++)
            {
                len += this.lengths[data[i] & 0xFF];
            }

            return (int)((len + 7) >> 3);
        }

        sealed class EncodeProcessor : IByteProcessor
        {
            readonly int[] codes;
            readonly byte[] lengths;
            internal IByteBuffer ouput;
            long current;
            int n;

            public EncodeProcessor(int[] codes, byte[] lengths)
            {
                this.codes = codes;
                this.lengths = lengths;
            }

            public bool Process(byte value)
            {
                int b = value & 0xFF;
                int nbits = this.lengths[b];

                this.current <<= nbits;
                this.current |= (uint)this.codes[b];
                this.n += nbits;

                while (this.n >= 8)
                {
                    this.n -= 8;
                    this.ouput.WriteByte((int)(this.current >> this.n));
                }

                return true;
            }

            internal void End()
            {
                try
                {
                    if (this.n > 0)
                    {
                        this.current <<= 8 - this.n;
                        this.current |= (uint)0xFF >> this.n; // this should be EOS symbol
                        this.ouput.WriteByte((int)this.current);
                    }
                }
                finally
                {
                    this.ouput = null;
                    this.current = 0;
                    this.n = 0;
                }
            }
        }

        sealed class EncodedLengthProcessor : IByteProcessor
        {
            readonly byte[] lengths;
            long len;

            public EncodedLengthProcessor(byte[] lengths)
            {
                this.lengths = lengths;
            }

            public bool Process(byte value)
            {
                this.len += this.lengths[value & 0xFF];
                return true;
            }

            internal void Reset()
            {
                this.len = 0;
            }

            internal int Count => (int)((this.len + 7) >> 3);
        }
    }
}