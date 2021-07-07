namespace DotNetty.Suite.Tests.Transport.Socket
{
    using System;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv;
    using Xunit;
    using Xunit.Abstractions;

    public class SocketConnectTest : AbstractSocketTest
    {
        static readonly Random s_random;
        static readonly byte[] s_data;

        static SocketConnectTest()
        {
            s_random = new Random();
            s_data = new byte[1048576];
            s_random.NextBytes(s_data);
        }

        public SocketConnectTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestLocalAddressAfterConnect(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestLocalAddressAfterConnect0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestLocalAddressAfterConnect_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestLocalAddressAfterConnect0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestLocalAddressAfterConnect_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestLocalAddressAfterConnect0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestLocalAddressAfterConnect_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestLocalAddressAfterConnect0(sb, cb);
        }

        private async Task TestLocalAddressAfterConnect0(ServerBootstrap sb, Bootstrap cb)
        {
            IChannel serverChannel = null;
            IChannel clientChannel = null;
            try
            {
                TaskCompletionSource<EndPoint> localAddressPromise = new TaskCompletionSource<EndPoint>();
                serverChannel = await sb.ChildHandler(new ChannelInboundHandlerAdapter1(localAddressPromise)).BindAsync();

                clientChannel = await cb.Handler(new ChannelHandlerAdapter()).RegisterAsync();

                if (!(clientChannel is TcpChannel))
                {
                    Assert.Null(clientChannel.LocalAddress);
                    Assert.Null(clientChannel.RemoteAddress);
                }

                await clientChannel.ConnectAsync(serverChannel.LocalAddress);
                AssertLocalAddress((IPEndPoint)clientChannel.LocalAddress);
                Assert.NotNull(clientChannel.RemoteAddress);

                AssertLocalAddress((IPEndPoint)localAddressPromise.Task.GetAwaiter().GetResult());
            }
            finally
            {
                if (clientChannel != null) { await clientChannel.CloseAsync(); }
                if (serverChannel != null) { await serverChannel.CloseAsync(); }
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        sealed class ChannelInboundHandlerAdapter1 : ChannelHandlerAdapter
        {
            private readonly TaskCompletionSource<EndPoint> _localAddressPromise;

            public ChannelInboundHandlerAdapter1(TaskCompletionSource<EndPoint> localAddressPromise)
            {
                _localAddressPromise = localAddressPromise;
            }

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                _localAddressPromise.SetResult(ctx.Channel.LocalAddress);
            }
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestChannelEventsFiredWhenClosedDirectly(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestChannelEventsFiredWhenClosedDirectly0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestChannelEventsFiredWhenClosedDirectly_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestChannelEventsFiredWhenClosedDirectly0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestChannelEventsFiredWhenClosedDirectly_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestChannelEventsFiredWhenClosedDirectly0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestChannelEventsFiredWhenClosedDirectly_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestChannelEventsFiredWhenClosedDirectly0(sb, cb);
        }

        private async Task TestChannelEventsFiredWhenClosedDirectly0(ServerBootstrap sb, Bootstrap cb)
        {
            BlockingCollection<int> events = new BlockingCollection<int>();

            IChannel sc = null;
            IChannel cc = null;
            try
            {
                sb.ChildHandler(new ChannelHandlerAdapter());
                sc = await sb.BindAsync();

                cb.Handler(new ChannelInboundHandlerAdapter0(events));
                // Connect and directly close again.
                cc = await cb.ConnectAsync(sc.LocalAddress);
                await cc.CloseAsync();
                Assert.Equal(0, events.Take());
                Assert.Equal(1, events.Take());
            }
            finally
            {
                if (cc != null) { await cc.CloseAsync(); }
                if (sc != null) { await sc.CloseAsync(); }
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        sealed class ChannelInboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            private readonly BlockingCollection<int> _events;

            public ChannelInboundHandlerAdapter0(BlockingCollection<int> events)
            {
                _events = events;
            }

            public override void ChannelActive(IChannelHandlerContext context)
            {
                _events.Add(0);
            }

            public override void ChannelInactive(IChannelHandlerContext context)
            {
                _events.Add(1);
            }
        }

        private static void AssertLocalAddress(IPEndPoint endPoint)
        {
            Assert.True(endPoint.Port > 0);
            Assert.True(IPAddress.IsLoopback(endPoint.Address));
        }
    }
}
