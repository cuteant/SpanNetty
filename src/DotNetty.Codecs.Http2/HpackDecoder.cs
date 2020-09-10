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
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.ExceptionServices;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    sealed class HpackDecoder
    {
        internal static readonly Http2Exception DecodeULE128DecompressionException =
            new Http2Exception(Http2Error.CompressionError, "HPACK - decompression failure", ShutdownHint.HardShutdown);

        internal static readonly Http2Exception DecodeULE128ToLongDecompressionException =
            new Http2Exception(Http2Error.CompressionError, "HPACK - long overflow", ShutdownHint.HardShutdown);

        internal static readonly Http2Exception DecodeULE128ToIntDecompressionException =
            new Http2Exception(Http2Error.CompressionError, "HPACK - int overflow", ShutdownHint.HardShutdown);

        internal static readonly Http2Exception DecodeIllegalIndexValue =
            new Http2Exception(Http2Error.CompressionError, "HPACK - illegal index value", ShutdownHint.HardShutdown);

        internal static readonly Http2Exception IndexHeaderIllegalIndexValue =
            new Http2Exception(Http2Error.CompressionError, "HPACK - illegal index value", ShutdownHint.HardShutdown);

        internal static readonly Http2Exception ReadNameIllegalIndexValue =
            new Http2Exception(Http2Error.CompressionError, "HPACK - illegal index value", ShutdownHint.HardShutdown);

        internal static readonly Http2Exception InvalidMaxDynamicTableSize =
            new Http2Exception(Http2Error.CompressionError, "HPACK - invalid max dynamic table size", ShutdownHint.HardShutdown);

        internal static readonly Http2Exception MaxDynamicTableSizeChangeRequired =
            new Http2Exception(Http2Error.CompressionError, "HPACK - max dynamic table size change required", ShutdownHint.HardShutdown);

        const byte ReadHeaderRepresentation = 0;

        const byte ReadMaxDynamicTableSize = 1;

        const byte ReadIndexedHeader = 2;

        const byte ReadIndexedHeaderName = 3;

        const byte ReadLiteralHeaderNameLengthPrefix = 4;

        const byte ReadLiteralHeaderNameLength = 5;

        const byte ReadLiteralHeaderName = 6;

        const byte ReadLiteralHeaderValueLengthPrefix = 7;

        const byte ReadLiteralHeaderValueLength = 8;

        const byte ReadLiteralHeaderValue = 9;

        private readonly HpackHuffmanDecoder _huffmanDecoder = new HpackHuffmanDecoder();
        private readonly HpackDynamicTable _hpackDynamicTable;

        private long _maxHeaderListSize;
        private long _maxDynamicTableSize;
        private long _encoderMaxDynamicTableSize;
        private bool _maxDynamicTableSizeChangeRequired;

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="maxHeaderListSize">maxHeaderListSize This is the only setting that can be configured before notifying the peer.
        /// This is because <a href="https://tools.ietf.org/html/rfc7540#section-6.5.1">SETTINGS_Http2CodecUtil.MAX_HEADER_LIST_SIZE</a>
        /// allows a lower than advertised limit from being enforced, and the default limit is unlimited
        /// (which is dangerous).</param>
        internal HpackDecoder(long maxHeaderListSize)
            : this(maxHeaderListSize, Http2CodecUtil.DefaultHeaderTableSize)
        {

        }

        /// <summary>
        /// Exposed Used for testing only! Default values used in the initial settings frame are overridden intentionally
        /// for testing but violate the RFC if used outside the scope of testing.
        /// </summary>
        /// <param name="maxHeaderListSize"></param>
        /// <param name="maxHeaderTableSize"></param>
        internal HpackDecoder(long maxHeaderListSize, int maxHeaderTableSize)
        {
            if ((ulong)(maxHeaderListSize - 1L) > SharedConstants.TooBigOrNegative64)
            {
                ThrowHelper.ThrowArgumentException_Positive(maxHeaderListSize, ExceptionArgument.maxHeaderListSize);
            }
            _maxHeaderListSize = maxHeaderListSize;

            _maxDynamicTableSize = _encoderMaxDynamicTableSize = maxHeaderTableSize;
            _maxDynamicTableSizeChangeRequired = false;
            _hpackDynamicTable = new HpackDynamicTable(maxHeaderTableSize);
        }

        /// <summary>
        /// Decode the header block into header fields.
        /// <para>This method assumes the entire header block is contained in <paramref name="input"/>.</para>
        /// </summary>
        /// <param name="streamId"></param>
        /// <param name="input"></param>
        /// <param name="headers"></param>
        /// <param name="validateHeaders"></param>
        public void Decode(int streamId, IByteBuffer input, IHttp2Headers headers, bool validateHeaders)
        {
            var sink = new Http2HeadersSink(streamId, headers, _maxHeaderListSize, validateHeaders);
            Decode(input, sink);

            // Now that we've read all of our headers we can perform the validation steps. We must
            // delay throwing until this point to prevent dynamic table corruption.
            sink.Finish();
        }

        public void Decode(IByteBuffer input, ISink sink)
        {
            int index = 0;
            int nameLength = 0;
            int valueLength = 0;
            byte state = ReadHeaderRepresentation;
            bool huffmanEncoded = false;
            ICharSequence name = null;

            HpackUtil.IndexType indexType = HpackUtil.IndexType.None;
            while (input.IsReadable())
            {
                switch (state)
                {
                    case ReadHeaderRepresentation:
                        byte b = input.ReadByte();
                        if (_maxDynamicTableSizeChangeRequired && (b & 0xE0) != 0x20)
                        {
                            // HpackEncoder MUST signal maximum dynamic table size change
                            ThrowHelper.ThrowHttp2Exception_MaxDynamicTableSizeChangeRequired();
                        }

                        if (b > 127)
                        {
                            // Indexed Header Field
                            index = b & 0x7F;
                            switch (index)
                            {
                                case 0:
                                    ThrowHelper.ThrowHttp2Exception_DecodeIllegalIndexValue();
                                    break;
                                case 0x7F:
                                    state = ReadIndexedHeader;
                                    break;
                                default:
                                    HpackHeaderField idxHeader = GetIndexedHeader(index);
                                    sink.AppendToHeaderList(idxHeader._name, idxHeader._value);
                                    break;
                            }
                        }
                        else if ((b & 0x40) == 0x40)
                        {
                            // Literal Header Field with Incremental Indexing
                            indexType = HpackUtil.IndexType.Incremental;
                            index = b & 0x3F;
                            switch (index)
                            {
                                case 0:
                                    state = ReadLiteralHeaderNameLengthPrefix;
                                    break;
                                case 0x3F:
                                    state = ReadIndexedHeaderName;
                                    break;
                                default:
                                    // Index was stored as the prefix
                                    name = ReadName(index);
                                    nameLength = name.Count;
                                    state = ReadLiteralHeaderValueLengthPrefix;
                                    break;
                            }
                        }
                        else if ((b & 0x20) == 0x20)
                        {
                            // Dynamic Table Size Update
                            index = b & 0x1F;
                            if (index == 0x1F)
                            {
                                state = ReadMaxDynamicTableSize;
                            }
                            else
                            {
                                SetDynamicTableSize(index);
                                state = ReadHeaderRepresentation;
                            }
                        }
                        else
                        {
                            // Literal Header Field without Indexing / never Indexed
                            indexType = ((b & 0x10) == 0x10) ? HpackUtil.IndexType.Never : HpackUtil.IndexType.None;
                            index = b & 0x0F;
                            switch (index)
                            {
                                case 0:
                                    state = ReadLiteralHeaderNameLengthPrefix;
                                    break;
                                case 0x0F:
                                    state = ReadIndexedHeaderName;
                                    break;
                                default:
                                    // Index was stored as the prefix
                                    name = ReadName(index);
                                    nameLength = name.Count;
                                    state = ReadLiteralHeaderValueLengthPrefix;
                                    break;
                            }
                        }

                        break;

                    case ReadMaxDynamicTableSize:
                        SetDynamicTableSize(DecodeULE128(input, (long)index));
                        state = ReadHeaderRepresentation;
                        break;

                    case ReadIndexedHeader:
                        HpackHeaderField indexedHeader = GetIndexedHeader(DecodeULE128(input, index));
                        sink.AppendToHeaderList(indexedHeader._name, indexedHeader._value);
                        state = ReadHeaderRepresentation;
                        break;

                    case ReadIndexedHeaderName:
                        // Header Name matches an entry in the Header Table
                        name = ReadName(DecodeULE128(input, index));
                        nameLength = name.Count;
                        state = ReadLiteralHeaderValueLengthPrefix;
                        break;

                    case ReadLiteralHeaderNameLengthPrefix:
                        b = input.ReadByte();
                        huffmanEncoded = (b & 0x80) == 0x80;
                        index = b & 0x7F;
                        if (index == 0x7f)
                        {
                            state = ReadLiteralHeaderNameLength;
                        }
                        else
                        {
                            nameLength = index;
                            state = ReadLiteralHeaderName;
                        }

                        break;

                    case ReadLiteralHeaderNameLength:
                        // Header Name is a Literal String
                        nameLength = DecodeULE128(input, index);
                        state = ReadLiteralHeaderName;
                        break;

                    case ReadLiteralHeaderName:
                        // Wait until entire name is readable
                        if (input.ReadableBytes < nameLength)
                        {
                            ThrowHelper.ThrowArgumentException_NotEnoughData(input);
                        }

                        name = ReadStringLiteral(input, nameLength, huffmanEncoded);

                        state = ReadLiteralHeaderValueLengthPrefix;
                        break;

                    case ReadLiteralHeaderValueLengthPrefix:
                        b = input.ReadByte();
                        huffmanEncoded = (b & 0x80) == 0x80;
                        index = b & 0x7F;
                        switch (index)
                        {
                            case 0x7f:
                                state = ReadLiteralHeaderValueLength;
                                break;
                            case 0:
                                InsertHeader(sink, name, AsciiString.Empty, indexType);
                                state = ReadHeaderRepresentation;
                                break;
                            default:
                                valueLength = index;
                                state = ReadLiteralHeaderValue;
                                break;
                        }

                        break;

                    case ReadLiteralHeaderValueLength:
                        // Header Value is a Literal String
                        valueLength = DecodeULE128(input, index);
                        state = ReadLiteralHeaderValue;
                        break;

                    case ReadLiteralHeaderValue:
                        // Wait until entire value is readable
                        if (input.ReadableBytes < valueLength)
                        {
                            ThrowHelper.ThrowArgumentException_NotEnoughData(input);
                        }

                        ICharSequence value = ReadStringLiteral(input, valueLength, huffmanEncoded);
                        InsertHeader(sink, name, value, indexType);
                        state = ReadHeaderRepresentation;
                        break;

                    default:
                        ThrowHelper.ThrowException_ShouldNotReachHere(state);
                        break;
                }
            }

            if (state != ReadHeaderRepresentation)
            {
                ThrowHelper.ThrowConnectionError_IncompleteHeaderBlockFragment();
            }
        }

        /// <summary>
        /// Set the maximum table size. If this is below the maximum size of the dynamic table used by
        /// the encoder, the beginning of the next header block MUST signal this change.
        /// </summary>
        /// <param name="maxHeaderTableSize"></param>
        public void SetMaxHeaderTableSize(long maxHeaderTableSize)
        {
            if (maxHeaderTableSize < Http2CodecUtil.MinHeaderTableSize || maxHeaderTableSize > Http2CodecUtil.MaxHeaderTableSize)
            {
                ThrowHelper.ThrowConnectionError_SetMaxHeaderTableSize(maxHeaderTableSize);
            }

            _maxDynamicTableSize = maxHeaderTableSize;
            if (_maxDynamicTableSize < _encoderMaxDynamicTableSize)
            {
                // decoder requires less space than encoder
                // encoder MUST signal this change
                _maxDynamicTableSizeChangeRequired = true;
                _hpackDynamicTable.SetCapacity(_maxDynamicTableSize);
            }
        }

        [Obsolete("=> SetMaxHeaderListSize(long maxHeaderListSize)")]
        public void SetMaxHeaderListSize(long maxHeaderListSize, long maxHeaderListSizeGoAway)
        {
            SetMaxHeaderListSize(maxHeaderListSize);
        }

        public void SetMaxHeaderListSize(long maxHeaderListSize)
        {
            if (maxHeaderListSize < Http2CodecUtil.MinHeaderListSize || maxHeaderListSize > Http2CodecUtil.MaxHeaderListSize)
            {
                ThrowHelper.ThrowConnectionError_SetMaxHeaderListSize(maxHeaderListSize);
            }
            _maxHeaderListSize = maxHeaderListSize;
        }

        public long GetMaxHeaderListSize()
        {
            return _maxHeaderListSize;
        }

        /// <summary>
        /// Return the maximum table size. This is the maximum size allowed by both the encoder and the
        /// decoder.
        /// </summary>
        /// <returns></returns>
        public long GetMaxHeaderTableSize()
        {
            return _hpackDynamicTable.Capacity();
        }

        /// <summary>
        /// Return the number of header fields input the dynamic table. Exposed for testing.
        /// </summary>
        /// <returns></returns>
        internal int Length()
        {
            return _hpackDynamicTable.Length();
        }

        /// <summary>
        /// Return the size of the dynamic table. Exposed for testing.
        /// </summary>
        /// <returns></returns>
        internal long Size()
        {
            return _hpackDynamicTable.Size();
        }

        /// <summary>
        /// Return the header field at the given index. Exposed for testing.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        internal HpackHeaderField GetHeaderField(int index)
        {
            return _hpackDynamicTable.GetEntry(index + 1);
        }

        private void SetDynamicTableSize(long dynamicTableSize)
        {
            if (dynamicTableSize > _maxDynamicTableSize)
            {
                ThrowHelper.ThrowHttp2Exception_InvalidMaxDynamicTableSize();
            }

            _encoderMaxDynamicTableSize = dynamicTableSize;
            _maxDynamicTableSizeChangeRequired = false;
            _hpackDynamicTable.SetCapacity(dynamicTableSize);
        }

        internal static HeaderType Validate(int streamId, ICharSequence name, HeaderType? previousHeaderType)
        {
            if (PseudoHeaderName.HasPseudoHeaderFormat(name))
            {
                if (previousHeaderType == HeaderType.RegularHeader)
                {
                    ThrowHelper.ThrowStreamError_AfterRegularHeader(streamId, name);
                }

                var pseudoHeader = PseudoHeaderName.GetPseudoHeader(name);
                if (pseudoHeader is null)
                {
                    ThrowHelper.ThrowStreamError_InvalidPseudoHeader(streamId, name);
                }

                HeaderType currentHeaderType = pseudoHeader.IsRequestOnly ?
                        HeaderType.RequestPseudoHeader : HeaderType.ResponsePseudoHeader;
                if (previousHeaderType.HasValue && currentHeaderType != previousHeaderType.Value)
                {
                    ThrowHelper.ThrowStreamError_MixOfRequest(streamId);
                }

                return currentHeaderType;
            }

            return HeaderType.RegularHeader;
        }

        private ICharSequence ReadName(int index)
        {
            if ((uint)index <= (uint)HpackStaticTable.Length)
            {
                HpackHeaderField hpackHeaderField = HpackStaticTable.GetEntry(index);
                return hpackHeaderField._name;
            }

            if ((uint)(index - HpackStaticTable.Length) <= (uint)_hpackDynamicTable.Length())
            {
                HpackHeaderField hpackHeaderField = _hpackDynamicTable.GetEntry(index - HpackStaticTable.Length);
                return hpackHeaderField._name;
            }

            ThrowHelper.ThrowHttp2Exception_ReadNameIllegalIndexValue(); return null;
        }

        private HpackHeaderField GetIndexedHeader(int index)
        {
            if ((uint)index <= (uint)HpackStaticTable.Length)
            {
                return HpackStaticTable.GetEntry(index);
            }
            if ((uint)(index - HpackStaticTable.Length) <= (uint)_hpackDynamicTable.Length())
            {
                return _hpackDynamicTable.GetEntry(index - HpackStaticTable.Length);
            }
            ThrowHelper.ThrowHttp2Exception_IndexHeaderIllegalIndexValue(); return null;
        }

        private void InsertHeader(ISink sink, ICharSequence name, ICharSequence value, HpackUtil.IndexType indexType)
        {
            sink.AppendToHeaderList(name, value);

            switch (indexType)
            {
                case HpackUtil.IndexType.None:
                case HpackUtil.IndexType.Never:
                    break;

                case HpackUtil.IndexType.Incremental:
                    _hpackDynamicTable.Add(new HpackHeaderField(name, value));
                    break;

                default:
                    ThrowHelper.ThrowException_ShouldNotReachHere();
                    break;
            }
        }

        private ICharSequence ReadStringLiteral(IByteBuffer input, int length, bool huffmanEncoded)
        {
            if (huffmanEncoded)
            {
                return _huffmanDecoder.Decode(input, length);
            }

            byte[] buf = new byte[length];
            _ = input.ReadBytes(buf);
            return new AsciiString(buf, false);
        }

        /// <summary>
        /// Unsigned Little Endian Base 128 Variable-Length Integer Encoding
        /// <para>Visible for testing only!</para>
        /// </summary>
        /// <param name="input"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        internal static int DecodeULE128(IByteBuffer input, int result)
        {
            int readerIndex = input.ReaderIndex;
            long v = DecodeULE128(input, (long)result);
            if (v > int.MaxValue)
            {
                // the maximum value that can be represented by a signed 32 bit number is:
                // [0x1,0x7f] + 0x7f + (0x7f << 7) + (0x7f << 14) + (0x7f << 21) + (0x6 << 28)
                // OR
                // 0x0 + 0x7f + (0x7f << 7) + (0x7f << 14) + (0x7f << 21) + (0x7 << 28)
                // we should reset the readerIndex if we overflowed the int type.
                _ = input.SetReaderIndex(readerIndex);
                ThrowHelper.ThrowHttp2Exception_DecodeULE128ToIntDecompression();
            }

            return (int)v;
        }

        /// <summary>
        /// Unsigned Little Endian Base 128 Variable-Length Integer Encoding
        /// <para>Visible for testing only!</para>
        /// </summary>
        /// <param name="input"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        internal static long DecodeULE128(IByteBuffer input, long result)
        {
            Debug.Assert(result <= 0x7f && result >= 0);
            bool resultStartedAtZero = 0ul >= (ulong)result;
            int writerIndex = input.WriterIndex;
            for (int readerIndex = input.ReaderIndex, shift = 0; readerIndex < writerIndex; ++readerIndex, shift += 7)
            {
                byte b = input.GetByte(readerIndex);
                if (shift == 56 && ((b & 0x80) != 0 || b == 0x7F && !resultStartedAtZero))
                {
                    // the maximum value that can be represented by a signed 64 bit number is:
                    // [0x01L, 0x7fL] + 0x7fL + (0x7fL << 7) + (0x7fL << 14) + (0x7fL << 21) + (0x7fL << 28) + (0x7fL << 35)
                    // + (0x7fL << 42) + (0x7fL << 49) + (0x7eL << 56)
                    // OR
                    // 0x0L + 0x7fL + (0x7fL << 7) + (0x7fL << 14) + (0x7fL << 21) + (0x7fL << 28) + (0x7fL << 35) +
                    // (0x7fL << 42) + (0x7fL << 49) + (0x7fL << 56)
                    // this means any more shifts will result in overflow so we should break out and throw an error.
                    ThrowHelper.ThrowHttp2Exception_DecodeULE128ToLongDecompression();
                }

                if (0u >= (uint)(b & 0x80))
                {
                    _ = input.SetReaderIndex(readerIndex + 1);
                    return result + ((b & 0x7FL) << shift);
                }

                result += (b & 0x7FL) << shift;
            }

            return ThrowHelper.ThrowHttp2Exception_DecodeULE128Decompression();
        }
    }

    /// <summary>
    /// HTTP/2 header types.
    /// </summary>
    internal enum HeaderType
    {
        RegularHeader,
        RequestPseudoHeader,
        ResponsePseudoHeader
    }

    interface ISink
    {
        void AppendToHeaderList(ICharSequence name, ICharSequence value);
        void Finish();
    }

    sealed class Http2HeadersSink : ISink
    {
        private readonly IHttp2Headers _headers;
        private readonly long _maxHeaderListSize;
        private readonly int _streamId;
        private readonly bool _validate;
        private long _headersLength;
        private bool _exceededMaxLength;
        private HeaderType? _previousType;
        private Http2Exception _validationException;

        public Http2HeadersSink(int streamId, IHttp2Headers headers, long maxHeaderListSize, bool validate)
        {
            _headers = headers;
            _maxHeaderListSize = maxHeaderListSize;
            _streamId = streamId;
            _validate = validate;
        }
        public void Finish()
        {
            if (_exceededMaxLength)
            {
                Http2CodecUtil.HeaderListSizeExceeded(_streamId, _maxHeaderListSize, true);
            }
            else if (_validationException is object)
            {
                ThrowValidationException();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowValidationException()
        {
            ExceptionDispatchInfo.Capture(_validationException).Throw();
        }

        public void AppendToHeaderList(ICharSequence name, ICharSequence value)
        {
            _headersLength += HpackHeaderField.SizeOf(name, value);
            _exceededMaxLength |= _headersLength > _maxHeaderListSize;

            if (_exceededMaxLength || _validationException is object)
            {
                // We don't store the header since we've already failed validation requirements.
                return;
            }

            if (_validate)
            {
                try
                {
                    _previousType = HpackDecoder.Validate(_streamId, name, _previousType);
                }
                catch (Http2Exception ex)
                {
                    _validationException = ex;
                    return;
                }
            }

            _ = _headers.Add(name, value);
        }
    }
}