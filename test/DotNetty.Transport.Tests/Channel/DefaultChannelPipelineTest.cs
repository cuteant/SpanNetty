namespace DotNetty.Transport.Tests.Channel
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using DotNetty.Transport.Channels.Local;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit;

    public class DefaultChannelPipelineTest : IDisposable
    {
        private readonly IEventLoopGroup _group;

        private IChannel _self;
        private IChannel _peer;

        public DefaultChannelPipelineTest()
        {
            _group = new DefaultEventLoopGroup(1);

        }

        private void Setup(params IChannelHandler[] handlers)
        {
            AtomicReference<IChannel> peerRef = new AtomicReference<IChannel>();
            ServerBootstrap sb = new ServerBootstrap();
            sb.Group(_group).Channel<LocalServerChannel>();
            sb.ChildHandler(new ChannelInboundHandlerAdapter00(peerRef));

            var sc = sb.BindAsync(LocalAddress.Any).GetAwaiter().GetResult();

            Bootstrap b = new Bootstrap();
            b.Group(_group).Channel<LocalChannel>();
            b.Handler(new ActionChannelInitializer<LocalChannel>(ch =>
            {
                ch.Pipeline.AddLast(handlers);
            }));

            _self = b.ConnectAsync(sc.LocalAddress).GetAwaiter().GetResult();
            _peer = peerRef.Value;

            sc.CloseAsync().GetAwaiter().GetResult();
        }

        sealed class ChannelInboundHandlerAdapter00 : ChannelHandlerAdapter
        {
            private readonly AtomicReference<IChannel> _peerRef;

            public ChannelInboundHandlerAdapter00(AtomicReference<IChannel> peerRef)
            {
                _peerRef = peerRef;
            }

            public override void ChannelRegistered(IChannelHandlerContext ctx)
            {
                _peerRef.Value = ctx.Channel;
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                ReferenceCountUtil.Release(msg);
            }
        }

        public void Dispose()
        {
            if (_peer != null)
            {
                _peer.CloseAsync().GetAwaiter().GetResult();
                _peer = null;
            }
            if (_self != null)
            {
                _self = null;
            }
            Task.WaitAll(_group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void TestFreeCalled()
        {
            CountdownEvent free = new CountdownEvent(1);

            var holder = new AbstractReferenceCounted1(free);

            StringInboundHandler handler = new StringInboundHandler();
            Setup(handler);

            _peer.WriteAndFlushAsync(holder).GetAwaiter().GetResult();

            Assert.True(free.Wait(TimeSpan.FromSeconds(10)));
            Assert.True(handler._called);
        }

        class AbstractReferenceCounted1 : AbstractReferenceCounted
        {
            private readonly CountdownEvent _free;

            public AbstractReferenceCounted1(CountdownEvent free) => _free = free;

            protected override void Deallocate()
            {
                _free.Signal();
            }

            public override IReferenceCounted Touch(object hint)
            {
                return this;
            }
        }

        class StringInboundHandler : ChannelHandlerAdapter
        {
            internal bool _called;

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                _called = true;
                if (!(msg is string))
                {
                    ctx.FireChannelRead(msg);
                }
            }
        }

        [Fact]
        public void TestRemoveChannelHandler()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;

            IChannelHandler handler1 = NewHandler();
            IChannelHandler handler2 = NewHandler();
            IChannelHandler handler3 = NewHandler();

            pipeline.AddLast("handler1", handler1);
            pipeline.AddLast("handler2", handler2);
            pipeline.AddLast("handler3", handler3);
            Assert.Same(pipeline.Get("handler1"), handler1);
            Assert.Same(pipeline.Get("handler2"), handler2);
            Assert.Same(pipeline.Get("handler3"), handler3);

            pipeline.Remove(handler1);
            Assert.Null(pipeline.Get("handler1"));
            pipeline.Remove(handler2);
            Assert.Null(pipeline.Get("handler2"));
            pipeline.Remove(handler3);
            Assert.Null(pipeline.Get("handler3"));
        }

        [Fact]
        public void TestRemoveIfExists()
        {
            DefaultChannelPipeline pipeline = new DefaultChannelPipeline(new LocalChannel());

            IChannelHandler handler1 = NewHandler();
            IChannelHandler handler2 = NewHandler();
            IChannelHandler handler3 = NewHandler();

            pipeline.AddLast("handler1", handler1);
            pipeline.AddLast("handler2", handler2);
            pipeline.AddLast("handler3", handler3);

            Assert.NotNull(pipeline.RemoveIfExists<IChannelHandler>(handler1));
            Assert.Null(pipeline.Get("handler1"));

            Assert.NotNull(pipeline.RemoveIfExists<IChannelHandler>("handler2"));
            Assert.Null(pipeline.Get("handler2"));

            Assert.NotNull(pipeline.RemoveIfExists<TestHandler>());
            Assert.Null(pipeline.Get("handler3"));
        }

        [Fact]
        public void TestRemoveIfExistsDoesNotThrowException()
        {
            DefaultChannelPipeline pipeline = new DefaultChannelPipeline(new LocalChannel());

            IChannelHandler handler1 = NewHandler();
            IChannelHandler handler2 = NewHandler();
            pipeline.AddLast("handler1", handler1);

            Assert.Null(pipeline.RemoveIfExists<IChannelHandler>("handlerXXX"));
            Assert.Null(pipeline.RemoveIfExists<IChannelHandler>(handler2));
            Assert.Null(pipeline.RemoveIfExists<ChannelOutboundHandlerAdapter00>()); // ChannelOutboundHandlerAdapter
            Assert.NotNull(pipeline.Get("handler1"));
        }
        class ChannelOutboundHandlerAdapter00 : ChannelHandlerAdapter { }

        [Fact]
        public void TestRemoveThrowNoSuchElementException()
        {
            DefaultChannelPipeline pipeline = new DefaultChannelPipeline(new LocalChannel());

            IChannelHandler handler1 = NewHandler();
            pipeline.AddLast("handler1", handler1);

            Assert.Throws<ArgumentException>(() => pipeline.Remove("handlerXXX"));
        }

        [Fact]
        public void TestReplaceChannelHandler()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;

            IChannelHandler handler1 = NewHandler();
            pipeline.AddLast("handler1", handler1);
            pipeline.AddLast("handler2", handler1);
            pipeline.AddLast("handler3", handler1);
            Assert.Same(pipeline.Get("handler1"), handler1);
            Assert.Same(pipeline.Get("handler2"), handler1);
            Assert.Same(pipeline.Get("handler3"), handler1);

            IChannelHandler newHandler1 = NewHandler();
            pipeline.Replace("handler1", "handler1", newHandler1);
            Assert.Same(pipeline.Get("handler1"), newHandler1);

            IChannelHandler newHandler3 = NewHandler();
            pipeline.Replace("handler3", "handler3", newHandler3);
            Assert.Same(pipeline.Get("handler3"), newHandler3);

            IChannelHandler newHandler2 = NewHandler();
            pipeline.Replace("handler2", "handler2", newHandler2);
            Assert.Same(pipeline.Get("handler2"), newHandler2);
        }

        [Fact]
        public void TestReplaceHandlerChecksDuplicateNames()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;

            IChannelHandler handler1 = NewHandler();
            IChannelHandler handler2 = NewHandler();
            pipeline.AddLast("handler1", handler1);
            pipeline.AddLast("handler2", handler2);

            IChannelHandler newHandler1 = NewHandler();
            Assert.Throws<ArgumentException>(() => pipeline.Replace("handler1", "handler2", newHandler1));
        }

        [Fact]
        public void TestReplaceNameWithGenerated()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;

            IChannelHandler handler1 = NewHandler();
            pipeline.AddLast("handler1", handler1);
            Assert.Same(pipeline.Get("handler1"), handler1);

            IChannelHandler newHandler1 = NewHandler();
            pipeline.Replace("handler1", null, newHandler1);
            //Assert.Same(pipeline.Get("DefaultChannelPipelineTest+TestHandler#0"), newHandler1);
            Assert.Same(pipeline.Get("TestHandler#0"), newHandler1);
            Assert.Null(pipeline.Get("handler1"));
        }

        [Fact]
        public void TestRenameChannelHandler()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;

            IChannelHandler handler1 = NewHandler();
            pipeline.AddLast("handler1", handler1);
            pipeline.AddLast("handler2", handler1);
            pipeline.AddLast("handler3", handler1);
            Assert.Same(pipeline.Get("handler1"), handler1);
            Assert.Same(pipeline.Get("handler2"), handler1);
            Assert.Same(pipeline.Get("handler3"), handler1);

            IChannelHandler newHandler1 = NewHandler();
            pipeline.Replace("handler1", "newHandler1", newHandler1);
            Assert.Same(pipeline.Get("newHandler1"), newHandler1);
            Assert.Null(pipeline.Get("handler1"));

            IChannelHandler newHandler3 = NewHandler();
            pipeline.Replace("handler3", "newHandler3", newHandler3);
            Assert.Same(pipeline.Get("newHandler3"), newHandler3);
            Assert.Null(pipeline.Get("handler3"));

            IChannelHandler newHandler2 = NewHandler();
            pipeline.Replace("handler2", "newHandler2", newHandler2);
            Assert.Same(pipeline.Get("newHandler2"), newHandler2);
            Assert.Null(pipeline.Get("handler2"));
        }

        [Fact]
        public void TestChannelHandlerContextNavigation()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;

            int HANDLER_ARRAY_LEN = 5;
            IChannelHandler[] firstHandlers = NewHandlers(HANDLER_ARRAY_LEN);
            IChannelHandler[] lastHandlers = NewHandlers(HANDLER_ARRAY_LEN);

            pipeline.AddFirst(firstHandlers);
            pipeline.AddLast(lastHandlers);

            VerifyContextNumber(pipeline, HANDLER_ARRAY_LEN * 2);
        }

        [Fact]
        public void TestThrowInExceptionCaught()
        {
            CountdownEvent latch = new CountdownEvent(1);
            AtomicInteger counter = new AtomicInteger();
            IChannel channel = new LocalChannel();
            try
            {
                _group.RegisterAsync(channel).GetAwaiter().GetResult();
                channel.Pipeline.AddLast(new ChannelInboundHandlerAdapter6(latch, counter));

                channel.Pipeline.FireChannelReadComplete();
                Assert.True(latch.Wait(TimeSpan.FromMinutes(1)));
                Assert.Equal(1, counter.Value);
            }
            finally
            {
                channel.CloseAsync().GetAwaiter().GetResult();
            }
        }

        [Fact]
        public void TestThrowInOtherHandlerAfterInvokedFromExceptionCaught()
        {
            CountdownEvent latch = new CountdownEvent(1);
            AtomicInteger counter = new AtomicInteger();
            IChannel channel = new LocalChannel();
            try
            {
                _group.RegisterAsync(channel).GetAwaiter().GetResult();
                channel.Pipeline.AddLast(new ChannelInboundHandlerAdapter5(),
                    new ChannelInboundHandlerAdapter6(latch, counter));

                channel.Pipeline.FireExceptionCaught(new Exception());
                Assert.True(latch.Wait(TimeSpan.FromMinutes(1)));
                Assert.Equal(1, counter.Value);
            }
            finally
            {
                channel.CloseAsync().GetAwaiter().GetResult();
            }
        }

        class ChannelInboundHandlerAdapter5 : ChannelHandlerAdapter
        {
            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                context.FireChannelReadComplete();
            }
        }

        class ChannelInboundHandlerAdapter6 : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _latch;
            private readonly AtomicInteger _counter;

            public ChannelInboundHandlerAdapter6(CountdownEvent latch, AtomicInteger counter)
            {
                _latch = latch;
                _counter = counter;
            }

            public override void ChannelReadComplete(IChannelHandlerContext context)
            {
                throw new TestException();
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                if (cause is TestException)
                {
                    ctx.Executor.Execute(() => _latch.Signal());
                }
                _counter.Increment();
                throw new Exception();
            }

            class TestException : Exception
            {
            }
        }

        [Fact]
        public void TestFireChannelRegistered()
        {
            CountdownEvent latch = new CountdownEvent(1);
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            pipeline.AddLast(new ActionChannelInitializer<IChannel>(ch =>
            {
                ch.Pipeline.AddLast(new ChannelInboundHandlerAdapter4(latch));
            }));
            _group.RegisterAsync(pipeline.Channel).GetAwaiter().GetResult();
            Assert.True(latch.Wait(TimeSpan.FromSeconds(2)));
        }

        class ChannelInboundHandlerAdapter4 : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _latch;

            public ChannelInboundHandlerAdapter4(CountdownEvent latch)
            {
                _latch = latch;
            }

            public override void ChannelRegistered(IChannelHandlerContext context)
            {
                _latch.Signal();
            }
        }

        [Fact]
        public void TestPipelineOperation()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;

            int handlerNum = 5;
            IChannelHandler[] handlers1 = NewHandlers(handlerNum);
            IChannelHandler[] handlers2 = NewHandlers(handlerNum);

            string prefixX = "x";
            for (int i = 0; i < handlerNum; i++)
            {
                if (i % 2 == 0)
                {
                    pipeline.AddFirst(prefixX + i, handlers1[i]);
                }
                else
                {
                    pipeline.AddLast(prefixX + i, handlers1[i]);
                }
            }

            for (int i = 0; i < handlerNum; i++)
            {
                if (i % 2 != 0)
                {
                    pipeline.AddBefore(prefixX + i, i.ToString(), handlers2[i]);
                }
                else
                {
                    pipeline.AddAfter(prefixX + i, i.ToString(), handlers2[i]);
                }
            }

            VerifyContextNumber(pipeline, handlerNum * 2);
        }

        [Fact]
        public void TestChannelHandlerContextOrder()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;

            pipeline.AddFirst("1", NewHandler());
            pipeline.AddLast("10", NewHandler());

            pipeline.AddBefore("10", "5", NewHandler());
            pipeline.AddAfter("1", "3", NewHandler());
            pipeline.AddBefore("5", "4", NewHandler());
            pipeline.AddAfter("5", "6", NewHandler());

            pipeline.AddBefore("1", "0", NewHandler());
            pipeline.AddAfter("10", "11", NewHandler());

            AbstractChannelHandlerContext ctx = (AbstractChannelHandlerContext)pipeline.FirstContext();
            Assert.NotNull(ctx);
            while (ctx != null)
            {
                int i = ToInt(ctx.Name);
                int j = Next(ctx);
                if (j != -1)
                {
                    Assert.True(i < j);
                }
                else
                {
                    Assert.Null(ctx.Next.Next);
                }
                ctx = ctx.Next;
            }

            VerifyContextNumber(pipeline, 8);
        }

        [Fact]
        public void TestLifeCycleAwareness()
        {
            Setup();

            IChannelPipeline p = _self.Pipeline;

            List<LifeCycleAwareTestHandler> handlers = new List<LifeCycleAwareTestHandler>();
            int COUNT = 20;
            CountdownEvent addLatch = new CountdownEvent(COUNT);
            for (int i = 0; i < COUNT; i++)
            {
                LifeCycleAwareTestHandler handler = new LifeCycleAwareTestHandler("handler-" + i);

                // Add handler.
                p.AddFirst(handler._name, handler);
                _self.EventLoop.Execute(() =>
                {
                    // Validate handler life-cycle methods called.
                    handler.Validate(true, false);

                    // Store handler into the list.
                    handlers.Add(handler);

                    addLatch.Signal();
                });
            }
            Assert.True(addLatch.Wait(TimeSpan.FromMinutes(1)));

            // Change the order of remove operations over all handlers in the pipeline.
            //Collections.shuffle(handlers);

            CountdownEvent removeLatch = new CountdownEvent(COUNT);

            foreach (LifeCycleAwareTestHandler handler in handlers)
            {
                Assert.Same(handler, p.Remove(handler._name));

                _self.EventLoop.Execute(() =>
                {
                    // Validate handler life-cycle methods called.
                    handler.Validate(true, true);
                    removeLatch.Signal();
                });
            }
            Assert.True(removeLatch.Wait(TimeSpan.FromMinutes(1)));
        }

        [Fact]
        public void TestRemoveAndForwardInbound()
        {
            BufferedTestHandler handler1 = new BufferedTestHandler();
            BufferedTestHandler handler2 = new BufferedTestHandler();

            Setup(handler1, handler2);

            _self.EventLoop.SubmitAsync(() =>
            {
                IChannelPipeline p = _self.Pipeline;
                handler1.InboundBuffer.Enqueue(8);
                Assert.Equal(8, handler1.InboundBuffer.Peek());
                Assert.Empty(handler2.InboundBuffer);
                p.Remove(handler1);
                Assert.Single(handler2.InboundBuffer);
                Assert.Equal(8, handler2.InboundBuffer.Peek());
                return 0;
            }).GetAwaiter().GetResult();
        }

        [Fact]
        public void TestRemoveAndForwardOutbound()
        {
            BufferedTestHandler handler1 = new BufferedTestHandler();
            BufferedTestHandler handler2 = new BufferedTestHandler();

            Setup(handler1, handler2);

            _self.EventLoop.SubmitAsync(() =>
            {
                IChannelPipeline p = _self.Pipeline;
                handler2.OutboundBuffer.Enqueue(8);
                Assert.Equal(8, handler2.OutboundBuffer.Peek());
                Assert.Empty(handler1.OutboundBuffer);
                p.Remove(handler2);
                Assert.Single(handler1.OutboundBuffer);
                Assert.Equal(8, handler1.OutboundBuffer.Peek());
                return 0;
            }).GetAwaiter().GetResult();
        }

        [Fact]
        public void TestReplaceAndForwardOutbound()
        {
            BufferedTestHandler handler1 = new BufferedTestHandler();
            BufferedTestHandler handler2 = new BufferedTestHandler();

            Setup(handler1);

            _self.EventLoop.SubmitAsync(() =>
            {
                IChannelPipeline p = _self.Pipeline;
                handler1.OutboundBuffer.Enqueue(8);
                Assert.Equal(8, handler1.OutboundBuffer.Peek());
                Assert.Empty(handler2.OutboundBuffer);
                p.Replace(handler1, "handler2", handler2);
                Assert.Equal(8, handler2.OutboundBuffer.Peek());
                return 0;
            }).GetAwaiter().GetResult();
        }

        [Fact]
        public void TestReplaceAndForwardInboundAndOutbound()
        {
            BufferedTestHandler handler1 = new BufferedTestHandler();
            BufferedTestHandler handler2 = new BufferedTestHandler();

            Setup(handler1);

            _self.EventLoop.SubmitAsync(() =>
            {
                IChannelPipeline p = _self.Pipeline;
                handler1.InboundBuffer.Enqueue(8);
                handler1.OutboundBuffer.Enqueue(8);

                Assert.Equal(8, handler1.InboundBuffer.Peek());
                Assert.Equal(8, handler1.OutboundBuffer.Peek());
                Assert.Empty(handler2.InboundBuffer);
                Assert.Empty(handler2.OutboundBuffer);

                p.Replace(handler1, "handler2", handler2);
                Assert.Equal(8, handler2.OutboundBuffer.Peek());
                Assert.Equal(8, handler2.InboundBuffer.Peek());
                return 0;
            }).GetAwaiter().GetResult();
        }

        [Fact]
        public void TestRemoveAndForwardInboundOutbound()
        {
            BufferedTestHandler handler1 = new BufferedTestHandler();
            BufferedTestHandler handler2 = new BufferedTestHandler();
            BufferedTestHandler handler3 = new BufferedTestHandler();

            Setup(handler1, handler2, handler3);

            _self.EventLoop.SubmitAsync(() =>
            {
                IChannelPipeline p = _self.Pipeline;
                handler2.InboundBuffer.Enqueue(8);
                handler2.OutboundBuffer.Enqueue(8);

                Assert.Equal(8, handler2.InboundBuffer.Peek());
                Assert.Equal(8, handler2.OutboundBuffer.Peek());

                Assert.Empty(handler1.OutboundBuffer);
                Assert.Empty(handler3.InboundBuffer);

                p.Remove(handler2);
                Assert.Equal(8, handler3.InboundBuffer.Peek());
                Assert.Equal(8, handler1.OutboundBuffer.Peek());
                return 0;
            }).GetAwaiter().GetResult();
        }

        // Tests for https://github.com/netty/netty/issues/2349
        [Fact(Skip = "TestCancelBind")]
        public void TestCancelBind()
        {
            //IChannelPipeline pipeline = new LocalChannel().Pipeline;
            //_group.RegisterAsync(pipeline.Channel);

            //var promise = pipeline.Channel.NewPromise();
            //Assert.True(promise.TrySetCanceled());
            //var future = pipeline.BindAsync(new LocalAddress("test"), promise);
            //Assert.True(future.IsCanceled);
        }

        [Fact(Skip = "TestCancelConnect")]
        public void TestCancelConnect()
        {
            //IChannelPipeline pipeline = new LocalChannel().Pipeline;
            //_group.RegisterAsync(pipeline.Channel);

            //var promise = pipeline.Channel.NewPromise();
            //Assert.True(promise.TrySetCanceled());
            //var future = pipeline.ConnectAsync(new LocalAddress("test"), promise);
            //Assert.True(future.IsCanceled);
        }

        [Fact]
        public void TestCancelDisconnect()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            _group.RegisterAsync(pipeline.Channel);

            var promise = pipeline.Channel.NewPromise();
            Assert.True(promise.TrySetCanceled());
            var future = pipeline.DisconnectAsync(promise);
            Assert.True(future.IsCanceled);
        }

        [Fact]
        public void TestCancelClose()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            _group.RegisterAsync(pipeline.Channel);

            var promise = pipeline.Channel.NewPromise();
            Assert.True(promise.TrySetCanceled());
            var future = pipeline.CloseAsync(promise);
            Assert.True(future.IsCanceled);
        }

        [Fact(Skip = "TestWrongPromiseChannel")]
        public async Task TestWrongPromiseChannel()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            await _group.RegisterAsync(pipeline.Channel);

            IChannelPipeline pipeline2 = new LocalChannel().Pipeline;
            await _group.RegisterAsync(pipeline2.Channel);

            try
            {
                var promise2 = pipeline2.Channel.NewPromise();
                await pipeline.CloseAsync(promise2);
                Assert.False(true);
            }
            catch (Exception exc)
            {
                Assert.IsType<ArgumentException>(exc);
            }
            finally
            {
                pipeline.CloseAsync().Ignore();
                pipeline2.CloseAsync().Ignore();
            }
        }

        [Fact]
        public async Task TestUnexpectedVoidChannelPromise()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            await _group.RegisterAsync(pipeline.Channel);

            try
            {
                var promise = new VoidChannelPromise(pipeline.Channel, false);
                await pipeline.CloseAsync(promise);
                Assert.False(true);
            }
            catch (Exception exc)
            {
                Assert.IsType<ArgumentException>(exc);
            }
            finally
            {
                pipeline.CloseAsync().Ignore();
            }
        }

        [Fact(Skip = "TestUnexpectedVoidChannelPromiseCloseFuture")]
        public void TestUnexpectedVoidChannelPromiseCloseFuture()
        {
            //IChannelPipeline pipeline = new LocalChannel().Pipeline;
            //await _group.RegisterAsync(pipeline.Channel);

            //try
            //{
            //    var promise = (ChannelPromise)pipeline.Channel.closeFuture();
            //    pipeline.CloseAsync(promise);
            //}
            //finally
            //{
            //    pipeline.CloseAsync();
            //}
        }

        [Fact]
        public async Task TestCancelDeregister()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            await _group.RegisterAsync(pipeline.Channel);

            IPromise promise = pipeline.Channel.NewPromise();
            Assert.True(promise.TrySetCanceled());
            var future = pipeline.DeregisterAsync(promise);
            Assert.True(future.IsCanceled);
        }

        [Fact]
        public async Task TestCancelWrite()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            await _group.RegisterAsync(pipeline.Channel);

            IPromise promise = pipeline.Channel.NewPromise();
            Assert.True(promise.TrySetCanceled());
            IByteBuffer buffer = Unpooled.Buffer();
            Assert.Equal(1, buffer.ReferenceCount);
            var future = pipeline.WriteAsync(buffer, promise);
            Assert.True(future.IsCanceled);
            Assert.Equal(0, buffer.ReferenceCount);
        }

        [Fact]
        public async Task TestCancelWriteAndFlush()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            await _group.RegisterAsync(pipeline.Channel);

            IPromise promise = pipeline.Channel.NewPromise();
            Assert.True(promise.TrySetCanceled());
            IByteBuffer buffer = Unpooled.Buffer();
            Assert.Equal(1, buffer.ReferenceCount);
            var future = pipeline.WriteAndFlushAsync(buffer, promise);
            Assert.True(future.IsCanceled);
            Assert.Equal(0, buffer.ReferenceCount);
        }

        [Fact]
        public void TestFirstContextEmptyPipeline()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            Assert.Null(pipeline.FirstContext());
        }

        [Fact]
        public void TestLastContextEmptyPipeline()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            Assert.Null(pipeline.LastContext());
        }

        [Fact]
        public void TestFirstHandlerEmptyPipeline()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            Assert.Null(pipeline.First());
        }

        [Fact]
        public void TestLastHandlerEmptyPipeline()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            Assert.Null(pipeline.Last());
        }

        [Fact]
        public void TestChannelInitializerException()
        {
            InvalidOperationException exception = new InvalidOperationException();
            AtomicReference<Exception> error = new AtomicReference<Exception>();
            CountdownEvent latch = new CountdownEvent(1);
            EmbeddedChannel channel = new EmbeddedChannel(new ChannelInitializer0(exception, error, latch));
            Assert.True(latch.Wait(TimeSpan.FromMinutes(1)));
            Assert.False(channel.IsActive);
            Assert.Same(exception, error.Value);
        }

        class ChannelInitializer0 : ChannelInitializer<IChannel>
        {
            private readonly InvalidOperationException _exception;
            private readonly AtomicReference<Exception> _error;
            private readonly CountdownEvent _latch;

            public ChannelInitializer0(InvalidOperationException exception, AtomicReference<Exception> error, CountdownEvent latch)
            {
                _exception = exception;
                _error = error;
                _latch = latch;
            }

            protected override void InitChannel(IChannel channel)
            {
                throw _exception;
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                base.ExceptionCaught(ctx, cause);
                _error.Value = cause;
                _latch.Signal();
            }
        }

        [Fact(Skip = "TestChannelUnregistrationWithCustomExecutor")]
        public void TestChannelUnregistrationWithCustomExecutor()
        {
            //CountdownEvent channelLatch = new CountdownEvent(1);
            //CountdownEvent handlerLatch = new CountdownEvent(1);
            //ChannelPipeline pipeline = new LocalChannel().Pipeline;
            //pipeline.AddLast(new ChannelInitializer<Channel>()
            //{
            //    //@Override
            //    //protected void initChannel(Channel ch) throws Exception {
            //    //    ch.Pipeline.AddLast(new WrapperExecutor(),
            //    //            new ChannelInboundHandlerAdapter() {

            //    //                @Override
            //    //                public void channelUnregistered(ChannelHandlerContext ctx) throws Exception {
            //    //                    channelLatch.countDown();
            //    //                }

            //    //                @Override
            //    //                public void handlerRemoved(ChannelHandlerContext ctx) throws Exception {
            //    //                    handlerLatch.countDown();
            //    //                }
            //    //            });
            //    //}
            //});
            //Channel channel = pipeline.channel();
            //group.register(channel);
            //channel.close();
            //channel.deregister();
            //Assert.True(channelLatch.await(2, TimeUnit.SECONDS));
            //Assert.True(handlerLatch.await(2, TimeUnit.SECONDS));
        }

        [Fact]
        public async Task TestAddHandlerBeforeRegisteredThenRemove()
        {
            IEventLoop loop = _group.GetNext();

            CheckEventExecutorHandler handler = new CheckEventExecutorHandler(loop);
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            pipeline.AddFirst(handler);
            Assert.False(handler._addedPromise.IsCompleted);
            await _group.RegisterAsync(pipeline.Channel);
            await handler._addedPromise.Task;
            pipeline.Remove(handler);
            await handler._removedPromise.Task;
        }

        [Fact]
        public async Task TestAddHandlerBeforeRegisteredThenReplace()
        {
            IEventLoop loop = _group.GetNext();
            CountdownEvent latch = new CountdownEvent(1);

            CheckEventExecutorHandler handler = new CheckEventExecutorHandler(loop);
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            pipeline.AddFirst(handler);
            Assert.False(handler._addedPromise.IsCompleted);
            await _group.RegisterAsync(pipeline.Channel);
            await handler._addedPromise.Task;
            pipeline.Replace(handler, null, new ChannelHandlerAdapter4(latch));
            await handler._removedPromise.Task;
            Assert.True(latch.Wait(TimeSpan.FromMinutes(1)));
        }

        class ChannelHandlerAdapter4 : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _latch;

            public ChannelHandlerAdapter4(CountdownEvent latch)
            {
                _latch = latch;
            }

            public override void HandlerAdded(IChannelHandlerContext context)
            {
                _latch.Signal();
            }
        }

        [Fact]
        public void TestAddRemoveHandlerNotRegistered()
        {
            AtomicReference<Exception> error = new AtomicReference<Exception>();
            IChannelHandler handler = new ErrorChannelHandler(error);
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            pipeline.AddFirst(handler);
            pipeline.Remove(handler);

            Assert.Null(error.Value);
        }

        [Fact]
        public void TestAddReplaceHandlerNotRegistered()
        {
            AtomicReference<Exception> error = new AtomicReference<Exception>();
            IChannelHandler handler = new ErrorChannelHandler(error);
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            pipeline.AddFirst(handler);
            pipeline.Replace(handler, null, new ErrorChannelHandler(error));

            Assert.Null(error.Value);
        }

        [Fact]
        public async Task TestHandlerAddedAndRemovedCalledInCorrectOrder()
        {
            IEventExecutorGroup group1 = new DefaultEventExecutorGroup(1);
            IEventExecutorGroup group2 = new DefaultEventExecutorGroup(1);

            try
            {
                var addedQueue = new BlockingCollection<CheckOrderHandler>();
                var removedQueue = new BlockingCollection<CheckOrderHandler>();

                CheckOrderHandler handler1 = new CheckOrderHandler(addedQueue, removedQueue);
                CheckOrderHandler handler2 = new CheckOrderHandler(addedQueue, removedQueue);
                CheckOrderHandler handler3 = new CheckOrderHandler(addedQueue, removedQueue);
                CheckOrderHandler handler4 = new CheckOrderHandler(addedQueue, removedQueue);

                IChannelPipeline pipeline = new LocalChannel().Pipeline;
                pipeline.AddLast(handler1);
                await _group.RegisterAsync(pipeline.Channel);
                pipeline.AddLast(group1, handler2);
                pipeline.AddLast(group2, handler3);
                pipeline.AddLast(handler4);

                Assert.Empty(removedQueue);
                await pipeline.Channel.CloseAsync();
                AssertHandler(addedQueue.Take(), handler1);

                // Depending on timing this can be handler2 or handler3 as these use different EventExecutorGroups.
                AssertHandler(addedQueue.Take(), handler2, handler3, handler4);
                AssertHandler(addedQueue.Take(), handler2, handler3, handler4);
                AssertHandler(addedQueue.Take(), handler2, handler3, handler4);

                Assert.Empty(addedQueue);

                AssertHandler(removedQueue.Take(), handler4);
                AssertHandler(removedQueue.Take(), handler3);
                AssertHandler(removedQueue.Take(), handler2);
                AssertHandler(removedQueue.Take(), handler1);
                Assert.Empty(removedQueue);
            }
            finally
            {
                Task.WaitAll(
                    group1.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    group2.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5))
                );
            }
        }

        [Fact]
        public async Task TestHandlerAddedExceptionFromChildHandlerIsPropagated()
        {
            IEventExecutorGroup group1 = new DefaultEventExecutorGroup(1);
            try
            {
                var promise = group1.GetNext().NewPromise();
                AtomicBoolean handlerAdded = new AtomicBoolean();
                Exception exception = new Exception();
                IChannelPipeline pipeline = new LocalChannel().Pipeline;
                pipeline.AddLast(group1, new CheckExceptionHandler(exception, promise));
                pipeline.AddFirst(new ChannelHandlerAdapter3(handlerAdded, exception));
                Assert.False(handlerAdded.Value);
                await _group.RegisterAsync(pipeline.Channel);
                await promise.Task;
            }
            finally
            {
                await group1.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5));
            }
        }

        class ChannelHandlerAdapter3 : ChannelHandlerAdapter
        {
            private readonly AtomicBoolean _handlerAdded;
            private readonly Exception _exception;

            public ChannelHandlerAdapter3(AtomicBoolean handlerAdded, Exception exception)
            {
                _handlerAdded = handlerAdded;
                _exception = exception;
            }

            public override void HandlerAdded(IChannelHandlerContext context)
            {
                _handlerAdded.Value = true;
                throw _exception;
            }
        }

        [Fact]
        public async Task TestHandlerRemovedExceptionFromChildHandlerIsPropagated()
        {
            IEventExecutorGroup group1 = new DefaultEventExecutorGroup(1);
            try
            {
                var promise = group1.GetNext().NewPromise();
                string handlerName = "foo";
                Exception exception = new Exception();
                IChannelPipeline pipeline = new LocalChannel().Pipeline;
                pipeline.AddLast(handlerName, new ChannelHandlerAdapter2(exception));
                pipeline.AddLast(group1, new CheckExceptionHandler(exception, promise));
                await _group.RegisterAsync(pipeline.Channel);
                pipeline.Remove(handlerName);
                await promise.Task;
            }
            finally
            {
                await group1.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5));
            }
        }

        class ChannelHandlerAdapter2 : ChannelHandlerAdapter
        {
            private readonly Exception _exception;

            public ChannelHandlerAdapter2(Exception exception) => _exception = exception;

            public override void HandlerRemoved(IChannelHandlerContext context)
            {
                throw _exception;
            }
        }

        [Fact]
        public async Task TestHandlerAddedThrowsAndRemovedThrowsException()
        {
            IEventExecutorGroup group1 = new DefaultEventExecutorGroup(1);
            try
            {
                CountdownEvent latch = new CountdownEvent(1);
                var promise = group1.GetNext().NewPromise();
                Exception exceptionAdded = new Exception();
                Exception exceptionRemoved = new Exception();
                string handlerName = "foo";
                IChannelPipeline pipeline = new LocalChannel().Pipeline;
                pipeline.AddLast(group1, new CheckExceptionHandler(exceptionAdded, promise));
                pipeline.AddFirst(handlerName, new ChannelHandlerAdapter1(latch, exceptionAdded, exceptionRemoved));
                await _group.RegisterAsync(pipeline.Channel);
                Assert.True(latch.Wait(TimeSpan.FromMinutes(1)));
                Assert.Null(pipeline.Context(handlerName));
                await promise.Task;
            }
            finally
            {
                await group1.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5));
            }
        }

        class ChannelHandlerAdapter1 : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _latch;
            private readonly Exception _exceptionAdded;
            private readonly Exception _exceptionRemoved;

            public ChannelHandlerAdapter1(CountdownEvent latch, Exception exceptionAdded, Exception exceptionRemoved)
            {
                _latch = latch;
                _exceptionAdded = exceptionAdded;
                _exceptionRemoved = exceptionRemoved;
            }

            public override void HandlerAdded(IChannelHandlerContext context)
            {
                throw _exceptionAdded;
            }

            public override void HandlerRemoved(IChannelHandlerContext context)
            {
                // Execute this later so we are sure the exception is handled first.
                context.Executor.Execute(() => _latch.Signal());
                throw _exceptionRemoved;
            }
        }

        [Fact]
        public async Task TestAddRemoveHandlerCalledOnceRegistered()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            CallbackCheckHandler handler = new CallbackCheckHandler();

            pipeline.AddFirst(handler);
            pipeline.Remove(handler);

            Assert.False(handler._addedHandler.Task.IsCompleted);
            Assert.False(handler._removedHandler.Task.IsCompleted);

            await _group.RegisterAsync(pipeline.Channel);
            var cause = handler._error.Value;
            if (cause != null)
            {
                throw cause;
            }

            var result = await handler._addedHandler.Task;
            Assert.True(result);
            result = await handler._removedHandler.Task;
            Assert.True(result);
        }

        [Fact]
        public async Task TestAddReplaceHandlerCalledOnceRegistered()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            CallbackCheckHandler handler = new CallbackCheckHandler();
            CallbackCheckHandler handler2 = new CallbackCheckHandler();

            pipeline.AddFirst(handler);
            pipeline.Replace(handler, null, handler2);

            Assert.False(handler._addedHandler.Task.IsCompleted);
            Assert.False(handler._removedHandler.Task.IsCompleted);
            Assert.False(handler2._addedHandler.Task.IsCompleted);
            Assert.False(handler2._removedHandler.Task.IsCompleted);

            await _group.RegisterAsync(pipeline.Channel);
            var cause = handler._error.Value;
            if (cause != null)
            {
                throw cause;
            }

            Assert.True(handler._addedHandler.Task.IsCompleted);
            Assert.True(handler._removedHandler.Task.IsCompleted);
            Assert.True(handler._addedHandler.Task.Result);
            Assert.True(handler._removedHandler.Task.Result);

            var cause2 = handler2._error.Value;
            if (cause2 != null)
            {
                throw cause2;
            }

            Assert.True(handler2._addedHandler.Task.IsCompleted);
            Assert.True(handler2._addedHandler.Task.Result);
            Assert.False(handler2._removedHandler.Task.IsCompleted);
            pipeline.Remove(handler2);
            var result = await handler2._removedHandler.Task;
            Assert.True(result);
        }

        [Fact]
        public async Task TestAddBefore()
        {
            IChannelPipeline pipeline1 = new LocalChannel().Pipeline;
            IChannelPipeline pipeline2 = new LocalChannel().Pipeline;

            IEventLoopGroup defaultGroup = new DefaultEventLoopGroup(2);
            try
            {
                IEventLoop eventLoop1 = defaultGroup.GetNext();
                IEventLoop eventLoop2 = defaultGroup.GetNext();

                await eventLoop1.RegisterAsync(pipeline1.Channel);
                await eventLoop2.RegisterAsync(pipeline2.Channel);

                CountdownEvent latch = new CountdownEvent(2 * 10);
                for (int i = 0; i < 10; i++)
                {
                    eventLoop1.Execute(new TestTask(pipeline2, latch));
                    eventLoop2.Execute(new TestTask(pipeline1, latch));
                }
                Assert.True(latch.Wait(TimeSpan.FromMinutes(1)));
            }
            finally
            {
                await defaultGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5));
            }
        }

        [Fact]
        public Task TestAddInListenerSocket()
        {
            return TestAddInListener(new TcpSocketChannel(), new MultithreadEventLoopGroup(1));
        }

        [Fact]
        public Task TestAddInListenerLocal()
        {
            return TestAddInListener(new LocalChannel(), new DefaultEventLoopGroup(1));
        }

        private static async Task TestAddInListener(IChannel channel, IEventLoopGroup group)
        {
            IChannelPipeline pipeline1 = channel.Pipeline;
            try
            {
                object evt = new object();
                var promise = new TaskCompletionSource<object>();
                await group.RegisterAsync(pipeline1.Channel);
                AtomicBoolean handlerAddedCalled = new AtomicBoolean();
                pipeline1.AddLast(new ChannelInboundHandlerAdapter3(promise, handlerAddedCalled));
                if (!handlerAddedCalled.Value)
                {
                    promise.SetException(new Exception("handlerAdded(...) should have been called"));
                    return;
                }
                // This event must be captured by the added handler.
                pipeline1.FireUserEventTriggered(evt);
                var result = promise.Task.GetAwaiter().GetResult();
                Assert.Same(evt, result);
            }
            finally
            {
                await pipeline1.Channel.CloseAsync();
                await group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5));
            }
        }

        class ChannelInboundHandlerAdapter3 : ChannelHandlerAdapter
        {
            private readonly TaskCompletionSource<object> _promise;
            private readonly AtomicBoolean _handlerAddedCalled;

            public ChannelInboundHandlerAdapter3(TaskCompletionSource<object> promise, AtomicBoolean handlerAddedCalled)
            {
                _promise = promise;
                _handlerAddedCalled = handlerAddedCalled;
            }

            public override void HandlerAdded(IChannelHandlerContext context)
            {
                _handlerAddedCalled.Value = true;
            }

            public override void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
                _promise.SetResult(evt);
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                _promise.SetException(exception);
            }
        }

        [Fact]
        public void TestNullName()
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            pipeline.AddLast(NewHandler());
            pipeline.AddLast(name: null, NewHandler());
            pipeline.AddFirst(NewHandler());
            pipeline.AddFirst(name: null, NewHandler());

            pipeline.AddLast("test", NewHandler());
            pipeline.AddAfter("test", null, NewHandler());

            pipeline.AddBefore("test", null, NewHandler());
        }

        [Fact(Skip = "TestUnorderedEventExecutor")]
        public void TestUnorderedEventExecutor()
        {
            //    ChannelPipeline pipeline1 = new LocalChannel().pipeline();
            //    EventExecutorGroup eventExecutors = new UnorderedThreadPoolEventExecutor(2);
            //    EventLoopGroup defaultGroup = new DefaultEventLoopGroup(1);
            //    try
            //    {
            //        EventLoop eventLoop1 = defaultGroup.next();
            //        eventLoop1.register(pipeline1.channel()).syncUninterruptibly();
            //        final CountdownEvent latch = new CountdownEvent(1);
            //        //pipeline1.addLast(eventExecutors, new ChannelInboundHandlerAdapter() {
            //        //    @Override
            //        //    public void handlerAdded(ChannelHandlerContext ctx) throws Exception {
            //        //        // Just block one of the two threads.
            //        //        LockSupport.park();
            //        //    }

            //        //    @Override
            //        //    public void userEventTriggered(ChannelHandlerContext ctx, Object evt) throws Exception {
            //        //        latch.countDown();
            //        //    }
            //        //});
            //        // Trigger an event, as we use UnorderedEventExecutor userEventTriggered should be called even when
            //        // handlerAdded(...) blocks.
            //        pipeline1.fireUserEventTriggered("");
            //        latch.await();
            //    }
            //    finally
            //    {
            //        defaultGroup.shutdownGracefully(0, 0, TimeUnit.SECONDS).syncUninterruptibly();
            //        eventExecutors.shutdownGracefully(0, 0, TimeUnit.SECONDS).syncUninterruptibly();
            //    }
        }

        [Fact]
        public void TestPinExecutor()
        {
            IEventExecutorGroup group = new DefaultEventExecutorGroup(2);
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            IChannelPipeline pipeline2 = new LocalChannel().Pipeline;

            pipeline.AddLast(group, "h1", new ChannelHandlerAdapter());
            pipeline.AddLast(group, "h2", new ChannelHandlerAdapter());
            pipeline2.AddLast(group, "h3", new ChannelHandlerAdapter());

            IEventExecutor executor1 = pipeline.Context("h1").Executor;
            IEventExecutor executor2 = pipeline.Context("h2").Executor;
            Assert.NotNull(executor1);
            Assert.NotNull(executor2);
            Assert.Same(executor1, executor2);
            IEventExecutor executor3 = pipeline2.Context("h3").Executor;
            Assert.NotNull(executor3);
            Assert.NotSame(executor3, executor2);
            group.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero).GetAwaiter().GetResult();
        }

        [Fact]
        public void TestNotPinExecutor()
        {
            IEventExecutorGroup group = new DefaultEventExecutorGroup(2);
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            pipeline.Channel.Configuration.SetOption(ChannelOption.SingleEventexecutorPerGroup, false);

            pipeline.AddLast(group, "h1", new ChannelHandlerAdapter());
            pipeline.AddLast(group, "h2", new ChannelHandlerAdapter());

            IEventExecutor executor1 = pipeline.Context("h1").Executor;
            IEventExecutor executor2 = pipeline.Context("h2").Executor;
            Assert.NotNull(executor1);
            Assert.NotNull(executor2);
            Assert.NotSame(executor1, executor2);
            group.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero).GetAwaiter().GetResult();
        }

        [Fact]
        public async Task TestVoidPromiseNotify()
        {
            IChannelPipeline pipeline1 = new LocalChannel().Pipeline;

            IEventLoopGroup defaultGroup = new DefaultEventLoopGroup(1);
            IEventLoop eventLoop1 = defaultGroup.GetNext();
            IPromise promise = eventLoop1.NewPromise();
            Exception exception = new ArgumentException();
            try
            {
                await eventLoop1.RegisterAsync(pipeline1.Channel);
                pipeline1.AddLast(new ChannelDuplexHandler1(promise, exception));
                var task = pipeline1.WriteAsync("test", pipeline1.VoidPromise());
                try
                {
                    await promise.Task.WithTimeout(TimeSpan.FromSeconds(1));
                    Assert.False(true);
                }
                catch (ArgumentException action)
                {
                    Assert.Same(exception, action);
                }
            }
            finally
            {
                await pipeline1.Channel.CloseAsync();
                await defaultGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5));
            }
        }

        class ChannelDuplexHandler1 : ChannelDuplexHandler
        {
            private readonly IPromise _promise;
            private readonly Exception _exception;

            public ChannelDuplexHandler1(IPromise promise, Exception exception)
            {
                _promise = promise;
                _exception = exception;
            }

            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                throw _exception;
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                _promise.SetException(exception);
            }
        }

        // Test for https://github.com/netty/netty/issues/8676.
        [Fact]
        public async Task TestHandlerRemovedOnlyCalledWhenHandlerAddedCalled()
        {
            IEventLoopGroup group = new DefaultEventLoopGroup(1);
            try
            {
                AtomicReference<Exception> errorRef = new AtomicReference<Exception>();

                // As this only happens via a race we will verify 500 times. This was good enough to have it failed most of
                // the time.
                for (int i = 0; i < 500; i++)
                {

                    IChannelPipeline pipeline = new LocalChannel().Pipeline;
                    await group.RegisterAsync(pipeline.Channel);

                    CountdownEvent latch = new CountdownEvent(1);

                    pipeline.AddLast(new ChannelInboundHandlerAdapter1(latch));

                    // Close the pipeline which will call destroy0(). This will remove each handler in the pipeline and
                    // should call handlerRemoved(...) if and only if handlerAdded(...) was called for the handler before.
                    pipeline.CloseAsync().Ignore();

                    pipeline.AddLast(new ChannelInboundHandlerAdapter2(errorRef));

                    latch.Signal();

                    await pipeline.Channel.CloseCompletion;

                    // Schedule something on the EventLoop to ensure all other scheduled tasks had a chance to complete.
                    await pipeline.Channel.EventLoop.SubmitAsync(() =>
                    {
                        // NOOP
                        return 0;
                    });
                    var error = errorRef.Value;
                    if (error != null)
                    {
                        Assert.False(true);
                        throw error;
                    }
                }
            }
            finally
            {
                await group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(1));
            }
        }

        class ChannelInboundHandlerAdapter1 : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _latch;

            public ChannelInboundHandlerAdapter1(CountdownEvent latch) => _latch = latch;

            public override void HandlerRemoved(IChannelHandlerContext context)
            {
                // Block just for a bit so we have a chance to trigger the race mentioned in the issue.
                _latch.Wait(TimeSpan.FromMilliseconds(50));
            }
        }

        class ChannelInboundHandlerAdapter2 : ChannelHandlerAdapter
        {
            private readonly AtomicReference<Exception> _errorRef;
            private bool _handerAddedCalled;

            public ChannelInboundHandlerAdapter2(AtomicReference<Exception> errorRef)
            {
                _errorRef = errorRef;
            }

            public override void HandlerAdded(IChannelHandlerContext context)
            {
                _handerAddedCalled = true;
            }

            public override void HandlerRemoved(IChannelHandlerContext context)
            {
                if (!_handerAddedCalled)
                {
                    _errorRef.Value = new Exception(
                            "handlerRemoved(...) called without handlerAdded(...) before");
                }
            }
        }

        [Fact]
        public async Task TestSkipHandlerMethodsIfAnnotated()
        {
            EmbeddedChannel channel = new EmbeddedChannel(true);
            IChannelPipeline pipeline = channel.Pipeline;

            OutboundCalledHandler outboundCalledHandler = new OutboundCalledHandler();
            SkipHandler skipHandler = new SkipHandler();
            InboundCalledHandler inboundCalledHandler = new InboundCalledHandler();
            pipeline.AddLast(outboundCalledHandler, skipHandler, inboundCalledHandler);

            pipeline.FireChannelRegistered();
            pipeline.FireChannelUnregistered();
            pipeline.FireChannelActive();
            pipeline.FireChannelInactive();
            pipeline.FireChannelRead("");
            pipeline.FireChannelReadComplete();
            pipeline.FireChannelWritabilityChanged();
            pipeline.FireUserEventTriggered("");
            pipeline.FireExceptionCaught(new Exception());

            await pipeline.DeregisterAsync();
            await pipeline.BindAsync(new IPEndPoint(IPAddress.IPv6Any, 0));
            await pipeline.ConnectAsync(new IPEndPoint(IPAddress.IPv6Any, 0));
            await pipeline.DisconnectAsync();
            await pipeline.CloseAsync();
            pipeline.WriteAsync("").Ignore();
            pipeline.Flush();
            pipeline.Read();

            pipeline.Remove(outboundCalledHandler);
            pipeline.Remove(inboundCalledHandler);
            pipeline.Remove(skipHandler);

            Assert.False(channel.Finish());

            outboundCalledHandler.AssertCalled();
            inboundCalledHandler.AssertCalled();
            skipHandler.AssertSkipped();
        }

        class SkipHandler : IChannelHandler
        {
            private int _state = 2;
            private Exception _errorRef;

            private void Fail()
            {
                _errorRef = new Exception("Method should never been called");
            }

            [Skip]
            public virtual void ChannelRegistered(IChannelHandlerContext context)
            {
                Fail();
                context.FireChannelRegistered();
            }

            [Skip]
            public virtual void ChannelUnregistered(IChannelHandlerContext context)
            {
                Fail();
                context.FireChannelUnregistered();
            }

            [Skip]
            public virtual void ChannelActive(IChannelHandlerContext context)
            {
                Fail();
                context.FireChannelActive();
            }

            [Skip]
            public virtual void ChannelInactive(IChannelHandlerContext context)
            {
                Fail();
                context.FireChannelInactive();
            }

            [Skip]
            public virtual void ChannelRead(IChannelHandlerContext context, object message)
            {
                Fail();
                context.FireChannelRead(message);
            }

            [Skip]
            public virtual void ChannelReadComplete(IChannelHandlerContext context)
            {
                Fail();
                context.FireChannelReadComplete();
            }

            [Skip]
            public virtual void ChannelWritabilityChanged(IChannelHandlerContext context)
            {
                Fail();
                context.FireChannelWritabilityChanged();
            }

            public virtual void HandlerAdded(IChannelHandlerContext context)
            {
                _state--;
            }

            public virtual void HandlerRemoved(IChannelHandlerContext context)
            {
                _state--;
            }

            [Skip]
            public virtual void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
                Fail();
                context.FireUserEventTriggered(evt);
            }

            [Skip]
            public virtual void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                Fail();
                context.WriteAsync(message, promise);
            }

            [Skip]
            public virtual void Flush(IChannelHandlerContext context)
            {
                Fail();
                context.Flush();
            }

            [Skip]
            public virtual Task BindAsync(IChannelHandlerContext context, EndPoint localAddress)
            {
                Fail();
                return context.BindAsync(localAddress);
            }

            [Skip]
            public virtual Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress)
            {
                Fail();
                return context.ConnectAsync(remoteAddress, localAddress);
            }

            [Skip]
            public virtual void Disconnect(IChannelHandlerContext context, IPromise promise)
            {
                Fail();
                context.DisconnectAsync(promise);
            }

            [Skip]
            public virtual void Close(IChannelHandlerContext context, IPromise promise)
            {
                Fail();
                context.CloseAsync(promise);
            }

            [Skip]
            public virtual void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                Fail();
                context.FireExceptionCaught(exception);
            }

            [Skip]
            public virtual void Deregister(IChannelHandlerContext context, IPromise promise)
            {
                Fail();
                context.DeregisterAsync(promise);
            }

            [Skip]
            public virtual void Read(IChannelHandlerContext context)
            {
                Fail();
                context.Read();
            }

            public virtual bool IsSharable => false;

            public void AssertSkipped()
            {
                Assert.Equal(0, _state);
                var error = _errorRef;
                if (error != null)
                {
                    throw error;
                }
            }
        }

        class OutboundCalledHandler : ChannelHandlerAdapter
        {
            private static readonly int MASK_BIND = 1;
            private static readonly int MASK_CONNECT = 1 << 1;
            private static readonly int MASK_DISCONNECT = 1 << 2;
            private static readonly int MASK_CLOSE = 1 << 3;
            private static readonly int MASK_DEREGISTER = 1 << 4;
            private static readonly int MASK_READ = 1 << 5;
            private static readonly int MASK_WRITE = 1 << 6;
            private static readonly int MASK_FLUSH = 1 << 7;
            private static readonly int MASK_ADDED = 1 << 8;
            private static readonly int MASK_REMOVED = 1 << 9;

            private int _executionMask;

            public override void HandlerAdded(IChannelHandlerContext context)
            {
                _executionMask |= MASK_ADDED;
            }

            public override void HandlerRemoved(IChannelHandlerContext context)
            {
                _executionMask |= MASK_REMOVED;
            }

            public override Task BindAsync(IChannelHandlerContext context, EndPoint localAddress)
            {
                _executionMask |= MASK_BIND;
                return TaskUtil.Completed;
            }

            public override Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress)
            {
                _executionMask |= MASK_CONNECT;
                return TaskUtil.Completed;
            }

            public override void Disconnect(IChannelHandlerContext context, IPromise promise)
            {
                _executionMask |= MASK_DISCONNECT;
                promise.Complete();
            }

            public override void Close(IChannelHandlerContext context, IPromise promise)
            {
                _executionMask |= MASK_CLOSE;
                promise.Complete();
            }

            public override void Deregister(IChannelHandlerContext context, IPromise promise)
            {
                _executionMask |= MASK_DEREGISTER;
                promise.Complete();
            }

            public override void Read(IChannelHandlerContext context)
            {
                _executionMask |= MASK_READ;
            }

            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                _executionMask |= MASK_WRITE;
                promise.Complete();
            }

            public override void Flush(IChannelHandlerContext context)
            {
                _executionMask |= MASK_FLUSH;
            }

            public void AssertCalled()
            {
                AssertCalled("handlerAdded", MASK_ADDED);
                AssertCalled("handlerRemoved", MASK_REMOVED);
                AssertCalled("bind", MASK_BIND);
                AssertCalled("connect", MASK_CONNECT);
                AssertCalled("disconnect", MASK_DISCONNECT);
                AssertCalled("close", MASK_CLOSE);
                AssertCalled("deregister", MASK_DEREGISTER);
                AssertCalled("read", MASK_READ);
                AssertCalled("write", MASK_WRITE);
                AssertCalled("flush", MASK_FLUSH);
            }

            private void AssertCalled(string methodName, int mask)
            {
                Assert.True((_executionMask & mask) != 0, methodName + " was not called");
            }
        }

        class InboundCalledHandler : ChannelHandlerAdapter
        {
            private static readonly int MASK_CHANNEL_REGISTER = 1;
            private static readonly int MASK_CHANNEL_UNREGISTER = 1 << 1;
            private static readonly int MASK_CHANNEL_ACTIVE = 1 << 2;
            private static readonly int MASK_CHANNEL_INACTIVE = 1 << 3;
            private static readonly int MASK_CHANNEL_READ = 1 << 4;
            private static readonly int MASK_CHANNEL_READ_COMPLETE = 1 << 5;
            private static readonly int MASK_USER_EVENT_TRIGGERED = 1 << 6;
            private static readonly int MASK_CHANNEL_WRITABILITY_CHANGED = 1 << 7;
            private static readonly int MASK_EXCEPTION_CAUGHT = 1 << 8;
            private static readonly int MASK_ADDED = 1 << 9;
            private static readonly int MASK_REMOVED = 1 << 10;

            private int _executionMask;

            public override void HandlerAdded(IChannelHandlerContext context)
            {
                _executionMask |= MASK_ADDED;
            }

            public override void HandlerRemoved(IChannelHandlerContext context)
            {
                _executionMask |= MASK_REMOVED;
            }

            public override void ChannelRegistered(IChannelHandlerContext context)
            {
                _executionMask |= MASK_CHANNEL_REGISTER;
            }

            public override void ChannelUnregistered(IChannelHandlerContext context)
            {
                _executionMask |= MASK_CHANNEL_UNREGISTER;
            }

            public override void ChannelActive(IChannelHandlerContext context)
            {
                _executionMask |= MASK_CHANNEL_ACTIVE;
            }

            public override void ChannelInactive(IChannelHandlerContext context)
            {
                _executionMask |= MASK_CHANNEL_INACTIVE;
            }

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                _executionMask |= MASK_CHANNEL_READ;
            }

            public override void ChannelReadComplete(IChannelHandlerContext context)
            {
                _executionMask |= MASK_CHANNEL_READ_COMPLETE;
            }

            public override void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
                _executionMask |= MASK_USER_EVENT_TRIGGERED;
            }

            public override void ChannelWritabilityChanged(IChannelHandlerContext context)
            {
                _executionMask |= MASK_CHANNEL_WRITABILITY_CHANGED;
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                _executionMask |= MASK_EXCEPTION_CAUGHT;
            }

            public void AssertCalled()
            {
                AssertCalled("handlerAdded", MASK_ADDED);
                AssertCalled("handlerRemoved", MASK_REMOVED);
                AssertCalled("channelRegistered", MASK_CHANNEL_REGISTER);
                AssertCalled("channelUnregistered", MASK_CHANNEL_UNREGISTER);
                AssertCalled("channelActive", MASK_CHANNEL_ACTIVE);
                AssertCalled("channelInactive", MASK_CHANNEL_INACTIVE);
                AssertCalled("channelRead", MASK_CHANNEL_READ);
                AssertCalled("channelReadComplete", MASK_CHANNEL_READ_COMPLETE);
                AssertCalled("userEventTriggered", MASK_USER_EVENT_TRIGGERED);
                AssertCalled("channelWritabilityChanged", MASK_CHANNEL_WRITABILITY_CHANGED);
                AssertCalled("exceptionCaught", MASK_EXCEPTION_CAUGHT);
            }

            private void AssertCalled(string methodName, int mask)
            {
                Assert.True((_executionMask & mask) != 0, methodName + " was not called");
            }
        }

        [Fact(Skip = "IllegalArgumentException")]
        public Task TestWriteThrowsReleaseMessage()
        {
            return TestWriteThrowsReleaseMessage0(false);
        }

        [Fact(Skip = "IllegalArgumentException")]
        public Task TestWriteAndFlushThrowsReleaseMessage()
        {
            return TestWriteThrowsReleaseMessage0(true);
        }

        private async Task TestWriteThrowsReleaseMessage0(bool flush)
        {
            IReferenceCounted referenceCounted = new AbstractReferenceCounted0();
            Assert.Equal(1, referenceCounted.ReferenceCount);

            IChannel channel = new LocalChannel();
            IChannel channel2 = new LocalChannel();
            await _group.RegisterAsync(channel);
            await _group.RegisterAsync(channel2);

            try
            {
                if (flush)
                {
                    await channel.WriteAndFlushAsync(referenceCounted, channel2.NewPromise());
                }
                else
                {
                    var task = channel.WriteAsync(referenceCounted, channel2.NewPromise());
                }
                Assert.False(true);
            }
            catch (NotYetConnectedException) // IllegalArgumentException
            {
                // expected
            }
            Assert.Equal(0, referenceCounted.ReferenceCount);

            await channel.CloseAsync();
            await channel2.CloseAsync();
        }

        class AbstractReferenceCounted0 : AbstractReferenceCounted
        {
            protected override void Deallocate()
            {
                // NOOP
            }

            public override IReferenceCounted Touch(object hint)
            {
                return this;
            }
        }

        [Fact]
        public void TestHandlerAddedFailedButHandlerStillRemoved()
        {
            TestHandlerAddedFailedButHandlerStillRemoved0(false);
        }

        [Fact]
        public void TestHandlerAddedFailedButHandlerStillRemovedWithLaterRegister()
        {
            TestHandlerAddedFailedButHandlerStillRemoved0(true);
        }

        private void TestHandlerAddedFailedButHandlerStillRemoved0(bool lateRegister)
        {
            IEventExecutorGroup executorGroup = new DefaultEventExecutorGroup(16);
            int numHandlers = 32;
            try
            {
                IChannel channel = new LocalChannel();
                channel.Configuration.SetOption(ChannelOption.SingleEventexecutorPerGroup, false);
                if (!lateRegister)
                {
                    _group.RegisterAsync(channel).GetAwaiter().GetResult();
                }
                channel.Pipeline.AddFirst(NewHandler());

                List<CountdownEvent> latchList = new List<CountdownEvent>(numHandlers);
                for (int i = 0; i < numHandlers; i++)
                {
                    CountdownEvent latch = new CountdownEvent(1);
                    channel.Pipeline.AddFirst(executorGroup, "h" + i, new BadChannelHandler(latch));
                    latchList.Add(latch);
                }
                if (lateRegister)
                {
                    _group.RegisterAsync(channel).GetAwaiter().GetResult();
                }

                for (int i = 0; i < numHandlers; i++)
                {
                    // Wait until the latch was countDown which means handlerRemoved(...) was called.
                    Assert.True(latchList[i].Wait(TimeSpan.FromMinutes(1)));
                    Assert.Null(channel.Pipeline.Get("h" + i));
                }
            }
            finally
            {
                executorGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            }
        }

        sealed class BadChannelHandler : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _latch;

            public BadChannelHandler(CountdownEvent latch)
            {
                _latch = latch;
            }

            public override void HandlerAdded(IChannelHandlerContext context)
            {
                Task.Delay(TimeSpan.FromMilliseconds(10)).Wait();
                throw new Exception();
            }

            public override void HandlerRemoved(IChannelHandlerContext context)
            {
                _latch.Signal();
            }
        }

        [Fact]
        public void HandlerAddedStateUpdatedBeforeHandlerAddedDoneForceEventLoop()
        {
            HandlerAddedStateUpdatedBeforeHandlerAddedDone(true);
        }

        [Fact]
        public void HandlerAddedStateUpdatedBeforeHandlerAddedDoneOnCallingThread()
        {
            HandlerAddedStateUpdatedBeforeHandlerAddedDone(false);
        }

        private void HandlerAddedStateUpdatedBeforeHandlerAddedDone(bool executeInEventLoop)
        {
            IChannelPipeline pipeline = new LocalChannel().Pipeline;
            object userEvent = new object();
            object writeObject = new object();
            CountdownEvent doneLatch = new CountdownEvent(1);

            _group.RegisterAsync(pipeline.Channel);

            void LocalRun()
            {
                pipeline.AddLast(new ChannelInboundHandlerAdapter0(userEvent, writeObject));
                pipeline.AddFirst(new ChannelDuplexHandler0(userEvent, writeObject, doneLatch));
            }

            if (executeInEventLoop)
            {
                pipeline.Channel.EventLoop.Execute(LocalRun);
            }
            else
            {
                LocalRun();
            }

            Assert.True(doneLatch.Wait(TimeSpan.FromMinutes(1)));
        }

        class ChannelInboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            private readonly object _userEvent;
            private readonly object _writeObject;

            public ChannelInboundHandlerAdapter0(object userEvent, object writeObject)
            {
                _userEvent = userEvent;
                _writeObject = writeObject;
            }

            public override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
            {
                if (evt == _userEvent)
                {
                    ctx.WriteAsync(_writeObject);
                }
                ctx.FireUserEventTriggered(evt);
            }
        }

        class ChannelDuplexHandler0 : ChannelDuplexHandler
        {
            private readonly object _userEvent;
            private readonly object _writeObject;
            private readonly CountdownEvent _doneLatch;

            public ChannelDuplexHandler0(object userEvent, object writeObject, CountdownEvent doneLatch)
            {
                _userEvent = userEvent;
                _writeObject = writeObject;
                _doneLatch = doneLatch;
            }

            public override void HandlerAdded(IChannelHandlerContext ctx)
            {
                ctx.FireUserEventTriggered(_userEvent);
            }

            public override void Write(IChannelHandlerContext ctx, object msg, IPromise promise)
            {
                if (msg == _writeObject)
                {
                    _doneLatch.Signal();
                }
                ctx.WriteAsync(msg, promise);
            }
        }

        sealed class TestTask : IRunnable
        {
            private readonly IChannelPipeline _pipeline;
            private readonly CountdownEvent _latch;

            public TestTask(IChannelPipeline pipeline, CountdownEvent latch)
            {
                _pipeline = pipeline;
                _latch = latch;
            }

            public void Run()
            {
                _pipeline.AddLast(new ChannelHandlerAdapter());
                _latch.Signal();
            }
        }

        sealed class CallbackCheckHandler : ChannelHandlerAdapter
        {
            internal readonly TaskCompletionSource<bool> _addedHandler = new TaskCompletionSource<bool>();
            internal readonly TaskCompletionSource<bool> _removedHandler = new TaskCompletionSource<bool>();
            internal readonly AtomicReference<Exception> _error = new AtomicReference<Exception>();

            public override void HandlerAdded(IChannelHandlerContext context)
            {
                if (!_addedHandler.TrySetResult(true))
                {
                    _error.Value = new Exception($"handlerAdded(...) called multiple times: {context.Name}");
                }
                else if (_removedHandler.Task.IsCompleted && _removedHandler.Task.Result == true)
                {
                    _error.Value = new Exception($"handlerRemoved(...) called before handlerAdded(...): {context.Name}");
                }
            }

            public override void HandlerRemoved(IChannelHandlerContext context)
            {
                if (!_removedHandler.TrySetResult(true))
                {
                    _error.Value = new Exception($"handlerRemoved(...) called multiple times: {context.Name}");
                }
                else if (_addedHandler.Task.IsCompleted && _addedHandler.Task.Result == false)
                {
                    _error.Value = new Exception($"handlerRemoved(...) called before handlerAdded(...): {context.Name}");
                }
            }
        }

        sealed class CheckExceptionHandler : ChannelHandlerAdapter
        {
            private readonly Exception _expected;
            private readonly IPromise _promise;

            public CheckExceptionHandler(Exception expected, IPromise promise)
            {
                _expected = expected;
                _promise = promise;
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                if (cause is ChannelPipelineException && cause.InnerException == _expected)
                {
                    _promise.Complete();
                }
                else
                {
                    _promise.SetException(new Exception("cause not the expected instance"));
                }
            }
        }

        private static void AssertHandler(CheckOrderHandler actual, params CheckOrderHandler[] handlers)
        {
            foreach (CheckOrderHandler h in handlers)
            {
                if (h == actual)
                {
                    actual.CheckError();
                    return;
                }
            }
            Assert.False(true); // handler was not one of the expected handlers
        }

        sealed class CheckOrderHandler : ChannelHandlerAdapter
        {
            private readonly BlockingCollection<CheckOrderHandler> _addedQueue;
            private readonly BlockingCollection<CheckOrderHandler> _removedQueue;
            private readonly AtomicReference<Exception> _error = new AtomicReference<Exception>();

            public CheckOrderHandler(BlockingCollection<CheckOrderHandler> addedQueue, BlockingCollection<CheckOrderHandler> removedQueue)
            {
                _addedQueue = addedQueue;
                _removedQueue = removedQueue;
            }

            public override void HandlerAdded(IChannelHandlerContext ctx)
            {
                _addedQueue.Add(this);
                CheckExecutor(ctx);
            }

            public override void HandlerRemoved(IChannelHandlerContext ctx)
            {
                _removedQueue.Add(this);
                CheckExecutor(ctx);
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                _error.Value = exception;
            }

            internal void CheckError()
            {
                var cause = _error.Value;
                if (cause != null)
                {
                    throw cause;
                }
            }

            private void CheckExecutor(IChannelHandlerContext ctx)
            {
                if (!ctx.Executor.InEventLoop)
                {
                    _error.Value = new Exception();
                }
            }
        }

        class CheckEventExecutorHandler : ChannelHandlerAdapter
        {
            internal readonly IEventExecutor _executor;
            internal readonly IPromise _addedPromise;
            internal readonly IPromise _removedPromise;

            public CheckEventExecutorHandler(IEventExecutor executor)
            {
                _executor = executor;
                _addedPromise = executor.NewPromise();
                _removedPromise = executor.NewPromise();
            }

            public override void HandlerAdded(IChannelHandlerContext ctx)
            {
                AssertExecutor(ctx, _addedPromise);
            }

            public override void HandlerRemoved(IChannelHandlerContext ctx)
            {
                AssertExecutor(ctx, _removedPromise);
            }

            private void AssertExecutor(IChannelHandlerContext ctx, IPromise promise)
            {
                bool same;
                try
                {
                    same = _executor == ctx.Executor;
                }
                catch (Exception cause)
                {
                    promise.SetException(cause);
                    return;
                }
                if (same)
                {
                    promise.Complete();
                }
                else
                {
                    promise.SetException(new Exception("EventExecutor not the same"));
                }
            }
        }

        class ErrorChannelHandler : ChannelHandlerAdapter
        {
            private readonly AtomicReference<Exception> _error;

            public ErrorChannelHandler(AtomicReference<Exception> error) => _error = error;

            public override void HandlerAdded(IChannelHandlerContext context)
            {
                _error.Value = new Exception(nameof(ErrorChannelHandler));
            }

            public override void HandlerRemoved(IChannelHandlerContext context)
            {
                _error.Value = new Exception(nameof(ErrorChannelHandler));
            }
        }

        private static int Next(AbstractChannelHandlerContext ctx)
        {
            AbstractChannelHandlerContext next = ctx.Next;
            if (next == null)
            {
                return int.MaxValue;
            }

            return ToInt(next.Name);
        }

        private static int ToInt(string name)
        {
            return int.TryParse(name, out int result) ? result : -1;
        }

        private static void VerifyContextNumber(IChannelPipeline pipeline, int expectedNumber)
        {
            AbstractChannelHandlerContext ctx = (AbstractChannelHandlerContext)pipeline.FirstContext();
            int handlerNumber = 0;
            while (ctx != ((DefaultChannelPipeline)pipeline)._tail)
            {
                handlerNumber++;
                ctx = ctx.Next;
            }
            Assert.Equal(expectedNumber, handlerNumber);
        }

        private static IChannelHandler[] NewHandlers(int num)
        {
            Assert.True(num > 0);

            IChannelHandler[] handlers = new IChannelHandler[num];
            for (int i = 0; i < num; i++)
            {
                handlers[i] = NewHandler();
            }

            return handlers;
        }

        private static IChannelHandler NewHandler()
        {
            return new TestHandler();
        }

        class TestHandler : ChannelHandlerAdapter
        {
            public override bool IsSharable => true;
        }

        class BufferedTestHandler : ChannelDuplexHandler
        {
            public readonly Queue<object> InboundBuffer = new Queue<object>();
            public readonly Queue<object> OutboundBuffer = new Queue<object>();

            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                OutboundBuffer.Enqueue(message);
            }

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                InboundBuffer.Enqueue(message);
            }

            public override void HandlerRemoved(IChannelHandlerContext ctx)
            {
                if (InboundBuffer.Count > 0)
                {
                    foreach (object o in InboundBuffer)
                    {
                        ctx.FireChannelRead(o);
                    }
                    ctx.FireChannelReadComplete();
                }
                if (OutboundBuffer.Count > 0)
                {
                    foreach (object o in OutboundBuffer)
                    {
                        ctx.WriteAsync(o);
                    }
                    ctx.Flush();
                }
            }
        }

        /// <summary>
        /// Test handler to validate life-cycle aware behavior.
        /// </summary>
        sealed class LifeCycleAwareTestHandler : ChannelHandlerAdapter
        {
            internal readonly string _name;

            private bool _afterAdd;
            private bool _afterRemove;

            public LifeCycleAwareTestHandler(string name) => _name = name;

            public void Validate(bool afterAdd, bool afterRemove)
            {
                Assert.Equal(/*name, */afterAdd, _afterAdd);
                Assert.Equal(/*name, */afterRemove, _afterRemove);
            }

            public override void HandlerAdded(IChannelHandlerContext context)
            {
                Validate(false, false);

                _afterAdd = true;
            }

            public override void HandlerRemoved(IChannelHandlerContext context)
            {
                Validate(true, false);

                _afterRemove = true;
            }
        }
    }
}