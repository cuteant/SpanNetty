namespace DotNetty.Transport.Tests.Channel.Local
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Local;
    using Xunit;

    public class LocalChannelTest : IDisposable
    {
        private static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<LocalChannelTest>();

        private static readonly LocalAddress TEST_ADDRESS = new LocalAddress("test.id");

        private readonly IEventLoopGroup _group1;
        private readonly IEventLoopGroup _group2;
        private readonly IEventLoopGroup _sharedGroup;

        public LocalChannelTest()
        {
            _group1 = new DefaultEventLoopGroup(2);
            _group2 = new DefaultEventLoopGroup(2);
            _sharedGroup = new DefaultEventLoopGroup(1);
        }

        public void Dispose()
        {
            Task.WaitAll(
                _group1.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                _group2.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                _sharedGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5))
            );
        }

        [Fact]
        public async Task TestLocalAddressReuse()
        {
            for (int i = 0; i < 2; i++)
            {
                Bootstrap cb = new Bootstrap();
                ServerBootstrap sb = new ServerBootstrap();

                cb.Group(_group1)
                  .Channel<LocalChannel>()
                  .Handler(new TestHandler());

                sb.Group(_group2)
                  .Channel<LocalServerChannel>()
                  .ChildHandler(new ActionChannelInitializer<LocalChannel>(ch =>
                  {
                      ch.Pipeline.AddLast(new TestHandler());
                  }));

                IChannel sc = null;
                IChannel cc = null;
                try
                {
                    // Start server
                    sc = await sb.BindAsync(TEST_ADDRESS);

                    CountdownEvent latch = new CountdownEvent(1);
                    // Connect to the server
                    cc = await cb.ConnectAsync(sc.LocalAddress);
                    IChannel ccCpy = cc;
                    cc.EventLoop.Execute(() =>
                    {
                        // Send a message event up the pipeline.
                        ccCpy.Pipeline.FireChannelRead("Hello, World");
                        latch.Signal();
                    });
                    Assert.True(latch.Wait(TimeSpan.FromSeconds(5)));

                    // Close the channel
                    await CloseChannel(cc);
                    await CloseChannel(sc);
                    await sc.CloseCompletion;

                    Assert.Null(LocalChannelRegistry.Get(TEST_ADDRESS));
                    //assertNull(String.format(
                    //        "Expected null, got channel '%s' for local address '%s'",
                    //        LocalChannelRegistry.get(TEST_ADDRESS), TEST_ADDRESS), LocalChannelRegistry.get(TEST_ADDRESS));
                }
                finally
                {
                    await CloseChannel(cc);
                    await CloseChannel(sc);
                }
            }
        }

        [Fact]
        public async Task TestWriteFailsFastOnClosedChannel()
        {
            Bootstrap cb = new Bootstrap();
            ServerBootstrap sb = new ServerBootstrap();

            cb.Group(_group1)
                    .Channel<LocalChannel>()
                    .Handler(new TestHandler());

            sb.Group(_group2)
                    .Channel<LocalServerChannel>()
                    .ChildHandler(new ActionChannelInitializer<LocalChannel>(ch =>
                    {
                        ch.Pipeline.AddLast(new TestHandler());
                    }));

            IChannel sc = null;
            IChannel cc = null;
            try
            {
                // Start server
                sc = await sb.BindAsync(TEST_ADDRESS);

                // Connect to the server
                cc = await cb.ConnectAsync(sc.LocalAddress);

                // Close the channel and write something.
                await cc.CloseAsync();
                try
                {
                    await cc.WriteAndFlushAsync(new object());
                    Assert.False(true, "must raise a ClosedChannelException");
                }
                catch (Exception e)
                {
                    Assert.True(e is ClosedChannelException);
                    // Ensure that the actual write attempt on a closed channel was never made by asserting that
                    // the ClosedChannelException has been created by AbstractUnsafe rather than transport implementations.
                    //if (e.getStackTrace().length > 0)
                    //{
                    //    assertThat(
                    //            e.getStackTrace()[0].getClassName(), is(AbstractChannel.class.getName() +
                    //                    "$AbstractUnsafe"));
                    //    e.printStackTrace();
                    //}
                }
            }
            finally
            {
                await CloseChannel(cc);
                await CloseChannel(sc);
            }
        }

        [Fact]
        public async Task TestServerCloseChannelSameEventLoop()
        {
            CountdownEvent latch = new CountdownEvent(1);
            ServerBootstrap sb = new ServerBootstrap()
                    .Group(_group2)
                    .Channel<LocalServerChannel>()
                    .ChildHandler(new SimpleChannelInboundHandler2(latch));
            IChannel sc = null;
            IChannel cc = null;
            try
            {
                sc = await sb.BindAsync(TEST_ADDRESS);

                Bootstrap b = new Bootstrap()
                        .Group(_group2)
                        .Channel<LocalChannel>()
                        .Handler(new SimpleChannelInboundHandler3());
                cc = await b.ConnectAsync(sc.LocalAddress);
                cc.WriteAndFlushAsync(new object()).Ignore();
                Assert.True(latch.Wait(TimeSpan.FromSeconds(5)));
            }
            finally
            {
                await CloseChannel(cc);
                await CloseChannel(sc);
            }
        }

        class SimpleChannelInboundHandler2 : SimpleChannelInboundHandler<object>
        {
            private readonly CountdownEvent _latch;

            public SimpleChannelInboundHandler2(CountdownEvent latch)
            {
                _latch = latch;
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
            {
                ctx.CloseAsync();
                _latch.Signal();
            }
        }

        class SimpleChannelInboundHandler3 : SimpleChannelInboundHandler<object>
        {
            protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
            {
                // discard
            }
        }

        [Fact(Skip = "LocalChannelRaceCondition")]
        public void LocalChannelRaceCondition()
        {
            //        final CountDownLatch closeLatch = new CountDownLatch(1);
            //        final EventLoopGroup clientGroup = new DefaultEventLoopGroup(1) {
            //            @Override
            //            protected EventLoop newChild(Executor threadFactory, Object... args)
            //                    throws Exception {
            //                return new SingleThreadEventLoop(this, threadFactory, true) {
            //                    @Override
            //                    protected void run() {
            //                        for (;;) {
            //                            Runnable task = takeTask();
            //                            if (task != null) {
            //                                /* Only slow down the anonymous class in LocalChannel#doRegister() */
            //                                if (task.getClass().getEnclosingClass() == LocalChannel.class) {
            //                                    try {
            //                                        closeLatch.await();
            //                                    } catch (InterruptedException e) {
            //                                        throw new Error(e);
            //}
            //                                }
            //                                task.run();
            //                                updateLastExecutionTime();
            //                            }

            //                            if (confirmShutdown()) {
            //                                break;
            //                            }
            //                        }
            //                    }
            //                };
            //            }
            //        };
            //        Channel sc = null;
            //Channel cc = null;
            //        try {
            //            ServerBootstrap sb = new ServerBootstrap();
            //sc = sb.group(group2).
            //                    channel(LocalServerChannel.class).
            //                    childHandler(new ChannelInitializer<Channel>() {
            //                        @Override
            //                        protected void initChannel(Channel ch) throws Exception
            //{
            //    ch.close();
            //    closeLatch.countDown();
            //}
            //                    }).
            //                    bind(TEST_ADDRESS).
            //                    sync().channel();
            //Bootstrap bootstrap = new Bootstrap();
            //bootstrap.group(clientGroup).
            //                    channel(LocalChannel.class).
            //                    handler(new ChannelInitializer<Channel>() {
            //                        @Override
            //                        protected void initChannel(Channel ch) throws Exception
            //{
            //    /* Do nothing */
            //}
            //                    });
            //            ChannelFuture future = bootstrap.connect(sc.localAddress());
            //assertTrue("Connection should finish, not time out", future.await(200));
            //            cc = future.channel();
            //        } finally {
            //            closeChannel(cc);
            //closeChannel(sc);
            //clientGroup.shutdownGracefully(0, 0, SECONDS).await();
            //        }
        }

        [Fact]
        public async Task TestReRegister()
        {
            Bootstrap cb = new Bootstrap();
            ServerBootstrap sb = new ServerBootstrap();

            cb.Group(_group1)
                    .Channel<LocalChannel>()
                    .Handler(new TestHandler());

            sb.Group(_group2)
                    .Channel<LocalServerChannel>()
                    .ChildHandler(new ActionChannelInitializer<LocalChannel>(ch =>
                    {
                        ch.Pipeline.AddLast(new TestHandler());
                    }));

            IChannel sc = null;
            IChannel cc = null;
            try
            {
                // Start server
                sc = await sb.BindAsync(TEST_ADDRESS);

                // Connect to the server
                cc = await cb.ConnectAsync(sc.LocalAddress);

                await cc.DeregisterAsync();
            }
            finally
            {
                await CloseChannel(cc);
                await CloseChannel(sc);
            }
        }

        [Fact]
        public async Task TestCloseInWritePromiseCompletePreservesOrder()
        {
            Bootstrap cb = new Bootstrap();
            ServerBootstrap sb = new ServerBootstrap();
            CountdownEvent messageLatch = new CountdownEvent(2);
            IByteBuffer data = Unpooled.WrappedBuffer(new byte[1024]);

            try
            {
                cb.Group(_group1)
                .Channel<LocalChannel>()
                .Handler(new TestHandler());

                sb.Group(_group2)
                .Channel<LocalServerChannel>()
                .ChildHandler(new ChannelInboundHandlerAdapter11(messageLatch, data));

                IChannel sc = null;
                IChannel cc = null;
                try
                {
                    // Start server
                    sc = await sb.BindAsync(TEST_ADDRESS);

                    // Connect to the server
                    cc = await cb.ConnectAsync(sc.LocalAddress);

                    IChannel ccCpy = cc;
                    // Make sure a write operation is executed in the eventloop
                    cc.Pipeline.LastContext().Executor.Execute(() =>
                    {
                        IPromise promise = ccCpy.NewPromise();
                        promise.Task.ContinueWith(future =>
                        {
                            ccCpy.Pipeline.LastContext().CloseAsync();
                        }, TaskContinuationOptions.ExecuteSynchronously);
                        ccCpy.WriteAndFlushAsync(data.RetainedDuplicate(), promise);
                    });

                    Assert.True(messageLatch.Wait(TimeSpan.FromSeconds(5)));
                    Assert.False(cc.IsOpen);
                }
                finally
                {
                    await CloseChannel(cc);
                    await CloseChannel(sc);
                }
            }
            finally
            {
                data.Release();
            }
        }

        class ChannelInboundHandlerAdapter11 : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _messageLatch;
            private readonly IByteBuffer _data;

            public ChannelInboundHandlerAdapter11(CountdownEvent messageLatch, IByteBuffer data)
            {
                _messageLatch = messageLatch;
                _data = data;
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                if (msg.Equals(_data))
                {
                    ReferenceCountUtil.SafeRelease(msg);
                    _messageLatch.Signal();
                }
                else
                {
                    base.ChannelRead(ctx, msg);
                }
            }

            public override void ChannelInactive(IChannelHandlerContext ctx)
            {
                _messageLatch.Signal();
                base.ChannelInactive(ctx);
            }
        }

        [Fact]
        public async Task TestCloseAfterWriteInSameEventLoopPreservesOrder()
        {
            Bootstrap cb = new Bootstrap();
            ServerBootstrap sb = new ServerBootstrap();
            CountdownEvent messageLatch = new CountdownEvent(3);
            IByteBuffer data = Unpooled.WrappedBuffer(new byte[1024]);

            try
            {
                cb.Group(_sharedGroup)
                        .Channel<LocalChannel>()
                        .Handler(new ChannelInboundHandlerAdapter9(messageLatch, data));

                sb.Group(_sharedGroup)
                        .Channel<LocalServerChannel>()
                        .ChildHandler(new ChannelInboundHandlerAdapter10(messageLatch, data));

                IChannel sc = null;
                IChannel cc = null;
                try
                {
                    // Start server
                    sc = await sb.BindAsync(TEST_ADDRESS);

                    // Connect to the server
                    cc = await cb.ConnectAsync(sc.LocalAddress);
                    Assert.True(messageLatch.Wait(TimeSpan.FromSeconds(5)));
                    Assert.False(cc.IsOpen);
                }
                finally
                {
                    await CloseChannel(cc);
                    await CloseChannel(sc);
                }
            }
            finally
            {
                data.Release();
            }
        }

        class ChannelInboundHandlerAdapter9 : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _messageLatch;
            private readonly IByteBuffer _data;

            public ChannelInboundHandlerAdapter9(CountdownEvent messageLatch, IByteBuffer data)
            {
                _messageLatch = messageLatch;
                _data = data;
            }

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                ctx.WriteAndFlushAsync(_data.RetainedDuplicate());
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                if (_data.Equals(msg))
                {
                    ReferenceCountUtil.SafeRelease(msg);
                    _messageLatch.Signal();
                }
                else
                {
                    base.ChannelRead(ctx, msg);
                }
            }
        }

        class ChannelInboundHandlerAdapter10 : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _messageLatch;
            private readonly IByteBuffer _data;

            public ChannelInboundHandlerAdapter10(CountdownEvent messageLatch, IByteBuffer data)
            {
                _messageLatch = messageLatch;
                _data = data;
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                if (_data.Equals(msg))
                {
                    _messageLatch.Signal();
                    ctx.WriteAndFlushAsync(_data);
                    ctx.CloseAsync();
                }
                else
                {
                    base.ChannelRead(ctx, msg);
                }
            }

            public override void ChannelInactive(IChannelHandlerContext ctx)
            {
                _messageLatch.Signal();
                base.ChannelInactive(ctx);
            }
        }

        [Fact]
        public async Task TestWriteInWritePromiseCompletePreservesOrder()
        {
            Bootstrap cb = new Bootstrap();
            ServerBootstrap sb = new ServerBootstrap();
            CountdownEvent messageLatch = new CountdownEvent(2);
            IByteBuffer data = Unpooled.WrappedBuffer(new byte[1024]);
            IByteBuffer data2 = Unpooled.WrappedBuffer(new byte[512]);

            try
            {
                cb.Group(_group1)
                .Channel<LocalChannel>()
                .Handler(new TestHandler());

                sb.Group(_group2)
                .Channel<LocalServerChannel>()
                .ChildHandler(new ChannelInboundHandlerAdapter8(messageLatch, data, data2));

                IChannel sc = null;
                IChannel cc = null;
                try
                {
                    // Start server
                    sc = await sb.BindAsync(TEST_ADDRESS);

                    // Connect to the server
                    cc = await cb.ConnectAsync(sc.LocalAddress);

                    IChannel ccCpy = cc;
                    // Make sure a write operation is executed in the eventloop
                    cc.Pipeline.LastContext().Executor.Execute(() =>
                    {
                        IPromise promise = ccCpy.NewPromise();
                        promise.Task.ContinueWith(future =>
                        {
                            ccCpy.WriteAndFlushAsync(data2.RetainedDuplicate(), ccCpy.NewPromise());
                        }, TaskContinuationOptions.ExecuteSynchronously);
                        ccCpy.WriteAndFlushAsync(data.RetainedDuplicate(), promise);
                    });

                    Assert.True(messageLatch.Wait(TimeSpan.FromSeconds(5)));
                }
                finally
                {
                    await CloseChannel(cc);
                    await CloseChannel(sc);
                }
            }
            finally
            {
                data.Release();
                data2.Release();
            }
        }

        class ChannelInboundHandlerAdapter8 : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _messageLatch;
            private readonly IByteBuffer _data;
            private readonly IByteBuffer _data2;

            public ChannelInboundHandlerAdapter8(CountdownEvent messageLatch, IByteBuffer data, IByteBuffer data2)
            {
                _messageLatch = messageLatch;
                _data = data;
                _data2 = data2;
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                long count = _messageLatch.CurrentCount;
                if ((_data.Equals(msg) && count == 2) || (_data2.Equals(msg) && count == 1))
                {
                    ReferenceCountUtil.SafeRelease(msg);
                    _messageLatch.Signal();
                }
                else
                {
                    base.ChannelRead(ctx, msg);
                }
            }
        }

        [Fact]
        public async Task TestPeerWriteInWritePromiseCompleteDifferentEventLoopPreservesOrder()
        {
            Bootstrap cb = new Bootstrap();
            ServerBootstrap sb = new ServerBootstrap();
            CountdownEvent messageLatch = new CountdownEvent(2);
            IByteBuffer data = Unpooled.WrappedBuffer(new byte[1024]);
            IByteBuffer data2 = Unpooled.WrappedBuffer(new byte[512]);
            CountdownEvent serverChannelLatch = new CountdownEvent(1);
            AtomicReference<IChannel> serverChannelRef = new AtomicReference<IChannel>();

            cb.Group(_group1)
                    .Channel<LocalChannel>()
                    .Handler(new ChannelInboundHandlerAdapter6(messageLatch, data2));

            sb.Group(_group2)
                    .Channel<LocalServerChannel>()
                    .ChildHandler(new ActionChannelInitializer<LocalChannel>(ch =>
                    {
                        ch.Pipeline.AddLast(new ChannelInboundHandlerAdapter7(messageLatch, data));
                        serverChannelRef.Value = ch;
                        serverChannelLatch.Signal();
                    }));

            IChannel sc = null;
            IChannel cc = null;
            try
            {
                // Start server
                sc = await sb.BindAsync(TEST_ADDRESS);

                // Connect to the server
                cc = await cb.ConnectAsync(sc.LocalAddress);
                Assert.True(serverChannelLatch.Wait(TimeSpan.FromSeconds(5)));

                IChannel ccCpy = cc;
                // Make sure a write operation is executed in the eventloop
                cc.Pipeline.LastContext().Executor.Execute(() =>
                {
                    IPromise promise = ccCpy.NewPromise();
                    promise.Task.ContinueWith(future =>
                    {
                        IChannel serverChannelCpy = serverChannelRef.Value;
                        serverChannelCpy.WriteAndFlushAsync(data2.RetainedDuplicate(), serverChannelCpy.NewPromise());
                    }, TaskContinuationOptions.ExecuteSynchronously);
                    ccCpy.WriteAndFlushAsync(data.RetainedDuplicate(), promise);
                });

                Assert.True(messageLatch.Wait(TimeSpan.FromSeconds(5)));
            }
            finally
            {
                await CloseChannel(cc);
                await CloseChannel(sc);
                data.Release();
                data2.Release();
            }
        }

        class ChannelInboundHandlerAdapter6 : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _messageLatch;
            private readonly IByteBuffer _data2;

            public ChannelInboundHandlerAdapter6(CountdownEvent messageLatch, IByteBuffer data2)
            {
                _messageLatch = messageLatch;
                _data2 = data2;
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                if (_data2.Equals(msg))
                {
                    ReferenceCountUtil.SafeRelease(msg);
                    _messageLatch.Signal();
                }
                else
                {
                    base.ChannelRead(ctx, msg);
                }
            }
        }

        class ChannelInboundHandlerAdapter7 : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _messageLatch;
            private readonly IByteBuffer _data;

            public ChannelInboundHandlerAdapter7(CountdownEvent messageLatch, IByteBuffer data)
            {
                _messageLatch = messageLatch;
                _data = data;
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                if (_data.Equals(msg))
                {
                    ReferenceCountUtil.SafeRelease(msg);
                    _messageLatch.Signal();
                }
                else
                {
                    base.ChannelRead(ctx, msg);
                }
            }
        }

        [Fact]
        public async Task TestPeerWriteInWritePromiseCompleteSameEventLoopPreservesOrder()
        {
            Bootstrap cb = new Bootstrap();
            ServerBootstrap sb = new ServerBootstrap();
            CountdownEvent messageLatch = new CountdownEvent(2);
            IByteBuffer data = Unpooled.WrappedBuffer(new byte[1024]);
            IByteBuffer data2 = Unpooled.WrappedBuffer(new byte[512]);
            CountdownEvent serverChannelLatch = new CountdownEvent(1);
            AtomicReference<IChannel> serverChannelRef = new AtomicReference<IChannel>();

            try
            {
                cb.Group(_sharedGroup)
                .Channel<LocalChannel>()
                .Handler(new ChannelInboundHandlerAdapter4(messageLatch, data2));

                sb.Group(_sharedGroup)
                .Channel<LocalServerChannel>()
                .ChildHandler(new ActionChannelInitializer<LocalChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new ChannelInboundHandlerAdapter5(messageLatch, data));
                    serverChannelRef.Value = ch;
                    serverChannelLatch.Signal();
                }));

                IChannel sc = null;
                IChannel cc = null;
                try
                {
                    // Start server
                    sc = await sb.BindAsync(TEST_ADDRESS);

                    // Connect to the server
                    cc = await cb.ConnectAsync(sc.LocalAddress);
                    Assert.True(serverChannelLatch.Wait(TimeSpan.FromSeconds(5)));

                    IChannel ccCpy = cc;
                    // Make sure a write operation is executed in the eventloop
                    cc.Pipeline.LastContext().Executor.Execute(() =>
                    {
                        IPromise promise = ccCpy.NewPromise();
                        promise.Task.ContinueWith(future =>
                        {
                            IChannel serverChannelCpy = serverChannelRef.Value;
                            serverChannelCpy.WriteAndFlushAsync(
                                    data2.RetainedDuplicate(), serverChannelCpy.NewPromise());
                        }, TaskContinuationOptions.ExecuteSynchronously);
                        ccCpy.WriteAndFlushAsync(data.RetainedDuplicate(), promise);
                    });

                    Assert.True(messageLatch.Wait(TimeSpan.FromSeconds(5)));
                }
                finally
                {
                    await CloseChannel(cc);
                    await CloseChannel(sc);
                }
            }
            finally
            {
                data.Release();
                data2.Release();
            }
        }

        class ChannelInboundHandlerAdapter4 : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _messageLatch;
            private readonly IByteBuffer _data2;

            public ChannelInboundHandlerAdapter4(CountdownEvent messageLatch, IByteBuffer data2)
            {
                _messageLatch = messageLatch;
                _data2 = data2;
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                if (_data2.Equals(msg) && _messageLatch.CurrentCount == 1)
                {
                    ReferenceCountUtil.SafeRelease(msg);
                    _messageLatch.Signal();
                }
                else
                {
                    base.ChannelRead(ctx, msg);
                }
            }
        }

        class ChannelInboundHandlerAdapter5 : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _messageLatch;
            private readonly IByteBuffer _data;

            public ChannelInboundHandlerAdapter5(CountdownEvent messageLatch, IByteBuffer data)
            {
                _messageLatch = messageLatch;
                _data = data;
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                if (_data.Equals(msg) && _messageLatch.CurrentCount == 2)
                {
                    ReferenceCountUtil.SafeRelease(msg);
                    _messageLatch.Signal();
                }
                else
                {
                    base.ChannelRead(ctx, msg);
                }
            }
        }

        [Fact]
        public async Task TestWriteWhilePeerIsClosedReleaseObjectAndFailPromise()
        {
            Bootstrap cb = new Bootstrap();
            ServerBootstrap sb = new ServerBootstrap();
            CountdownEvent serverMessageLatch = new CountdownEvent(1);
            CountdownEvent serverChannelCloseLatch = new CountdownEvent(1);
            CountdownEvent clientChannelCloseLatch = new CountdownEvent(1);
            CountdownEvent writeFailLatch = new CountdownEvent(1);
            IByteBuffer data = Unpooled.WrappedBuffer(new byte[1024]);
            IByteBuffer data2 = Unpooled.WrappedBuffer(new byte[512]);
            CountdownEvent serverChannelLatch = new CountdownEvent(1);
            AtomicReference<IChannel> serverChannelRef = new AtomicReference<IChannel>();

            try
            {
                cb.Group(_group1)
                .Channel<LocalChannel>()
                .Handler(new TestHandler());

                sb.Group(_group2)
                .Channel<LocalServerChannel>()
                .ChildHandler(new ActionChannelInitializer<LocalChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new ChannelInboundHandlerAdapter3(serverMessageLatch, data));
                    serverChannelRef.Value = ch;
                    serverChannelLatch.Signal();
                }));

                IChannel sc = null;
                IChannel cc = null;
                try
                {
                    // Start server
                    sc = await sb.BindAsync(TEST_ADDRESS);

                    // Connect to the server
                    cc = await cb.ConnectAsync(sc.LocalAddress);
                    Assert.True(serverChannelLatch.Wait(TimeSpan.FromSeconds(5)));

                    IChannel ccCpy = cc;
                    IChannel serverChannelCpy = serverChannelRef.Value;
                    serverChannelCpy.CloseCompletion.ContinueWith(t => serverChannelCloseLatch.Signal()).Ignore();
                    ccCpy.CloseCompletion.ContinueWith(t => clientChannelCloseLatch.Signal()).Ignore();

                    // Make sure a write operation is executed in the eventloop
                    cc.Pipeline.LastContext().Executor.Execute(() =>
                    {
                        ccCpy.WriteAndFlushAsync(data.RetainedDuplicate(), ccCpy.NewPromise())
                            .ContinueWith(t =>
                            {
                                serverChannelCpy.EventLoop.Execute(() =>
                                {
                                    // The point of this test is to write while the peer is closed, so we should
                                    // ensure the peer is actually closed before we write.
                                    int waitCount = 0;
                                    while (ccCpy.IsOpen)
                                    {
                                        try
                                        {
                                            Thread.Sleep(50);
                                        }
                                        catch (Exception) // InterruptedException ignored
                                        {
                                            // ignored
                                        }
                                        if (++waitCount > 5)
                                        {
                                            Assert.False(true);
                                        }
                                    }
                                    serverChannelCpy
                                        .WriteAndFlushAsync(data2.RetainedDuplicate(), serverChannelCpy.NewPromise())
                                        .ContinueWith(future =>
                                        {
                                            if (future.IsFailure() &&
                                                future.Exception.InnerException is ClosedChannelException)
                                            {
                                                writeFailLatch.Signal();
                                            }
                                        }, TaskContinuationOptions.ExecuteSynchronously);
                                });
                                ccCpy.CloseAsync().Ignore();
                            }, TaskContinuationOptions.ExecuteSynchronously);
                    });

                    Assert.True(serverMessageLatch.Wait(TimeSpan.FromSeconds(5)));
                    Assert.True(writeFailLatch.Wait(TimeSpan.FromSeconds(5)));
                    Assert.True(serverChannelCloseLatch.Wait(TimeSpan.FromSeconds(5)));
                    Assert.True(clientChannelCloseLatch.Wait(TimeSpan.FromSeconds(5)));
                    Assert.False(ccCpy.IsOpen);
                    Assert.False(serverChannelCpy.IsOpen);
                }
                finally
                {
                    await CloseChannel(cc);
                    await CloseChannel(sc);
                }
            }
            finally
            {
                data.Release();
                data2.Release();
            }
        }

        class ChannelInboundHandlerAdapter3 : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _serverMessageLatch;
            private readonly IByteBuffer _data;

            public ChannelInboundHandlerAdapter3(CountdownEvent serverMessageLatch, IByteBuffer data)
            {
                _serverMessageLatch = serverMessageLatch;
                _data = data;
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                if (_data.Equals(msg))
                {
                    ReferenceCountUtil.SafeRelease(msg);
                    _serverMessageLatch.Signal();
                }
                else
                {
                    base.ChannelRead(ctx, msg);
                }
            }
        }

        [Fact(Skip = "DevOps Pipelins: connect promise should be done")]
        public async Task TestConnectFutureBeforeChannelActive()
        {
            Bootstrap cb = new Bootstrap();
            ServerBootstrap sb = new ServerBootstrap();

            cb.Group(_group1)
                    .Channel<LocalChannel>()
                    .Handler(new ChannelHandlerAdapter());

            sb.Group(_group2)
                    .Channel<LocalServerChannel>()
                    .ChildHandler(new ActionChannelInitializer<LocalChannel>(ch =>
                    {
                        ch.Pipeline.AddLast(new TestHandler());
                    }));

            IChannel sc = null;
            IChannel cc = null;
            try
            {
                // Start server
                sc = await sb.BindAsync(TEST_ADDRESS);

                cc = await cb.RegisterAsync();

                IPromise promise = cc.NewPromise();
                var assertPromise = cc.EventLoop.NewPromise();

                cc.Pipeline.AddLast(new TestHandler0(promise, assertPromise));
                // Connect to the server
                await cc.ConnectAsync(sc.LocalAddress);
                promise.TryComplete();

                await assertPromise.Task;
                Assert.True(promise.IsSuccess);
            }
            finally
            {
                await CloseChannel(cc);
                await CloseChannel(sc);
            }
        }

        class TestHandler0 : TestHandler
        {
            private readonly IPromise _promise;
            private readonly IPromise _assertPromise;

            public TestHandler0(IPromise promise, IPromise assertPromise)
            {
                _promise = promise;
                _assertPromise = assertPromise;
            }

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                Thread.Sleep(10);
                // Ensure the promise was done before the handler method is triggered.
                if (_promise.IsCompleted)
                {
                    _assertPromise.Complete();
                }
                else
                {
                    _assertPromise.SetException(new Exception("connect promise should be done"));
                }
            }
        }

        [Fact]
        public void TestConnectionRefused()
        {
            Bootstrap sb = new Bootstrap();
            Assert.ThrowsAsync<ConnectException>(async () =>
                await sb.Group(_group1)
                        .Channel<LocalChannel>()
                        .Handler(new TestHandler())
                        .ConnectAsync(LocalAddress.Any));
        }

        class TestHandler : ChannelHandlerAdapter
        {
            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                Logger.Info(string.Format("Received message: {0}", msg));
                ReferenceCountUtil.SafeRelease(msg);
            }
        }

        [Fact]
        public async Task TestNotLeakBuffersWhenCloseByRemotePeer()
        {
            Bootstrap cb = new Bootstrap();
            ServerBootstrap sb = new ServerBootstrap();

            cb.Group(_sharedGroup)
                    .Channel<LocalChannel>()
                    .Handler(new SimpleChannelInboundHandler1());

            sb.Group(_sharedGroup)
                    .Channel<LocalServerChannel>()
                    .ChildHandler(new ActionChannelInitializer<LocalChannel>(ch =>
                    {
                        ch.Pipeline.AddLast(new SimpleChannelInboundHandler0());
                    }));

            IChannel sc = null;
            LocalChannel cc = null;
            try
            {
                // Start server
                sc = await sb.BindAsync(TEST_ADDRESS);

                // Connect to the server
                cc = (LocalChannel)await cb.ConnectAsync(sc.LocalAddress);

                // Close the channel
                await CloseChannel(cc);
                Assert.True(cc._inboundBuffer.IsEmpty);
                await CloseChannel(sc);
            }
            finally
            {
                await CloseChannel(cc);
                await CloseChannel(sc);
            }
        }

        class SimpleChannelInboundHandler1 : SimpleChannelInboundHandler<IByteBuffer>
        {
            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                ctx.WriteAndFlushAsync(ctx.Allocator.Buffer().WriteZero(100));
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, IByteBuffer msg)
            {
                // Just drop the buffer
            }
        }

        class SimpleChannelInboundHandler0 : SimpleChannelInboundHandler<IByteBuffer>
        {
            protected override void ChannelRead0(IChannelHandlerContext ctx, IByteBuffer buffer)
            {
                while (buffer.IsReadable())
                {
                    // Fill the ChannelOutboundBuffer with multiple buffers
                    ctx.WriteAsync(buffer.ReadRetainedSlice(1));
                }
                // Flush and so transfer the written buffers to the inboundBuffer of the remote peer.
                // After this point the remote peer is responsible to release all the buffers.
                ctx.Flush();
                // This close call will trigger the remote peer close as well.
                ctx.CloseAsync();
            }
        }

        [Fact]
        public Task TestAutoReadDisabledSharedGroup()
        {
            return TestAutoReadDisabled(_sharedGroup, _sharedGroup);
        }

        [Fact]
        public Task TestAutoReadDisabledDifferentGroup()
        {
            return TestAutoReadDisabled(_group1, _group2);
        }

        private static async Task TestAutoReadDisabled(IEventLoopGroup serverGroup, IEventLoopGroup clientGroup)
        {
            CountdownEvent latch = new CountdownEvent(100);
            Bootstrap cb = new Bootstrap();
            ServerBootstrap sb = new ServerBootstrap();

            cb.Group(clientGroup)
                    .Channel<LocalChannel>()
                    .Option(ChannelOption.AutoRead, false)
                    .Handler(new ChannelInboundHandlerAdapter2());

            sb.Group(serverGroup)
                    .Channel<LocalServerChannel>()
                    .ChildOption(ChannelOption.AutoRead, false)
                    .ChildHandler(new ChannelInboundHandlerAdapter1(latch));

            IChannel sc = null;
            IChannel cc = null;
            try
            {
                // Start server
                sc = await sb.BindAsync(TEST_ADDRESS);
                cc = await cb.ConnectAsync(TEST_ADDRESS);

                Assert.True(latch.Wait(TimeSpan.FromMinutes(1)));
            }
            finally
            {
                await CloseChannel(cc);
                await CloseChannel(sc);
            }
        }

        class ChannelInboundHandlerAdapter2 : ChannelHandlerAdapter
        {
            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                WriteAndFlushReadOnSuccess(ctx, "test");
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                WriteAndFlushReadOnSuccess(ctx, msg);
            }
        }

        class ChannelInboundHandlerAdapter1 : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _latch;

            public ChannelInboundHandlerAdapter1(CountdownEvent latch) => _latch = latch;

            public override void ChannelActive(IChannelHandlerContext context)
            {
                context.Read();
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                _latch.Signal();
                if (_latch.CurrentCount > 0)
                {
                    WriteAndFlushReadOnSuccess(ctx, msg);
                }
            }
        }

        private static void WriteAndFlushReadOnSuccess(IChannelHandlerContext ctx, object msg)
        {
            ctx.WriteAndFlushAsync(msg).ContinueWith(t =>
            {
                if (t.IsSuccess())
                {
                    ctx.Read();
                }
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        [Fact]
        public Task TestMaxMessagesPerReadRespectedWithAutoReadSharedGroup()
        {
            return TestMaxMessagesPerReadRespected(_sharedGroup, _sharedGroup, true);
        }

        [Fact]
        public Task TestMaxMessagesPerReadRespectedWithoutAutoReadSharedGroup()
        {
            return TestMaxMessagesPerReadRespected(_sharedGroup, _sharedGroup, false);
        }

        [Fact]
        public Task TestMaxMessagesPerReadRespectedWithAutoReadDifferentGroup()
        {
            return TestMaxMessagesPerReadRespected(_group1, _group2, true);
        }

        [Fact]
        public Task TestMaxMessagesPerReadRespectedWithoutAutoReadDifferentGroup()
        {
            return TestMaxMessagesPerReadRespected(_group1, _group2, false);
        }

        private static async Task TestMaxMessagesPerReadRespected(
            IEventLoopGroup serverGroup, IEventLoopGroup clientGroup, bool autoRead)
        {
            CountdownEvent countDownLatch = new CountdownEvent(5);
            Bootstrap cb = new Bootstrap();
            ServerBootstrap sb = new ServerBootstrap();

            cb.Group(serverGroup)
                .Channel<LocalChannel>()
                .Option(ChannelOption.AutoRead, autoRead)
                .Option(ChannelOption.MaxMessagesPerRead, 1)
                .Handler(new ChannelReadHandler(countDownLatch, autoRead));
            sb.Group(clientGroup)
                .Channel<LocalServerChannel>()
                .ChildHandler(new ChannelInboundHandlerAdapter0());

            IChannel sc = null;
            IChannel cc = null;
            try
            {
                // Start server
                sc = await sb.BindAsync(TEST_ADDRESS);
                cc = await cb.ConnectAsync(TEST_ADDRESS);

                Assert.True(countDownLatch.Wait(TimeSpan.FromMinutes(1)));
            }
            finally
            {
                await CloseChannel(cc);
                await CloseChannel(sc);
            }
        }

        sealed class ChannelInboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                for (int i = 0; i < 10; i++)
                {
                    ctx.WriteAsync(i);
                }
                ctx.Flush();
            }
        }

        [Fact]
        public Task TestServerMaxMessagesPerReadRespectedWithAutoReadSharedGroup()
        {
            return TestServerMaxMessagesPerReadRespected(_sharedGroup, _sharedGroup, true);
        }

        [Fact]
        public Task TestServerMaxMessagesPerReadRespectedWithoutAutoReadSharedGroup()
        {
            return TestServerMaxMessagesPerReadRespected(_sharedGroup, _sharedGroup, false);
        }

        [Fact]
        public Task TestServerMaxMessagesPerReadRespectedWithAutoReadDifferentGroup()
        {
            return TestServerMaxMessagesPerReadRespected(_group1, _group2, true);
        }

        [Fact]
        public Task TestServerMaxMessagesPerReadRespectedWithoutAutoReadDifferentGroup()
        {
            return TestServerMaxMessagesPerReadRespected(_group1, _group2, false);
        }

        private async Task TestServerMaxMessagesPerReadRespected(IEventLoopGroup serverGroup, IEventLoopGroup clientGroup, bool autoRead)
        {
            CountdownEvent countDownLatch = new CountdownEvent(5);
            Bootstrap cb = new Bootstrap();
            ServerBootstrap sb = new ServerBootstrap();

            cb.Group(clientGroup)
                .Channel<LocalChannel>()
                .Handler(new ActionChannelInitializer<LocalChannel>(ch => { /*NOOP*/ }));

            sb.Group(serverGroup)
                .Channel<LocalServerChannel>()
                .Option(ChannelOption.AutoRead, autoRead)
                .Option(ChannelOption.MaxMessagesPerRead, 1)
                .Handler(new ChannelReadHandler(countDownLatch, autoRead))
                .ChildHandler(new ActionChannelInitializer<LocalChannel>(ch => { /*NOOP*/ }));

            IChannel sc = null;
            IChannel cc = null;
            try
            {
                // Start server
                sc = await sb.BindAsync(TEST_ADDRESS);
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        cc = await cb.ConnectAsync(TEST_ADDRESS);
                    }
                    finally
                    {
                        await CloseChannel(cc);
                    }
                }

                Assert.True(countDownLatch.Wait(TimeSpan.FromMinutes(1)));
            }
            finally
            {
                await CloseChannel(sc);
            }
        }

        private static Task CloseChannel(IChannel cc)
        {
            if (cc != null)
            {
                return cc.CloseAsync();
            }
            return TaskUtil.Completed;
        }

        sealed class ChannelReadHandler : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _latch;
            private readonly bool _autoRead;
            private int _read;

            public ChannelReadHandler(CountdownEvent latch, bool autoRead)
            {
                _latch = latch;
                _autoRead = autoRead;
            }

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                if (!_autoRead)
                {
                    ctx.Read();
                }
                ctx.FireChannelActive();
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                Assert.Equal(0, _read);
                _read++;
                ctx.FireChannelRead(msg);
            }

            public override void ChannelReadComplete(IChannelHandlerContext ctx)
            {
                Assert.Equal(1, _read);
                _latch.Signal();
                if (_latch.CurrentCount > 0)
                {
                    if (!_autoRead)
                    {
                        // The read will be scheduled 100ms in the future to ensure we not receive any
                        // channelRead calls in the meantime.
                        ctx.Executor.Schedule(() =>
                        {
                            _read = 0;
                            ctx.Read();
                        }, TimeSpan.FromMilliseconds(100));
                    }
                    else
                    {
                        _read = 0;
                    }
                }
                else
                {
                    _read = 0;
                }
                ctx.FireChannelReadComplete();
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                ctx.FireExceptionCaught(cause);
                ctx.CloseAsync();
            }
        }
    }
}