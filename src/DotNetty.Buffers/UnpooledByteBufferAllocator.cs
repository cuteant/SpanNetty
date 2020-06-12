// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System.Threading;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;

    /// <summary>
    ///     Unpooled implementation of <see cref="IByteBufferAllocator" />.
    /// </summary>
    public sealed class UnpooledByteBufferAllocator : AbstractByteBufferAllocator, IByteBufferAllocatorMetricProvider
    {
        readonly UnpooledByteBufferAllocatorMetric _metric = new UnpooledByteBufferAllocatorMetric();
        readonly bool _disableLeakDetector;

        public static readonly UnpooledByteBufferAllocator Default =
            new UnpooledByteBufferAllocator(PlatformDependent.DirectBufferPreferred);

        public UnpooledByteBufferAllocator()
            : this(false, false)
        {
        }

        public unsafe UnpooledByteBufferAllocator(bool preferDirect)
            : this(preferDirect, false)
        {
        }

        public unsafe UnpooledByteBufferAllocator(bool preferDirect, bool disableLeakDetector)
            : base(preferDirect)
        {
            _disableLeakDetector = disableLeakDetector;
        }

        protected override IByteBuffer NewHeapBuffer(int initialCapacity, int maxCapacity) =>
            new InstrumentedUnpooledHeapByteBuffer(this, initialCapacity, maxCapacity);

        protected unsafe override IByteBuffer NewDirectBuffer(int initialCapacity, int maxCapacity)
        {
            IByteBuffer buf = new InstrumentedUnpooledUnsafeDirectByteBuffer(this, initialCapacity, maxCapacity);
            return _disableLeakDetector ? buf : ToLeakAwareBuffer(buf);
        }

        public override CompositeByteBuffer CompositeHeapBuffer(int maxNumComponents)
        {
            var buf = new CompositeByteBuffer(this, false, maxNumComponents);
            return _disableLeakDetector ? buf : ToLeakAwareBuffer(buf);
        }

        public unsafe override CompositeByteBuffer CompositeDirectBuffer(int maxNumComponents)
        {
            var buf = new CompositeByteBuffer(this, true, maxNumComponents);
            return _disableLeakDetector ? buf : ToLeakAwareBuffer(buf);
        }

        public override bool IsDirectBufferPooled => false;

        public IByteBufferAllocatorMetric Metric => _metric;

        internal void IncrementDirect(int amount) => _metric.DirectCounter(amount);

        internal void DecrementDirect(int amount) => _metric.DirectCounter(-amount);

        internal void IncrementHeap(int amount) => _metric.HeapCounter(amount);

        internal void DecrementHeap(int amount) => _metric.HeapCounter(-amount);

        sealed class InstrumentedUnpooledHeapByteBuffer : UnpooledHeapByteBuffer
        {
            internal InstrumentedUnpooledHeapByteBuffer(
                UnpooledByteBufferAllocator alloc, int initialCapacity, int maxCapacity)
                : base(alloc, initialCapacity, maxCapacity)
            {
                ((UnpooledByteBufferAllocator)Allocator).IncrementHeap(initialCapacity);
            }

            protected override byte[] AllocateArray(int initialCapacity)
            {
                byte[] bytes = base.AllocateArray(initialCapacity);
                ((UnpooledByteBufferAllocator)Allocator).IncrementHeap(bytes.Length);
                return bytes;
            }

            protected override void FreeArray(byte[] bytes)
            {
                int length = bytes.Length;
                base.FreeArray(bytes);
                ((UnpooledByteBufferAllocator)Allocator).DecrementHeap(length);
            }
        }

        sealed class InstrumentedUnpooledUnsafeDirectByteBuffer : UnpooledUnsafeDirectByteBuffer
        {
            internal InstrumentedUnpooledUnsafeDirectByteBuffer(
                UnpooledByteBufferAllocator alloc, int initialCapacity, int maxCapacity)
                : base(alloc, initialCapacity, maxCapacity)
            {
                ((UnpooledByteBufferAllocator)Allocator).IncrementDirect(initialCapacity);
            }

            protected override byte[] AllocateDirect(int initialCapacity)
            {
                byte[] bytes = base.AllocateDirect(initialCapacity);
                ((UnpooledByteBufferAllocator)Allocator).IncrementDirect(bytes.Length);
                return bytes;
            }

            protected override void FreeDirect(byte[] array)
            {
                int capacity = array.Length;
                base.FreeDirect(array);
                ((UnpooledByteBufferAllocator)Allocator).DecrementDirect(capacity);
            }
        }

        sealed class UnpooledByteBufferAllocatorMetric : IByteBufferAllocatorMetric
        {
            private long _usedHeapMemory;
            private long _userDirectMemory;

            public long UsedHeapMemory => Volatile.Read(ref _usedHeapMemory);

            public long UsedDirectMemory => Volatile.Read(ref _userDirectMemory);

            public void HeapCounter(int amount) => Interlocked.Add(ref _usedHeapMemory, amount);

            public void DirectCounter(int amount) => Interlocked.Add(ref _userDirectMemory, amount);

            public override string ToString() => $"{StringUtil.SimpleClassName(this)} (usedHeapMemory: {UsedHeapMemory}; usedDirectMemory: {UsedDirectMemory})";
        }
    }
}