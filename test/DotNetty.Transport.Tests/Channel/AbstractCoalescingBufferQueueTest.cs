namespace DotNetty.Transport.Tests.Channel
{
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class AbstractCoalescingBufferQueueTest
    {
        // See https://github.com/netty/netty/issues/10286
        [Fact]
        public void TestDecrementAllWhenWriteAndRemoveAll()
        {
            TestDecrementAll(true);
        }

        // See https://github.com/netty/netty/issues/10286
        [Fact]
        public void TestDecrementAllWhenReleaseAndFailAll()
        {
            TestDecrementAll(false);
        }

        private static void TestDecrementAll(bool write)
        {
            EmbeddedChannel channel = new EmbeddedChannel(new ChannelOutboundHandlerAdapter0(), new ChannelHandlerAdapter());
            AbstractCoalescingBufferQueue queue = new TestCoalescingBufferQueue(channel, 128);

            byte[] bytes = new byte[128];
            var promise = new DefaultPromise();
            promise.Task.ContinueWith(t =>
            {
                queue.Add(Unpooled.WrappedBuffer(bytes));
                Assert.Equal(bytes.Length, queue.ReadableBytes());
            }, TaskContinuationOptions.ExecuteSynchronously);
            queue.Add(Unpooled.WrappedBuffer(bytes), promise);

            Assert.Equal(bytes.Length, queue.ReadableBytes());

            IChannelHandlerContext ctx = channel.Pipeline.LastContext();
            if (write)
            {
                queue.WriteAndRemoveAll(ctx);
            }
            else
            {
                queue.ReleaseAndFailAll(/*ctx, */new ClosedChannelException());
            }
            IByteBuffer buffer = queue.Remove(channel.Allocator, 128, channel.NewPromise());
            Assert.False(buffer.IsReadable());
            buffer.Release();

            Assert.True(queue.IsEmpty());
            Assert.Equal(0, queue.ReadableBytes());

            Assert.False(channel.Finish());
        }

        class ChannelOutboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            public override void Write(IChannelHandlerContext ctx, object msg, IPromise promise)
            {
                ReferenceCountUtil.Release(msg);
                promise.Complete();
            }
        }

        class TestCoalescingBufferQueue : AbstractCoalescingBufferQueue
        {
            public TestCoalescingBufferQueue(IChannel channel, int initSize)
                : base(channel, initSize)
            {
            }

            protected override IByteBuffer Compose(IByteBufferAllocator alloc, IByteBuffer cumulation, IByteBuffer next)
            {
                return ComposeIntoComposite(alloc, cumulation, next);
            }

            protected override IByteBuffer RemoveEmptyValue()
            {
                return Unpooled.Empty;
            }
        }
    }
}
