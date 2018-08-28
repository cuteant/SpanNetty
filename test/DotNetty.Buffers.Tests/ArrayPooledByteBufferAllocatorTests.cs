using Xunit;

namespace DotNetty.Buffers.Tests
{
    public class ArrayPooledByteBufferAllocatorTests : AbstractByteBufferAllocatorTests
    {
        protected override int ExpectedCapacity { get; } = 128; // for BufferManager

        protected override IByteBufferAllocator NewAllocator(bool preferDirect) => new ArrayPooledByteBufferAllocator(preferDirect);

        protected override IByteBufferAllocator NewUnpooledAllocator() => new UnpooledByteBufferAllocator(false);
    }
}
