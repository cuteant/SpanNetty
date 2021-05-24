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

    public class SocketDataReadInitialStateTest : AbstractSocketTest
    {
        public SocketDataReadInitialStateTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestAutoReadOffNoDataReadUntilReadCalled(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestAutoReadOffNoDataReadUntilReadCalled0(sb, cb, false);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestAutoReadOffNoDataReadUntilReadCalled_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestAutoReadOffNoDataReadUntilReadCalled0(sb, cb, false);
        }

        [Theory(Skip = "TODO 暂时忽略，单独运行可通过测试")]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestAutoReadOffNoDataReadUntilReadCalled_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestAutoReadOffNoDataReadUntilReadCalled0(sb, cb, true);
        }

        [Theory(Skip = "TODO 暂时忽略，单独运行可通过测试")]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestAutoReadOffNoDataReadUntilReadCalled_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestAutoReadOffNoDataReadUntilReadCalled0(sb, cb, true);
        }

        private async Task TestAutoReadOffNoDataReadUntilReadCalled0(ServerBootstrap sb, Bootstrap cb, bool isLibuvServer)
        {
            IChannel serverChannel = null;
            IChannel clientChannel = null;
            const int sleepMs = 100;
            try
            {
                sb.Option(ChannelOption.AutoRead, isLibuvServer); // LibuvServer 不支持 No-AutoRead
                sb.ChildOption(ChannelOption.AutoRead, false);
                cb.Option(ChannelOption.AutoRead, false);
                CountdownEvent serverReadyLatch = new CountdownEvent(1);
                CountdownEvent acceptorReadLatch = new CountdownEvent(1);
                CountdownEvent serverReadLatch = new CountdownEvent(1);
                CountdownEvent clientReadLatch = new CountdownEvent(1);
                AtomicReference<IChannel> serverConnectedChannelRef = new AtomicReference<IChannel>();

                sb.Handler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new ChannelInboundHandlerAdapter0(acceptorReadLatch));
                }));

                sb.ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    serverConnectedChannelRef.Value = ch;
                    ch.Pipeline.AddLast(new SimpleChannelInboundHandler0(serverReadLatch));
                    serverReadyLatch.SafeSignal();
                }));

                cb.Handler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new SimpleChannelInboundHandler1(clientReadLatch));
                }));

                serverChannel = await sb.BindAsync();
                clientChannel = await cb.ConnectAsync(serverChannel.LocalAddress);
                await clientChannel.WriteAndFlushAsync(clientChannel.Allocator.Buffer().WriteZero(1));

                // The acceptor shouldn't read any data until we call read() below, but give it some time to see if it will.
                if (!isLibuvServer)
                {
                    Thread.Sleep(sleepMs);
                    Assert.Equal(1, acceptorReadLatch.CurrentCount);
                    serverChannel.Read();
                    serverReadyLatch.Wait();
                }

                IChannel serverConnectedChannel = serverConnectedChannelRef.Value;
                Assert.NotNull(serverConnectedChannel);

                // Allow some amount of time for the server peer to receive the message (which isn't expected to happen
                // until we call read() below).
                Thread.Sleep(sleepMs);
                Assert.Equal(1, serverReadLatch.CurrentCount);
                serverConnectedChannel.Read();
                serverReadLatch.Wait();

                // Allow some amount of time for the client to read the echo.
                Thread.Sleep(sleepMs);
                Assert.Equal(1, clientReadLatch.CurrentCount);
                clientChannel.Read();
                clientReadLatch.Wait();
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

        sealed class ChannelInboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _acceptorReadLatch;

            public ChannelInboundHandlerAdapter0(CountdownEvent acceptorReadLatch)
            {
                _acceptorReadLatch = acceptorReadLatch;
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                _acceptorReadLatch.SafeSignal();
                ctx.FireChannelRead(msg);
            }
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestAutoReadOnDataReadImmediately(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestAutoReadOnDataReadImmediately0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestAutoReadOnDataReadImmediately_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestAutoReadOnDataReadImmediately0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestAutoReadOnDataReadImmediately_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestAutoReadOnDataReadImmediately0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestAutoReadOnDataReadImmediately_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestAutoReadOnDataReadImmediately0(sb, cb);
        }

        private async Task TestAutoReadOnDataReadImmediately0(ServerBootstrap sb, Bootstrap cb)
        {
            IChannel serverChannel = null;
            IChannel clientChannel = null;
            try
            {
                sb.Option(ChannelOption.AutoRead, true);
                sb.ChildOption(ChannelOption.AutoRead, true);
                cb.Option(ChannelOption.AutoRead, true);
                CountdownEvent serverReadLatch = new CountdownEvent(1);
                CountdownEvent clientReadLatch = new CountdownEvent(1);

                sb.ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new SimpleChannelInboundHandler0(serverReadLatch));
                }));

                cb.Handler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new SimpleChannelInboundHandler1(clientReadLatch));
                }));

                serverChannel = await sb.BindAsync();
                clientChannel = await cb.ConnectAsync(serverChannel.LocalAddress);
                clientChannel.WriteAndFlushAsync(clientChannel.Allocator.Buffer().WriteZero(1)).Ignore();
                serverReadLatch.Wait();
                clientReadLatch.Wait();
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

        sealed class SimpleChannelInboundHandler0 : SimpleChannelInboundHandler<IByteBuffer>
        {
            private readonly CountdownEvent _serverReadLatch;

            public SimpleChannelInboundHandler0(CountdownEvent serverReadLatch)
            {
                _serverReadLatch = serverReadLatch;
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, IByteBuffer msg)
            {
                ctx.WriteAndFlushAsync(msg.RetainedDuplicate());
                _serverReadLatch.SafeSignal();
            }
        }

        sealed class SimpleChannelInboundHandler1 : SimpleChannelInboundHandler<object>
        {
            private readonly CountdownEvent _clientReadLatch;

            public SimpleChannelInboundHandler1(CountdownEvent clientReadLatch)
            {
                _clientReadLatch = clientReadLatch;
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
            {
                _clientReadLatch.SafeSignal();
            }
        }
    }
}
