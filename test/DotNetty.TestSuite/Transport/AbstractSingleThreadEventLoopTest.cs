namespace DotNetty.TestSuite.Transport
{
    using System;
    using System.Threading;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Local;
    using Xunit;

    public abstract class AbstractSingleThreadEventLoopTest<TServerChannel>
        where TServerChannel : IServerChannel, new()
    {
        [Fact(Skip = "TestChannelsRegistered")]
        public void TestChannelsRegistered()
        {
            //EventLoopGroup group = newEventLoopGroup();
            //final SingleThreadEventLoop loop = (SingleThreadEventLoop)group.next();

            //try
            //{
            //    final Channel ch1 = newChannel();
            //    final Channel ch2 = newChannel();

            //    int rc = registeredChannels(loop);
            //    boolean channelCountSupported = rc != -1;

            //    if (channelCountSupported)
            //    {
            //        assertEquals(0, registeredChannels(loop));
            //    }

            //    assertTrue(loop.register(ch1).syncUninterruptibly().isSuccess());
            //    assertTrue(loop.register(ch2).syncUninterruptibly().isSuccess());
            //    if (channelCountSupported)
            //    {
            //        assertEquals(2, registeredChannels(loop));
            //    }

            //    assertTrue(ch1.deregister().syncUninterruptibly().isSuccess());
            //    if (channelCountSupported)
            //    {
            //        assertEquals(1, registeredChannels(loop));
            //    }
            //}
            //finally
            //{
            //    group.shutdownGracefully();
            //}
        }

        // Only reliable if run from event loop
        private static int RegisteredChannels(SingleThreadEventLoopBase loop)
        {
            //var task = loop.SubmitAsync(() =>
            //{
            //    return loop.
            //});
            return -1;
        }

        [Fact]
        public void ShutdownBeforeStart()
        {
            IEventLoopGroup group = NewEventLoopGroup();
            Assert.False(group.TerminationCompletion.Wait(TimeSpan.FromMilliseconds(2)));
            group.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero);
            Assert.True(group.TerminationCompletion.Wait(TimeSpan.FromMilliseconds(200)));
        }

        [Fact]
        public void ShutdownGracefullyZeroQuietBeforeStart()
        {
            IEventLoopGroup group = NewEventLoopGroup();
            Assert.True(group.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.FromSeconds(2)).Wait(TimeSpan.FromMilliseconds(200)));

        }

        // Copied from AbstractEventLoopTest
        [Fact]
        public virtual void TestShutdownGracefullyNoQuietPeriod()
        {
            IEventLoopGroup loop = NewEventLoopGroup();
            ServerBootstrap b = new ServerBootstrap();
            b.Group(loop)
            .Channel<TServerChannel>()
            .ChildHandler(new ChannelHandlerAdapter());

            // Not close the Channel to ensure the EventLoop is still shutdown in time.
            var cf = typeof(TServerChannel) == typeof(LocalServerChannel)
                    ? b.BindAsync(new LocalAddress("local")) : b.BindAsync(0);
            cf.GetAwaiter().GetResult();

            var f = loop.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.FromMinutes(1));
            Assert.True(loop.TerminationCompletion.Wait(TimeSpan.FromMilliseconds(600)));
            Assert.True(f.IsSuccess());
            Assert.True(loop.IsShutdown);
            Assert.True(loop.IsTerminated);
        }

        [Fact]
        public void ShutdownGracefullyBeforeStart()
        {
            IEventLoopGroup group = NewEventLoopGroup();
            Assert.True(group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1)).Wait(TimeSpan.FromMilliseconds(500)));
        }

        [Fact]
        public void GracefulShutdownAfterStart()
        {
            IEventLoop loop = NewEventLoopGroup().GetNext();
            CountdownEvent latch = new CountdownEvent(1);
            loop.Execute(() => latch.Signal());

            // Wait for the event loop thread to start.
            latch.Wait();

            // Request the event loop thread to stop.
            loop.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(3));

            // Wait until the event loop is terminated.
            Assert.True(loop.TerminationCompletion.Wait(TimeSpan.FromMilliseconds(500)));

            AssertRejection(loop);
        }

        sealed class NoOpRunnable : IRunnable
        {
            public static readonly NoOpRunnable Instance = new NoOpRunnable();

            private NoOpRunnable() { }

            public void Run()
            {
            }
        }

        private static void AssertRejection(IEventExecutor loop)
        {
            try
            {
                loop.Execute(NoOpRunnable.Instance);
                Assert.False(true); // A task must be rejected after shutdown() is called.
            }
            catch (RejectedExecutionException)
            {
                // Expected
            }
        }

        protected abstract IEventLoopGroup NewEventLoopGroup();

        protected abstract IChannel NewChannel();
    }
}