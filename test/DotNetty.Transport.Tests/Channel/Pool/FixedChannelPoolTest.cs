// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Channel.Pool
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Local;
    using DotNetty.Transport.Channels.Pool;
    using Xunit;

    [Collection("ChannelPoolTests")]
    public class FixedChannelPoolTest : IDisposable
    {
        readonly IEventLoopGroup _group;

        static FixedChannelPoolTest()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exc = e.ExceptionObject;
            var err = exc.ToString();
        }

        public FixedChannelPoolTest()
        {
            _group = new DefaultEventLoopGroup();
        }

        public void Dispose()
        {
            _group?.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }

        [Fact]
        public async Task TestAcquire()
        {
            var addr = new LocalAddress(ChannelPoolTestUtils.GetLocalAddrId());
            Bootstrap cb = new Bootstrap().RemoteAddress(addr).Group(_group).Channel<LocalChannel>();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(_group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);
            var handler = new CountingChannelPoolHandler();

            var pool = new FixedChannelPool(cb, handler, 1);

            IChannel channel = await pool.AcquireAsync();
            var future = pool.AcquireAsync();
            Assert.False(future.IsCompleted);

            await pool.ReleaseAsync(channel);
            await Task.Delay(TimeSpan.FromSeconds(1));
            Assert.True(future.IsCompleted);

            IChannel channel2 = future.Result;
            Assert.Same(channel, channel2);
            Assert.Equal(1, handler.ChannelCount);
            Assert.Equal(1, handler.AcquiredCount);
            Assert.Equal(1, handler.ReleasedCount);

            await sc.CloseAsync();
            await channel2.CloseAsync();
            pool.Close();
        }

        [Fact]
        public Task TestAcquireTimeout()
        {
            return TestAcquireTimeout0(500);
        }

        [Fact]
        public Task TestAcquireWithZeroTimeout()
        {
            return TestAcquireTimeout0(0);
        }

        private async Task TestAcquireTimeout0(long timeoutMillis)
        {
            var addr = new LocalAddress(ChannelPoolTestUtils.GetLocalAddrId());
            Bootstrap cb = new Bootstrap().RemoteAddress(addr).Group(_group).Channel<LocalChannel>();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(_group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);
            var handler = new TestChannelPoolHandler();
            var pool = new FixedChannelPool(cb, handler, ChannelActiveHealthChecker.Instance, FixedChannelPool.AcquireTimeoutAction.Fail, TimeSpan.FromMilliseconds(timeoutMillis), 1, int.MaxValue);

            IChannel channel = await pool.AcquireAsync();
            try
            {
                await Assert.ThrowsAsync<TimeoutException>(async () => await pool.AcquireAsync());
            }
            finally
            {
                await sc.CloseAsync();
                await channel.CloseAsync();
                pool.Close();
            }
        }

        [Fact]
        public async Task TestAcquireNewConnection()
        {
            var addr = new LocalAddress(ChannelPoolTestUtils.GetLocalAddrId());
            Bootstrap cb = new Bootstrap().RemoteAddress(addr).Group(_group).Channel<LocalChannel>();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(_group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);
            var handler = new TestChannelPoolHandler();
            var pool = new FixedChannelPool(
                cb,
                handler,
                ChannelActiveHealthChecker.Instance,
                FixedChannelPool.AcquireTimeoutAction.New,
                TimeSpan.FromMilliseconds(500),
                1,
                int.MaxValue);

            IChannel channel = await pool.AcquireAsync();
            IChannel channel2 = await pool.AcquireAsync();
            Assert.NotSame(channel, channel2);
            await sc.CloseAsync();
            await channel.CloseAsync();
            await channel2.CloseAsync();
            pool.Close();
        }

        /**
         * Tests that the acquiredChannelCount is not added up several times for the same channel acquire request.
         * @throws Exception
         */
        [Fact]
        public async Task TestAcquireNewConnectionWhen()
        {
            var addr = new LocalAddress(ChannelPoolTestUtils.GetLocalAddrId());
            Bootstrap cb = new Bootstrap().RemoteAddress(addr).Group(_group).Channel<LocalChannel>();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(_group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);
            var handler = new TestChannelPoolHandler();
            var pool = new FixedChannelPool(cb, handler, 1);
            IChannel channel1 = await pool.AcquireAsync();
            await channel1.CloseAsync();
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
            pool.ReleaseAsync(channel1);
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法

            IChannel channel2 = await pool.AcquireAsync();

            Assert.NotSame(channel1, channel2);
            await sc.CloseAsync();
            await channel2.CloseAsync();
            pool.Close();
        }

        [Fact]
        public async Task TestAcquireBoundQueue()
        {
            var addr = new LocalAddress(ChannelPoolTestUtils.GetLocalAddrId());
            Bootstrap cb = new Bootstrap().RemoteAddress(addr).Group(_group).Channel<LocalChannel>();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(_group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);
            var handler = new TestChannelPoolHandler();
            var pool = new FixedChannelPool(cb, handler, 1, 1);

            IChannel channel = await pool.AcquireAsync();
            var future = pool.AcquireAsync();
            Assert.False(future.IsCompleted);

            try
            {
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await pool.AcquireAsync());
            }
            finally
            {
                await sc.CloseAsync();
                await channel.CloseAsync();
                pool.Close();
            }
        }

        [Fact]
        public async Task TestReleaseDifferentPool()
        {
            var addr = new LocalAddress(ChannelPoolTestUtils.GetLocalAddrId());
            Bootstrap cb = new Bootstrap().RemoteAddress(addr).Group(_group).Channel<LocalChannel>();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(_group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);
            var handler = new TestChannelPoolHandler();
            var pool = new FixedChannelPool(cb, handler, 1, 1);
            var pool2 = new FixedChannelPool(cb, handler, 1, 1);

            IChannel channel = await pool.AcquireAsync();

            try
            {
                await Assert.ThrowsAsync<ArgumentException>(async () => await pool2.ReleaseAsync(channel));
            }
            finally
            {
                await sc.CloseAsync();
                await channel.CloseAsync();
                pool.Close();
                pool2.Close();
            }
        }

        [Fact]
        public async Task TestReleaseAfterClosePool()
        {
            var addr = new LocalAddress(ChannelPoolTestUtils.GetLocalAddrId());
            Bootstrap cb = new Bootstrap().RemoteAddress(addr).Group(_group).Channel<LocalChannel>();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(_group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);

            var pool = new FixedChannelPool(cb, new TestChannelPoolHandler(), 2);
            IChannel channel = await pool.AcquireAsync();
            pool.Close();

            await _group.GetNext().SubmitAsync(() => TaskUtil.Completed);
            var e = await Assert.ThrowsAsync<InvalidOperationException>(async () => await pool.ReleaseAsync(channel));
            Assert.Same(FixedChannelPool.PoolClosedOnReleaseException, e);

            // Since the pool is closed, the Channel should have been closed as well.
            await channel.CloseCompletion;
            Assert.False(channel.IsOpen, "Unexpected open channel");
            await sc.CloseAsync();
            pool.Close();
        }

        [Fact]
        public async Task TestReleaseClosed()
        {
            var addr = new LocalAddress(ChannelPoolTestUtils.GetLocalAddrId());
            Bootstrap cb = new Bootstrap().RemoteAddress(addr).Group(_group).Channel<LocalChannel>();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(_group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);

            var pool = new FixedChannelPool(cb, new TestChannelPoolHandler(), 2);
            IChannel channel = await pool.AcquireAsync();
            await channel.CloseAsync();
            await pool.ReleaseAsync(channel);

            await sc.CloseAsync();
            pool.Close();
        }

        sealed class TestChannelPoolHandler : IChannelPoolHandler
        {
            public void ChannelReleased(IChannel channel)
            {
                // NOOP
            }

            public void ChannelAcquired(IChannel channel)
            {
                // NOOP
            }

            public void ChannelCreated(IChannel channel)
            {
                // NOOP
            }
        }

        [Fact]
        public async Task TestCloseAsync()
        {
            var addr = new LocalAddress(ChannelPoolTestUtils.GetLocalAddrId());
            Bootstrap cb = new Bootstrap().RemoteAddress(addr).Group(_group).Channel<LocalChannel>();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(_group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);

            var pool = new FixedChannelPool(cb, new TestChannelPoolHandler(), 2);
            await pool.AcquireAsync();
            await pool.AcquireAsync();

            var closePromise = sc.NewPromise();
            await pool.CloseAsync();

            Assert.Equal(0, pool.AcquiredChannelCount);
            await sc.CloseAsync(closePromise);

            Assert.True(closePromise.IsSuccess);
        }
    }
}
