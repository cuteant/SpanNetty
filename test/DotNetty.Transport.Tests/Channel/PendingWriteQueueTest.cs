namespace DotNetty.Transport.Tests.Channel
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class PendingWriteQueueTest
    {
        private static readonly object s_lock = new object();

        [Fact]
        public void TestRemoveAndWrite()
        {
            AssertWrite(new TestHandler0(), 1);
        }

        class TestHandler0 : TestHandler
        {
            public override void Flush(IChannelHandlerContext context)
            {
                Assert.False(context.Channel.IsWritable, "Should not be writable anymore");

                var future = _queue.RemoveAndWriteAsync();
                future.ContinueWith(t => AssertQueueEmpty(_queue), TaskContinuationOptions.ExecuteSynchronously);
                base.Flush(context);
            }
        }

        [Fact]
        public void TestRemoveAndWriteAll()
        {
            AssertWrite(new TestHandler1(), 3);
        }

        class TestHandler1 : TestHandler
        {
            public override void Flush(IChannelHandlerContext context)
            {
                Assert.False(context.Channel.IsWritable, "Should not be writable anymore");

                var future = _queue.RemoveAndWriteAllAsync();
                future.ContinueWith(t => AssertQueueEmpty(_queue), TaskContinuationOptions.ExecuteSynchronously);
                base.Flush(context);
            }
        }

        [Fact]
        public void TestRemoveAndFail()
        {
            AssertWriteFails(new TestHandler2(), 1);
        }

        class TestHandler2 : TestHandler
        {
            public override void Flush(IChannelHandlerContext context)
            {
                _queue.RemoveAndFail(new TestException());
                base.Flush(context);
            }
        }

        [Fact]
        public void TestRemoveAndFailAll()
        {
            AssertWriteFails(new TestHandler3(), 3);
        }

        class TestHandler3 : TestHandler
        {
            public override void Flush(IChannelHandlerContext context)
            {
                _queue.RemoveAndFailAll(new TestException());
                base.Flush(context);
            }
        }

        [Fact]
        public void ShouldFireChannelWritabilityChangedAfterRemoval()
        {
            AtomicReference<IChannelHandlerContext> ctxRef = new AtomicReference<IChannelHandlerContext>();
            AtomicReference<PendingWriteQueue> queueRef = new AtomicReference<PendingWriteQueue>();
            IByteBuffer msg = Unpooled.CopiedBuffer("test", Encoding.ASCII);

            EmbeddedChannel channel = new EmbeddedChannel(new ChannelInboundHandlerAdapter0(ctxRef, queueRef));

            channel.Configuration.WriteBufferLowWaterMark = 1;
            channel.Configuration.WriteBufferHighWaterMark = 3;

            PendingWriteQueue queue = queueRef.Value;

            // Trigger channelWritabilityChanged() by adding a message that's larger than the high watermark.
            queue.Add(msg, channel.NewPromise());

            channel.Finish();

            Assert.Equal(0, msg.ReferenceCount);
        }

        class ChannelInboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            private readonly AtomicReference<IChannelHandlerContext> _ctxRef;
            private readonly AtomicReference<PendingWriteQueue> _queueRef;

            public ChannelInboundHandlerAdapter0(AtomicReference<IChannelHandlerContext> ctxRef, AtomicReference<PendingWriteQueue> queueRef)
            {
                _ctxRef = ctxRef;
                _queueRef = queueRef;
            }

            public override void HandlerAdded(IChannelHandlerContext context)
            {
                _ctxRef.Value = context;
                _queueRef.Value = new PendingWriteQueue(context);
            }

            public override void ChannelWritabilityChanged(IChannelHandlerContext context)
            {
                PendingWriteQueue queue = _queueRef.Value;

                IByteBuffer msg = (IByteBuffer)queue.Current;
                if (msg is null)
                {
                    return;
                }

                Assert.Equal(1, msg.ReferenceCount);

                // This call will trigger another channelWritabilityChanged() event because the number of
                // pending bytes will go below the low watermark.
                //
                // If PendingWriteQueue.remove() did not remove the current entry before triggering
                // channelWritabilityChanged() event, we will end up with attempting to remove the same
                // element twice, resulting in the double release.
                queue.Remove();

                Assert.Equal(0, msg.ReferenceCount);
            }
        }

        private static void AssertWrite(IChannelHandler handler, int count)
        {
            IByteBuffer buffer = Unpooled.CopiedBuffer("Test", Encoding.ASCII);
            EmbeddedChannel channel = new EmbeddedChannel(handler);
            channel.Configuration.WriteBufferLowWaterMark = 1;
            channel.Configuration.WriteBufferHighWaterMark = 3;

            IByteBuffer[] buffers = new IByteBuffer[count];
            for (int i = 0; i < buffers.Length; i++)
            {
                buffers[i] = buffer.RetainedDuplicate();
            }
            Assert.True(channel.WriteOutbound(buffers));
            Assert.True(channel.Finish());
            try
            {
                channel.CloseCompletion.GetAwaiter().GetResult();
            }
            catch { }

            for (int i = 0; i < buffers.Length; i++)
            {
                AssertBuffer(channel, buffer);
            }
            buffer.Release();
            Assert.Null(channel.ReadOutbound());
        }

        private static void AssertBuffer(EmbeddedChannel channel, IByteBuffer buffer)
        {
            IByteBuffer written = channel.ReadOutbound<IByteBuffer>();
            Assert.Equal(buffer, written);
            written.Release();
        }

        private static void AssertQueueEmpty(PendingWriteQueue queue)
        {
            Assert.True(queue.IsEmpty);
            Assert.Equal(0, queue.Size);
            Assert.Equal(0, queue.Bytes);
            Assert.Null(queue.Current);
            Assert.Null(queue.RemoveAndWriteAsync());
            Assert.Null(queue.RemoveAndWriteAllAsync());
        }

        private static void AssertWriteFails(IChannelHandler handler, int count)
        {
            IByteBuffer buffer = Unpooled.CopiedBuffer("Test", Encoding.ASCII);
            EmbeddedChannel channel = new EmbeddedChannel(handler);
            IByteBuffer[] buffers = new IByteBuffer[count];
            for (int i = 0; i < buffers.Length; i++)
            {
                buffers[i] = buffer.RetainedDuplicate();
            }
            try
            {
                Assert.False(channel.WriteOutbound(buffers));
                Assert.False(true);
            }
            catch (Exception e)
            {
                if (e is AggregateException aggregateException)
                {
                    Assert.IsType<TestException>(aggregateException.InnerException);
                }
                else
                {
                    Assert.IsType<TestException>(e);
                }
            }
            Assert.False(channel.Finish());
            try
            {
                channel.CloseCompletion.GetAwaiter().GetResult();
            }
            catch { }

            buffer.Release();
            Assert.Null(channel.ReadOutbound());
        }

        private static EmbeddedChannel NewChannel()
        {
            // Add a handler so we can access a ChannelHandlerContext via the ChannelPipeline.
            return new EmbeddedChannel(new ChannelHandlerAdapter() { });
        }

        [Fact]
        public void TestRemoveAndFailAllReentrantFailAll()
        {
            EmbeddedChannel channel = NewChannel();
            PendingWriteQueue queue = new PendingWriteQueue(channel.Pipeline.FirstContext());

            IPromise promise = channel.NewPromise();
            promise.Task.ContinueWith(t => queue.RemoveAndFailAll(new InvalidOperationException()), TaskContinuationOptions.ExecuteSynchronously);
            queue.Add(1L, promise);

            IPromise promise2 = channel.NewPromise();
            queue.Add(2L, promise2);
            queue.RemoveAndFailAll(new Exception());
            Assert.True(promise.IsCompleted);
            Assert.False(promise.IsSuccess);
            Assert.True(promise2.IsCompleted);
            Assert.False(promise2.IsSuccess);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void TestRemoveAndWriteAllReentrantWrite()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new ChannelOutboundHandlerAdapter0(), new ChannelHandlerAdapter());

            PendingWriteQueue queue = new PendingWriteQueue(channel.Pipeline.LastContext());

            IPromise promise = channel.NewPromise();
            IPromise promise3 = channel.NewPromise();
            promise.Task.ContinueWith(t => queue.Add(3L, promise3), TaskContinuationOptions.ExecuteSynchronously);
            queue.Add(1L, promise);
            IPromise promise2 = channel.NewPromise();
            queue.Add(2L, promise2);
            queue.RemoveAndWriteAllAsync();

            Assert.True(promise.IsCompleted);
            Assert.True(promise.IsSuccess);
            Assert.True(promise2.IsCompleted);
            Assert.True(promise2.IsSuccess);
            Assert.True(promise3.IsCompleted);
            Assert.True(promise3.IsSuccess);
            Assert.True(channel.Finish());
            Assert.Equal(1L, channel.ReadOutbound<long>());
            Assert.Equal(2L, channel.ReadOutbound<long>());
            Assert.Equal(3L, channel.ReadOutbound<long>());
        }

        class ChannelOutboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                // Convert to writeAndFlush(...) so the promise will be notified by the transport.
                context.WriteAndFlushAsync(message, promise);
            }
        }

        [Fact]
        public void TestRemoveAndWriteAllWithVoidPromise()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new ChannelOutboundHandlerAdapter0(), new ChannelHandlerAdapter());

            PendingWriteQueue queue = new PendingWriteQueue(channel.Pipeline.LastContext());

            IPromise promise = channel.NewPromise();
            queue.Add(1L, promise);
            queue.Add(2L, channel.VoidPromise());
            queue.RemoveAndWriteAllAsync();

            Assert.True(channel.Finish());
            Assert.True(promise.IsCompleted);
            Assert.True(promise.IsSuccess);
            Assert.Equal(1L, channel.ReadOutbound<long>());
            Assert.Equal(2L, channel.ReadOutbound<long>());
        }

        [Fact]
        public void TestRemoveAndFailAllReentrantWrite()
        {
            List<int> failOrder = new List<int>();
            EmbeddedChannel channel = NewChannel();
            PendingWriteQueue queue = new PendingWriteQueue(channel.Pipeline.FirstContext());

            IPromise promise = channel.NewPromise();
            IPromise promise3 = channel.NewPromise();
            promise3.Task.ContinueWith(t =>
            {
                lock (s_lock)
                {
                    failOrder.Add(3);
                }
            }, TaskContinuationOptions.ExecuteSynchronously);
            promise.Task.ContinueWith(t =>
            {
                lock (s_lock)
                {
                    failOrder.Add(1);
                }
                queue.Add(3L, promise3);
            }, TaskContinuationOptions.ExecuteSynchronously);
            queue.Add(1L, promise);

            IPromise promise2 = channel.NewPromise();
            promise2.Task.ContinueWith(t =>
            {
                lock (s_lock)
                {
                    failOrder.Add(2);
                }
            }, TaskContinuationOptions.ExecuteSynchronously);
            queue.Add(2L, promise2);
            queue.RemoveAndFailAll(new Exception());
            Assert.True(promise.IsCompleted);
            Assert.False(promise.IsSuccess);
            Assert.True(promise2.IsCompleted);
            Assert.False(promise2.IsSuccess);
            Assert.True(promise3.IsCompleted);
            Assert.False(promise3.IsSuccess);
            Assert.False(channel.Finish());
            Assert.Equal(1, failOrder[0]);
            Assert.Equal(2, failOrder[1]);
            Assert.Equal(3, failOrder[2]);
        }

        [Fact]
        public void TestRemoveAndWriteAllReentrance()
        {
            EmbeddedChannel channel = NewChannel();
            PendingWriteQueue queue = new PendingWriteQueue(channel.Pipeline.FirstContext());

            IPromise promise = channel.NewPromise();
            promise.Task.ContinueWith(t => queue.RemoveAndWriteAllAsync(), TaskContinuationOptions.ExecuteSynchronously);
            queue.Add(1L, promise);

            IPromise promise2 = channel.NewPromise();
            queue.Add(2L, promise2);
            queue.RemoveAndWriteAllAsync();
            channel.Flush();
            Assert.True(promise.IsSuccess);
            Assert.True(promise2.IsSuccess);
            Assert.True(channel.Finish());

            Assert.Equal(1L, channel.ReadOutbound<long>());
            Assert.Equal(2L, channel.ReadOutbound<long>());
            Assert.Null(channel.ReadOutbound());
            Assert.Null(channel.ReadOutbound());
        }

        // See https://github.com/netty/netty/issues/3967
        [Fact]
        public void TestCloseChannelOnCreation()
        {
            EmbeddedChannel channel = NewChannel();
            IChannelHandlerContext context = channel.Pipeline.FirstContext();
            try
            {
                channel.CloseAsync().GetAwaiter().GetResult();
            }
            catch { }

            PendingWriteQueue queue = new PendingWriteQueue(context);

            var ex = new InvalidOperationException();
            IPromise promise = channel.NewPromise();
            queue.Add(1L, promise);
            queue.RemoveAndFailAll(ex);
            Assert.Same(ex, promise.Task.Exception.InnerException);
        }

        class TestHandler : ChannelDuplexHandler
        {
            protected PendingWriteQueue _queue;
            private int _expectedSize;

            public override void ChannelActive(IChannelHandlerContext context)
            {
                base.ChannelActive(context);
                AssertQueueEmpty(_queue);
                Assert.True(context.Channel.IsWritable); // Should be writable
            }

            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                _queue.Add(message, promise);
                Assert.False(_queue.IsEmpty);
                Assert.Equal(++_expectedSize, _queue.Size);
                Assert.NotNull(_queue.Current);
            }

            public override void HandlerAdded(IChannelHandlerContext context)
            {
                _queue = new PendingWriteQueue(context);
            }
        }

        class TestException : Exception
        {

        }
    }
}