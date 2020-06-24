namespace DotNetty.Transport.Tests.Channel
{
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using Moq;
    using Xunit;

    public class AdaptiveRecvByteBufAllocatorTest
    {
        private Mock<IChannelConfiguration> _config;
        private IByteBufferAllocator _alloc = UnpooledByteBufferAllocator.Default;
        private IRecvByteBufAllocatorHandle _handle;

        public AdaptiveRecvByteBufAllocatorTest()
        {
            _config = new Mock<IChannelConfiguration>();
            _config.SetupGet(x => x.IsAutoRead).Returns(true);
            AdaptiveRecvByteBufAllocator recvByteBufAllocator = new AdaptiveRecvByteBufAllocator(64, 512, 1024 * 1024 * 10);
            _handle = recvByteBufAllocator.NewHandle();
            _handle.Reset(_config.Object);
        }

        [Fact]
        public void RampUpBeforeReadCompleteWhenLargeDataPending()
        {
            // Simulate that there is always more data when we attempt to read so we should always ramp up.
            AllocReadExpected(_handle, _alloc, 512);
            AllocReadExpected(_handle, _alloc, 8192);
            AllocReadExpected(_handle, _alloc, 131072);
            AllocReadExpected(_handle, _alloc, 2097152);
            _handle.ReadComplete();

            _handle.Reset(_config.Object);
            AllocReadExpected(_handle, _alloc, 8388608);
        }

        [Fact]
        public void MemoryAllocationIntervalsTest()
        {
            ComputingNext(512, 512);
            ComputingNext(8192, 1110);
            ComputingNext(8192, 1200);
            ComputingNext(4096, 1300);
            ComputingNext(4096, 1500);
            ComputingNext(2048, 1700);
            ComputingNext(2048, 1550);
            ComputingNext(2048, 2000);
            ComputingNext(2048, 1900);
        }

        private void ComputingNext(long expectedSize, int actualReadBytes)
        {
            Assert.Equal(expectedSize, _handle.Guess());
            _handle.Reset(_config.Object);
            _handle.LastBytesRead = actualReadBytes;
            _handle.ReadComplete();
        }

        [Fact]
        public void LastPartialReadDoesNotRampDown()
        {
            AllocReadExpected(_handle, _alloc, 512);
            // Simulate there is just 1 byte remaining which is unread. However the total bytes in the current read cycle
            // means that we should stay at the current step for the next ready cycle.
            AllocRead(_handle, _alloc, 8192, 1);
            _handle.ReadComplete();

            _handle.Reset(_config.Object);
            AllocReadExpected(_handle, _alloc, 8192);
        }

        [Fact]
        public void LastPartialReadCanRampUp()
        {
            AllocReadExpected(_handle, _alloc, 512);
            // We simulate there is just 1 less byte than we try to read, but because of the adaptive steps the total amount
            // of bytes read for this read cycle steps up to prepare for the next read cycle.
            AllocRead(_handle, _alloc, 8192, 8191);
            _handle.ReadComplete();

            _handle.Reset(_config.Object);
            AllocReadExpected(_handle, _alloc, 131072);
        }

        private static void AllocReadExpected(IRecvByteBufAllocatorHandle handle,
                                              IByteBufferAllocator alloc,
                                              int expectedSize)
        {
            AllocRead(handle, alloc, expectedSize, expectedSize);
        }

        private static void AllocRead(IRecvByteBufAllocatorHandle handle,
                                      IByteBufferAllocator alloc,
                                      int expectedBufferSize,
                                      int lastRead)
        {
            IByteBuffer buf = handle.Allocate(alloc);
            Assert.Equal(expectedBufferSize, buf.Capacity);
            handle.AttemptedBytesRead = expectedBufferSize;
            handle.LastBytesRead = lastRead;
            handle.IncMessagesRead(1);
            buf.Release();
        }
    }
}
