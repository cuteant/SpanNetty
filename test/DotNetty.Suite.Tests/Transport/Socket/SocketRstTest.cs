namespace DotNetty.Suite.Tests.Transport.Socket
{
    using System;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using Xunit;
    using Xunit.Abstractions;

    public class SocketRstTest : AbstractSocketTest
    {
        static readonly Random s_random;
        static readonly byte[] s_data;

        static SocketRstTest()
        {
            s_random = new Random();
            s_data = new byte[1048576];
            s_random.NextBytes(s_data);
        }

        public SocketRstTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public async Task TestSoLingerZeroCausesOnlyRstOnClose(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            try
            {
                await TestSoLingerZeroCausesOnlyRstOnClose0(sb, cb);
            }
            finally
            {
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public async Task TestSoLingerZeroCausesOnlyRstOnClose_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            try
            {
                await TestSoLingerZeroCausesOnlyRstOnClose0(sb, cb);
            }
            finally
            {
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public async Task TestSoLingerZeroCausesOnlyRstOnClose_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            try
            {
                await TestSoLingerZeroCausesOnlyRstOnClose0(sb, cb);
            }
            finally
            {
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public async Task TestSoLingerZeroCausesOnlyRstOnClose_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            try
            {
                await TestSoLingerZeroCausesOnlyRstOnClose0(sb, cb);
            }
            finally
            {
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        private async Task TestSoLingerZeroCausesOnlyRstOnClose0(ServerBootstrap sb, Bootstrap cb)
        {
            AtomicReference<IChannel> serverChannelRef = new AtomicReference<IChannel>();
            AtomicReference<Exception> throwableRef = new AtomicReference<Exception>();
            CountdownEvent latch = new CountdownEvent(1);
            CountdownEvent latch2 = new CountdownEvent(1);
            // SO_LINGER=0 means that we must send ONLY a RST when closing (not a FIN + RST).
            sb.ChildOption(ChannelOption.SoLinger, 0);
            sb.ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
            {
                serverChannelRef.CompareAndSet(null, ch);
                latch.SafeSignal();
            }));
            cb.Handler(new ActionChannelInitializer<IChannel>(ch =>
            {
                ch.Pipeline.AddLast(new ChannelInboundHandlerAdapter0(throwableRef, latch2));
            }));
            IChannel sc = await sb.BindAsync();
            IChannel cc = await cb.ConnectAsync(sc.LocalAddress);

            // Wait for the server to get setup.
            latch.Wait();

            // The server has SO_LINGER=0 and so it must send a RST when close is called.
            serverChannelRef.Value.CloseAsync().Ignore();

            // Wait for the client to get channelInactive.
            latch2.Wait();

            // Verify the client received a RST.
            var cause = throwableRef.Value;
            if (cause is object)
            {
                Assert.True(cause is SocketException, "actual [type, message]: [" + cause.GetType() + ", " + cause.Message + "]");

                //AssertRstOnCloseException((SocketException)cause, cc);
                Assert.True(cause.Message.Contains("reset") || cause.Message.Contains("closed"), "actual message: " + cause.Message);
            }
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public async Task TestNoRstIfSoLingerOnClose(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            try
            {
                await TestNoRstIfSoLingerOnClose0(sb, cb);
            }
            finally
            {
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public async Task TestNoRstIfSoLingerOnClose_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            try
            {
                await TestNoRstIfSoLingerOnClose0(sb, cb);
            }
            finally
            {
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public async Task TestNoRstIfSoLingerOnClose_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            try
            {
                await TestNoRstIfSoLingerOnClose0(sb, cb);
            }
            finally
            {
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public async Task TestNoRstIfSoLingerOnClose_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            try
            {
                await TestNoRstIfSoLingerOnClose0(sb, cb);
            }
            finally
            {
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        private async Task TestNoRstIfSoLingerOnClose0(ServerBootstrap sb, Bootstrap cb)
        {
            AtomicReference<IChannel> serverChannelRef = new AtomicReference<IChannel>();
            AtomicReference<Exception> throwableRef = new AtomicReference<Exception>();
            CountdownEvent latch = new CountdownEvent(1);
            CountdownEvent latch2 = new CountdownEvent(1);
            sb.ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
            {
                serverChannelRef.CompareAndSet(null, ch);
                latch.SafeSignal();
            }));
            cb.Handler(new ActionChannelInitializer<IChannel>(ch =>
            {
                ch.Pipeline.AddLast(new ChannelInboundHandlerAdapter0(throwableRef, latch2));
            }));
            IChannel sc = await sb.BindAsync();
            await cb.ConnectAsync(sc.LocalAddress);

            // Wait for the server to get setup.
            latch.Wait();

            // The server has SO_LINGER=0 and so it must send a RST when close is called.
            serverChannelRef.Value.CloseAsync().Ignore();

            // Wait for the client to get channelInactive.
            latch2.Wait();

            // Verify the client did not received a RST.
            Assert.Null(throwableRef.Value);
        }

        sealed class ChannelInboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            private readonly AtomicReference<Exception> _throwableRef;
            private readonly CountdownEvent _latch2;

            public ChannelInboundHandlerAdapter0(AtomicReference<Exception> throwableRef, CountdownEvent latch2)
            {
                _throwableRef = throwableRef;
                _latch2 = latch2;
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                _throwableRef.CompareAndSet(null, cause);
            }

            public override void ChannelInactive(IChannelHandlerContext ctx)
            {
                _latch2.SafeSignal();
            }
        }
    }
}
