using System.Threading;
using CuteAnt.Buffers;
using DotNetty.Common;
using DotNetty.Common.Internal;
using DotNetty.Common.Utilities;

namespace DotNetty.Buffers
{
    /// <summary>
    ///     Unpooled implementation of <see cref="IByteBufferAllocator" />.
    /// </summary>
    public sealed class BufferManagerByteBufferAllocator : AbstractByteBufferAllocator, IByteBufferAllocatorMetricProvider
    {
        readonly BufferManagerByteBufferAllocatorMetric metric = new BufferManagerByteBufferAllocatorMetric();
        readonly bool disableLeakDetector;

        public static readonly BufferManagerByteBufferAllocator Default =
            new BufferManagerByteBufferAllocator(PlatformDependent.DirectBufferPreferred);

        public BufferManagerByteBufferAllocator()
            : this(PlatformDependent.DirectBufferPreferred, false)
        {
        }

        public BufferManagerByteBufferAllocator(bool preferDirect)
            : this(preferDirect, false)
        {
        }

        public BufferManagerByteBufferAllocator(bool preferDirect, bool disableLeakDetector)
            : base(preferDirect)
        {
            this.disableLeakDetector = disableLeakDetector;
        }

        protected override IByteBuffer NewHeapBuffer(int initialCapacity, int maxCapacity)
            => InstrumentedBufferManagerHeapByteBuffer.Create(this, initialCapacity, maxCapacity);

        protected override IByteBuffer NewDirectBuffer(int initialCapacity, int maxCapacity)
            => InstrumentedUBufferManagerUnsafeDirectByteBuffer.Create(this, initialCapacity, maxCapacity);

        public override CompositeByteBuffer CompositeHeapBuffer(int maxNumComponents)
        {
            var buf = new CompositeByteBuffer(this, false, maxNumComponents);
            return this.disableLeakDetector ? buf : ToLeakAwareBuffer(buf);
        }

        public override CompositeByteBuffer CompositeDirectBuffer(int maxNumComponents)
        {
            var buf = new CompositeByteBuffer(this, true, maxNumComponents);
            return this.disableLeakDetector ? buf : ToLeakAwareBuffer(buf);
        }

        public override bool IsDirectBufferPooled => true;

        public IByteBufferAllocatorMetric Metric => this.metric;

        internal void IncrementDirect(int amount) => this.metric.DirectCounter(amount);

        internal void DecrementDirect(int amount) => this.metric.DirectCounter(-amount);

        internal void IncrementHeap(int amount) => this.metric.HeapCounter(amount);

        internal void DecrementHeap(int amount) => this.metric.HeapCounter(-amount);
    }

    sealed class InstrumentedBufferManagerHeapByteBuffer : BufferManagerHeapByteBuffer
    {
        static readonly ThreadLocalPool<InstrumentedBufferManagerHeapByteBuffer> s_recycler = new ThreadLocalPool<InstrumentedBufferManagerHeapByteBuffer>(handle => new InstrumentedBufferManagerHeapByteBuffer(handle, 0));

        internal static InstrumentedBufferManagerHeapByteBuffer Create(BufferManagerByteBufferAllocator allocator, int initialCapacity, int maxCapacity)
        {
            var buf = s_recycler.Take();
            buf.Reuse(allocator, BufferManagerUtil.DefaultBufferPool, initialCapacity, maxCapacity);
            return buf;
        }

        internal InstrumentedBufferManagerHeapByteBuffer(ThreadLocalPool.Handle recyclerHandle, int maxCapacity)
            : base(recyclerHandle, maxCapacity)
        {
        }

        protected override byte[] AllocateArray(int initialCapacity)
        {
            byte[] bytes = base.AllocateArray(initialCapacity);
            _allocator.IncrementHeap(bytes.Length);
            return bytes;
        }

        protected override void FreeArray(byte[] bytes)
        {
            int length = bytes.Length;
            base.FreeArray(bytes);
            _allocator.DecrementHeap(length);
        }
    }

    sealed class InstrumentedUBufferManagerUnsafeDirectByteBuffer : BufferManagerUnsafeDirectByteBuffer
    {
        static readonly ThreadLocalPool<InstrumentedUBufferManagerUnsafeDirectByteBuffer> s_recycler = new ThreadLocalPool<InstrumentedUBufferManagerUnsafeDirectByteBuffer>(handle => new InstrumentedUBufferManagerUnsafeDirectByteBuffer(handle, 0));

        internal static InstrumentedUBufferManagerUnsafeDirectByteBuffer Create(BufferManagerByteBufferAllocator allocator, int initialCapacity, int maxCapacity)
        {
            var buf = s_recycler.Take();
            buf.Reuse(allocator, BufferManagerUtil.DefaultBufferPool, initialCapacity, maxCapacity);
            return buf;
        }

        internal InstrumentedUBufferManagerUnsafeDirectByteBuffer(ThreadLocalPool.Handle recyclerHandle, int maxCapacity)
            : base(recyclerHandle, maxCapacity)
        {
        }

        protected override byte[] AllocateArray(int initialCapacity)
        {
            byte[] bytes = base.AllocateArray(initialCapacity);
            _allocator.IncrementDirect(bytes.Length);
            return bytes;
        }

        protected override void FreeArray(byte[] array)
        {
            int capacity = array.Length;
            base.FreeArray(array);
            _allocator.DecrementDirect(capacity);
        }
    }

    sealed class BufferManagerByteBufferAllocatorMetric : IByteBufferAllocatorMetric
    {
        long usedHeapMemory;
        long userDirectMemory;

        public long UsedHeapMemory => Volatile.Read(ref this.usedHeapMemory);

        public long UsedDirectMemory => Volatile.Read(ref this.userDirectMemory);

        public void HeapCounter(int amount) => Interlocked.Add(ref this.usedHeapMemory, amount);

        public void DirectCounter(int amount) => Interlocked.Add(ref this.userDirectMemory, amount);

        public override string ToString() => $"{StringUtil.SimpleClassName(this)} (usedHeapMemory: {this.UsedHeapMemory}; usedDirectMemory: {this.UsedDirectMemory})";
    }
}