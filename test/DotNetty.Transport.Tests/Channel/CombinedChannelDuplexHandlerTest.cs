namespace DotNetty.Transport.Tests.Channel
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class CombinedChannelDuplexHandlerTest
    {
        private static readonly object MSG = new object();
        private static IPEndPoint ADDRESS = new IPEndPoint(IPAddress.IPv6Any, IPEndPoint.MinPort);

        private enum Event
        {
            REGISTERED,
            UNREGISTERED,
            ACTIVE,
            INACTIVE,
            CHANNEL_READ,
            CHANNEL_READ_COMPLETE,
            EXCEPTION_CAUGHT,
            USER_EVENT_TRIGGERED,
            CHANNEL_WRITABILITY_CHANGED,
            HANDLER_ADDED,
            HANDLER_REMOVED,
            BIND,
            CONNECT,
            WRITE,
            FLUSH,
            READ,
            REGISTER,
            DEREGISTER,
            CLOSE,
            DISCONNECT
        }

        [Fact]
        public void TestInboundRemoveBeforeAdded()
        {
            CombinedChannelDuplexHandler<ChannelHandlerAdapter, ChannelHandlerAdapter> handler =
                    new CombinedChannelDuplexHandler<ChannelHandlerAdapter, ChannelHandlerAdapter>(
                            new ChannelHandlerAdapter(), new ChannelHandlerAdapter());
            Assert.Throws<InvalidOperationException>(() => handler.RemoveInboundHandler());
        }

        [Fact]
        public void TestOutboundRemoveBeforeAdded()
        {
            CombinedChannelDuplexHandler<ChannelHandlerAdapter, ChannelHandlerAdapter> handler =
                    new CombinedChannelDuplexHandler<ChannelHandlerAdapter, ChannelHandlerAdapter>(
                            new ChannelHandlerAdapter(), new ChannelHandlerAdapter());
            Assert.Throws<InvalidOperationException>(() => handler.RemoveOutboundHandler());
        }

        //[Fact]
        //public void TestInboundHandlerImplementsOutboundHandler()
        //{
        //    Assert.Throws<ArgumentException>(() =>
        //        new CombinedChannelDuplexHandler<ChannelHandlerAdapter, ChannelHandlerAdapter>(
        //            new ChannelDuplexHandler(), new ChannelHandlerAdapter()));
        //}

        //[Fact]
        //public void TestOutboundHandlerImplementsInboundHandler()
        //{
        //    Assert.Throws<ArgumentException>(() =>
        //        new CombinedChannelDuplexHandler<ChannelHandlerAdapter, ChannelHandlerAdapter>(
        //            new ChannelHandlerAdapter(), new ChannelDuplexHandler()));
        //}

        [Fact]
        public void TestInitNotCalledBeforeAdded()
        {
            CombinedChannelDuplexHandler<ChannelHandlerAdapter, ChannelHandlerAdapter> handler =
                new CombinedChannelDuplexHandler<ChannelHandlerAdapter, ChannelHandlerAdapter>() { };
            Assert.Throws<InvalidOperationException>(() => handler.HandlerAdded(null));
        }

        [Fact]
        public void TestExceptionCaughtBothCombinedHandlers()
        {
            Exception exception = new Exception();
            Queue<IChannelHandler> queue = new Queue<IChannelHandler>();

            var inboundHandler = new ChannelInboundHandlerAdapter0(exception, queue);
            var outboundHandler = new ChannelOutboundHandlerAdapter0(exception, queue);
            var lastHandler = new ChannelInboundHandlerAdapter1(exception, queue);
            EmbeddedChannel channel = new EmbeddedChannel(
                    new CombinedChannelDuplexHandler<ChannelHandlerAdapter, ChannelHandlerAdapter>(
                            inboundHandler, outboundHandler), lastHandler);
            channel.Pipeline.FireExceptionCaught(exception);
            Assert.False(channel.Finish());
            Assert.Same(inboundHandler, queue.Dequeue());
            Assert.Same(outboundHandler, queue.Dequeue());
            Assert.Same(lastHandler, queue.Dequeue());
            Assert.Empty(queue);
        }

        class ChannelInboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            private readonly Exception _exception;
            private readonly Queue<IChannelHandler> _queue;

            public ChannelInboundHandlerAdapter0(Exception exception, Queue<IChannelHandler> queue)
            {
                _exception = exception;
                _queue = queue;
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                Assert.Same(_exception, exception);
                _queue.Enqueue(this);
                context.FireExceptionCaught(exception);
            }
        }

        class ChannelOutboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            private readonly Exception _exception;
            private readonly Queue<IChannelHandler> _queue;

            public ChannelOutboundHandlerAdapter0(Exception exception, Queue<IChannelHandler> queue)
            {
                _exception = exception;
                _queue = queue;
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                Assert.Same(_exception, exception);
                _queue.Enqueue(this);
                context.FireExceptionCaught(exception);
            }
        }

        class ChannelInboundHandlerAdapter1 : ChannelHandlerAdapter
        {
            private readonly Exception _exception;
            private readonly Queue<IChannelHandler> _queue;

            public ChannelInboundHandlerAdapter1(Exception exception, Queue<IChannelHandler> queue)
            {
                _exception = exception;
                _queue = queue;
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                Assert.Same(_exception, exception);
                _queue.Enqueue(this);
            }
        }

        [Fact]
        public void TestInboundEvents()
        {
            Queue<Event> queue = new Queue<Event>();
            var inboundHandler = new ChannelInboundHandlerAdapter2(queue);

            CombinedChannelDuplexHandler<ChannelHandlerAdapter, ChannelHandlerAdapter> handler =
                    new CombinedChannelDuplexHandler<ChannelHandlerAdapter, ChannelHandlerAdapter>(
                    inboundHandler, new ChannelHandlerAdapter());

            EmbeddedChannel channel = new EmbeddedChannel(handler);
            channel.Pipeline.FireChannelWritabilityChanged();
            channel.Pipeline.FireUserEventTriggered(MSG);
            channel.Pipeline.FireChannelRead(MSG);
            channel.Pipeline.FireChannelReadComplete();

            Assert.Equal(Event.HANDLER_ADDED, queue.Dequeue());
            Assert.Equal(Event.REGISTERED, queue.Dequeue());
            Assert.Equal(Event.ACTIVE, queue.Dequeue());
            Assert.Equal(Event.CHANNEL_WRITABILITY_CHANGED, queue.Dequeue());
            Assert.Equal(Event.USER_EVENT_TRIGGERED, queue.Dequeue());
            Assert.Equal(Event.CHANNEL_READ, queue.Dequeue());
            Assert.Equal(Event.CHANNEL_READ_COMPLETE, queue.Dequeue());

            handler.RemoveInboundHandler();
            Assert.Equal(Event.HANDLER_REMOVED, queue.Dequeue());

            // These should not be handled by the inboundHandler anymore as it was removed before
            channel.Pipeline.FireChannelWritabilityChanged();
            channel.Pipeline.FireUserEventTriggered(MSG);
            channel.Pipeline.FireChannelRead(MSG);
            channel.Pipeline.FireChannelReadComplete();

            // Should have not received any more events as it was removed before via removeInboundHandler()
            Assert.Empty(queue);
            Assert.True(channel.Finish());
            Assert.Empty(queue);
        }

        class ChannelInboundHandlerAdapter2 : ChannelHandlerAdapter
        {
            private readonly Queue<Event> _queue;

            public ChannelInboundHandlerAdapter2(Queue<Event> queue) => _queue = queue;

            public override void HandlerAdded(IChannelHandlerContext context)
            {
                _queue.Enqueue(Event.HANDLER_ADDED);
            }

            public override void HandlerRemoved(IChannelHandlerContext context)
            {
                _queue.Enqueue(Event.HANDLER_REMOVED);
            }

            public override void ChannelRegistered(IChannelHandlerContext context)
            {
                _queue.Enqueue(Event.REGISTERED);
            }

            public override void ChannelUnregistered(IChannelHandlerContext context)
            {
                _queue.Enqueue(Event.UNREGISTERED);
            }

            public override void ChannelActive(IChannelHandlerContext context)
            {
                _queue.Enqueue(Event.ACTIVE);
            }

            public override void ChannelInactive(IChannelHandlerContext context)
            {
                _queue.Enqueue(Event.INACTIVE);
            }

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                _queue.Enqueue(Event.CHANNEL_READ);
            }

            public override void ChannelReadComplete(IChannelHandlerContext context)
            {
                _queue.Enqueue(Event.CHANNEL_READ_COMPLETE);
            }

            public override void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
                _queue.Enqueue(Event.USER_EVENT_TRIGGERED);
            }

            public override void ChannelWritabilityChanged(IChannelHandlerContext context)
            {
                _queue.Enqueue(Event.CHANNEL_WRITABILITY_CHANGED);
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                _queue.Enqueue(Event.EXCEPTION_CAUGHT);
            }
        }

        [Fact]
        public void TestOutboundEvents()
        {
            Queue<Event> queue = new Queue<Event>();
            var inboundHandler = new ChannelHandlerAdapter();
            var outboundHandler = new ChannelOutboundHandlerAdapter2(queue);

            CombinedChannelDuplexHandler<ChannelHandlerAdapter, ChannelHandlerAdapter> handler =
                    new CombinedChannelDuplexHandler<ChannelHandlerAdapter, ChannelHandlerAdapter>(
                            inboundHandler, outboundHandler);

            EmbeddedChannel channel = new EmbeddedChannel();
            channel.Pipeline.AddFirst(handler);

            DoOutboundOperations(channel);

            Assert.Equal(Event.HANDLER_ADDED, queue.Dequeue());
            Assert.Equal(Event.BIND, queue.Dequeue());
            Assert.Equal(Event.CONNECT, queue.Dequeue());
            Assert.Equal(Event.WRITE, queue.Dequeue());
            Assert.Equal(Event.FLUSH, queue.Dequeue());
            Assert.Equal(Event.READ, queue.Dequeue());
            Assert.Equal(Event.CLOSE, queue.Dequeue());
            Assert.Equal(Event.CLOSE, queue.Dequeue());
            Assert.Equal(Event.DEREGISTER, queue.Dequeue());

            handler.RemoveOutboundHandler();
            Assert.Equal(Event.HANDLER_REMOVED, queue.Dequeue());

            // These should not be handled by the inboundHandler anymore as it was removed before
            DoOutboundOperations(channel);

            // Should have not received any more events as it was removed before via removeInboundHandler()
            Assert.Empty(queue);
            Assert.True(channel.Finish());
            Assert.Empty(queue);
        }

        private static void DoOutboundOperations(IChannel channel)
        {
            channel.Pipeline.BindAsync(ADDRESS);
            channel.Pipeline.ConnectAsync(ADDRESS);
            channel.Pipeline.WriteAsync(MSG);
            channel.Pipeline.Flush();
            channel.Pipeline.Read();
            channel.Pipeline.DisconnectAsync();
            channel.Pipeline.CloseAsync();
            channel.Pipeline.DeregisterAsync();
        }

        class ChannelOutboundHandlerAdapter2 : ChannelHandlerAdapter
        {
            private readonly Queue<Event> _queue;

            public ChannelOutboundHandlerAdapter2(Queue<Event> queue) => _queue = queue;

            public override void HandlerAdded(IChannelHandlerContext context)
            {
                _queue.Enqueue(Event.HANDLER_ADDED);
            }

            public override void HandlerRemoved(IChannelHandlerContext context)
            {
                _queue.Enqueue(Event.HANDLER_REMOVED);
            }

            public override Task BindAsync(IChannelHandlerContext context, EndPoint localAddress)
            {
                _queue.Enqueue(Event.BIND);
                return TaskUtil.Completed;
            }

            public override Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress)
            {
                _queue.Enqueue(Event.CONNECT);
                return TaskUtil.Completed;
            }

            public override void Disconnect(IChannelHandlerContext context, IPromise promise)
            {
                _queue.Enqueue(Event.DISCONNECT);
            }

            public override void Close(IChannelHandlerContext context, IPromise promise)
            {
                _queue.Enqueue(Event.CLOSE);
            }

            public override void Deregister(IChannelHandlerContext context, IPromise promise)
            {
                _queue.Enqueue(Event.DEREGISTER);
            }

            public override void Read(IChannelHandlerContext context)
            {
                _queue.Enqueue(Event.READ);
            }

            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                _queue.Enqueue(Event.WRITE);
            }

            public override void Flush(IChannelHandlerContext context)
            {
                _queue.Enqueue(Event.FLUSH);
            }
        }

        [Fact]
        public async Task TestPromisesPassed()
        {
            var outboundHandler = new ChannelOutboundHandlerAdapter3();
            EmbeddedChannel ch = new EmbeddedChannel(outboundHandler,
                    new CombinedChannelDuplexHandler<ChannelHandlerAdapter, ChannelHandlerAdapter>(
                            new ChannelHandlerAdapter(), new ChannelHandlerAdapter()));
            IChannelPipeline pipeline = ch.Pipeline;

            IPromise promise = null;
            await pipeline.ConnectAsync(ADDRESS);

            await pipeline.BindAsync(ADDRESS);

            promise = ch.NewPromise();
            pipeline.CloseAsync(promise).Ignore();
            await promise.Task;

            promise = ch.NewPromise();
            pipeline.DisconnectAsync(promise).Ignore();
            await promise.Task;

            promise = ch.NewPromise();
            pipeline.WriteAsync("test", promise).Ignore();
            await promise.Task;

            promise = ch.NewPromise();
            pipeline.DeregisterAsync(promise).Ignore();
            await promise.Task;
            ch.Finish();
        }

        class ChannelOutboundHandlerAdapter3 : ChannelHandlerAdapter
        {
            public override Task BindAsync(IChannelHandlerContext context, EndPoint localAddress)
            {
                return TaskUtil.Completed;
            }

            public override Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress)
            {
                return TaskUtil.Completed;
            }

            public override void Disconnect(IChannelHandlerContext context, IPromise promise)
            {
                promise.Complete();
            }

            public override void Close(IChannelHandlerContext context, IPromise promise)
            {
                promise.Complete();
            }

            public override void Deregister(IChannelHandlerContext context, IPromise promise)
            {
                promise.Complete();
            }

            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                promise.Complete();
            }
        }

        [Fact]
        public void TestNotSharable()
        {
            Assert.Throws<InvalidOperationException>(() => new CombinedChannelDuplexHandler0());
        }

        class CombinedChannelDuplexHandler0 : CombinedChannelDuplexHandler<ChannelHandlerAdapter, ChannelHandlerAdapter>
        {
            public override bool IsSharable => true;
        }
    }
}