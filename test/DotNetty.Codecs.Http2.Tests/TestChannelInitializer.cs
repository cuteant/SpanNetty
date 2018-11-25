
namespace DotNetty.Codecs.Http2.Tests
{
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Channel initializer useful in tests.
    /// </summary>
    public class TestChannelInitializer : ChannelInitializer<IChannel>
    {
        internal IChannelHandler handler;
        internal AtomicInteger maxReads = null;

        protected override void InitChannel(IChannel channel)
        {
            if (handler != null)
            {
                channel.Pipeline.AddLast(handler);
                handler = null;
            }
            if (maxReads != null)
            {
                channel.Configuration.RecvByteBufAllocator = new TestNumReadsRecvByteBufAllocator(maxReads);
            }
        }
    }

    sealed class TestNumReadsRecvByteBufAllocator : IRecvByteBufAllocator
    {
        private readonly AtomicInteger numReads;

        public TestNumReadsRecvByteBufAllocator(AtomicInteger numReads)
        {
            this.numReads = numReads;
        }

        public IRecvByteBufAllocatorHandle NewHandle()
        {
            return new TestNumReadsRecvByteBufAllocatorHandle(this.numReads);
        }
    }

    sealed class TestNumReadsRecvByteBufAllocatorHandle : IRecvByteBufAllocatorHandle
    {
        private int attemptedBytesRead;
        private int lastBytesRead;
        private int numMessagesRead;
        private readonly AtomicInteger numReads;

        public TestNumReadsRecvByteBufAllocatorHandle(AtomicInteger numReads) => this.numReads = numReads;

        public int LastBytesRead { get => this.lastBytesRead; set => this.lastBytesRead = value; }
        public int AttemptedBytesRead { get => this.attemptedBytesRead; set => this.attemptedBytesRead = value; }

        public IByteBuffer Allocate(IByteBufferAllocator alloc)
        {
            return alloc.Buffer(this.Guess(), this.Guess());
        }

        public bool ContinueReading()
        {
            return this.numMessagesRead < numReads.Value;
        }

        public int Guess()
        {
            return 1; // only ever allocate buffers of size 1 to ensure the number of reads is controlled.
        }

        public void IncMessagesRead(int numMessages)
        {
            this.numMessagesRead += numMessages;
        }

        public void ReadComplete()
        {
            // Nothing needs to be done or adjusted after each read cycle is completed.
        }

        public void Reset(IChannelConfiguration config)
        {
            this.numMessagesRead = 0;
        }
    }
}
