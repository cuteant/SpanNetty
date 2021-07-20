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
 * Copyright (c) Microsoft. All rights reserved.
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com)
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Buffers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Internal;

    /// <summary>
    /// Description of algorithm for PageRun/PoolSubpage allocation from PoolChunk
    ///
    /// Notation: The following terms are important to understand the code
    /// > page  - a page is the smallest unit of memory chunk that can be allocated
    /// > run   - a run is a collection of pages
    /// > chunk - a chunk is a collection of runs
    /// > in this code chunkSize = maxPages / pageSize
    ///
    /// To begin we allocate a byte array of size = chunkSize
    /// Whenever a ByteBuf of given size needs to be created we search for the first position
    /// in the byte array that has enough empty space to accommodate the requested size and
    /// return a (long) handle that encodes this offset information, (this memory segment is then
    /// marked as reserved so it is always used by exactly one ByteBuf and no more)
    ///
    /// For simplicity all sizes are normalized according to <see cref="SizeClasses.Size2SizeIdx(int)"/> method.
    /// This ensures that when we request for memory segments of size > pageSize the normalizedCapacity
    /// equals the next nearest size in <see cref="SizeClasses"/>.
    ///
    ///
    ///  A chunk has the following layout:
    ///
    ///     /-----------------\
    ///     | run             |
    ///     |                 |
    ///     |                 |
    ///     |-----------------|
    ///     | run             |
    ///     |                 |
    ///     |-----------------|
    ///     | unalloctated    |
    ///     | (freed)         |
    ///     |                 |
    ///     |-----------------|
    ///     | subpage         |
    ///     |-----------------|
    ///     | unallocated     |
    ///     | (freed)         |
    ///     | ...             |
    ///     | ...             |
    ///     | ...             |
    ///     |                 |
    ///     |                 |
    ///     |                 |
    ///     \-----------------/
    ///
    ///
    /// handle:
    /// -------
    /// a handle is a long number, the bit layout of a run looks like:
    ///
    /// oooooooo ooooooos ssssssss ssssssue bbbbbbbb bbbbbbbb bbbbbbbb bbbbbbbb
    ///
    /// o: runOffset (page offset in the chunk), 15bit
    /// s: size (number of pages) of this run, 15bit
    /// u: isUsed?, 1bit
    /// e: isSubpage?, 1bit
    /// b: bitmapIdx of subpage, zero if it's not subpage, 32bit
    ///
    /// runsAvailMap:
    /// ------
    /// a map which manages all runs (used and not in used).
    /// For each run, the first runOffset and last runOffset are stored in runsAvailMap.
    /// key: runOffset
    /// value: handle
    ///
    /// runsAvail:
    /// ----------
    /// an array of <see cref="LongPriorityQueue"/>.
    /// Each queue manages same size of runs.
    /// Runs are sorted by offset, so that we always allocate runs with smaller offset.
    ///
    ///
    /// Algorithm:
    /// ----------
    ///
    ///   As we allocate runs, we update values stored in runsAvailMap and runsAvail so that the property is maintained.
    ///
    /// Initialization -
    ///  In the beginning we store the initial run which is the whole chunk.
    ///  The initial run:
    ///  runOffset = 0
    ///  size = chunkSize
    ///  isUsed = no
    ///  isSubpage = no
    ///  bitmapIdx = 0
    ///
    ///
    /// Algorithm: [allocateRun(size)]
    /// ----------
    /// 1) find the first avail run using in runsAvails according to size
    /// 2) if pages of run is larger than request pages then split it, and save the tailing run
    ///    for later using
    ///
    /// Algorithm: [allocateSubpage(size)]
    /// ----------
    /// 1) find a not full subpage according to size.
    ///    if it already exists just return, otherwise allocate a new PoolSubpage and call init()
    ///    note that this subpage object is added to subpagesPool in the PoolArena when we init() it
    /// 2) call subpage.allocate()
    ///
    /// Algorithm: [free(handle, length, nioBuffer)]
    /// ----------
    /// 1) if it is a subpage, return the slab back into this subpage
    /// 2) if the subpage is not used or it is a run, then start free this run
    /// 3) merge continuous avail runs
    /// 4) save the merged run
    /// </summary>
    internal sealed class PoolChunk<T> : IPoolChunkMetric
    {
        private const int SIZE_BIT_LENGTH = 15;
        private const int INUSED_BIT_LENGTH = 1;
        private const int SUBPAGE_BIT_LENGTH = 1;
        private const int BITMAP_IDX_BIT_LENGTH = 32;

        internal const int IS_SUBPAGE_SHIFT = BITMAP_IDX_BIT_LENGTH;
        internal const int IS_USED_SHIFT = SUBPAGE_BIT_LENGTH + IS_SUBPAGE_SHIFT;
        internal const int SIZE_SHIFT = INUSED_BIT_LENGTH + IS_USED_SHIFT;
        internal const int RUN_OFFSET_SHIFT = SIZE_BIT_LENGTH + SIZE_SHIFT;

        internal readonly PoolArena<T> Arena;
        internal readonly T Memory;
        internal readonly bool Unpooled;
        internal readonly int Offset;
        internal readonly IntPtr NativePointer;

        /// <summary>store the first page and last page of each avail run</summary>
        private LongLongHashMap _runsAvailMap;

        /// <summary>manage all avail runs</summary>
        private LongPriorityQueue[] _runsAvail;

        /// <summary>manage all subpages in this chunk</summary>
        private readonly PoolSubpage<T>[] _subpages;

        private readonly int _pageSize;
        private readonly int _pageShifts;
        private readonly int _chunkSize;

        internal int _freeBytes;

        internal PoolChunkList<T> Parent;
        internal PoolChunk<T> Prev;
        internal PoolChunk<T> Next;

        // TODO: Test if adding padding helps under contention
        //private long pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7;

        internal PoolChunk(PoolArena<T> arena, T memory, int pageSize, int pageShifts, int chunkSize, int maxPageIdx, int offset, IntPtr pointer)
        {
            Unpooled = false;
            Arena = arena;
            Memory = memory;
            _pageSize = pageSize;
            _pageShifts = pageShifts;
            _chunkSize = chunkSize;
            Offset = offset;
            NativePointer = pointer;
            _freeBytes = chunkSize;

            _runsAvail = NewRunsAvailqueueArray(maxPageIdx);
            _runsAvailMap = new LongLongHashMap(LongPriorityQueue.NO_VALUE);
            _subpages = new PoolSubpage<T>[chunkSize >> pageShifts];

            //insert initial run, offset = 0, pages = chunkSize / pageSize
            int pages = chunkSize >> pageShifts;
            long initHandle = (long)pages << SIZE_SHIFT;
            InsertAvailRun(0, pages, initHandle);
        }

        /// <summary>Creates a special chunk that is not pooled.</summary>
        internal PoolChunk(PoolArena<T> arena, T memory, int size, int offset, IntPtr pointer)
        {
            Unpooled = true;
            Arena = arena;
            Memory = memory;
            Offset = offset;
            NativePointer = pointer;
            _pageSize = 0;
            _pageShifts = 0;
            _runsAvailMap = null;
            _runsAvail = null;
            _subpages = null;
            _chunkSize = size;
        }

        private static LongPriorityQueue[] NewRunsAvailqueueArray(int size)
        {
            var queueArray = new LongPriorityQueue[size];
            for (int i = 0; i < queueArray.Length; i++)
            {
                queueArray[i] = new LongPriorityQueue();
            }
            return queueArray;
        }

        private void InsertAvailRun(int runOffset, int pages, long handle)
        {
            int pageIdxFloor = Arena.Pages2PageIdxFloor(pages);
            var queue = _runsAvail[pageIdxFloor];
            queue.Offer(handle);

            // insert first page of run
            InsertAvailRun0(runOffset, handle);
            if (pages > 1)
            {
                // insert last page of run
                InsertAvailRun0(LastPage(runOffset, pages), handle);
            }
        }

        private void InsertAvailRun0(int runOffset, long handle)
        {
            var pre = _runsAvailMap.Put(runOffset, handle);
            Debug.Assert(pre == LongPriorityQueue.NO_VALUE);
        }

        private void RemoveAvailRun(long handle)
        {
            int pageIdxFloor = Arena.Pages2PageIdxFloor(RunPages(handle));
            var queue = _runsAvail[pageIdxFloor];
            RemoveAvailRun(queue, handle);
        }

        private void RemoveAvailRun(LongPriorityQueue queue, long handle)
        {
            queue.Remove(handle);

            int runOffset = RunOffset(handle);
            int pages = RunPages(handle);
            // remove first page of run
            _runsAvailMap.Remove(runOffset);
            if (pages > 1)
            {
                // remove last page of run
                _runsAvailMap.Remove(LastPage(runOffset, pages));
            }
        }

        private static int LastPage(int runOffset, int pages)
        {
            return runOffset + pages - 1;
        }

        private long GetAvailRunByOffset(int runOffset)
        {
            return _runsAvailMap.Get(runOffset);
        }

        public int Usage
        {
            get
            {
                int freeBytes;
                lock (Arena)
                {
                    freeBytes = _freeBytes;
                }

                return GetUsage(freeBytes);
            }
        }

        private int GetUsage(int freeBytes)
        {
            if (0u >= (uint)freeBytes)
            {
                return 100;
            }

            int freePercentage = (int)(freeBytes * 100L / ChunkSize);
            if (0u >= (uint)freePercentage)
            {
                return 99;
            }

            return 100 - freePercentage;
        }

        internal bool Allocate(PooledByteBuffer<T> buf, int reqCapacity, int sizeIdx, PoolThreadCache<T> cache)
        {
            long handle;
            if (sizeIdx <= Arena._smallMaxSizeIdx)
            {
                // small
                handle = AllocateSubpage(sizeIdx);
                if (handle < 0L)
                {
                    return false;
                }
                Debug.Assert(IsSubpage(handle));
            }
            else
            {
                // normal
                // runSize must be multiple of pageSize
                int runSize = Arena.SizeIdx2Size(sizeIdx);
                handle = AllocateRun(runSize);
                if (handle < 0L)
                {
                    return false;
                }
            }

            InitBuf(buf, handle, reqCapacity, cache);
            return true;
        }

        private long AllocateRun(int runSize)
        {
            int pages = runSize >> _pageShifts;
            int pageIdx = Arena.Pages2PageIdx(pages);

            lock (_runsAvail)
            {
                // find first queue which has at least one big enough run
                int queueIdx = RunFirstBestFit(pageIdx);
                if (queueIdx == -1)
                {
                    return -1;
                }

                // get run with min offset in this queue
                var queue = _runsAvail[queueIdx];

                long handle = queue.Poll();

                Debug.Assert(handle != LongPriorityQueue.NO_VALUE && !IsUsed(handle), "invalid handle: " + handle);

                RemoveAvailRun(queue, handle);

                if (handle != -1)
                {
                    handle = SplitLargeRun(handle, pages);
                }

                _freeBytes -= RunSize(_pageShifts, handle);
                return handle;
            }
        }

        private int CalculateRunSize(int sizeIdx)
        {
            int maxElements = 1 << _pageShifts - SizeClasses.LOG2_QUANTUM;
            int runSize = 0;
            int nElements;

            int elemSize = Arena.SizeIdx2Size(sizeIdx);

            // find lowest common multiple of pageSize and elemSize
            do
            {
                runSize += _pageSize;
                nElements = runSize / elemSize;
            } while (nElements < maxElements && runSize != nElements * elemSize);

            while (nElements > maxElements)
            {
                runSize -= _pageSize;
                nElements = runSize / elemSize;
            }

            Debug.Assert(nElements > 0);
            Debug.Assert(runSize <= _chunkSize);
            Debug.Assert(runSize >= elemSize);

            return runSize;
        }

        private int RunFirstBestFit(int pageIdx)
        {
            if (_freeBytes == _chunkSize)
            {
                return Arena._nPSizes - 1;
            }
            for (int i = pageIdx; i < Arena._nPSizes; i++)
            {
                var queue = _runsAvail[i];
                if (queue is object && !queue.IsEmpty())
                {
                    return i;
                }
            }
            return -1;
        }

        private long SplitLargeRun(long handle, int needPages)
        {
            Debug.Assert(needPages > 0);

            int totalPages = RunPages(handle);
            Debug.Assert(needPages <= totalPages);

            int remPages = totalPages - needPages;

            if (remPages > 0)
            {
                int runOffset = RunOffset(handle);

                // keep track of trailing unused pages for later use
                int availOffset = runOffset + needPages;
                long availRun = ToRunHandle(availOffset, remPages, 0);
                InsertAvailRun(availOffset, remPages, availRun);

                // not avail
                return ToRunHandle(runOffset, needPages, 1);
            }

            //mark it as used
            handle |= 1L << IS_USED_SHIFT;
            return handle;
        }

        /// <summary>
        /// Create / initialize a new PoolSubpage of normCapacity. Any PoolSubpage created / initialized here is added to
        /// subpage pool in the PoolArena that owns this PoolChunk
        /// </summary>
        /// <param name="sizeIdx">sizeIdx of normalized size</param>
        /// <returns>index in memoryMap</returns>
        private long AllocateSubpage(int sizeIdx)
        {
            // Obtain the head of the PoolSubPage pool that is owned by the PoolArena and synchronize on it.
            // This is need as we may add it back and so alter the linked-list structure.
            PoolSubpage<T> head = Arena.FindSubpagePoolHead(sizeIdx);
            lock (head)
            {
                // allocate a new run
                int runSize = CalculateRunSize(sizeIdx);
                // runSize must be multiples of pageSize
                long runHandle = AllocateRun(runSize);
                if (runHandle < 0L)
                {
                    return -1;
                }

                int runOffset = RunOffset(runHandle);
                Debug.Assert(_subpages[runOffset] is null);
                int elemSize = Arena.SizeIdx2Size(sizeIdx);

                PoolSubpage<T> subpage = new(head, this, _pageShifts, runOffset,
                                   RunSize(_pageShifts, runHandle), elemSize);

                _subpages[runOffset] = subpage;
                return subpage.Allocate();
            }
        }

        /// <summary>
        /// Free a subpage or a run of pages When a subpage is freed from PoolSubpage, it might be added back to subpage pool
        /// of the owning PoolArena. If the subpage pool in PoolArena has at least one other PoolSubpage of given elemSize,
        /// we can completely free the owning Page so it is available for subsequent allocations
        /// </summary>
        /// <param name="handle">handle to free</param>
        /// <param name="normCapacity"></param>
        internal void Free(long handle, int normCapacity)
        {
            if (IsSubpage(handle))
            {
                int sizeIdx = Arena.Size2SizeIdx(normCapacity);
                PoolSubpage<T> head = Arena.FindSubpagePoolHead(sizeIdx);

                int sIdx = RunOffset(handle);
                PoolSubpage<T> subpage = _subpages[sIdx];
                Debug.Assert(subpage is object && subpage.DoNotDestroy);

                // Obtain the head of the PoolSubPage pool that is owned by the PoolArena and synchronize on it.
                // This is need as we may add it back and so alter the linked-list structure.
                lock (head)
                {
                    if (subpage.Free(head, BitmapIdx(handle)))
                    {
                        //the subpage is still used, do not free it
                        return;
                    }
                    Debug.Assert(!subpage.DoNotDestroy);
                    // Null out slot in the array as it was freed and we should not use it anymore.
                    _subpages[sIdx] = null;
                }
            }

            // start free run
            int pages = RunPages(handle);

            lock (_runsAvail)
            {
                // collapse continuous runs, successfully collapsed runs
                // will be removed from runsAvail and runsAvailMap
                long finalRun = CollapseRuns(handle);

                // set run as not used
                finalRun &= ~(1L << IS_USED_SHIFT);
                // if it is a subpage, set it to run
                finalRun &= ~(1L << IS_SUBPAGE_SHIFT);

                InsertAvailRun(RunOffset(finalRun), RunPages(finalRun), finalRun);
                _freeBytes += pages << _pageShifts;
            }
        }

        private long CollapseRuns(long handle)
        {
            return CollapseNext(CollapsePast(handle));
        }

        private long CollapsePast(long handle)
        {
            for (; ; )
            {
                int runOffset = RunOffset(handle);
                int runPages = RunPages(handle);

                var pastRun = GetAvailRunByOffset(runOffset - 1);
                if (0ul >= (ulong)(LongPriorityQueue.NO_VALUE - pastRun))
                {
                    return handle;
                }

                int pastOffset = RunOffset(pastRun);
                int pastPages = RunPages(pastRun);

                // is continuous
                if (pastRun != handle && pastOffset + pastPages == runOffset)
                {
                    // remove past run
                    RemoveAvailRun(pastRun);
                    handle = ToRunHandle(pastOffset, pastPages + runPages, 0);
                }
                else
                {
                    return handle;
                }
            }
        }

        private long CollapseNext(long handle)
        {
            for (; ; )
            {
                int runOffset = RunOffset(handle);
                int runPages = RunPages(handle);

                var nextRun = GetAvailRunByOffset(runOffset + runPages);
                if (0ul >= (ulong)(LongPriorityQueue.NO_VALUE - nextRun))
                {
                    return handle;
                }

                int nextOffset = RunOffset(nextRun);
                int nextPages = RunPages(nextRun);

                // is continuous
                if (nextRun != handle && runOffset + runPages == nextOffset)
                {
                    // remove next run
                    RemoveAvailRun(nextRun);
                    handle = ToRunHandle(runOffset, runPages + nextPages, 0);
                }
                else
                {
                    return handle;
                }
            }
        }

        private static long ToRunHandle(int runOffset, int runPages, int inUsed)
        {
            return (long)runOffset << RUN_OFFSET_SHIFT
                   | (long)runPages << SIZE_SHIFT
                   | (long)inUsed << IS_USED_SHIFT;
        }

        internal void InitBuf(PooledByteBuffer<T> buf, long handle, int reqCapacity, PoolThreadCache<T> threadCache)
        {
            if (IsRun(handle))
            {
                buf.Init(this, handle, RunOffset(handle) << _pageShifts,
                         reqCapacity, RunSize(_pageShifts, handle), Arena.Parent.ThreadCache<T>());
            }
            else
            {
                InitBufWithSubpage(buf, handle, reqCapacity, threadCache);
            }
        }

        internal void InitBufWithSubpage(PooledByteBuffer<T> buf, long handle, int reqCapacity, PoolThreadCache<T> threadCache)
        {
            int runOffset = RunOffset(handle);
            int bitmapIdx = BitmapIdx(handle);

            PoolSubpage<T> s = _subpages[runOffset];
            Debug.Assert(s.DoNotDestroy);
            Debug.Assert(reqCapacity <= s.ElemSize);

            buf.Init(this, handle,
                     (runOffset << _pageShifts) + bitmapIdx * s.ElemSize + Offset,
                     reqCapacity, s.ElemSize, threadCache);
        }

        public int ChunkSize => _chunkSize;

        public int FreeBytes
        {
            get
            {
                lock (Arena)
                {
                    return _freeBytes;
                }
            }
        }

        public override string ToString()
        {
            var freeBytes = FreeBytes;
            var sb = StringBuilderManager.Allocate()
                .Append("Chunk(")
                .Append(RuntimeHelpers.GetHashCode(this).ToString("X"))
                .Append(": ")
                .Append(Usage)
                .Append("%, ")
                .Append(_chunkSize - freeBytes)
                .Append('/')
                .Append(_chunkSize)
                .Append(')');
            return StringBuilderManager.ReturnAndFree(sb);
        }

        internal void Destroy() => Arena.DestroyChunk(this);

        internal static int RunOffset(long handle)
        {
            return (int)(handle >> RUN_OFFSET_SHIFT);
        }

        internal static int RunSize(int pageShifts, long handle)
        {
            return RunPages(handle) << pageShifts;
        }

        internal static int RunPages(long handle)
        {
            return (int)(handle >> SIZE_SHIFT & 0x7fff);
        }

        private static bool IsUsed(long handle)
        {
            return (handle >> IS_USED_SHIFT & 1L) == 1L;
        }

        private static bool IsRun(long handle)
        {
            return !IsSubpage(handle);
        }

        internal static bool IsSubpage(long handle)
        {
            return (handle >> IS_SUBPAGE_SHIFT & 1L) == 1L;
        }

        private static int BitmapIdx(long handle)
        {
            return (int)handle;
        }
    }
}