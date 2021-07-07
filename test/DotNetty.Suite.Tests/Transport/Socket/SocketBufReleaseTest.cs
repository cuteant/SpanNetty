namespace DotNetty.Suite.Tests.Transport.Socket
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using Xunit;
    using Xunit.Abstractions;

    public class SocketBufReleaseTest : AbstractSocketTest
    {
        public SocketBufReleaseTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestBufRelease(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestBufRelease0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestBufRelease_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestBufRelease0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestBufRelease_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestBufRelease0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestBufRelease_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestBufRelease0(sb, cb);
        }

        private async Task TestBufRelease0(ServerBootstrap sb, Bootstrap cb)
        {
            try
            {
                BufWriterHandler serverHandler = new BufWriterHandler();
                BufWriterHandler clientHandler = new BufWriterHandler();

                sb.ChildHandler(serverHandler);
                cb.Handler(clientHandler);

                IChannel sc = await sb.BindAsync();
                IChannel cc = await cb.ConnectAsync(sc.LocalAddress);

                // Ensure the server socket accepted the client connection *and* initialized pipeline successfully.
                await serverHandler._channelFuture.Task;

                // and then close all sockets.
                await sc.CloseAsync();
                await cc.CloseAsync();

                serverHandler.Check();
                clientHandler.Check();

                serverHandler.Release();
                clientHandler.Release();
            }
            finally
            {
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        class BufWriterHandler : SimpleChannelInboundHandler<object>
        {
            private readonly Random _random = new Random();
            private readonly CountdownEvent _latch = new CountdownEvent(1);
            private IByteBuffer _buf;
            internal readonly TaskCompletionSource<IChannel> _channelFuture = new TaskCompletionSource<IChannel>();

            public override void HandlerAdded(IChannelHandlerContext ctx)
            {
                _channelFuture.TrySetResult(ctx.Channel);
            }

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                byte[] data = new byte[1024];
                _random.NextBytes(data);

                _buf = ctx.Allocator.Buffer();
                // call retain on it so it can't be put back on the pool
                _buf.WriteBytes(data).Retain();
                ctx.Channel.WriteAndFlushAsync(_buf).ContinueWith(t =>
                {
                    _latch.SafeSignal();
                });
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
            {
                // discard
            }

            public void Check()
            {
                _latch.Wait();
                Assert.Equal(1, _buf.ReferenceCount);
            }

            internal void Release()
            {
                _buf.Release();
            }
        }
    }
}
