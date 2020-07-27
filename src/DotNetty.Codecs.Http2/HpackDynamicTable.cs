// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    sealed class HpackDynamicTable
    {
        // a circular queue of header fields
        private HpackHeaderField[] _hpackHeaderFields;
        private int _head;
        private int _tail;
        private long _size;
        private long _capacity = -1L; // ensure setCapacity creates the array

        /// <summary>
        /// Creates a new dynamic table with the specified initial capacity.
        /// </summary>
        /// <param name="initialCapacity"></param>
        internal HpackDynamicTable(long initialCapacity)
        {
            SetCapacity(initialCapacity);
        }

        /// <summary>
        /// Return the number of header fields in the dynamic table.
        /// </summary>
        public int Length()
        {
            int length;
            if (_head < _tail)
            {
                length = _hpackHeaderFields.Length - _tail + _head;
            }
            else
            {
                length = _head - _tail;
            }

            return length;
        }

        /// <summary>
        /// Return the current size of the dynamic table. This is the sum of the size of the entries.
        /// </summary>
        /// <returns></returns>
        public long Size() => _size;

        /// <summary>
        /// Return the maximum allowable size of the dynamic table.
        /// </summary>
        public long Capacity() => _capacity;

        /// <summary>
        /// Return the header field at the given index. The first and newest entry is always at index 1,
        /// and the oldest entry is at the index length().
        /// </summary>
        /// <param name="index"></param>
        public HpackHeaderField GetEntry(int index)
        {
            uint uIndex = (uint)index;
            var len = Length();
            if (0u >= uIndex || uIndex > (uint)len)
            {
                ThrowHelper.ThrowIndexOutOfRangeException(index, len);
            }

            int i = _head - index;
            if (i < 0)
            {
                return _hpackHeaderFields[i + _hpackHeaderFields.Length];
            }
            else
            {
                return _hpackHeaderFields[i];
            }
        }

        /// <summary>
        /// Add the header field to the dynamic table. Entries are evicted from the dynamic table until
        /// the size of the table and the new header field is less than or equal to the table's capacity.
        /// If the size of the new entry is larger than the table's capacity, the dynamic table will be
        /// cleared.
        /// </summary>
        /// <param name="header"></param>
        public void Add(HpackHeaderField header)
        {
            int headerSize = header.Size();
            if (headerSize > _capacity)
            {
                Clear();
                return;
            }

            while (_capacity - _size < headerSize)
            {
                _ = Remove();
            }

            _hpackHeaderFields[_head++] = header;
            _size += header.Size();
            if (_head == _hpackHeaderFields.Length)
            {
                _head = 0;
            }
        }

        /// <summary>
        /// Remove and return the oldest header field from the dynamic table.
        /// </summary>
        public HpackHeaderField Remove()
        {
            HpackHeaderField removed = _hpackHeaderFields[_tail];
            if (removed is null)
            {
                return null;
            }

            _size -= removed.Size();
            _hpackHeaderFields[_tail++] = null;
            if (_tail == _hpackHeaderFields.Length)
            {
                _tail = 0;
            }

            return removed;
        }

        /// <summary>
        /// Remove all entries from the dynamic table.
        /// </summary>
        public void Clear()
        {
            while (_tail != _head)
            {
                _hpackHeaderFields[_tail++] = null;
                if (_tail == _hpackHeaderFields.Length)
                {
                    _tail = 0;
                }
            }

            _head = 0;
            _tail = 0;
            _size = 0;
        }

        /// <summary>
        /// Set the maximum size of the dynamic table. Entries are evicted from the dynamic table until
        /// the size of the table is less than or equal to the maximum size.
        /// </summary>
        /// <param name="capacity"></param>
        public void SetCapacity(long capacity)
        {
            if (capacity < Http2CodecUtil.MinHeaderTableSize || capacity > Http2CodecUtil.MaxHeaderTableSize)
            {
                ThrowHelper.ThrowArgumentException_InvalidCapacity(capacity);
            }

            // initially capacity will be -1 so init won't return here
            if (_capacity == capacity) { return; }

            _capacity = capacity;

            if (0ul >= (ulong)capacity)
            {
                Clear();
            }
            else
            {
                // initially _size will be 0 so remove won't be called
                while (_size > capacity)
                {
                    _ = Remove();
                }
            }

            int maxEntries = (int)(capacity / HpackHeaderField.HeaderEntryOverhead);
            if (capacity % HpackHeaderField.HeaderEntryOverhead != 0)
            {
                maxEntries++;
            }

            // check if capacity change requires us to reallocate the array
            if (_hpackHeaderFields is object && _hpackHeaderFields.Length == maxEntries)
            {
                return;
            }

            HpackHeaderField[] tmp = new HpackHeaderField[maxEntries];

            // initially length will be 0 so there will be no copy
            int len = Length();
            int cursor = _tail;
            for (int i = 0; i < len; i++)
            {
                HpackHeaderField entry = _hpackHeaderFields[cursor++];
                tmp[i] = entry;
                if (cursor == _hpackHeaderFields.Length)
                {
                    cursor = 0;
                }
            }

            _tail = 0;
            _head = _tail + len;
            _hpackHeaderFields = tmp;
        }
    }
}