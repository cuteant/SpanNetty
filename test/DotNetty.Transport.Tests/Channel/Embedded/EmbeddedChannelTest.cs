// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Channel.Embedded
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class EmbeddedChannelTest
    {
        [Fact]
        public void TestNotRegistered()
        {
            EmbeddedChannel channel = new EmbeddedChannel(hasDisconnect: false, register: false);
            Assert.False(channel.Registered);
            channel.Register();
            Assert.True(channel.Registered);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void TestRegistered()
        {
            EmbeddedChannel channel = new EmbeddedChannel(hasDisconnect: false, register: true);
            Assert.True(channel.Registered);
            Assert.Throws<InvalidOperationException>(() => channel.Register());
            Assert.False(channel.Finish());
        }

        class ChannelHandler1 : ChannelHandlerAdapter
        {
            readonly int first;
            readonly int second;

            public ChannelHandler1(int first, int second)
            {
                this.first = first;
                this.second = second;
            }

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                context.FireChannelRead(this.first);
                context.FireChannelRead(this.second);
            }
        }

        [Fact]
        public void TestConstructWithChannelInitializer()
        {
            int first = 1;
            int second = 2;
            IChannelHandler handler = new ChannelHandler1(first, second);
            var channel = new EmbeddedChannel(new ActionChannelInitializer<IChannel>(ch => { ch.Pipeline.AddLast(handler); }));
            IChannelPipeline pipeline = channel.Pipeline;
            Assert.Same(handler, pipeline.FirstContext().Handler);
            Assert.True(channel.WriteInbound(3));
            Assert.True(channel.Finish());
            Assert.Equal(first, channel.ReadInbound<object>());
            Assert.Equal(second, channel.ReadInbound<object>());
            Assert.Null(channel.ReadInbound<object>());
        }

        [Fact]
        public void TestScheduling()
        {
            var ch = new EmbeddedChannel(new ChannelHandlerAdapter());
            var latch = new CountdownEvent(2);
            Task future = ch.EventLoop.ScheduleAsync(() => latch.Signal(), TimeSpan.FromSeconds(1));
            future.ContinueWith(t => latch.Signal());
            PreciseTimeSpan next = ch.RunScheduledPendingTasks();
            Assert.True(next > PreciseTimeSpan.Zero);
            // Sleep for the nanoseconds but also give extra 50ms as the clock my not be very precise and so fail the test
            // otherwise.
            Thread.Sleep(next.ToTimeSpan() + TimeSpan.FromMilliseconds(50));
            Assert.Equal(PreciseTimeSpan.MinusOne, ch.RunScheduledPendingTasks());
            latch.Wait();
        }

        [Fact]
        public void TestScheduledCancelled()
        {
            var ch = new EmbeddedChannel(new ChannelHandlerAdapter());
            Task future = ch.EventLoop.ScheduleAsync(() => { }, TimeSpan.FromDays(1));
            ch.Finish();
            Assert.True(future.IsCanceled);
        }

        [Fact]
        public async Task TestScheduledCancelledDirectly()
        {
            var ch = new EmbeddedChannel(new ChannelHandlerAdapter());

            IScheduledTask task1 = ch.EventLoop.Schedule(() => { }, new TimeSpan(1));
            IScheduledTask task2 = ch.EventLoop.Schedule(() => { }, new TimeSpan(1));
            IScheduledTask task3 = ch.EventLoop.Schedule(() => { }, new TimeSpan(1));
            task2.Cancel();
            ch.RunPendingTasks();
            Task<bool> checkTask1 = ch.EventLoop.SubmitAsync(() => task1.Completion.IsCompleted);
            Task<bool> checkTask2 = ch.EventLoop.SubmitAsync(() => task2.Completion.IsCanceled);
            Task<bool> checkTask3 = ch.EventLoop.SubmitAsync(() => task3.Completion.IsCompleted);
            ch.RunPendingTasks();
            ch.CheckException();
            Assert.True(await checkTask1);
            Assert.True(await checkTask2);
            Assert.True(await checkTask3);
        }

        [Fact]
        public async Task TestScheduledCancelledAsync()
        {
            var ch = new EmbeddedChannel(new ChannelHandlerAdapter());
            var cts = new CancellationTokenSource();
            Task task = ch.EventLoop.ScheduleAsync(() => { }, TimeSpan.FromDays(1), cts.Token);
            await Task.Run(() => cts.Cancel());
            Task<bool> checkTask = ch.EventLoop.SubmitAsync(() => task.IsCanceled);
            ch.RunPendingTasks();
            Assert.True(await checkTask);
        }

        class ChannelHandler3 : ChannelHandlerAdapter
        {
            readonly CountdownEvent latch;
            AtomicReference<Exception> error;

            public ChannelHandler3(CountdownEvent latch, AtomicReference<Exception> error)
            {
                this.latch = latch;
                this.error = error;
            }

            public override void HandlerAdded(IChannelHandlerContext context)
            {
                try
                {
                    Assert.True(context.Executor.InEventLoop);
                }
                catch (Exception ex)
                {
                    this.error = ex;
                }
                finally
                {
                    this.latch.Signal();
                }
            }
        }

        [Theory]
        [InlineData(3000)]
        public void TestHandlerAddedExecutedInEventLoop(int timeout)
        {
            var latch = new CountdownEvent(1);
            var ex = new AtomicReference<Exception>();
            IChannelHandler handler = new ChannelHandler3(latch, ex);
            var channel = new EmbeddedChannel(handler);
            Assert.False(channel.Finish());
            Assert.True(latch.Wait(timeout));
            Exception cause = ex.Value;
            if (cause != null)
            {
                throw cause;
            }
        }

        [Fact]
        public void TestConstructWithoutHandler()
        {
            var channel = new EmbeddedChannel();
            Assert.True(channel.WriteInbound(1));
            Assert.True(channel.WriteOutbound(2));
            Assert.True(channel.Finish());
            Assert.Equal(1, channel.ReadInbound<object>());
            Assert.Null(channel.ReadInbound<object>());
            Assert.Equal(2, channel.ReadOutbound<object>());
            Assert.Null(channel.ReadOutbound<object>());
        }

        [Theory]
        [InlineData(1000)]
        public void TestFireChannelInactiveAndUnregisteredOnClose(int timeout)
        {
            this.TestFireChannelInactiveAndUnregistered(channel => channel.CloseAsync(), timeout);

            this.TestFireChannelInactiveAndUnregistered(channel => channel.CloseAsync(channel.NewPromise()), timeout);
        }

        [Theory]
        [InlineData(1000)]
        public void TestFireChannelInactiveAndUnregisteredOnDisconnect(int timeout)
        {
            this.TestFireChannelInactiveAndUnregistered(channel => channel.DisconnectAsync(), timeout);

            this.TestFireChannelInactiveAndUnregistered(channel => channel.DisconnectAsync(channel.NewPromise()), timeout);
        }

        void TestFireChannelInactiveAndUnregistered(Func<IChannel, Task> action, int timeout)
        {
            var latch = new CountdownEvent(3);
            var channel = new EmbeddedChannel(new ChannelHandlerWithInactiveAndRegister(latch));
            action(channel);
            Assert.True(latch.Wait(timeout));
        }

        class ChannelHandlerWithInactiveAndRegister : ChannelHandlerAdapter
        {
            readonly CountdownEvent latch;

            public ChannelHandlerWithInactiveAndRegister(CountdownEvent latch)
            {
                this.latch = latch;
            }

            public override void ChannelInactive(IChannelHandlerContext context)
            {
                this.latch.Signal();
                context.Executor.Execute(() =>
                {
                    // should be executed
                    this.latch.Signal();
                });
            }

            public override void ChannelUnregistered(IChannelHandlerContext context) => this.latch.Signal();
        }

        sealed class EventOutboundHandler : ChannelHandlerAdapter
        {
            internal const int DISCONNECT = 0;
            internal const int CLOSE = 1;

            internal readonly Deque<int> queue = new Deque<int>();

            public override void Disconnect(IChannelHandlerContext context, IPromise promise)
            {
                this.queue.AddToBack(DISCONNECT);
                promise.TryComplete();
            }

            public override void Close(IChannelHandlerContext context, IPromise promise)
            {
                this.queue.AddToBack(CLOSE);
                promise.TryComplete();
            }

            public int PollEvent() => this.queue.RemoveFromFront();
        }

        [Fact]
        public async Task TestHasDisconnect()
        {
            EventOutboundHandler handler = new EventOutboundHandler();
            EmbeddedChannel channel = new EmbeddedChannel(true, handler);
            await channel.DisconnectAsync();
            await channel.CloseAsync();
            Assert.Equal(EventOutboundHandler.DISCONNECT, handler.PollEvent());
            Assert.Equal(EventOutboundHandler.CLOSE, handler.PollEvent());
            Assert.True(handler.queue.IsEmpty);
        }

        [Fact]
        public async Task TestHasNoDisconnect()
        {
            EventOutboundHandler handler = new EventOutboundHandler();
            EmbeddedChannel channel = new EmbeddedChannel(false, handler);
            await channel.DisconnectAsync();
            await channel.CloseAsync();
            Assert.Equal(EventOutboundHandler.CLOSE, handler.PollEvent());
            Assert.Equal(EventOutboundHandler.CLOSE, handler.PollEvent());
            Assert.True(handler.queue.IsEmpty);
        }

        [Fact]
        public void TestFinishAndReleaseAll()
        {
            IByteBuffer input = Unpooled.Buffer();
            IByteBuffer output = Unpooled.Buffer();
            try
            {
                EmbeddedChannel channel = new EmbeddedChannel();
                Assert.True(channel.WriteInbound(input));
                Assert.Equal(1, input.ReferenceCount);

                Assert.True(channel.WriteOutbound(output));
                Assert.Equal(1, output.ReferenceCount);

                Assert.True(channel.FinishAndReleaseAll());
                Assert.Equal(0, input.ReferenceCount);
                Assert.Equal(0, output.ReferenceCount);

                Assert.Null(channel.ReadInbound<object>());
                Assert.Null(channel.ReadOutbound<object>());
            }
            finally
            {
                Release(input, output);
            }
        }

        [Fact]
        public void TestReleaseInbound()
        {
            IByteBuffer input = Unpooled.Buffer();
            IByteBuffer output = Unpooled.Buffer();
            try
            {
                EmbeddedChannel channel = new EmbeddedChannel();
                Assert.True(channel.WriteInbound(input));
                Assert.Equal(1, input.ReferenceCount);

                Assert.True(channel.WriteOutbound(output));
                Assert.Equal(1, output.ReferenceCount);

                Assert.True(channel.ReleaseInbound());
                Assert.Equal(0, input.ReferenceCount);
                Assert.Equal(1, output.ReferenceCount);

                Assert.True(channel.Finish());
                Assert.Null(channel.ReadInbound<object>());

                IByteBuffer buffer = channel.ReadOutbound<IByteBuffer>();
                Assert.Same(output, buffer);
                buffer.Release();

                Assert.Null(channel.ReadOutbound<object>());
            }
            finally
            {
                Release(input, output);
            }
        }

        [Fact]
        public void TestReleaseOutbound()
        {
            IByteBuffer input = Unpooled.Buffer();
            IByteBuffer output = Unpooled.Buffer();
            try
            {
                EmbeddedChannel channel = new EmbeddedChannel();
                Assert.True(channel.WriteInbound(input));
                Assert.Equal(1, input.ReferenceCount);

                Assert.True(channel.WriteOutbound(output));
                Assert.Equal(1, output.ReferenceCount);

                Assert.True(channel.ReleaseOutbound());
                Assert.Equal(1, input.ReferenceCount);
                Assert.Equal(0, output.ReferenceCount);

                Assert.True(channel.Finish());
                Assert.Null(channel.ReadOutbound<object>());

                IByteBuffer buffer = channel.ReadInbound<IByteBuffer>();
                Assert.Same(input, buffer);
                buffer.Release();

                Assert.Null(channel.ReadInbound<object>());
            }
            finally
            {
                Release(input, output);
            }
        }

        class ChannelHandlerForWriteLater : ChannelHandlerAdapter
        {
            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                context.Executor.Execute(() => context.WriteAsync(message, promise));
            }
        }

        [Fact]
        public void TestWriteLater()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new ChannelHandlerForWriteLater());
            object msg = new object();

            Assert.True(channel.WriteOutbound(msg));
            Assert.True(channel.Finish());
            Assert.Same(msg, channel.ReadOutbound<object>());
            Assert.Null(channel.ReadOutbound<object>());
        }

        class ChannelHandlerForWriteScheduled : ChannelHandlerAdapter
        {
            readonly int delay;

            public ChannelHandlerForWriteScheduled(int delay) => this.delay = delay;

            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                context.Executor.Schedule(() => context.WriteAndFlushAsync(message, promise), TimeSpan.FromMilliseconds(this.delay));
            }
        }

        [Fact]
        public void TestWriteScheduled()
        {
            int delay = 500;
            EmbeddedChannel channel = new EmbeddedChannel(new ChannelHandlerForWriteScheduled(delay));
            object msg = new object();

            Assert.False(channel.WriteOutbound(msg));
            Thread.Sleep(delay * 2);
            Assert.True(channel.Finish());
            Assert.Same(msg, channel.ReadOutbound<object>());
            Assert.Null(channel.ReadOutbound<object>());
        }

        class ChannelHandlerForFlushInbound : ChannelHandlerAdapter
        {
            readonly CountdownEvent latch;

            public ChannelHandlerForFlushInbound(CountdownEvent latch) => this.latch = latch;

            public override void ChannelReadComplete(IChannelHandlerContext context) => this.latch.Signal();
        }

        [Fact]
        public void TestFlushInbound()
        {
            var latch = new CountdownEvent(1);
            EmbeddedChannel channel = new EmbeddedChannel(new ChannelHandlerForFlushInbound(latch));

            channel.FlushInbound();
            Assert.True(latch.Wait(TimeSpan.FromSeconds(1)));
        }

        class ChannelHandlerForWriteOneInbound : ChannelHandlerAdapter
        {
            readonly CountdownEvent latch;
            readonly AtomicInteger flushCount;

            public ChannelHandlerForWriteOneInbound(CountdownEvent latch, AtomicInteger flushCount)
            {
                this.latch = latch;
                this.flushCount = flushCount;
            }

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                ReferenceCountUtil.Release(message);
                this.latch.Signal();
            }

            public override void ChannelReadComplete(IChannelHandlerContext context) => this.flushCount.Increment();
        }

        [Fact]
        public async Task TestWriteOneInbound()
        {
            var latch = new CountdownEvent(1);
            var flushCount = new AtomicInteger(0);
            EmbeddedChannel channel = new EmbeddedChannel(new ChannelHandlerForWriteOneInbound(latch, flushCount));

#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
            channel.WriteOneInbound("Hello, Netty!");
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法

            Assert.True(latch.Wait(TimeSpan.FromSeconds(1L)));

            await channel.CloseAsync();

            // There was no #flushInbound() call so nobody should have called
            // #channelReadComplete()
            Assert.Equal(0, flushCount.Value);
        }

        class ChannelHandlerForFlushOutbound : ChannelHandlerAdapter
        {
            readonly CountdownEvent latch;

            public ChannelHandlerForFlushOutbound(CountdownEvent latch) => this.latch = latch;

            public override void Flush(IChannelHandlerContext context) => this.latch.Signal();
        }

        [Fact]
        public void TestFlushOutbound()
        {
            var latch = new CountdownEvent(1);
            EmbeddedChannel channel = new EmbeddedChannel(new ChannelHandlerForFlushOutbound(latch));

            channel.FlushOutbound();
            Assert.True(latch.Wait(TimeSpan.FromSeconds(1L)));
        }

        class ChannelHandlerForWriteOneOutbound : ChannelHandlerAdapter
        {
            readonly CountdownEvent latch;
            readonly AtomicInteger flushCount;

            public ChannelHandlerForWriteOneOutbound(CountdownEvent latch, AtomicInteger flushCount)
            {
                this.latch = latch;
                this.flushCount = flushCount;
            }

            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                context.WriteAsync(message, promise);
                this.latch.Signal();
            }

            public override void Flush(IChannelHandlerContext context)
            {
                this.flushCount.Increment();
            }
        }

        [Fact]
        public async Task TestWriteOneOutbound()
        {
            var latch = new CountdownEvent(1);
            var flushCount = new AtomicInteger(0);
            EmbeddedChannel channel = new EmbeddedChannel(new ChannelHandlerForWriteOneOutbound(latch, flushCount));

            // This shouldn't trigger a #flush()
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
            channel.WriteOneOutbound("Hello, Netty!");
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法

            if (!latch.Wait(TimeSpan.FromSeconds(1L)))
            {
                Assert.False(true, "Nobody called #write() in time.");
            }

            await channel.CloseAsync();

            // There was no #flushOutbound() call so nobody should have called #flush()
            Assert.Equal(0, flushCount.Value);
        }

        [Fact]
        public async Task TestEnsureOpen()
        {
            EmbeddedChannel channel = new EmbeddedChannel();
            await channel.CloseAsync();

            try
            {
                channel.WriteOutbound("Hello, Netty!");
                Assert.False(true, "This should have failed with a ClosedChannelException");
            }
            catch (Exception expected)
            {
                Assert.True(expected is ClosedChannelException);
            }

            try
            {
                channel.WriteInbound("Hello, Netty!");
                Assert.False(true, "This should have failed with a ClosedChannelException");
            }
            catch (Exception expected)
            {
                Assert.True(expected is ClosedChannelException);
            }
        }

        class EmbeddedChannel1 : EmbeddedChannel
        {
            readonly CountdownEvent latch;
            public EmbeddedChannel1(CountdownEvent latch) => this.latch = latch;

            protected override void HandleInboundMessage(object msg) => this.latch.Signal();
        }

        [Fact]
        public void TestHandleInboundMessage()
        {
            var latch = new CountdownEvent(1);
            EmbeddedChannel channel = new EmbeddedChannel1(latch);

            channel.WriteOneInbound("Hello, Netty!");

            if (!latch.Wait(TimeSpan.FromSeconds(1L)))
            {
                Assert.False(true, "Nobody called #handleInboundMessage() in time.");
            }
        }

        class EmbeddedChannel2 : EmbeddedChannel
        {
            readonly CountdownEvent latch;
            public EmbeddedChannel2(CountdownEvent latch) => this.latch = latch;

            protected override void HandleOutboundMessage(object msg) => this.latch.Signal();
        }

        [Fact]
        public void TestHandleOutboundMessage()
        {
            var latch = new CountdownEvent(1);
            EmbeddedChannel channel = new EmbeddedChannel2(latch);

            channel.WriteOneOutbound("Hello, Netty!");
            if (latch.Wait(TimeSpan.FromMilliseconds(50L)))
            {
                Assert.False(true, "Somebody called unexpectedly #flush()");
            }

            channel.FlushOutbound();
            if (!latch.Wait(TimeSpan.FromSeconds(1L)))
            {
                Assert.False(true, "Nobody called #handleOutboundMessage() in time.");
            }
        }

        class ChannelHandlerChannelInactiveFired : ChannelHandlerAdapter
        {
            readonly AtomicBoolean inactive;

            public ChannelHandlerChannelInactiveFired(AtomicBoolean inactive) => this.inactive = inactive;

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                context.CloseAsync();
            }

            public override void ChannelInactive(IChannelHandlerContext context)
            {
                inactive.Value = true;
            }
        }

        [Fact]
        public void TestChannelInactiveFired()
        {
            AtomicBoolean inactive = new AtomicBoolean(false);
            EmbeddedChannel channel = new EmbeddedChannel(new ChannelHandlerChannelInactiveFired(inactive));

            channel.Pipeline.FireExceptionCaught(new InvalidOperationException());

            Assert.True(inactive.Value);
        }

        private static void Release(params IByteBuffer[] buffers)
        {
            foreach (var buffer in buffers)
            {
                if (buffer.ReferenceCount > 0)
                {
                    buffer.Release();
                }
            }
        }
    }
}