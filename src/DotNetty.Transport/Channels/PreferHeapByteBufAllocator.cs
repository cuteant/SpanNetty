namespace DotNetty.Transport.Channels
{
    using DotNetty.Buffers;

    public sealed class PreferHeapByteBufAllocator : IByteBufferAllocator
    {
        private readonly IByteBufferAllocator _allocator;

        public PreferHeapByteBufAllocator(IByteBufferAllocator allocator)
        {
            if (allocator is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.allocator); }
            _allocator = allocator;
        }

        public bool IsDirectBufferPooled => _allocator.IsDirectBufferPooled;

        public IByteBuffer Buffer() => _allocator.HeapBuffer();

        public IByteBuffer Buffer(int initialCapacity) => _allocator.HeapBuffer(initialCapacity);

        public IByteBuffer Buffer(int initialCapacity, int maxCapacity) => _allocator.HeapBuffer(initialCapacity, maxCapacity);

        public int CalculateNewCapacity(int minNewCapacity, int maxCapacity) => _allocator.CalculateNewCapacity(minNewCapacity, maxCapacity);

        public CompositeByteBuffer CompositeBuffer() => _allocator.CompositeHeapBuffer();

        public CompositeByteBuffer CompositeBuffer(int maxComponents) => _allocator.CompositeHeapBuffer(maxComponents);

        public CompositeByteBuffer CompositeDirectBuffer() => _allocator.CompositeDirectBuffer();

        public CompositeByteBuffer CompositeDirectBuffer(int maxComponents) => _allocator.CompositeDirectBuffer(maxComponents);

        public CompositeByteBuffer CompositeHeapBuffer() => _allocator.CompositeHeapBuffer();

        public CompositeByteBuffer CompositeHeapBuffer(int maxComponents) => _allocator.CompositeHeapBuffer();

        public IByteBuffer DirectBuffer() => _allocator.DirectBuffer();

        public IByteBuffer DirectBuffer(int initialCapacity) => _allocator.DirectBuffer(initialCapacity);

        public IByteBuffer DirectBuffer(int initialCapacity, int maxCapacity) => _allocator.DirectBuffer(initialCapacity, maxCapacity);

        public IByteBuffer HeapBuffer() => _allocator.HeapBuffer();

        public IByteBuffer HeapBuffer(int initialCapacity) => _allocator.HeapBuffer(initialCapacity);

        public IByteBuffer HeapBuffer(int initialCapacity, int maxCapacity) => _allocator.HeapBuffer(initialCapacity, maxCapacity);
    }
}
