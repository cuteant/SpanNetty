namespace DotNetty.Transport.Tests.Channel
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using DotNetty.Transport.Channels.Local;
    using Xunit;
    using Xunit.Sdk;

    public class ChannelInitializerTest : IDisposable
    {
        private static readonly int TIMEOUT_MILLIS = 1000;
        private static readonly LocalAddress SERVER_ADDRESS = new LocalAddress("addr");

        private IEventLoopGroup _group;
        private ServerBootstrap _server;
        private Bootstrap _client;
        private InspectableHandler _testHandler;

        public ChannelInitializerTest()
        {
            _group = new DefaultEventLoopGroup(1);
            _server = new ServerBootstrap()
                    .Group(_group)
                    .Channel<LocalServerChannel>()
                .LocalAddress(SERVER_ADDRESS);
            _client = new Bootstrap()
                    .Group(_group)
                    .Channel<LocalChannel>()
                    .Handler(new ChannelHandlerAdapter());
            _testHandler = new InspectableHandler();
        }

        public void Dispose()
        {
            _group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(TIMEOUT_MILLIS)).GetAwaiter().GetResult();
        }

        [Fact]
        public Task TestInitChannelThrowsRegisterFirst()
        {
            return TestInitChannelThrows(true);
        }

        [Fact]
        public Task TestInitChannelThrowsRegisterAfter()
        {
            return TestInitChannelThrows(false);
        }

        private async Task TestInitChannelThrows(bool registerFirst)
        {
            Exception exception = new Exception();
            AtomicReference<Exception> causeRef = new AtomicReference<Exception>();

            IChannelPipeline pipeline = new LocalChannel().Pipeline;

            if (registerFirst)
            {
                await _group.RegisterAsync(pipeline.Channel);
            }
            pipeline.AddFirst(new ChannelInitializer0(exception, causeRef));

            if (!registerFirst)
            {
                await _group.RegisterAsync(pipeline.Channel);
            }
            await pipeline.Channel.CloseAsync();
            await pipeline.Channel.CloseCompletion;

            Assert.Same(exception, causeRef.Value);
        }

        class ChannelInitializer0 : ChannelInitializer<IChannel>
        {
            private readonly Exception _exception;
            private readonly AtomicReference<Exception> _causeRef;

            public ChannelInitializer0(Exception exception, AtomicReference<Exception> causeRef)
            {
                _exception = exception;
                _causeRef = causeRef;
            }

            protected override void InitChannel(IChannel channel)
            {
                throw _exception;
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                _causeRef.Value = cause;
                base.ExceptionCaught(ctx, cause);
            }
        }

        [Fact]
        public async Task TestChannelInitializerInInitializerCorrectOrdering()
        {
            ChannelHandlerAdapter handler1 = new ChannelHandlerAdapter();
            ChannelHandlerAdapter handler2 = new ChannelHandlerAdapter();
            ChannelHandlerAdapter handler3 = new ChannelHandlerAdapter();
            ChannelHandlerAdapter handler4 = new ChannelHandlerAdapter();

            _client.Handler(new ActionChannelInitializer<IChannel>(ch =>
            {
                ch.Pipeline.AddLast(handler1);
                ch.Pipeline.AddLast(new ActionChannelInitializer<IChannel>(ch0 =>
                {
                    ch0.Pipeline.AddLast(handler2);
                    ch0.Pipeline.AddLast(handler3);
                }));
                ch.Pipeline.AddLast(handler4);
            })).LocalAddress(LocalAddress.Any);

            var channel = await _client.BindAsync();
            try
            {
                // Execute some task on the EventLoop and wait until its done to be sure all handlers are added to the
                // pipeline.
                await channel.EventLoop.SubmitAsync(() =>
                {
                    // NOOP
                    return 0;
                });
                var handlers = channel.Pipeline.GetEnumerator();
                Assert.True(handlers.MoveNext());
                Assert.Same(handler1, handlers.Current);
                Assert.True(handlers.MoveNext());
                Assert.Same(handler2, handlers.Current);
                Assert.True(handlers.MoveNext());
                Assert.Same(handler3, handlers.Current);
                Assert.True(handlers.MoveNext());
                Assert.Same(handler4, handlers.Current);
                Assert.False(handlers.MoveNext());
            }
            finally
            {
                await channel.CloseAsync();
            }
        }

        [Fact]
        public async Task TestChannelInitializerReentrance()
        {
            AtomicInteger registeredCalled = new AtomicInteger(0);
            ChannelHandlerAdapter handler1 = new ChannelInboundHandlerAdapter0(registeredCalled);
            AtomicInteger initChannelCalled = new AtomicInteger(0);
            _client.Handler(new ActionChannelInitializer<IChannel>(ch =>
            {
                initChannelCalled.Increment();
                ch.Pipeline.AddLast(handler1);
                ch.Pipeline.FireChannelRegistered();
            })).LocalAddress(LocalAddress.Any);

            var channel = await _client.BindAsync();
            try
            {
                // Execute some task on the EventLoop and wait until its done to be sure all handlers are added to the
                // pipeline.
                await channel.EventLoop.SubmitAsync(() =>
                {
                    // NOOP
                    return 0;
                });
                Assert.Equal(1, initChannelCalled.Value);
                Assert.Equal(2, registeredCalled.Value);
            }
            finally
            {
                await channel.CloseAsync();
            }
        }

        class ChannelInboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            private readonly AtomicInteger _registeredCalled;

            public ChannelInboundHandlerAdapter0(AtomicInteger registeredCalled)
            {
                _registeredCalled = registeredCalled;
            }

            public override void ChannelRegistered(IChannelHandlerContext context)
            {
                _registeredCalled.Increment();
            }
        }

        [Fact]
        public Task FirstHandlerInPipelineShouldReceiveChannelRegisteredEvent()
        {
            return TestChannelRegisteredEventPropagation(new ActionChannelInitializer<LocalChannel>(ch =>
            {
                ch.Pipeline.AddFirst(_testHandler);
            }));
        }

        [Fact]
        public Task LastHandlerInPipelineShouldReceiveChannelRegisteredEvent()
        {
            return TestChannelRegisteredEventPropagation(new ActionChannelInitializer<LocalChannel>(ch =>
            {
                ch.Pipeline.AddLast(_testHandler);
            }));
        }

        private async Task TestChannelRegisteredEventPropagation(ChannelInitializer<LocalChannel> init)
        {
            IChannel clientChannel = null, serverChannel = null;
            try
            {
                _server.ChildHandler(init);
                serverChannel = await _server.BindAsync();
                clientChannel = await _client.ConnectAsync(SERVER_ADDRESS);
                Assert.Equal(1, _testHandler.ChannelRegisteredCount.Value);
            }
            finally
            {
                CloseChannel(clientChannel);
                CloseChannel(serverChannel);
            }
        }

        [Fact]
        public void TestAddFirstChannelInitializer()
        {
            TestAddChannelInitializer(true);
        }

        [Fact]
        public void TestAddLastChannelInitializer()
        {
            TestAddChannelInitializer(false);
        }

        private static void TestAddChannelInitializer(bool first)
        {
            AtomicBoolean called = new AtomicBoolean();
            EmbeddedChannel channel = new EmbeddedChannel(new ActionChannelInitializer<IChannel>(ch =>
            {
                var handler = new ActionChannelInitializer<IChannel>(ch0 =>
                {
                    called.Value = true;
                });
                if (first)
                {
                    ch.Pipeline.AddFirst(handler);
                }
                else
                {
                    ch.Pipeline.AddLast(handler);
                }
            }));
            channel.Finish();
            Assert.True(called.Value);
        }

        [Fact]
        public async Task TestChannelInitializerEventExecutor()
        {
            AtomicInteger invokeCount = new AtomicInteger();
            AtomicInteger completeCount = new AtomicInteger();
            AtomicReference<Exception> errorRef = new AtomicReference<Exception>();
            LocalAddress addr = new LocalAddress("test");

            IEventExecutor executor = new DefaultEventLoop();

            CountdownEvent latch = new CountdownEvent(1);
            ServerBootstrap serverBootstrap = new ServerBootstrap()
                .Channel<LocalServerChannel>()
                .Group(_group)
                .LocalAddress(addr)
                .ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(executor, new ChannelInitializer1(invokeCount, completeCount, errorRef, latch));
                }));

            IChannel server = await serverBootstrap.BindAsync();

            Bootstrap clientBootstrap = new Bootstrap()
                .Channel<LocalChannel>()
                .Group(_group)
                .RemoteAddress(addr)
                .Handler(new ChannelHandlerAdapter());

            IChannel client = await clientBootstrap.ConnectAsync();
            await client.WriteAndFlushAsync("Hello World");

            await client.CloseAsync();
            await server.CloseAsync();

            await client.CloseCompletion;
            await server.CloseCompletion;

            // Wait until the handler is removed from the pipeline and so no more events are handled by it.
            Assert.True(latch.Wait(TimeSpan.FromMinutes(1)));

            Assert.Equal(1, invokeCount.Value);
            Assert.Equal(invokeCount.Value, completeCount.Value);

            Exception cause = errorRef.Value;
            if (cause != null)
            {
                throw cause;
            }

            await executor.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5));
        }

        // TODO review
        //class TestEventLoop : SingleThreadEventLoopBase
        //{
        //    //public override bool IsInEventLoop(XThread t)
        //    //{
        //    //    return false;
        //    //}

        //    //public override bool IsShuttingDown => false;

        //    //public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        //    //{
        //    //    throw new NotSupportedException();
        //    //}

        //    //public override Task TerminationCompletion => throw new NotSupportedException();
        //}

        class ChannelInitializer1 : ChannelInitializer<IChannel>
        {
            private readonly AtomicInteger _invokeCount;
            private readonly AtomicInteger _completeCount;
            private readonly AtomicReference<Exception> _errorRef;
            private readonly CountdownEvent _latch;

            public ChannelInitializer1(
                AtomicInteger invokeCount,
                AtomicInteger completeCount,
                AtomicReference<Exception> errorRef,
                CountdownEvent latch)
            {
                _invokeCount = invokeCount;
                _completeCount = completeCount;
                _errorRef = errorRef;
                _latch = latch;
            }

            protected override void InitChannel(IChannel channel)
            {
                _invokeCount.Increment();
                IChannelHandlerContext ctx = channel.Pipeline.Context(this);
                Assert.NotNull(ctx);
                channel.Pipeline.AddAfter(ctx.Executor,
                    ctx.Name, null, new ChannelInboundHandlerAdapter1(_latch));
                _completeCount.Increment();
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                if (cause is NotNullException)
                {
                    _errorRef.Value = cause;
                }
            }
        }

        class ChannelInboundHandlerAdapter1 : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _latch;

            public ChannelInboundHandlerAdapter1(CountdownEvent latch)
            {
                _latch = latch;
            }

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                // just drop on the floor.
            }

            public override void HandlerRemoved(IChannelHandlerContext context)
            {
                _latch.Signal();
            }
        }

        private static void CloseChannel(IChannel c)
        {
            if (c != null)
            {
                try
                {
                    c.CloseAsync().GetAwaiter().GetResult();
                }
                catch { }
            }
        }

        sealed class InspectableHandler : ChannelDuplexHandler
        {
            public AtomicInteger ChannelRegisteredCount = new AtomicInteger(0);

            public override void ChannelRegistered(IChannelHandlerContext context)
            {
                ChannelRegisteredCount.Increment();
                context.FireChannelRegistered();
            }
        }
    }
}