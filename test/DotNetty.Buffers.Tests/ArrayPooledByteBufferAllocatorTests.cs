using Xunit;

namespace DotNetty.Buffers.Tests
{
    public class ArrayPooledByteBufferAllocatorTests : AbstractByteBufferAllocatorTests
    {
#if NETCOREAPP
        protected override int ExpectedCapacity { get; } = 16; // for arraypool<byte>.shared
#else
        protected override int ExpectedCapacity { get; } = 128; // for BufferManager
#endif

        protected override IByteBufferAllocator NewAllocator(bool preferDirect) => new ArrayPooledByteBufferAllocator(preferDirect);

        protected override IByteBufferAllocator NewUnpooledAllocator() => new UnpooledByteBufferAllocator(false);
    }
}
