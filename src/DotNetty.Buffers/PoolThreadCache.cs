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
    using System.Threading;
    using DotNetty.Common;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using Thread = DotNetty.Common.Concurrency.XThread;

    /// <summary>
    /// Acts a Thread cache for allocations. This implementation is moduled after
    /// <a href="http://people.freebsd.org/~jasone/jemalloc/bsdcan2006/jemalloc.pdf">jemalloc</a> and the descripted
    /// technics of
    /// <a href="https://www.facebook.com/notes/facebook-engineering/scalable-memory-allocation-using-jemalloc/480222803919">
    /// Scalable memory allocation using jemalloc</a>.
    /// </summary>
    internal sealed class PoolThreadCache<T>
    {
        private static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<PoolThreadCache<T>>();
        private const int c_integerSizeMinusOne = IntegerExtensions.SizeInBits - 1;

        internal readonly PoolArena<T> HeapArena;
        internal readonly PoolArena<T> DirectArena;

        // Hold the caches for the different size classes, which are tiny, small and normal.
        private readonly MemoryRegionCache[] _smallSubPageHeapCaches;
        private readonly MemoryRegionCache[] _smallSubPageDirectCaches;
        private readonly MemoryRegionCache[] _normalHeapCaches;
        private readonly MemoryRegionCache[] _normalDirectCaches;

        private readonly int _freeSweepAllocationThreshold;
        private int _freed = SharedConstants.False;

        private int _allocations;

        private readonly Thread _deathWatchThread;
        private readonly Action _freeTask;

        // TODO: Test if adding padding helps under contention
        //private long pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7;

        internal PoolThreadCache(PoolArena<T> heapArena, PoolArena<T> directArena,
            int smallCacheSize, int normalCacheSize, int maxCachedBufferCapacity, int freeSweepAllocationThreshold)
        {
            if ((uint)maxCachedBufferCapacity > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_PositiveOrZero(maxCachedBufferCapacity, ExceptionArgument.maxCachedBufferCapacity); }

            _freeSweepAllocationThreshold = freeSweepAllocationThreshold;
            HeapArena = heapArena;
            DirectArena = directArena;
            if (directArena is object)
            {
                _smallSubPageDirectCaches = CreateSubPageCaches(
                    smallCacheSize, directArena.NumSmallSubpagePools);

                _normalDirectCaches = CreateNormalCaches(
                    normalCacheSize, maxCachedBufferCapacity, directArena);

                directArena.IncrementNumThreadCaches();
            }
            else
            {
                // No directArea is configured so just null out all caches
                _smallSubPageDirectCaches = null;
                _normalDirectCaches = null;
            }
            if (heapArena is object)
            {
                // Create the caches for the heap allocations
                _smallSubPageHeapCaches = CreateSubPageCaches(
                    smallCacheSize, heapArena.NumSmallSubpagePools);

                _normalHeapCaches = CreateNormalCaches(
                    normalCacheSize, maxCachedBufferCapacity, heapArena);

                heapArena.IncrementNumThreadCaches();
            }
            else
            {
                // No heapArea is configured so just null out all caches
                _smallSubPageHeapCaches = null;
                _normalHeapCaches = null;
            }

            // We only need to watch the thread when any cache is used.
            if (_smallSubPageDirectCaches is object || _normalDirectCaches is object ||
                _smallSubPageHeapCaches is object || _normalHeapCaches is object)
            {
                if (freeSweepAllocationThreshold < 1) { ThrowHelper.ThrowArgumentException_Positive(freeSweepAllocationThreshold, ExceptionArgument.freeSweepAllocationThreshold); }
                _freeTask = () => FreeImpl(true);
                _deathWatchThread = Thread.CurrentThread;

                // The thread-local cache will keep a list of pooled buffers which must be returned to
                // the pool when the thread is not alive anymore.
                ThreadDeathWatcher.Watch(_deathWatchThread, _freeTask);
            }
            else
            {
                _freeTask = null;
                _deathWatchThread = null;
            }
        }

        private static MemoryRegionCache[] CreateSubPageCaches(int cacheSize, int numCaches)
        {
            if (cacheSize > 0 && numCaches > 0)
            {
                var cache = new MemoryRegionCache[numCaches];
                for (int i = 0; i < cache.Length; i++)
                {
                    // TODO: maybe use cacheSize / cache.length
                    cache[i] = new SubPageMemoryRegionCache(cacheSize);
                }
                return cache;
            }
            else
            {
                return null;
            }
        }

        private static MemoryRegionCache[] CreateNormalCaches(
            int cacheSize, int maxCachedBufferCapacity, PoolArena<T> area)
        {
            if (cacheSize > 0 && maxCachedBufferCapacity > 0)
            {
                int max = Math.Min(area.ChunkSize, maxCachedBufferCapacity);
                // Create as many normal caches as we support based on how many sizeIdx we have and what the upper
                // bound is that we want to cache in general.
                List<MemoryRegionCache> cache = new List<MemoryRegionCache>();
                for (int idx = area.NumSmallSubpagePools; idx < area._nSizes && area.SizeIdx2Size(idx) <= max; idx++)
                {
                    cache.Add(new NormalMemoryRegionCache(cacheSize));
                }
                return cache.ToArray();
            }
            else
            {
                return null;
            }
        }

        // val > 0
        [MethodImpl(InlineMethod.AggressiveOptimization)]
        internal static int Log2(int val)
        {
            return c_integerSizeMinusOne - val.NumberOfLeadingZeros();
        }

        /// <summary>Try to allocate a small buffer out of the cache.</summary>
        /// <returns><c>true</c> if successful <c>false</c> otherwise</returns>
        internal bool AllocateSmall(PoolArena<T> area, PooledByteBuffer<T> buf, int reqCapacity, int sizeIdx) =>
            Allocate(CacheForSmall(area, sizeIdx), buf, reqCapacity);

        /// <summary>Try to allocate a small buffer out of the cache</summary>
        /// <returns><c>true</c> if successful <c>false</c> otherwise</returns>
        internal bool AllocateNormal(PoolArena<T> area, PooledByteBuffer<T> buf, int reqCapacity, int sizeIdx) =>
            Allocate(CacheForNormal(area, sizeIdx), buf, reqCapacity);

        private bool Allocate(MemoryRegionCache cache, PooledByteBuffer<T> buf, int reqCapacity)
        {
            if (cache is null)
            {
                // no cache found so just return false here
                return false;
            }
            bool allocated = cache.Allocate(buf, reqCapacity, this);
            if (++_allocations >= _freeSweepAllocationThreshold)
            {
                _allocations = 0;
                Trim();
            }
            return allocated;
        }

        /// <summary>Add <see cref="PoolChunk{T}"/> and <paramref name="handle"/> to the cache if there is enough room.</summary>
        /// <returns><c>true</c> if it fit into the cache <c>false</c> otherwise.</returns>
        internal bool Add(PoolArena<T> area, PoolChunk<T> chunk, long handle, int normCapacity, SizeClass sizeClass)
        {
            int sizeIdx = area.Size2SizeIdx(normCapacity);
            MemoryRegionCache cache = Cache(area, sizeIdx, sizeClass);
            if (cache is null)
            {
                return false;
            }
            return cache.Add(chunk, handle, normCapacity);
        }

        private MemoryRegionCache Cache(PoolArena<T> area, int sizeIdx, SizeClass sizeClass)
        {
            switch (sizeClass)
            {
                case SizeClass.Normal:
                    return CacheForNormal(area, sizeIdx);
                case SizeClass.Small:
                    return CacheForSmall(area, sizeIdx);
                default:
                    ThrowHelper.ThrowArgumentOutOfRangeException(); return default;
            }
        }

        /// <summary>Should be called if the Thread that uses this cache is about to exist to release resources out of the cache</summary>
        internal void Free()
        {
            if (_freeTask is object)
            {
                Debug.Assert(_deathWatchThread is object);
                ThreadDeathWatcher.Unwatch(_deathWatchThread, _freeTask);
            }

            FreeImpl(true);
        }

        private void FreeImpl(bool finalizer)
        {
            // need to ensure we only call this one time.
            if (0u >= (uint)(SharedConstants.True - Interlocked.Exchange(ref _freed, SharedConstants.True))) { return; }

            int numFreed = Free(_smallSubPageDirectCaches, finalizer) +
                Free(_normalDirectCaches, finalizer) +
                Free(_smallSubPageHeapCaches, finalizer) +
                Free(_normalHeapCaches, finalizer);

            if (numFreed > 0 && Logger.DebugEnabled)
            {
                Logger.FreedThreadLocalBufferFromThread(numFreed, _deathWatchThread);
            }

            DirectArena?.DecrementNumThreadCaches();
            HeapArena?.DecrementNumThreadCaches();
        }

        private static int Free(MemoryRegionCache[] caches, bool finalizer)
        {
            if (caches is null)
            {
                return 0;
            }

            int numFreed = 0;
            foreach (MemoryRegionCache c in caches)
            {
                numFreed += Free(c, finalizer);
            }
            return numFreed;
        }

        private static int Free(MemoryRegionCache cache, bool finalizer)
        {
            if (cache is null)
            {
                return 0;
            }
            return cache.Free(finalizer);
        }

        internal void Trim()
        {
            Trim(_smallSubPageDirectCaches);
            Trim(_normalDirectCaches);
            Trim(_smallSubPageHeapCaches);
            Trim(_normalHeapCaches);
        }

        private static void Trim(MemoryRegionCache[] caches)
        {
            if (caches is null) { return; }
            foreach (MemoryRegionCache c in caches)
            {
                Trim(c);
            }
        }

        private static void Trim(MemoryRegionCache cache) => cache?.Trim();

        private MemoryRegionCache CacheForSmall(PoolArena<T> area, int sizeIdx)
        {
            return Cache(area.IsDirect ? _smallSubPageDirectCaches : _smallSubPageHeapCaches, sizeIdx);
        }

        private MemoryRegionCache CacheForNormal(PoolArena<T> area, int sizeIdx)
        {
            // We need to substract area.numSmallSubpagePools as sizeIdx is the overall index for all sizes.
            int idx = sizeIdx - area.NumSmallSubpagePools;
            return Cache(area.IsDirect ? _normalDirectCaches : _normalHeapCaches, idx);
        }

        private static MemoryRegionCache Cache(MemoryRegionCache[] cache, int sizeIdx)
        {
            if (cache is null || sizeIdx > cache.Length - 1)
            {
                return null;
            }
            return cache[sizeIdx];
        }

        /// <summary>Cache used for buffers which are backed by TINY or SMALL size.</summary>
        private sealed class SubPageMemoryRegionCache : MemoryRegionCache
        {
            internal SubPageMemoryRegionCache(int size)
                : base(size, SizeClass.Small)
            {
            }

            protected override void InitBuf(
                PoolChunk<T> chunk, long handle, PooledByteBuffer<T> buf, int reqCapacity, PoolThreadCache<T> threadCache) =>
                chunk.InitBufWithSubpage(buf, handle, reqCapacity, threadCache);
        }

        /// <summary>
        /// Cache used for buffers which are backed by NORMAL size.
        /// </summary>
        private sealed class NormalMemoryRegionCache : MemoryRegionCache
        {
            internal NormalMemoryRegionCache(int size)
                : base(size, SizeClass.Normal)
            {
            }

            protected override void InitBuf(
                PoolChunk<T> chunk, long handle, PooledByteBuffer<T> buf, int reqCapacity, PoolThreadCache<T> threadCache) =>
                chunk.InitBuf(buf, handle, reqCapacity, threadCache);
        }

        private abstract class MemoryRegionCache
        {
            private readonly int _size;
            private readonly IQueue<Entry> _queue;
            private readonly SizeClass _sizeClass;
            private int _allocations;

            protected MemoryRegionCache(int size, SizeClass sizeClass)
            {
                _size = MathUtil.SafeFindNextPositivePowerOfTwo(size);
                _queue = PlatformDependent.NewFixedMpscQueue<Entry>(_size);
                _sizeClass = sizeClass;
            }

            /// <summary>
            /// Init the <see cref="PooledByteBuffer{T}"/> using the provided chunk and handle with the capacity restrictions.
            /// </summary>
            protected abstract void InitBuf(PoolChunk<T> chunk, long handle,
                PooledByteBuffer<T> buf, int reqCapacity, PoolThreadCache<T> threadCache);

            /// <summary>
            /// Add to cache if not already full.
            /// </summary>
            public bool Add(PoolChunk<T> chunk, long handle, int normCapacity)
            {
                Entry entry = NewEntry(chunk, handle, normCapacity);
                bool queued = _queue.TryEnqueue(entry);
                if (!queued)
                {
                    // If it was not possible to cache the chunk, immediately recycle the entry
                    entry.Recycle();
                }

                return queued;
            }

            /// <summary>
            /// Allocate something out of the cache if possible and remove the entry from the cache.
            /// </summary>
            public bool Allocate(PooledByteBuffer<T> buf, int reqCapacity, PoolThreadCache<T> threadCache)
            {
                if (!_queue.TryDequeue(out Entry entry))
                {
                    return false;
                }
                InitBuf(entry.Chunk, entry.Handle, buf, reqCapacity, threadCache);
                entry.Recycle();

                // allocations is not thread-safe which is fine as this is only called from the same thread all time.
                ++_allocations;
                return true;
            }

            /// <summary>Clear out this cache and free up all previous cached <see cref="PoolChunk{T}"/>s and <c>handle</c>s.</summary>
            public int Free(bool finalizer) => Free(int.MaxValue, finalizer);

            private int Free(int max, bool finalizer)
            {
                int numFreed = 0;
                for (; numFreed < max; numFreed++)
                {
                    if (_queue.TryDequeue(out Entry entry))
                    {
                        FreeEntry(entry, finalizer);
                    }
                    else
                    {
                        // all cleared
                        return numFreed;
                    }
                }
                return numFreed;
            }

            /// <summary>Free up cached <see cref="PoolChunk{T}"/>s if not allocated frequently enough.</summary>
            public void Trim()
            {
                int toFree = _size - _allocations;
                _allocations = 0;

                // We not even allocated all the number that are
                if (toFree > 0)
                {
                    _ = Free(toFree, false);
                }
            }

            private void FreeEntry(Entry entry, bool finalizer)
            {
                PoolChunk<T> chunk = entry.Chunk;
                long handle = entry.Handle;

                if (!finalizer)
                {
                    // recycle now so PoolChunk can be GC'ed. This will only be done if this is not freed because of a finalizer.
                    entry.Recycle();
                }

                chunk.Arena.FreeChunk(chunk, handle, entry.NormCapacity, _sizeClass, finalizer);
            }

            private sealed class Entry
            {
                private readonly ThreadLocalPool.Handle _recyclerHandle;
                public PoolChunk<T> Chunk;
                public long Handle = -1;
                public int NormCapacity;

                public Entry(ThreadLocalPool.Handle recyclerHandle)
                {
                    _recyclerHandle = recyclerHandle;
                }

                internal void Recycle()
                {
                    Chunk = null;
                    Handle = -1;
                    _recyclerHandle.Release(this);
                }
            }

            private static Entry NewEntry(PoolChunk<T> chunk, long handle, int normCapacity)
            {
                Entry entry = Recycler.Take();
                entry.Chunk = chunk;
                entry.Handle = handle;
                entry.NormCapacity = normCapacity;
                return entry;
            }

            private static readonly ThreadLocalPool<Entry> Recycler = new(handle => new Entry(handle));
        }
    }
}