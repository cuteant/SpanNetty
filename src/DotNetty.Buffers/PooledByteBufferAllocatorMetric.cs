// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Common.Utilities;

    public sealed class PooledByteBufferAllocatorMetric : IByteBufferAllocatorMetric
    {
        readonly PooledByteBufferAllocator allocator;

        internal PooledByteBufferAllocatorMetric(PooledByteBufferAllocator allocator)
        {
            this.allocator = allocator;
        }

#if NET40
    public IList<IPoolArenaMetric> HeapArenas() => this.allocator.HeapArenas();
#else
    public IReadOnlyList<IPoolArenaMetric> HeapArenas() => this.allocator.HeapArenas();
#endif

        public int TinyCacheSize => this.allocator.TinyCacheSize;

        public int SmallCacheSize => this.allocator.SmallCacheSize;

        public int NormalCacheSize => this.allocator.NormalCacheSize;

        public int ChunkSize => this.allocator.ChunkSize;

        public long UsedHeapMemory => this.allocator.UsedHeapMemory;

        public int NumThreadLocalCaches()
        {
            var arenas = this.HeapArenas();
            if (arenas == null)
            {
                return 0;
            }

            int total = 0;
            foreach (IPoolArenaMetric metric in arenas)
            {
                total += metric.NumThreadCaches;
            }

            return total;
        }

        public override string ToString()
        {
            var sb = new StringBuilder(256);
            sb.Append(StringUtil.SimpleClassName(this))
                .Append("(usedHeapMemory: ").Append(this.UsedHeapMemory)
                .Append("; numHeapArenas: ").Append(this.HeapArenas().Count)
                .Append("; tinyCacheSize: ").Append(this.TinyCacheSize)
                .Append("; smallCacheSize: ").Append(this.SmallCacheSize)
                .Append("; normalCacheSize: ").Append(this.NormalCacheSize)
                .Append("; numThreadLocalCaches: ").Append(this.NumThreadLocalCaches())
                .Append("; chunkSize: ").Append(this.ChunkSize).Append(')');
            return sb.ToString();
        }
    }
}
