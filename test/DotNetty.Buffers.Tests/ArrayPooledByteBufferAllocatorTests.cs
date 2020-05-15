using Xunit;

namespace DotNetty.Buffers.Tests
{
    public class ArrayPooledByteBufferAllocatorTests : AbstractByteBufferAllocatorTests
    {
        protected override int ExpectedCapacity { get; } = 16; // for arraypool<byte>.shared

        protected override IByteBufferAllocator NewAllocator(bool preferDirect) => new ArrayPooledByteBufferAllocator(preferDirect);

        protected override IByteBufferAllocator NewUnpooledAllocator() => new UnpooledByteBufferAllocator(false);
    }
}
