namespace DotNetty.Transport.Tests.Channel
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class CoalescingBufferQueueTest : IDisposable
    {
        private IByteBuffer cat;
        private IByteBuffer mouse;

        private IPromise catPromise, emptyPromise;
        private IPromise voidPromise;
        //private ChannelFutureListener mouseListener;

        private bool mouseDone;
        private bool mouseSuccess;

        private EmbeddedChannel channel;
        private CoalescingBufferQueue writeQueue;

        public CoalescingBufferQueueTest()
        {
            this.mouseDone = false;
            this.mouseSuccess = false;
            this.channel = new EmbeddedChannel();
            this.writeQueue = new CoalescingBufferQueue(channel, 16, true);
            this.catPromise = new TaskCompletionSource();
            //    mouseListener = new ChannelFutureListener() {
            //    @Override
            //    public void operationComplete(ChannelFuture future) throws Exception {
            //        mouseDone = true;
            //        mouseSuccess = future.IsSuccess();
            //    }
            //};
            this.emptyPromise = channel.NewPromise();
            this.voidPromise = channel.VoidPromise();

            this.cat = Unpooled.WrappedBuffer(Encoding.ASCII.GetBytes("cat"));
            this.mouse = Unpooled.WrappedBuffer(Encoding.ASCII.GetBytes("mouse"));
        }

        public void Dispose()
        {
            Assert.False(this.channel.Finish());
        }

        [Fact]
        public void AddFirstPromiseRetained()
        {
            writeQueue.Add(cat, catPromise);
            AssertQueueSize(3, false);
            writeQueue.Add(mouse, NewFutureListener());
            AssertQueueSize(8, false);
            IPromise aggregatePromise = NewPromise();
            Assert.Equal("catmous", Dequeue(7, aggregatePromise));
            IByteBuffer remainder = Unpooled.WrappedBuffer(Encoding.ASCII.GetBytes("mous"));
            writeQueue.AddFirst(remainder, aggregatePromise);
            IPromise aggregatePromise2 = NewPromise();
            Assert.Equal("mouse", Dequeue(5, aggregatePromise2));
            aggregatePromise2.Complete();
            Assert.True(catPromise.IsSuccess);
            Assert.True(mouseSuccess);
            Assert.Equal(0, cat.ReferenceCount);
            Assert.Equal(0, mouse.ReferenceCount);
        }

        [Fact]
        public void AddFirstVoidPromise()
        {
            writeQueue.Add(cat, catPromise);
            AssertQueueSize(3, false);
            writeQueue.Add(mouse, NewFutureListener());
            AssertQueueSize(8, false);
            IPromise aggregatePromise = NewPromise();
            Assert.Equal("catmous", Dequeue(7, aggregatePromise));
            IByteBuffer remainder = Unpooled.WrappedBuffer(Encoding.ASCII.GetBytes("mous"));
            writeQueue.AddFirst(remainder, voidPromise);
            IPromise aggregatePromise2 = NewPromise();
            Assert.Equal("mouse", Dequeue(5, aggregatePromise2));
            aggregatePromise2.Complete();
            // Because we used a void promise above, we shouldn't complete catPromise until aggregatePromise is completed.
            Assert.False(catPromise.IsSuccess);
            Assert.True(mouseSuccess);
            aggregatePromise.Complete();
            Assert.True(catPromise.IsSuccess);
            Assert.True(mouseSuccess);
            Assert.Equal(0, cat.ReferenceCount);
            Assert.Equal(0, mouse.ReferenceCount);
        }

        [Fact]
        public void AggregateWithFullRead()
        {
            writeQueue.Add(cat, catPromise);
            AssertQueueSize(3, false);
            writeQueue.Add(mouse, NewFutureListener());
            AssertQueueSize(8, false);
            IPromise aggregatePromise = NewPromise();
            Assert.Equal("catmouse", Dequeue(8, aggregatePromise));
            AssertQueueSize(0, true);
            Assert.False(catPromise.IsSuccess);
            Assert.False(mouseDone);
            aggregatePromise.Complete();
            Assert.True(catPromise.IsSuccess);
            Assert.True(mouseSuccess);
            Assert.Equal(0, cat.ReferenceCount);
            Assert.Equal(0, mouse.ReferenceCount);
        }

        [Fact]
        public void WithVoidPromise()
        {
            writeQueue.Add(cat, voidPromise);
            writeQueue.Add(mouse, voidPromise);
            AssertQueueSize(8, false);
            Assert.Equal("catm", Dequeue(4, NewPromise()));
            AssertQueueSize(4, false);
            Assert.Equal("ouse", Dequeue(4, NewPromise()));
            AssertQueueSize(0, true);
            Assert.Equal(0, cat.ReferenceCount);
            Assert.Equal(0, mouse.ReferenceCount);
        }

        [Fact]
        public void AggregateWithPartialRead()
        {
            writeQueue.Add(cat, catPromise);
            writeQueue.Add(mouse, NewFutureListener());
            IPromise aggregatePromise = NewPromise();
            Assert.Equal("catm", Dequeue(4, aggregatePromise));
            AssertQueueSize(4, false);
            Assert.False(catPromise.IsSuccess);
            Assert.False(mouseDone);
            aggregatePromise.Complete();
            Assert.True(catPromise.IsSuccess);
            Assert.False(mouseDone);

            aggregatePromise = NewPromise();
            Assert.Equal("ouse", Dequeue(int.MaxValue, aggregatePromise));
            AssertQueueSize(0, true);
            Assert.False(mouseDone);
            aggregatePromise.Complete();
            Assert.True(mouseSuccess);
            Assert.Equal(0, cat.ReferenceCount);
            Assert.Equal(0, mouse.ReferenceCount);
        }

        [Fact]
        public void ReadExactAddedBufferSizeReturnsOriginal()
        {
            writeQueue.Add(cat, catPromise);
            writeQueue.Add(mouse, NewFutureListener());

            IPromise aggregatePromise = NewPromise();
            Assert.Same(cat, writeQueue.Remove(3, aggregatePromise));
            Assert.False(catPromise.IsSuccess);
            aggregatePromise.Complete();
            Assert.True(catPromise.IsSuccess);
            Assert.Equal(1, cat.ReferenceCount);
            cat.Release();

            aggregatePromise = NewPromise();
            Assert.Same(mouse, writeQueue.Remove(5, aggregatePromise));
            Assert.False(mouseDone);
            aggregatePromise.Complete();
            Assert.True(mouseSuccess);
            Assert.Equal(1, mouse.ReferenceCount);
            mouse.Release();
        }

        [Fact]
        public void ReadEmptyQueueReturnsEmptyBuffer()
        {
            // Not used in this test.
            cat.Release();
            mouse.Release();

            AssertQueueSize(0, true);
            IPromise aggregatePromise = NewPromise();
            Assert.Equal("", Dequeue(int.MaxValue, aggregatePromise));
            AssertQueueSize(0, true);
        }

        [Fact]
        public void ReleaseAndFailAll()
        {
            writeQueue.Add(cat, catPromise);
            writeQueue.Add(mouse, NewFutureListener());
            var cause = new Exception("ooops");
            writeQueue.ReleaseAndFailAll(cause);
            IPromise aggregatePromise = NewPromise();
            AssertQueueSize(0, true);
            Assert.Equal(0, cat.ReferenceCount);
            Assert.Equal(0, mouse.ReferenceCount);
            Assert.Same(cause, catPromise.Task.Exception.InnerException);
            Assert.Equal("", Dequeue(int.MaxValue, aggregatePromise));
            AssertQueueSize(0, true);
        }

        [Fact]
        public void EmptyBuffersAreCoalesced()
        {
            IByteBuffer empty = Unpooled.Buffer(0, 1);
            AssertQueueSize(0, true);
            writeQueue.Add(cat, catPromise);
            writeQueue.Add(empty, emptyPromise);
            AssertQueueSize(3, false);
            IPromise aggregatePromise = NewPromise();
            Assert.Equal("cat", Dequeue(3, aggregatePromise));
            AssertQueueSize(0, true);
            Assert.False(catPromise.IsSuccess);
            Assert.False(emptyPromise.IsSuccess);
            aggregatePromise.Complete();
            Assert.True(catPromise.IsSuccess);
            Assert.True(emptyPromise.IsSuccess);
            Assert.Equal(0, cat.ReferenceCount);
            Assert.Equal(0, empty.ReferenceCount);
        }

        [Fact]
        public void Merge()
        {
            writeQueue.Add(cat, catPromise);
            CoalescingBufferQueue otherQueue = new CoalescingBufferQueue(channel);
            otherQueue.Add(mouse, NewFutureListener());
            otherQueue.CopyTo(writeQueue);
            AssertQueueSize(8, false);
            IPromise aggregatePromise = NewPromise();
            Assert.Equal("catmouse", Dequeue(8, aggregatePromise));
            AssertQueueSize(0, true);
            Assert.False(catPromise.IsSuccess);
            Assert.False(mouseDone);
            aggregatePromise.Complete();
            Assert.True(catPromise.IsSuccess);
            Assert.True(mouseSuccess);
            Assert.Equal(0, cat.ReferenceCount);
            Assert.Equal(0, mouse.ReferenceCount);
        }

        [Fact]
        public void WritabilityChanged()
        {
            WritabilityChanged0(false);
        }

        [Fact]
        public void WritabilityChangedFailAll()
        {
            WritabilityChanged0(true);
        }

        private void WritabilityChanged0(bool fail)
        {
            //channel.Configuration.SetWriteBufferWaterMark(new WriteBufferWaterMark(3, 4));
            channel.Configuration.WriteBufferLowWaterMark = 3;
            channel.Configuration.WriteBufferHighWaterMark = 4;
            Assert.True(channel.IsWritable);
            writeQueue.Add(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 }));
            Assert.True(channel.IsWritable);
            writeQueue.Add(Unpooled.WrappedBuffer(new byte[] { 4, 5 }));
            Assert.False(channel.IsWritable);
            Assert.Equal(5, writeQueue.ReadableBytes());

            if (fail)
            {
                writeQueue.ReleaseAndFailAll(new InvalidOperationException());
            }
            else
            {
                IByteBuffer buffer = writeQueue.RemoveFirst(voidPromise);
                Assert.Equal(1, buffer.ReadByte());
                Assert.Equal(2, buffer.ReadByte());
                Assert.Equal(3, buffer.ReadByte());
                Assert.False(buffer.IsReadable());
                buffer.Release();
                Assert.True(channel.IsWritable);

                buffer = writeQueue.RemoveFirst(voidPromise);
                Assert.Equal(4, buffer.ReadByte());
                Assert.Equal(5, buffer.ReadByte());
                Assert.False(buffer.IsReadable());
                buffer.Release();
            }

            Assert.True(channel.IsWritable);
            Assert.True(writeQueue.IsEmpty());
        }

        private IPromise NewPromise()
        {
            return this.channel.NewPromise();
        }

        private IPromise NewFutureListener()
        {
            var promise = this.channel.NewPromise();
            promise.Task.ContinueWith(t =>
            {
                this.mouseDone = true;
                this.mouseSuccess = t.IsCompleted && !t.IsFaulted && !t.IsCanceled;
            }, TaskContinuationOptions.ExecuteSynchronously);
            return promise;
        }

        private void AssertQueueSize(int size, bool isEmpty)
        {
            Assert.Equal(size, writeQueue.ReadableBytes());
            if (isEmpty)
            {
                Assert.True(writeQueue.IsEmpty());
            }
            else
            {
                Assert.False(writeQueue.IsEmpty());
            }
        }

        private string Dequeue(int numBytes, IPromise aggregatePromise)
        {
            IByteBuffer removed = writeQueue.Remove(numBytes, aggregatePromise);
            string result = removed.ToString(Encoding.ASCII);
            ReferenceCountUtil.SafeRelease(removed);
            return result;
        }

    }
}
