// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using Xunit;

    public class PoolArenaTests
    {
        private static readonly int PAGE_SIZE = 8192;
        private static readonly int PAGE_SHIFTS = 11;
        //chunkSize = pageSize * (2 ^ pageShifts)
        private static readonly int CHUNK_SIZE = 16777216;

        [Fact]
        public void NormalizeCapacity()
        {
            NormalizeCapacity0(true);
        }

        [Fact]
        public void NormalizeCapacityHeap()
        {
            NormalizeCapacity0(false);
        }

        private void NormalizeCapacity0(bool preferDirect)
        {
            PoolArena<byte[]> arena = preferDirect
                ? new DirectArena(null, PAGE_SIZE, PAGE_SHIFTS, CHUNK_SIZE)
                : new HeapArena(null, PAGE_SIZE, PAGE_SHIFTS, CHUNK_SIZE);
            int[] reqCapacities = { 0, 15, 510, 1024, 1023, 1025 };
            int[] expectedResult = { 16, 16, 512, 1024, 1024, 1280 };
            for (int i = 0; i < reqCapacities.Length; i++)
            {
                Assert.Equal(expectedResult[i], arena.SizeIdx2Size(arena.Size2SizeIdx(reqCapacities[i])));
            }
        }

        [Fact]
        public void Size2SizeIdx()
        {
            PoolArena<byte[]> arena = new DirectArena(null, PAGE_SIZE, PAGE_SHIFTS, CHUNK_SIZE);

            for (int sz = 0; sz <= CHUNK_SIZE; sz++)
            {
                int sizeIdx = arena.Size2SizeIdx(sz);
                Assert.True(sz <= arena.SizeIdx2Size(sizeIdx));
                if (sizeIdx > 0)
                {
                    Assert.True(sz > arena.SizeIdx2Size(sizeIdx - 1));
                }
            }
        }

        [Fact]
        public void Pages2PageIdx()
        {
            int pageShifts = PAGE_SHIFTS;

            PoolArena<byte[]> arena = new DirectArena(null, PAGE_SIZE, PAGE_SHIFTS, CHUNK_SIZE);

            int maxPages = CHUNK_SIZE >> pageShifts;
            for (int pages = 1; pages <= maxPages; pages++)
            {
                int pageIdxFloor = arena.Pages2PageIdxFloor(pages);
                Assert.True(pages << pageShifts >= arena.PageIdx2Size(pageIdxFloor));
                if (pageIdxFloor > 0 && pages < maxPages)
                {
                    Assert.True(pages << pageShifts < arena.PageIdx2Size(pageIdxFloor + 1));
                }

                int pageIdxCeiling = arena.Pages2PageIdx(pages);
                Assert.True(pages << pageShifts <= arena.PageIdx2Size(pageIdxCeiling));
                if (pageIdxCeiling > 0)
                {
                    Assert.True(pages << pageShifts > arena.PageIdx2Size(pageIdxCeiling - 1));
                }
            }
        }

        [Fact]
        public void SizeIdx2size()
        {
            PoolArena<byte[]> arena = new DirectArena(null, PAGE_SIZE, PAGE_SHIFTS, CHUNK_SIZE);
            for (int i = 0; i < arena._nSizes; i++)
            {
                Assert.Equal(arena.SizeIdx2SizeCompute(i), arena.SizeIdx2Size(i));
            }
        }

        [Fact]
        public void PageIdx2size()
        {
            PoolArena<byte[]> arena = new DirectArena(null, PAGE_SIZE, PAGE_SHIFTS, CHUNK_SIZE);
            for (int i = 0; i < arena._nPSizes; i++)
            {
                Assert.Equal(arena.PageIdx2SizeCompute(i), arena.PageIdx2Size(i));
            }
        }

        [Fact]
        public void AllocationCounter()
        {
            var allocator = new PooledByteBufferAllocator(
                true,   // preferDirect
                0,      // nHeapArena
                1,      // nDirectArena
                8192,   // pageSize
                11,     // maxOrder
                0,      // smallCacheSize
                0      // normalCacheSize
            );

            // create small buffer
            IByteBuffer b1 = allocator.Buffer(800);
            // create normal buffer
            IByteBuffer b2 = allocator.Buffer(8192 * 5);

            Assert.NotNull(b1);
            Assert.NotNull(b2);

            // then release buffer to deallocated memory while threadlocal cache has been disabled
            // allocations counter value must equals deallocations counter value
            Assert.True(b1.Release());
            Assert.True(b2.Release());

            Assert.True(allocator.DirectArenas().Count >= 1);
            IPoolArenaMetric metric = allocator.DirectArenas()[0];

            Assert.Equal(2, metric.NumDeallocations);
            Assert.Equal(2, metric.NumAllocations);

            Assert.Equal(1, metric.NumSmallDeallocations);
            Assert.Equal(1, metric.NumSmallAllocations);
            Assert.Equal(1, metric.NumNormalDeallocations);
            Assert.Equal(1, metric.NumNormalAllocations);
        }

        //[Fact]
        //public void DirectArenaMemoryCopy()
        //{
        //    IByteBuffer src = PooledByteBufferAllocator.Default.DirectBuffer(512);
        //    IByteBuffer dst = PooledByteBufferAllocator.Default.DirectBuffer(512);

        //    var pooledSrc = UnwrapIfNeeded(src);
        //    var pooledDst = UnwrapIfNeeded(dst);

        //    // This causes the internal reused ByteBuffer duplicate limit to be set to 128
        //    pooledDst.WriteBytes(ByteBuffer.allocate(128));
        //    // Ensure internal ByteBuffer duplicate limit is properly reset (used in memoryCopy non-Unsafe case)
        //    pooledDst.Chunk.Arena.memo.MemoryCopy(pooledSrc.memory, 0, pooledDst, 512);

        //    src.release();
        //    dst.release();
        //}

        private PooledByteBuffer<byte> UnwrapIfNeeded(IByteBuffer buf)
        {
            return (PooledByteBuffer<byte>)(buf is PooledByteBuffer<byte> ? buf : buf.Unwrap());
        }
    }
}
