/*
 * Copyright 2012 The Netty Project
 *
 * The Netty Project licenses this file to you under the Apache License,
 * version 2.0 (the "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at:
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    sealed class HpackHuffmanEncoder
    {
        private readonly int[] _codes;
        private readonly byte[] _lengths;
        private readonly EncodedLengthProcessor _encodedLengthProcessor;
        private readonly EncodeProcessor _encodeProcessor;

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
            _encodedLengthProcessor = new EncodedLengthProcessor(lengths);
            _encodeProcessor = new EncodeProcessor(codes, lengths);
            _codes = codes;
            _lengths = lengths;
        }

        /// <summary>
        /// Compresses the input string literal using the Huffman coding.
        /// </summary>
        /// <param name="ouput">the output stream for the compressed data</param>
        /// <param name="data">the string literal to be Huffman encoded</param>
        public void Encode(IByteBuffer ouput, ICharSequence data)
        {
            if (ouput is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.ouput); }

            if (data is AsciiString str)
            {
                try
                {
                    _encodeProcessor._ouput = ouput;
                    _ = str.ForEachByte(_encodeProcessor);
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    _encodeProcessor.End();
                }
            }
            else
            {
                EncodeSlowPath(ouput, data);
            }
        }

        void EncodeSlowPath(IByteBuffer ouput, ICharSequence data)
        {
            long current = 0;
            int n = 0;

            for (int i = 0; i < data.Count; i++)
            {
                int b = data[i] & 0xFF;
                int code = _codes[b];
                int nbits = _lengths[b];

                current <<= nbits;
                current |= (uint)code;
                n += nbits;

                while (n >= 8)
                {
                    n -= 8;
                    _ = ouput.WriteByte((int)(current >> n));
                }
            }

            if (n > 0)
            {
                current <<= 8 - n;
                current |= (uint)0xFF >> n; // this should be EOS symbol
                _ = ouput.WriteByte((int)current);
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
                    _encodedLengthProcessor.Reset();
                    _ = str.ForEachByte(_encodedLengthProcessor);
                    return _encodedLengthProcessor.Count;
                }
                catch (Exception)
                {
                    throw;
                    //return -1;
                }
            }
            else
            {
                return GetEncodedLengthSlowPath(data);
            }
        }

        int GetEncodedLengthSlowPath(ICharSequence data)
        {
            long len = 0;
            for (int i = 0; i < data.Count; i++)
            {
                len += _lengths[data[i] & 0xFF];
            }

            return (int)((len + 7) >> 3);
        }

        sealed class EncodeProcessor : IByteProcessor
        {
            private readonly int[] _codes;
            private readonly byte[] _lengths;
            internal IByteBuffer _ouput;
            private long _current;
            private int _n;

            public EncodeProcessor(int[] codes, byte[] lengths)
            {
                _codes = codes;
                _lengths = lengths;
            }

            public bool Process(byte value)
            {
                int b = value & 0xFF;
                int nbits = _lengths[b];

                _current <<= nbits;
                _current |= (uint)_codes[b];
                _n += nbits;

                while (_n >= 8)
                {
                    _n -= 8;
                    _ = _ouput.WriteByte((int)(_current >> _n));
                }

                return true;
            }

            internal void End()
            {
                try
                {
                    if (_n > 0)
                    {
                        _current <<= 8 - _n;
                        _current |= (uint)0xFF >> _n; // this should be EOS symbol
                        _ = _ouput.WriteByte((int)_current);
                    }
                }
                finally
                {
                    _ouput = null;
                    _current = 0;
                    _n = 0;
                }
            }
        }

        sealed class EncodedLengthProcessor : IByteProcessor
        {
            private readonly byte[] _lengths;
            private long _len;

            public EncodedLengthProcessor(byte[] lengths)
            {
                _lengths = lengths;
            }

            public bool Process(byte value)
            {
                _len += _lengths[value & 0xFF];
                return true;
            }

            internal void Reset()
            {
                _len = 0;
            }

            internal int Count => (int)((_len + 7) >> 3);
        }
    }
}