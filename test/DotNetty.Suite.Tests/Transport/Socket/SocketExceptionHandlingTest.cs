namespace DotNetty.Suite.Tests.Transport.Socket
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using Xunit;
    using Xunit.Abstractions;

    public class SocketExceptionHandlingTest : AbstractSocketTest
    {
        static readonly Random s_random;
        static readonly byte[] s_data;

        static SocketExceptionHandlingTest()
        {
            s_random = new Random();
            s_data = new byte[1048576];
            s_random.NextBytes(s_data);
        }

        public SocketExceptionHandlingTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestReadPendingIsResetAfterEachRead(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestReadPendingIsResetAfterEachRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestReadPendingIsResetAfterEachRead_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestReadPendingIsResetAfterEachRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestReadPendingIsResetAfterEachRead_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestReadPendingIsResetAfterEachRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestReadPendingIsResetAfterEachRead_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestReadPendingIsResetAfterEachRead0(sb, cb);
        }

        private async Task TestReadPendingIsResetAfterEachRead0(ServerBootstrap sb, Bootstrap cb)
        {
            IChannel serverChannel = null;
            IChannel clientChannel = null;
            try
            {
                MyInitializer serverInitializer = new MyInitializer();
                sb.Option(ChannelOption.SoBacklog, 1024);
                sb.ChildHandler(serverInitializer);

                serverChannel = await sb.BindAsync();

                cb.Handler(new MyInitializer());
                clientChannel = await cb.ConnectAsync(serverChannel.LocalAddress);

                await clientChannel.WriteAndFlushAsync(Unpooled.WrappedBuffer(new byte[1024]));

                // We expect to get 2 exceptions (1 from BuggyChannelHandler and 1 from ExceptionHandler).
                Assert.True(serverInitializer._exceptionHandler._latch1.Wait(TimeSpan.FromSeconds(5)));

                // After we get the first exception, we should get no more, this is expected to timeout.
                Assert.False(serverInitializer._exceptionHandler._latch2.Wait(TimeSpan.FromSeconds(1)),
                    "Encountered " + serverInitializer._exceptionHandler._count.Value + " exceptions when 1 was expected");
            }
            finally
            {
                if (serverChannel != null) { await serverChannel.CloseAsync(); }
                if (clientChannel != null) { await clientChannel.CloseAsync(); }
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        sealed class MyInitializer : ChannelInitializer<IChannel>
        {
            internal readonly ExceptionHandler _exceptionHandler = new ExceptionHandler();

            protected override void InitChannel(IChannel channel)
            {
                IChannelPipeline pipeline = channel.Pipeline;

                pipeline.AddLast(new BuggyChannelHandler());
                pipeline.AddLast(_exceptionHandler);
            }
        }

        sealed class BuggyChannelHandler : ChannelHandlerAdapter
        {
            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                ReferenceCountUtil.Release(message);
                throw new NullReferenceException("I am a bug!");
            }
        }

        sealed class ExceptionHandler : ChannelHandlerAdapter
        {
            internal readonly AtomicLong _count = new AtomicLong();
            /**
             * We expect to get 1 call to {@link #exceptionCaught(ChannelHandlerContext, Throwable)}.
             */
            internal readonly CountdownEvent _latch1 = new CountdownEvent(1);
            internal readonly CountdownEvent _latch2 = new CountdownEvent(1);

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                if (_count.Increment() <= 2)
                {
                    _latch1.SafeSignal();
                }
                else
                {
                    _latch2.SafeSignal();
                }
                // This should not throw any exception.
                context.CloseAsync();
            }
        }
    }
}
