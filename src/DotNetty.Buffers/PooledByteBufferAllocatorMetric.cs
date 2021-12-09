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
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;

    /// <summary>Exposed metric for <see cref="PooledByteBufferAllocator"/>.</summary>
    public sealed class PooledByteBufferAllocatorMetric : IByteBufferAllocatorMetric
    {
        readonly PooledByteBufferAllocator _allocator;

        internal PooledByteBufferAllocatorMetric(PooledByteBufferAllocator allocator)
        {
            _allocator = allocator;
        }

        /// <summary>Return a <see cref="IReadOnlyList{T}"/> of all heap <see cref="IPoolArenaMetric"/>s that are provided by this pool.</summary>
        public IReadOnlyList<IPoolArenaMetric> HeapArenas() => _allocator.HeapArenas();

        /// <summary>Return a <see cref="IReadOnlyList{T}"/> of all direct <see cref="IPoolArenaMetric"/>s that are provided by this pool.</summary>
        public IReadOnlyList<IPoolArenaMetric> DirectArenas() => _allocator.DirectArenas();

        /// <summary>Return the size of the tiny cache.</summary>
        [Obsolete("Tiny caches have been merged into small caches.")]
        public int TinyCacheSize => _allocator.TinyCacheSize;

        /// <summary>Return the size of the small cache.</summary>
        public int SmallCacheSize => _allocator.SmallCacheSize;

        /// <summary>Return the size of the normal cache.</summary>
        public int NormalCacheSize => _allocator.NormalCacheSize;

        /// <summary>Return the chunk size for an arena.</summary>
        public int ChunkSize => _allocator.ChunkSize;

        /// <inheritdoc />
        public long UsedHeapMemory => _allocator.UsedHeapMemory;

        /// <inheritdoc />
        public long UsedDirectMemory => _allocator.UsedDirectMemory;

        /// <summary>Return the number of thread local caches used by this <see cref="PooledByteBufferAllocator"/>.</summary>
        public int NumThreadLocalCaches()
        {
            int total = 0;
            var arenas = HeapArenas();
            if (arenas is object)
            {
                foreach (IPoolArenaMetric metric in arenas)
                {
                    total += metric.NumThreadCaches;
                }
            }

            arenas = DirectArenas();
            if (arenas is object)
            {
                foreach (IPoolArenaMetric metric in arenas)
                {
                    total += metric.NumThreadCaches;
                }
            }

            return total;
        }

        public override string ToString()
        {
            var sb = StringBuilderManager.Allocate(256);
            _ = sb.Append(StringUtil.SimpleClassName(this))
                .Append("(usedHeapMemory: ").Append(UsedHeapMemory)
                .Append("; usedDirectMemory: ").Append(UsedDirectMemory)
                .Append("; numHeapArenas: ").Append(HeapArenas().Count)
                .Append("; numDirectArenas: ").Append(DirectArenas().Count)
                .Append("; smallCacheSize: ").Append(SmallCacheSize)
                .Append("; normalCacheSize: ").Append(NormalCacheSize)
                .Append("; numThreadLocalCaches: ").Append(NumThreadLocalCaches())
                .Append("; chunkSize: ").Append(ChunkSize).Append(')');
            return StringBuilderManager.ReturnAndFree(sb);
        }
    }
}
