namespace DotNetty.Transport.Tests.Bootstrapping
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Local;
    using Xunit;

    public class ServerBootstrapTest
    {
        [Fact]
        public async Task TestHandlerRegister()
        {
            CountdownEvent latch = new CountdownEvent(1);
            AtomicReference<Exception> error = new AtomicReference<Exception>();
            var group = new MultithreadEventLoopGroup(1);
            try
            {
                ServerBootstrap sb = new ServerBootstrap();
                sb.Channel<LocalServerChannel>()
                    .Group(group)
                    .ChildHandler(new ChannelHandlerAdapter())
                    .Handler(new TestRegisterHandler(latch, error));

                await sb.RegisterAsync();
                latch.Wait();
                Assert.Null(error.Value);
            }
            finally
            {
                await group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5));
            }
        }

        sealed class TestRegisterHandler : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _latch;
            private AtomicReference<Exception> _error;

            public TestRegisterHandler(CountdownEvent latch, AtomicReference<Exception> error)
            {
                _latch = latch;
                _error = error;
            }

            public override void HandlerAdded(IChannelHandlerContext context)
            {
                try
                {
                    Assert.True(context.Executor.InEventLoop);
                }
                catch (Exception cause)
                {
                    _error.Value = cause;
                }
                finally
                {
                    _latch.Signal();
                }
            }
        }

        [Fact]
        public async Task TestParentHandler()
        {
            await TestParentHandler0(false);
        }

        [Fact]
        public async Task TestParentHandlerViaChannelInitializer()
        {
            await TestParentHandler0(true);
        }

        private static async Task TestParentHandler0(bool channelInitializer)
        {
            LocalAddress addr = new LocalAddress(Guid.NewGuid().ToString());
            CountdownEvent readLatch = new CountdownEvent(1);
            CountdownEvent initLatch = new CountdownEvent(1);

            IChannelHandler handler = new TestHandler(readLatch, initLatch);

            IEventLoopGroup group = new MultithreadEventLoopGroup(1);
            IChannel sch = null;
            IChannel cch = null;

            try
            {
                ServerBootstrap sb = new ServerBootstrap();
                sb.Channel<LocalServerChannel>()
                        .Group(group)
                        .ChildHandler(new ChannelHandlerAdapter());
                if (channelInitializer)
                {
                    sb.Handler(new ActionChannelInitializer<IChannel>(ch => ch.Pipeline.AddLast(handler)));
                }
                else
                {
                    sb.Handler(handler);
                }

                Bootstrap cb = new Bootstrap();
                cb.Group(group)
                        .Channel<LocalChannel>()
                        .Handler(new ChannelHandlerAdapter());

                sch = await sb.BindAsync(addr);

                cch = await cb.ConnectAsync(addr);

                initLatch.Wait();
                readLatch.Wait();
            }
            finally
            {
                if (sch != null)
                {
                    await sch.CloseAsync();
                }
                if (cch != null)
                {
                    await cch.CloseAsync();
                }
                await group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5));
            }
        }

        sealed class TestHandler : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _readLatch;
            private readonly CountdownEvent _initLatch;

            public TestHandler(CountdownEvent readLatch, CountdownEvent initLatch)
            {
                _readLatch = readLatch;
                _initLatch = initLatch;
            }

            public override void HandlerAdded(IChannelHandlerContext context)
            {
                _initLatch.Signal();
                base.HandlerAdded(context);
            }

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                _readLatch.Signal();
                base.ChannelRead(context, message);
            }
        }
    }
}