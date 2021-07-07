namespace DotNetty.Suite.Tests.Transport.Socket
{
    using System;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;
    using Xunit;
    using Xunit.Abstractions;

    public class SocketGatheringWriteTest : AbstractSocketTest, IDisposable
    {
        private static readonly Random s_random;
        static readonly byte[] s_data;

        static SocketGatheringWriteTest()
        {
            s_random = new Random();
            s_data = new byte[1048576];
            s_random.NextBytes(s_data);
        }

        public SocketGatheringWriteTest(ITestOutputHelper output)
            : base(output)
        {
        }

        public void Dispose()
        {
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestGatheringWrite(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestGatheringWrite0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestGatheringWrite_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestGatheringWrite0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestGatheringWrite_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestGatheringWrite0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestGatheringWrite_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestGatheringWrite0(sb, cb);
        }

        private async Task TestGatheringWrite0(ServerBootstrap sb, Bootstrap cb)
        {
            try
            {
                await TestGatheringWrite0(sb, cb, s_data, false, true);
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
        public Task TestGatheringWriteNotAutoRead(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestGatheringWriteNotAutoRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestGatheringWriteNotAutoRead_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestGatheringWriteNotAutoRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestGatheringWriteNotAutoRead_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestGatheringWriteNotAutoRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestGatheringWriteNotAutoRead_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestGatheringWriteNotAutoRead0(sb, cb);
        }

        private async Task TestGatheringWriteNotAutoRead0(ServerBootstrap sb, Bootstrap cb)
        {
            try
            {
                await TestGatheringWrite0(sb, cb, s_data, false, false);
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
        public Task TestGatheringWriteWithComposite(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestGatheringWriteWithComposite0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestGatheringWriteWithComposite_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestGatheringWriteWithComposite0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestGatheringWriteWithComposite_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestGatheringWriteWithComposite0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestGatheringWriteWithComposite_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestGatheringWriteWithComposite0(sb, cb);
        }

        private async Task TestGatheringWriteWithComposite0(ServerBootstrap sb, Bootstrap cb)
        {
            try
            {
                await TestGatheringWrite0(sb, cb, s_data, true, true);
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
        public Task TestGatheringWriteWithCompositeNotAutoRead(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestGatheringWriteWithCompositeNotAutoRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestGatheringWriteWithCompositeNotAutoRead_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestGatheringWriteWithCompositeNotAutoRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestGatheringWriteWithCompositeNotAutoRead_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestGatheringWriteWithCompositeNotAutoRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestGatheringWriteWithCompositeNotAutoRead_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestGatheringWriteWithCompositeNotAutoRead0(sb, cb);
        }

        private async Task TestGatheringWriteWithCompositeNotAutoRead0(ServerBootstrap sb, Bootstrap cb)
        {
            try
            {
                await TestGatheringWrite0(sb, cb, s_data, true, false);
            }
            finally
            {
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        // Test for https://github.com/netty/netty/issues/2647
        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestGatheringWriteBig(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestGatheringWriteBig0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestGatheringWriteBig_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestGatheringWriteBig0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestGatheringWriteBig_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestGatheringWriteBig0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestGatheringWriteBig_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestGatheringWriteBig0(sb, cb);
        }

        private async Task TestGatheringWriteBig0(ServerBootstrap sb, Bootstrap cb)
        {
            byte[] bigData = new byte[1024 * 1024 * 50];
            s_random.NextBytes(bigData);
            try
            {
                await TestGatheringWrite0(sb, cb, bigData, false, true);
            }
            finally
            {
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        private async Task TestGatheringWrite0(
            ServerBootstrap sb, Bootstrap cb, byte[] data, bool composite, bool autoRead)
        {
            sb.ChildOption(ChannelOption.AutoRead, autoRead);
            cb.Option(ChannelOption.AutoRead, autoRead);

            IPromise serverDonePromise = new DefaultPromise();
            TestServerHandler sh = new TestServerHandler(autoRead, serverDonePromise, data.Length);
            TestHandler ch = new TestHandler(autoRead);

            cb.Handler(ch);
            sb.ChildHandler(sh);

            IChannel sc = await sb.BindAsync();
            IChannel cc = await cb.ConnectAsync(sc.LocalAddress);

            for (int i = 0; i < data.Length;)
            {
                int length = Math.Min(s_random.Next(1024 * 8), data.Length - i);
                if (composite && i % 2 == 0)
                {
                    int firstBufLength = length / 2;
                    CompositeByteBuffer comp = Unpooled.CompositeBuffer();
                    comp.AddComponent(true, Unpooled.WrappedBuffer(data, i, firstBufLength))
                        .AddComponent(true, Unpooled.WrappedBuffer(data, i + firstBufLength, length - firstBufLength));
                    cc.WriteAsync(comp).Ignore();
                }
                else
                {
                    cc.WriteAsync(Unpooled.WrappedBuffer(data, i, length)).Ignore();
                }
                i += length;
            }

            var cf = cc.WriteAndFlushAsync(Unpooled.Empty);
            Assert.NotEqual(cc.VoidPromise().Task, cf);
            try
            {
                Assert.True(cf.Wait(60000));
                await cf;
            }
            catch (Exception)
            {
                throw;
            }

            await serverDonePromise.Task;
            await sh._channel.CloseAsync();
            await ch._channel.CloseAsync();
            await sc.CloseAsync();

            if (sh._exception.Value != null && !(sh._exception.Value is SocketException || (sh._exception.Value is ChannelException chexc && chexc.InnerException is OperationException) || sh._exception.Value is OperationException))
            {
                throw sh._exception.Value;
            }
            if (sh._exception.Value != null)
            {
                throw sh._exception.Value;
            }
            if (ch._exception.Value != null && !(ch._exception.Value is SocketException || (sh._exception.Value is ChannelException chexc1 && chexc1.InnerException is OperationException) || sh._exception.Value is OperationException))
            {
                throw ch._exception.Value;
            }
            if (ch._exception.Value != null)
            {
                throw ch._exception.Value;
            }
            IByteBuffer expected = Unpooled.WrappedBuffer(data);
            Assert.Equal(expected, sh._received);
            expected.Release();
            sh._received.Release();
        }

        sealed class TestServerHandler : TestHandler
        {
            private readonly int _expectedBytes;
            private readonly IPromise _doneReadingPromise;
            internal readonly IByteBuffer _received = Unpooled.Buffer();

            public TestServerHandler(bool autoRead, IPromise doneReadingPromise, int expectedBytes)
                : base(autoRead)
            {
                _doneReadingPromise = doneReadingPromise;
                _expectedBytes = expectedBytes;
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, IByteBuffer input)
            {
                _received.WriteBytes(input);
                if (_received.ReadableBytes >= _expectedBytes)
                {
                    _doneReadingPromise.Complete();
                }
            }

            internal override void HandleException(IChannelHandlerContext ctx, Exception cause)
            {
                _doneReadingPromise.TrySetException(cause);
                base.HandleException(ctx, cause);
            }

            public override void ChannelInactive(IChannelHandlerContext ctx)
            {
                _doneReadingPromise.TrySetException(new InvalidOperationException("server closed!"));
                base.ChannelInactive(ctx);
            }
        }

        class TestHandler : SimpleChannelInboundHandler<IByteBuffer>
        {
            private readonly bool _autoRead;
            internal volatile IChannel _channel;
            internal AtomicReference<Exception> _exception = new AtomicReference<Exception>();

            public TestHandler(bool autoRead)
            {
                _autoRead = autoRead;
            }

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                _channel = ctx.Channel;
                if (!_autoRead)
                {
                    ctx.Read();
                }
                base.ChannelActive(ctx);
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, IByteBuffer input)
            {
            }

            public override void ChannelReadComplete(IChannelHandlerContext ctx)
            {
                if (!_autoRead)
                {
                    ctx.Read();
                }
                base.ChannelReadComplete(ctx);
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                if (_exception.CompareAndSet(null, cause))
                {
                    HandleException(ctx, cause);
                }
                base.ExceptionCaught(ctx, cause);
            }

            internal virtual void HandleException(IChannelHandlerContext ctx, Exception cause)
            {
                ctx.CloseAsync();
            }
        }
    }
}
