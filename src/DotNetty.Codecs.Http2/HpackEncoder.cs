// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Diagnostics;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// An HPACK encoder.
    /// 
    /// <para>Implementation note:  This class is security sensitive, and depends on users correctly identifying their headers
    /// as security sensitive or not.  If a header is considered not sensitive, methods names "insensitive" are used which
    /// are fast, but don't provide any security guarantees.</para>
    /// </summary>
    sealed class HpackEncoder
    {
        internal const int HuffCodeThreshold = 512;
        static readonly Encoding Enc = Encoding.GetEncoding("ISO-8859-1");

        // a linked hash map of header fields
        private readonly HeaderEntry[] _headerFields;
        private readonly HeaderEntry _head = new HeaderEntry(-1, AsciiString.Empty, AsciiString.Empty, int.MaxValue, null);
        private readonly HpackHuffmanEncoder _hpackHuffmanEncoder = new HpackHuffmanEncoder();
        private readonly byte _hashMask;
        private readonly bool _ignoreMaxHeaderListSize;
        private readonly int _huffCodeThreshold;
        private long _size;
        private long _maxHeaderTableSize;
        private long _maxHeaderListSize;

        /// <summary>
        /// Creates a new encoder.
        /// </summary>
        internal HpackEncoder()
            : this(false)
        {
        }

        /// <summary>
        /// Creates a new encoder.
        /// </summary>
        public HpackEncoder(bool ignoreMaxHeaderListSize)
            : this(ignoreMaxHeaderListSize, 16, HuffCodeThreshold)
        {
        }

        /// <summary>
        /// Creates a new encoder.
        /// </summary>
        public HpackEncoder(bool ignoreMaxHeaderListSize, int arraySizeHint, int huffCodeThreshold)
        {
            _ignoreMaxHeaderListSize = ignoreMaxHeaderListSize;
            _maxHeaderTableSize = Http2CodecUtil.DefaultHeaderTableSize;
            _maxHeaderListSize = Http2CodecUtil.MaxHeaderListSize;
            // Enforce a bound of [2, 128] because hashMask is a byte. The max possible value of hashMask is one less
            // than the length of this array, and we want the mask to be > 0.
            _headerFields = new HeaderEntry[MathUtil.FindNextPositivePowerOfTwo(Math.Max(2, Math.Min(arraySizeHint, 128)))];
            _hashMask = (byte)(_headerFields.Length - 1);
            _head.Before = _head.After = _head;
            _huffCodeThreshold = huffCodeThreshold;
        }

        /// <summary>
        /// Encode the header field into the header block.
        /// The given <see cref="ICharSequence"/>s must be immutable!
        /// </summary>
        /// <param name="streamId"></param>
        /// <param name="output"></param>
        /// <param name="headers"></param>
        /// <param name="sensitivityDetector"></param>
        public void EncodeHeaders(int streamId, IByteBuffer output, IHttp2Headers headers, ISensitivityDetector sensitivityDetector)
        {
            if (_ignoreMaxHeaderListSize)
            {
                EncodeHeadersIgnoreMaxHeaderListSize(output, headers, sensitivityDetector);
            }
            else
            {
                EncodeHeadersEnforceMaxHeaderListSize(streamId, output, headers, sensitivityDetector);
            }
        }

        void EncodeHeadersEnforceMaxHeaderListSize(int streamId, IByteBuffer output, IHttp2Headers headers, ISensitivityDetector sensitivityDetector)
        {
            long headerSize = 0;
            // To ensure we stay consistent with our peer check the size is valid before we potentially modify HPACK state.
            foreach (HeaderEntry<ICharSequence, ICharSequence> header in headers)
            {
                ICharSequence name = header.Key;
                ICharSequence value = header.Value;
                // OK to increment now and check for bounds after because this value is limited to unsigned int and will not
                // overflow.
                headerSize += HpackHeaderField.SizeOf(name, value);
                if (headerSize > _maxHeaderListSize)
                {
                    Http2CodecUtil.HeaderListSizeExceeded(streamId, _maxHeaderListSize, false);
                }
            }

            EncodeHeadersIgnoreMaxHeaderListSize(@output, headers, sensitivityDetector);
        }

        void EncodeHeadersIgnoreMaxHeaderListSize(IByteBuffer output, IHttp2Headers headers, ISensitivityDetector sensitivityDetector)
        {
            foreach (HeaderEntry<ICharSequence, ICharSequence> header in headers)
            {
                ICharSequence name = header.Key;
                ICharSequence value = header.Value;
                EncodeHeader(output, name, value, sensitivityDetector.IsSensitive(name, value), HpackHeaderField.SizeOf(name, value));
            }
        }

        /// <summary>
        /// Encode the header field into the header block.
        /// The given <see cref="ICharSequence"/>s must be immutable!
        /// </summary>
        /// <param name="output"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="sensitive"></param>
        /// <param name="headerSize"></param>
        void EncodeHeader(IByteBuffer output, ICharSequence name, ICharSequence value, bool sensitive, long headerSize)
        {
            // If the header value is sensitive then it must never be indexed
            if (sensitive)
            {
                int nameIndex = GetNameIndex(name);
                EncodeLiteral(output, name, value, HpackUtil.IndexType.Never, nameIndex);
                return;
            }

            // If the peer will only use the static table
            if (0ul >= (ulong)_maxHeaderTableSize)
            {
                int staticTableIndex = HpackStaticTable.GetIndexInsensitive(name, value);
                if (staticTableIndex == -1)
                {
                    int nameIndex = HpackStaticTable.GetIndex(name);
                    EncodeLiteral(output, name, value, HpackUtil.IndexType.None, nameIndex);
                }
                else
                {
                    EncodeInteger(output, 0x80, 7, staticTableIndex);
                }

                return;
            }

            // If the headerSize is greater than the max table size then it must be encoded literally
            if (headerSize > _maxHeaderTableSize)
            {
                int nameIndex = GetNameIndex(name);
                EncodeLiteral(output, name, value, HpackUtil.IndexType.None, nameIndex);
                return;
            }

            HeaderEntry headerField = GetEntryInsensitive(name, value);
            if (headerField is object)
            {
                int index = GetIndex(headerField.Index) + HpackStaticTable.Length;
                // Section 6.1. Indexed Header Field Representation
                EncodeInteger(output, 0x80, 7, index);
            }
            else
            {
                int staticTableIndex = HpackStaticTable.GetIndexInsensitive(name, value);
                if (staticTableIndex != -1)
                {
                    // Section 6.1. Indexed Header Field Representation
                    EncodeInteger(output, 0x80, 7, staticTableIndex);
                }
                else
                {
                    EnsureCapacity(headerSize);
                    EncodeLiteral(output, name, value, HpackUtil.IndexType.Incremental, GetNameIndex(name));
                    Add(name, value, headerSize);
                }
            }
        }

        /// <summary>
        /// Set the maximum table size.
        /// </summary>
        /// <param name="output"></param>
        /// <param name="maxHeaderTableSize"></param>
        public void SetMaxHeaderTableSize(IByteBuffer output, long maxHeaderTableSize)
        {
            if (maxHeaderTableSize < Http2CodecUtil.MinHeaderTableSize || maxHeaderTableSize > Http2CodecUtil.MaxHeaderTableSize)
            {
                ThrowHelper.ThrowConnectionError_SetMaxHeaderTableSize(maxHeaderTableSize);
            }

            if (_maxHeaderTableSize == maxHeaderTableSize)
            {
                return;
            }

            _maxHeaderTableSize = maxHeaderTableSize;
            EnsureCapacity(0);
            // Casting to integer is safe as we verified the maxHeaderTableSize is a valid unsigned int.
            EncodeInteger(output, 0x20, 5, maxHeaderTableSize);
        }

        /// <summary>
        /// Return the maximum table size.
        /// </summary>
        /// <returns></returns>
        public long GetMaxHeaderTableSize()
        {
            return _maxHeaderTableSize;
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
        /// Encode integer according to <a href="https://tools.ietf.org/html/rfc7541#section-5.1">Section 5.1</a>.
        /// </summary>
        /// <param name="output"></param>
        /// <param name="mask"></param>
        /// <param name="n"></param>
        /// <param name="idx"></param>
        static void EncodeInteger(IByteBuffer output, int mask, int n, int idx)
        {
            EncodeInteger(output, mask, n, (long)idx);
        }

        /// <summary>
        /// Encode integer according to <a href="https://tools.ietf.org/html/rfc7541#section-5.1">Section 5.1</a>.
        /// </summary>
        /// <param name="output"></param>
        /// <param name="mask"></param>
        /// <param name="n"></param>
        /// <param name="idx"></param>
        static void EncodeInteger(IByteBuffer output, int mask, int n, long idx)
        {
            Debug.Assert(n >= 0 && n <= 8, "N: " + n);
            int nbits = 0xFF.RightUShift(8 - n);
            if (idx < nbits)
            {
                _ = output.WriteByte((int)((uint)mask | (uint)idx));
            }
            else
            {
                _ = output.WriteByte(mask | nbits);
                long length = idx - nbits;
                for (; (length & ~0x7F) != 0; length = length.RightUShift(7))
                {
                    _ = output.WriteByte((int)((length & 0x7F) | 0x80));
                }

                _ = output.WriteByte((int)length);
            }
        }

        /// <summary>
        /// Encode string literal according to Section 5.2.
        /// </summary>
        /// <param name="output"></param>
        /// <param name="str"></param>
        void EncodeStringLiteral(IByteBuffer output, ICharSequence str)
        {
            int huffmanLength;
            uint ustrLen = (uint)str.Count;
            if (ustrLen >= (uint)_huffCodeThreshold &&
                (uint)(huffmanLength = _hpackHuffmanEncoder.GetEncodedLength(str)) < ustrLen)
            {
                EncodeInteger(output, 0x80, 7, huffmanLength);
                _hpackHuffmanEncoder.Encode(output, str);
            }
            else
            {
                EncodeInteger(output, 0x00, 7, str.Count);
                if (str is AsciiString asciiString)
                {
                    // Fast-path
                    _ = output.WriteBytes(asciiString.Array, asciiString.Offset, asciiString.Count);
                }
                else
                {
                    // Only ASCII is allowed in http2 headers, so its fine to use this.
                    // https://tools.ietf.org/html/rfc7540#section-8.1.2
                    _ = output.WriteCharSequence(str, Enc);
                }
            }
        }

        /// <summary>
        /// Encode literal header field according to Section 6.2.
        /// </summary>
        /// <param name="output"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="indexType"></param>
        /// <param name="nameIndex"></param>
        void EncodeLiteral(IByteBuffer output, ICharSequence name, ICharSequence value, HpackUtil.IndexType indexType, int nameIndex)
        {
            bool nameIndexValid = nameIndex != -1;
            switch (indexType)
            {
                case HpackUtil.IndexType.Incremental:
                    EncodeInteger(output, 0x40, 6, nameIndexValid ? nameIndex : 0);
                    break;
                case HpackUtil.IndexType.None:
                    EncodeInteger(output, 0x00, 4, nameIndexValid ? nameIndex : 0);
                    break;
                case HpackUtil.IndexType.Never:
                    EncodeInteger(output, 0x10, 4, nameIndexValid ? nameIndex : 0);
                    break;
                default:
                    ThrowHelper.ThrowException_ShouldNotReachHere();
                    break;
            }

            if (!nameIndexValid)
            {
                EncodeStringLiteral(output, name);
            }

            EncodeStringLiteral(output, value);
        }

        int GetNameIndex(ICharSequence name)
        {
            int index = HpackStaticTable.GetIndex(name);
            if (index == -1)
            {
                index = GetIndex(name);
                if (index >= 0)
                {
                    index += HpackStaticTable.Length;
                }
            }

            return index;
        }

        /// <summary>
        /// Ensure that the dynamic table has enough room to hold 'headerSize' more bytes. Removes the
        /// oldest entry from the dynamic table until sufficient space is available.
        /// </summary>
        /// <param name="headerSize"></param>
        void EnsureCapacity(long headerSize)
        {
            while (_maxHeaderTableSize - _size < headerSize)
            {
                int index = Length();
                if (0u >= (uint)index)
                {
                    break;
                }

                _ = Remove();
            }
        }

        /// <summary>
        /// Return the number of header fields in the dynamic table. Exposed for testing.
        /// </summary>
        internal int Length()
        {
            return 0ul >= (ulong)_size ? 0 : _head.After.Index - _head.Before.Index + 1;
        }

        /// <summary>
        /// Return the size of the dynamic table. Exposed for testing.
        /// </summary>
        internal long Size()
        {
            return _size;
        }

        /// <summary>
        /// Return the header field at the given index. Exposed for testing.
        /// </summary>
        /// <param name="index"></param>
        internal HpackHeaderField GetHeaderField(int index)
        {
            HeaderEntry entry = _head;
            while (index-- >= 0)
            {
                entry = entry.Before;
            }

            return entry;
        }

        /// <summary>
        /// Returns the header entry with the lowest index value for the header field. Returns null if
        /// header field is not in the dynamic table.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        HeaderEntry GetEntryInsensitive(ICharSequence name, ICharSequence value)
        {
            if (0u >= (uint)Length() || name is null || value is null)
            {
                return null;
            }

            int h = AsciiString.GetHashCode(name);
            int i = Index(h);
            for (HeaderEntry e = _headerFields[i]; e is object; e = e.Next)
            {
                // Check the value before then name, as it is more likely the value will be different incase there is no
                // match.
                if (e.Hash == h && HpackUtil.EqualsVariableTime(value, e._value) && HpackUtil.EqualsVariableTime(name, e._name))
                {
                    return e;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the lowest index value for the header field name in the dynamic table. Returns -1 if
        /// the header field name is not in the dynamic table.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        int GetIndex(ICharSequence name)
        {
            if (0u >= (uint)Length() || name is null)
            {
                return -1;
            }

            int h = AsciiString.GetHashCode(name);
            int i = Index(h);
            for (HeaderEntry e = _headerFields[i]; e is object; e = e.Next)
            {
                if (e.Hash == h && HpackUtil.EqualsConstantTime(name, e._name) != 0)
                {
                    return GetIndex(e.Index);
                }
            }

            return -1;
        }

        /// <summary>
        /// Compute the index into the dynamic table given the index in the header entry.
        /// </summary>
        /// <param name="index"></param>
        int GetIndex(int index)
        {
            return index == -1 ? -1 : index - _head.Before.Index + 1;
        }

        /// <summary>
        /// Add the header field to the dynamic table. Entries are evicted from the dynamic table until
        /// the size of the table and the new header field is less than the table's maxHeaderTableSize. If the size
        /// of the new entry is larger than the table's maxHeaderTableSize, the dynamic table will be cleared.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="headerSize"></param>
        void Add(ICharSequence name, ICharSequence value, long headerSize)
        {
            // Clear the table if the header field size is larger than the maxHeaderTableSize.
            if (headerSize > _maxHeaderTableSize)
            {
                Clear();
                return;
            }

            // Evict oldest entries until we have enough maxHeaderTableSize.
            while (_maxHeaderTableSize - _size < headerSize)
            {
                _ = Remove();
            }

            int h = AsciiString.GetHashCode(name);
            int i = Index(h);
            HeaderEntry old = _headerFields[i];
            HeaderEntry e = new HeaderEntry(h, name, value, _head.Before.Index - 1, old);
            _headerFields[i] = e;
            e.AddBefore(_head);
            _size += headerSize;
        }

        /// <summary>
        /// Remove and return the oldest header field from the dynamic table.
        /// </summary>
        /// <returns></returns>
        HpackHeaderField Remove()
        {
            if (0ul >= (ulong)_size)
            {
                return null;
            }

            HeaderEntry eldest = _head.After;
            int h = eldest.Hash;
            int i = Index(h);
            HeaderEntry prev = _headerFields[i];
            HeaderEntry e = prev;
            while (e is object)
            {
                HeaderEntry next = e.Next;
                if (e == eldest)
                {
                    if (prev == eldest)
                    {
                        _headerFields[i] = next;
                    }
                    else
                    {
                        prev.Next = next;
                    }

                    eldest.Remove();
                    _size -= eldest.Size();
                    return eldest;
                }

                prev = e;
                e = next;
            }

            return null;
        }

        /// <summary>
        /// Remove all entries from the dynamic table.
        /// </summary>
        void Clear()
        {
            for (var i = 0; i < _headerFields.Length; i++)
            {
                _headerFields[i] = null;
            }

            //Arrays.fill(headerFields, null);
            _head.Before = _head.After = _head;
            _size = 0;
        }

        /// <summary>
        /// Returns the index into the hash table for the hash code h.
        /// </summary>
        /// <param name="h"></param>
        /// <returns></returns>
        int Index(int h)
        {
            return h & _hashMask;
        }

        /// <summary>
        /// A linked hash map HpackHeaderField entry.
        /// </summary>
        sealed class HeaderEntry : HpackHeaderField
        {
            // These fields comprise the doubly linked list used for iteration.
            internal HeaderEntry Before;
            internal HeaderEntry After;

            // These fields comprise the chained list for header fields with the same hash.
            internal HeaderEntry Next;
            internal readonly int Hash;

            // This is used to compute the index in the dynamic table.
            internal readonly int Index;

            /// <summary>
            /// Creates new entry.
            /// </summary>
            /// <param name="hash"></param>
            /// <param name="name"></param>
            /// <param name="value"></param>
            /// <param name="index"></param>
            /// <param name="next"></param>
            internal HeaderEntry(int hash, ICharSequence name, ICharSequence value, int index, HeaderEntry next)
                : base(name, value)
            {
                Index = index;
                Hash = hash;
                Next = next;
            }

            /// <summary>
            /// Removes this entry from the linked list.
            /// </summary>
            internal void Remove()
            {
                Before.After = After;
                After.Before = Before;
                Before = null; // null references to prevent nepotism in generational GC.
                After = null;
                Next = null;
            }

            /// <summary>
            /// Inserts this entry before the specified existing entry in the list.
            /// </summary>
            /// <param name="existingEntry"></param>
            internal void AddBefore(HeaderEntry existingEntry)
            {
                After = existingEntry;
                Before = existingEntry.Before;
                Before.After = this;
                After.Before = this;
            }
        }
    }
}