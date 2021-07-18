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
 *   https://github.com/cuteant/spannetty
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Buffers
{
    using System.Diagnostics;

    /// <summary>
    /// SizeClasses requires {@code pageShifts} to be defined prior to inclusion,
    /// and it in turn defines:
    /// <para>
    ///   LOG2_SIZE_CLASS_GROUP: Log of size class count for each size doubling.
    ///   LOG2_MAX_LOOKUP_SIZE: Log of max size class in the lookup table.
    ///   sizeClasses: Complete table of [index, log2Group, log2Delta, nDelta, isMultiPageSize,
    ///                 isSubPage, log2DeltaLookup] tuples.
    ///     index: Size class index.
    ///     log2Group: Log of group base size (no deltas added).
    ///     log2Delta: Log of delta to previous size class.
    ///     nDelta: Delta multiplier.
    ///     isMultiPageSize: 'yes' if a multiple of the page size, 'no' otherwise.
    ///     isSubPage: 'yes' if a subpage size class, 'no' otherwise.
    ///     log2DeltaLookup: Same as log2Delta if a lookup table size class, 'no'
    ///                      otherwise.
    /// </para>
    /// <para>
    ///   nSubpages: Number of subpages size classes.
    ///   nSizes: Number of size classes.
    ///   nPSizes: Number of size classes that are multiples of pageSize.
    ///
    ///   smallMaxSizeIdx: Maximum small size class index.
    ///
    ///   lookupMaxclass: Maximum size class included in lookup table.
    ///   log2NormalMinClass: Log of minimum normal size class.
    /// </para>
    /// <para>
    ///   The first size class and spacing are 1 &lt;&lt; LOG2_QUANTUM.
    ///   Each group has 1 &lt;&lt; LOG2_SIZE_CLASS_GROUP of size classes.
    ///
    ///   size = 1 &lt;&lt; log2Group + nDelta /// (1 &lt;&lt; log2Delta)
    ///
    ///   The first size class has an unusual encoding, because the size has to be
    ///   split between group and delta*nDelta.
    ///
    ///   If pageShift = 13, sizeClasses looks like this:
    ///
    ///   (index, log2Group, log2Delta, nDelta, isMultiPageSize, isSubPage, log2DeltaLookup)
    /// </para>
    /// <para>
    ///   ( 0,     4,        4,         0,       no,             yes,        4)
    ///   ( 1,     4,        4,         1,       no,             yes,        4)
    ///   ( 2,     4,        4,         2,       no,             yes,        4)
    ///   ( 3,     4,        4,         3,       no,             yes,        4)
    /// </para>
    /// <para>
    ///   ( 4,     6,        4,         1,       no,             yes,        4)
    ///   ( 5,     6,        4,         2,       no,             yes,        4)
    ///   ( 6,     6,        4,         3,       no,             yes,        4)
    ///   ( 7,     6,        4,         4,       no,             yes,        4)
    /// </para>
    /// <para>
    ///   ( 8,     7,        5,         1,       no,             yes,        5)
    ///   ( 9,     7,        5,         2,       no,             yes,        5)
    ///   ( 10,    7,        5,         3,       no,             yes,        5)
    ///   ( 11,    7,        5,         4,       no,             yes,        5)
    ///   ...
    ///   ...
    ///   ( 72,    23,       21,        1,       yes,            no,        no)
    ///   ( 73,    23,       21,        2,       yes,            no,        no)
    ///   ( 74,    23,       21,        3,       yes,            no,        no)
    ///   ( 75,    23,       21,        4,       yes,            no,        no)
    /// </para>
    ///   ( 76,    24,       22,        1,       yes,            no,        no)
    /// </summary>
    internal abstract class SizeClasses : ISizeClassesMetric
    {
        internal const int LOG2_QUANTUM = 4;

        private const int LOG2_SIZE_CLASS_GROUP = 2;
        private const int LOG2_MAX_LOOKUP_SIZE = 12;

        private const int INDEX_IDX = 0;
        private const int LOG2GROUP_IDX = 1;
        private const int LOG2DELTA_IDX = 2;
        private const int NDELTA_IDX = 3;
        private const int PAGESIZE_IDX = 4;
        private const int SUBPAGE_IDX = 5;
        private const int LOG2_DELTA_LOOKUP_IDX = 6;

        private const sbyte c_no = 0, c_yes = 1;

        internal readonly int PageSize;
        internal readonly int PageShifts;
        internal readonly int ChunkSize;
        private readonly int _directMemoryCacheAlignment;

        internal readonly int _nSizes;
        internal int _nSubpages;
        internal int _nPSizes;

        internal int _smallMaxSizeIdx;

        private int _lookupMaxSize;

        private readonly short[][] _sizeClasses;

        private readonly int[] _pageIdx2SizeTab;

        // lookup table for sizeIdx <= smallMaxSizeIdx
        private readonly int[] _sizeIdx2SizeTab;

        // lookup table used for size <= lookupMaxclass
        // spacing is 1 << LOG2_QUANTUM, so the size of array is lookupMaxclass >> LOG2_QUANTUM
        private readonly int[] _size2IdxTab;

        protected SizeClasses(int pageSize, int pageShifts, int chunkSize, int directMemoryCacheAlignment)
        {
            PageSize = pageSize;
            PageShifts = pageShifts;
            ChunkSize = chunkSize;
            _directMemoryCacheAlignment = directMemoryCacheAlignment;

            int group = PoolThreadCache<byte[]>.Log2(chunkSize) + 1 - LOG2_QUANTUM;

            // generate size classes
            // [index, log2Group, log2Delta, nDelta, isMultiPageSize, isSubPage, log2DeltaLookup]
            var count = group << LOG2_SIZE_CLASS_GROUP;
            _sizeClasses = new short[count][];
            for (int idx = 0; idx < count; idx++)
            {
                _sizeClasses[idx] = new short[7];
            }
            _nSizes = InitSizeClasses();

            // generate lookup table
            _sizeIdx2SizeTab = new int[_nSizes];
            _pageIdx2SizeTab = new int[_nPSizes];
            Idx2SizeTab(_sizeIdx2SizeTab, _pageIdx2SizeTab);

            _size2IdxTab = new int[_lookupMaxSize >> LOG2_QUANTUM];
            Size2IdxTab(_size2IdxTab);
        }

        private int InitSizeClasses()
        {
            int normalMaxSize = -1;

            int index = 0;
            int size = 0;

            int log2Group = LOG2_QUANTUM;
            int log2Delta = LOG2_QUANTUM;
            int ndeltaLimit = 1 << LOG2_SIZE_CLASS_GROUP;

            // First small group, nDelta start at 0.
            // first size class is 1 << LOG2_QUANTUM
            int nDelta = 0;
            while (nDelta < ndeltaLimit)
            {
                size = SizeClass(index++, log2Group, log2Delta, nDelta++);
            }
            log2Group += LOG2_SIZE_CLASS_GROUP;

            // All remaining groups, nDelta start at 1.
            while (size < ChunkSize)
            {
                nDelta = 1;

                while (nDelta <= ndeltaLimit && size < ChunkSize)
                {
                    size = SizeClass(index++, log2Group, log2Delta, nDelta++);
                    normalMaxSize = size;
                }

                log2Group++;
                log2Delta++;
            }

            // _chunkSize must be normalMaxSize
            Debug.Assert(ChunkSize == normalMaxSize);

            // return number of size index
            return index;
        }

        // calculate size class
        private int SizeClass(int index, int log2Group, int log2Delta, int nDelta)
        {
            short isMultiPageSize;
            if (log2Delta >= PageShifts)
            {
                isMultiPageSize = c_yes;
            }
            else
            {
                int pageSize = 1 << PageShifts;
                int size0 = (1 << log2Group) + (1 << log2Delta) * nDelta;

                isMultiPageSize = size0 == size0 / pageSize * pageSize ? c_yes : c_no;
            }

            int log2Ndelta = 0u >= (uint)nDelta ? 0 : PoolThreadCache<byte[]>.Log2(nDelta);

            sbyte remove = 1 << log2Ndelta < nDelta ? c_yes : c_no;

            int log2Size = log2Delta + log2Ndelta == log2Group ? log2Group + 1 : log2Group;
            if (log2Size == log2Group)
            {
                remove = c_yes;
            }

            short isSubpage = log2Size < PageShifts + LOG2_SIZE_CLASS_GROUP ? c_yes : c_no;

            int log2DeltaLookup = log2Size < LOG2_MAX_LOOKUP_SIZE ||
                                  log2Size == LOG2_MAX_LOOKUP_SIZE && remove == c_no
                    ? log2Delta : c_no;

            short[] sz = new[]
            {
                (short)index, (short)log2Group, (short)log2Delta,
                (short)nDelta, isMultiPageSize, isSubpage, (short)log2DeltaLookup
            };

            _sizeClasses[index] = sz;
            int size = (1 << log2Group) + (nDelta << log2Delta);

            if (sz[PAGESIZE_IDX] == c_yes)
            {
                _nPSizes++;
            }
            if (sz[SUBPAGE_IDX] == c_yes)
            {
                _nSubpages++;
                _smallMaxSizeIdx = index;
            }
            if (sz[LOG2_DELTA_LOOKUP_IDX] != c_no)
            {
                _lookupMaxSize = size;
            }
            return size;
        }

        private void Idx2SizeTab(int[] sizeIdx2sizeTab, int[] pageIdx2sizeTab)
        {
            int pageIdx = 0;

            for (int i = 0; i < _nSizes; i++)
            {
                short[] sizeClass = _sizeClasses[i];
                int log2Group = sizeClass[LOG2GROUP_IDX];
                int log2Delta = sizeClass[LOG2DELTA_IDX];
                int nDelta = sizeClass[NDELTA_IDX];

                int size = (1 << log2Group) + (nDelta << log2Delta);
                sizeIdx2sizeTab[i] = size;

                if (sizeClass[PAGESIZE_IDX] == c_yes)
                {
                    pageIdx2sizeTab[pageIdx++] = size;
                }
            }
        }

        private void Size2IdxTab(int[] size2idxTab)
        {
            int idx = 0;
            int size = 0;

            for (int i = 0; size <= _lookupMaxSize; i++)
            {
                int log2Delta = _sizeClasses[i][LOG2DELTA_IDX];
                int times = 1 << log2Delta - LOG2_QUANTUM;

                while (size <= _lookupMaxSize && times-- > 0)
                {
                    size2idxTab[idx++] = i;
                    size = idx + 1 << LOG2_QUANTUM;
                }
            }
        }

        /// <summary>Computes size from lookup table according to sizeIdx.</summary>
        /// <param name="sizeIdx">TBD</param>
        /// <returns>size</returns>
        public int SizeIdx2Size(int sizeIdx)
        {
            return _sizeIdx2SizeTab[sizeIdx];
        }

        /// <summary>Computes size according to sizeIdx.</summary>
        /// <param name="sizeIdx">TBD</param>
        /// <returns>size</returns>
        public int SizeIdx2SizeCompute(int sizeIdx)
        {
            int group = sizeIdx >> LOG2_SIZE_CLASS_GROUP;
            int mod = sizeIdx & (1 << LOG2_SIZE_CLASS_GROUP) - 1;

            int groupSize = 0u >= (uint)group ? 0 :
                    1 << LOG2_QUANTUM + LOG2_SIZE_CLASS_GROUP - 1 << group;

            int shift = 0u >= (uint)group ? 1 : group;
            int lgDelta = shift + LOG2_QUANTUM - 1;
            int modSize = mod + 1 << lgDelta;

            return groupSize + modSize;
        }

        /// <summary>Computes size from lookup table according to pageIdx.</summary>
        /// <param name="pageIdx">TBD</param>
        /// <returns>size which is multiples of pageSize.</returns>
        public long PageIdx2Size(int pageIdx)
        {
            return _pageIdx2SizeTab[pageIdx];
        }

        /// <summary>Computes size according to pageIdx.</summary>
        /// <param name="pageIdx">TBD</param>
        /// <returns>size which is multiples of pageSize</returns>
        public long PageIdx2SizeCompute(int pageIdx)
        {
            int group = pageIdx >> LOG2_SIZE_CLASS_GROUP;
            int mod = pageIdx & (1 << LOG2_SIZE_CLASS_GROUP) - 1;

            long groupSize = 0u >= (uint)group ? 0 :
                    1L << PageShifts + LOG2_SIZE_CLASS_GROUP - 1 << group;

            int shift = 0u >= (uint)group ? 1 : group;
            int log2Delta = shift + PageShifts - 1;
            int modSize = mod + 1 << log2Delta;

            return groupSize + modSize;
        }

        /// <summary>Normalizes request size up to the nearest size class.</summary>
        /// <param name="size">request size</param>
        /// <returns>sizeIdx of the size class</returns>
        public int Size2SizeIdx(int size)
        {
            if (0u >= (uint)size) { return 0; }
            if (size > ChunkSize) { return _nSizes; }

            if (_directMemoryCacheAlignment > 0)
            {
                size = AlignSize(size);
            }

            if (size <= _lookupMaxSize)
            {
                //size-1 / MIN_TINY
                return _size2IdxTab[size - 1 >> LOG2_QUANTUM];
            }

            int x = PoolThreadCache<byte[]>.Log2((size << 1) - 1);
            int shift = x < LOG2_SIZE_CLASS_GROUP + LOG2_QUANTUM + 1
                    ? 0 : x - (LOG2_SIZE_CLASS_GROUP + LOG2_QUANTUM);

            int group = shift << LOG2_SIZE_CLASS_GROUP;

            int log2Delta = x < LOG2_SIZE_CLASS_GROUP + LOG2_QUANTUM + 1
                    ? LOG2_QUANTUM : x - LOG2_SIZE_CLASS_GROUP - 1;

            int deltaInverseMask = -1 << log2Delta;
            int mod = (size - 1 & deltaInverseMask) >> log2Delta &
                      (1 << LOG2_SIZE_CLASS_GROUP) - 1;

            return group + mod;
        }

        /// <summary>Normalizes request size up to the nearest pageSize class.</summary>
        /// <param name="pages">multiples of pageSizes</param>
        /// <returns>pageIdx of the pageSize class</returns>
        public int Pages2PageIdx(int pages)
        {
            return Pages2PageIdxCompute(pages, false);
        }

        /// <summary>Normalizes request size down to the nearest pageSize class.</summary>
        /// <param name="pages">multiples of pageSizes</param>
        /// <returns>pageIdx of the pageSize class</returns>
        public int Pages2PageIdxFloor(int pages)
        {
            return Pages2PageIdxCompute(pages, true);
        }

        private int Pages2PageIdxCompute(int pages, bool floor)
        {
            int pageSize = pages << PageShifts;
            if (pageSize > ChunkSize)
            {
                return _nPSizes;
            }

            int x = PoolThreadCache<byte[]>.Log2((pageSize << 1) - 1);

            int shift = x < LOG2_SIZE_CLASS_GROUP + PageShifts
                    ? 0 : x - (LOG2_SIZE_CLASS_GROUP + PageShifts);

            int group = shift << LOG2_SIZE_CLASS_GROUP;

            int log2Delta = x < LOG2_SIZE_CLASS_GROUP + PageShifts + 1 ?
                    PageShifts : x - LOG2_SIZE_CLASS_GROUP - 1;

            int deltaInverseMask = -1 << log2Delta;
            int mod = (pageSize - 1 & deltaInverseMask) >> log2Delta &
                      (1 << LOG2_SIZE_CLASS_GROUP) - 1;

            int pageIdx = group + mod;

            if (floor && _pageIdx2SizeTab[pageIdx] > pages << PageShifts)
            {
                pageIdx--;
            }

            return pageIdx;
        }

        /// <summary>Round size up to the nearest multiple of alignment.</summary>
        private int AlignSize(int size)
        {
            int delta = size & _directMemoryCacheAlignment - 1;
            return 0u >= (uint)delta ? size : size + _directMemoryCacheAlignment - delta;
        }

        /// <summary>Normalizes usable size that would result from allocating an object with the
        /// specified size and alignment.</summary>
        /// <param name="size">request size</param>
        /// <returns>normalized size</returns>
        public int NormalizeSize(int size)
        {
            if (0u >= (uint)size)
            {
                return _sizeIdx2SizeTab[0];
            }
            if (_directMemoryCacheAlignment > 0)
            {
                size = AlignSize(size);
            }

            if (size <= _lookupMaxSize)
            {
                int ret = _sizeIdx2SizeTab[_size2IdxTab[size - 1 >> LOG2_QUANTUM]];
                Debug.Assert(ret == NormalizeSizeCompute(size));
                return ret;
            }
            return NormalizeSizeCompute(size);
        }

        private static int NormalizeSizeCompute(int size)
        {
            int x = PoolThreadCache<byte[]>.Log2((size << 1) - 1);
            int log2Delta = x < LOG2_SIZE_CLASS_GROUP + LOG2_QUANTUM + 1
                    ? LOG2_QUANTUM : x - LOG2_SIZE_CLASS_GROUP - 1;
            int delta = 1 << log2Delta;
            int delta_mask = delta - 1;
            return size + delta_mask & ~delta_mask;
        }
    }
}