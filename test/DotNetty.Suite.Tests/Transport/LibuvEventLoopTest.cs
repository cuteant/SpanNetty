namespace DotNetty.Suite.Tests.Transport
{
    using System;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv;
    using Xunit;

    public class LibuvEventLoopTest : AbstractSingleThreadEventLoopTest<TcpServerChannel>
    {
        protected override IEventLoopGroup NewEventLoopGroup()
        {
            return new EventLoopGroup();
        }

        protected override IChannel NewChannel()
        {
            return new TcpChannel();
        }

        [Fact]
        public override void TestShutdownGracefullyNoQuietPeriod()
        {
            IEventLoopGroup loop = new DispatcherEventLoopGroup();
            ServerBootstrap b = new ServerBootstrap();
            b.Group(loop)
            .Channel<TcpServerChannel>()
            .ChildHandler(new ChannelHandlerAdapter());

            // Not close the Channel to ensure the EventLoop is still shutdown in time.
            var cf =  b.BindAsync(0);
            cf.GetAwaiter().GetResult();

            var f = loop.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.FromMinutes(1));
            Assert.True(loop.TerminationCompletion.Wait(TimeSpan.FromMilliseconds(600)));
            Assert.True(f.IsSuccess());
            Assert.True(loop.IsShutdown);
            Assert.True(loop.IsTerminated);
        }
    }
}