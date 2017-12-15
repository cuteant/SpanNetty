using Xunit;

namespace DotNetty.Buffers.Tests
{
    public class BufferManagerByteBufferAllocatorTests : AbstractByteBufferAllocatorTests
    {
        protected override int ExpectedCapacity { get; } = 128; // for BufferManager

        protected override IByteBufferAllocator NewAllocator(bool preferDirect) => new BufferManagerByteBufferAllocator(preferDirect);

        protected override IByteBufferAllocator NewUnpooledAllocator() => new UnpooledByteBufferAllocator(false);
    }
}
