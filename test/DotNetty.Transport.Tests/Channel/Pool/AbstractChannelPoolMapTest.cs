namespace DotNetty.Transport.Tests.Channel.Pool
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Local;
    using DotNetty.Transport.Channels.Pool;
    using Xunit;

    public class AbstractChannelPoolMapTest
    {
        [Fact]
        public async Task TestMap()
        {
            IEventLoopGroup group = new MultithreadEventLoopGroup();
            LocalAddress addr = new LocalAddress(ChannelPoolTestUtils.GetLocalAddrId());

            // Start server
            IChannel sc = await StartServerBootstrapAsync(group, addr);

            Bootstrap cb = new Bootstrap();
            cb.RemoteAddress(addr);
            cb.Group(group).Channel<LocalChannel>();
            var poolMap = new TestChannelPoolMap0(cb);

            IEventLoop loop = group.GetNext();

            Assert.True(poolMap.IsEmpty);
            Assert.Equal(0, poolMap.Count);

            SimpleChannelPool pool = poolMap.Get(loop);
            Assert.Equal(1, poolMap.Count);

            Assert.Same(pool, poolMap.Get(loop));
            Assert.True(poolMap.Remove(loop));
            Assert.False(poolMap.Remove(loop));

            Assert.Equal(0, poolMap.Count);

            await pool.AcquireAsync();
            poolMap.Close();

            await sc.CloseAsync();
        }

        class TestChannelPoolMap0 : AbstractChannelPoolMap<IEventExecutor, SimpleChannelPool>
        {
            private readonly Bootstrap _cb;

            public TestChannelPoolMap0(Bootstrap cb) => _cb = cb;

            protected override SimpleChannelPool NewPool(IEventExecutor key)
            {
                return new SimpleChannelPool(_cb.Clone((IEventLoopGroup)key), new TestChannelPoolHandler());
            }
        }

        [Fact]
        public async Task TestRemoveClosesChannelPool()
        {
            IEventLoopGroup group = new MultithreadEventLoopGroup();
            LocalAddress addr = new LocalAddress(ChannelPoolTestUtils.GetLocalAddrId());

            // Start server
            IChannel sc = await StartServerBootstrapAsync(group, addr);

            Bootstrap cb = new Bootstrap();
            cb.RemoteAddress(addr);
            cb.Group(group)
              .Channel<LocalChannel>();
            var poolMap = new TestChannelPoolMap1(cb);

            IEventLoop loop = group.GetNext();

            TestPool pool = poolMap.Get(loop);
            Assert.True(poolMap.Remove(loop));

            // the pool should be closed eventually after remove
            await pool._closeFuture.Task.WithTimeout(TimeSpan.FromSeconds(1));
            Assert.True(pool._closeFuture.IsCompleted);
            poolMap.Close();

            await sc.CloseAsync();
        }

        [Fact]
        public async Task TestCloseClosesPoolsImmediately()
        {
            IEventLoopGroup group = new MultithreadEventLoopGroup();
            LocalAddress addr = new LocalAddress(ChannelPoolTestUtils.GetLocalAddrId());

            // Start server
            IChannel sc = await StartServerBootstrapAsync(group, addr);

            Bootstrap cb = new Bootstrap();
            cb.RemoteAddress(addr);
            cb.Group(group)
              .Channel<LocalChannel>();
            var poolMap = new TestChannelPoolMap1(cb);

            IEventLoop loop = group.GetNext();

            TestPool pool = poolMap.Get(loop);
            Assert.False(pool._closeFuture.IsCompleted);

            // the pool should be closed immediately after remove
            poolMap.Close();
            Assert.True(pool._closeFuture.IsCompleted);

            await sc.CloseAsync();
        }

        class TestChannelPoolMap1 : AbstractChannelPoolMap<IEventExecutor, TestPool>
        {
            private readonly Bootstrap _cb;

            public TestChannelPoolMap1(Bootstrap cb) => _cb = cb;

            protected override TestPool NewPool(IEventExecutor key)
            {
                return new TestPool(_cb.Clone((IEventLoopGroup)key), new TestChannelPoolHandler());
            }
        }

        static Task<IChannel> StartServerBootstrapAsync(IEventLoopGroup group, LocalAddress addr)
        {
            ServerBootstrap sb = new ServerBootstrap()
                .Group(group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            return sb.BindAsync(addr);
        }

        sealed class TestChannelPoolHandler : AbstractChannelPoolHandler
        {
            public override void ChannelCreated(IChannel channel)
            {
                // NOOP
            }
        }

        sealed class TestPool : SimpleChannelPool
        {
            internal readonly IPromise _closeFuture;

            public TestPool(Bootstrap bootstrap, IChannelPoolHandler handler)
                : base(bootstrap, handler)
            {
                IEventExecutor executor = bootstrap.Group().GetNext();
                _closeFuture = executor.NewPromise();
            }

            public override Task CloseAsync()
            {
                var poolClose = base.CloseAsync();
                poolClose.LinkOutcome(_closeFuture);
                return poolClose;
            }
        }
    }
}
