// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    sealed class HpackDynamicTable
    {
        // a circular queue of header fields
        HpackHeaderField[] hpackHeaderFields;
        int head;
        int tail;
        long size;
        long capacity = -1L; // ensure setCapacity creates the array

        /// <summary>
        /// Creates a new dynamic table with the specified initial capacity.
        /// </summary>
        /// <param name="initialCapacity"></param>
        internal HpackDynamicTable(long initialCapacity)
        {
            this.SetCapacity(initialCapacity);
        }

        /// <summary>
        /// Return the number of header fields in the dynamic table.
        /// </summary>
        public int Length()
        {
            int length;
            if (this.head < this.tail)
            {
                length = this.hpackHeaderFields.Length - this.tail + this.head;
            }
            else
            {
                length = this.head - this.tail;
            }

            return length;
        }

        /// <summary>
        /// Return the current size of the dynamic table. This is the sum of the size of the entries.
        /// </summary>
        /// <returns></returns>
        public long Size() => this.size;

        /// <summary>
        /// Return the maximum allowable size of the dynamic table.
        /// </summary>
        public long Capacity() => this.capacity;

        /// <summary>
        /// Return the header field at the given index. The first and newest entry is always at index 1,
        /// and the oldest entry is at the index length().
        /// </summary>
        /// <param name="index"></param>
        public HpackHeaderField GetEntry(int index)
        {
            uint uIndex = (uint)index;
            if (0u >= uIndex || uIndex > (uint)this.Length())
            {
                ThrowHelper.ThrowIndexOutOfRangeException();
            }

            int i = this.head - index;
            if (i < 0)
            {
                return this.hpackHeaderFields[i + this.hpackHeaderFields.Length];
            }
            else
            {
                return this.hpackHeaderFields[i];
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
            if (headerSize > this.capacity)
            {
                this.Clear();
                return;
            }

            while (this.capacity - this.size < headerSize)
            {
                this.Remove();
            }

            this.hpackHeaderFields[this.head++] = header;
            this.size += header.Size();
            if (this.head == this.hpackHeaderFields.Length)
            {
                this.head = 0;
            }
        }

        /// <summary>
        /// Remove and return the oldest header field from the dynamic table.
        /// </summary>
        public HpackHeaderField Remove()
        {
            HpackHeaderField removed = this.hpackHeaderFields[this.tail];
            if (removed is null)
            {
                return null;
            }

            this.size -= removed.Size();
            this.hpackHeaderFields[this.tail++] = null;
            if (this.tail == this.hpackHeaderFields.Length)
            {
                this.tail = 0;
            }

            return removed;
        }

        /// <summary>
        /// Remove all entries from the dynamic table.
        /// </summary>
        public void Clear()
        {
            while (this.tail != this.head)
            {
                this.hpackHeaderFields[this.tail++] = null;
                if (this.tail == this.hpackHeaderFields.Length)
                {
                    this.tail = 0;
                }
            }

            this.head = 0;
            this.tail = 0;
            this.size = 0;
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
            if (this.capacity == capacity) { return; }

            this.capacity = capacity;

            if (0ul >= (ulong)capacity)
            {
                this.Clear();
            }
            else
            {
                // initially _size will be 0 so remove won't be called
                while (this.size > capacity)
                {
                    this.Remove();
                }
            }

            int maxEntries = (int)(capacity / HpackHeaderField.HeaderEntryOverhead);
            if (capacity % HpackHeaderField.HeaderEntryOverhead != 0)
            {
                maxEntries++;
            }

            // check if capacity change requires us to reallocate the array
            if (this.hpackHeaderFields is object && this.hpackHeaderFields.Length == maxEntries)
            {
                return;
            }

            HpackHeaderField[] tmp = new HpackHeaderField[maxEntries];

            // initially length will be 0 so there will be no copy
            int len = this.Length();
            int cursor = this.tail;
            for (int i = 0; i < len; i++)
            {
                HpackHeaderField entry = this.hpackHeaderFields[cursor++];
                tmp[i] = entry;
                if (cursor == this.hpackHeaderFields.Length)
                {
                    cursor = 0;
                }
            }

            this.tail = 0;
            this.head = this.tail + len;
            this.hpackHeaderFields = tmp;
        }
    }
}