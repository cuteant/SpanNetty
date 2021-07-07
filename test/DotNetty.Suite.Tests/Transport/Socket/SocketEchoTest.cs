namespace DotNetty.Suite.Tests.Transport.Socket
{
    using System;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;
    using Xunit;
    using Xunit.Abstractions;

    public class SocketEchoTest : AbstractSocketTest, IDisposable
    {
        private static readonly Random s_random = new Random();
        static readonly byte[] s_data = new byte[1048576];

        private readonly IEventExecutorGroup _group;

        public SocketEchoTest(ITestOutputHelper output)
            : base(output)
        {
            _group = new DefaultEventExecutorGroup(2);
        }

        public void Dispose()
        {
            _group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEcho(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEcho0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEcho_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEcho0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEcho_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEcho0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEcho_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEcho0(sb, cb);
        }

        private async Task TestSimpleEcho0(ServerBootstrap sb, Bootstrap cb)
        {
            try
            {
                await TestSimpleEcho0(sb, cb, false, false, true);
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
        public Task TestSimpleEchoNotAutoRead(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoNotAutoRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEchoNotAutoRead_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoNotAutoRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEchoNotAutoRead_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoNotAutoRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEchoNotAutoRead_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoNotAutoRead0(sb, cb);
        }

        private async Task TestSimpleEchoNotAutoRead0(ServerBootstrap sb, Bootstrap cb)
        {
            try
            {
                await TestSimpleEcho0(sb, cb, false, false, false);
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
        public Task TestSimpleEchoWithAdditionalExecutor(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoWithAdditionalExecutor0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEchoWithAdditionalExecutor_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoWithAdditionalExecutor0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEchoWithAdditionalExecutor_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoWithAdditionalExecutor0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEchoWithAdditionalExecutor_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoWithAdditionalExecutor0(sb, cb);
        }

        private async Task TestSimpleEchoWithAdditionalExecutor0(ServerBootstrap sb, Bootstrap cb)
        {
            try
            {
                await TestSimpleEcho0(sb, cb, true, false, true);
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
        public Task TestSimpleEchoWithAdditionalExecutorNotAutoRead(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoWithAdditionalExecutorNotAutoRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEchoWithAdditionalExecutorNotAutoRead_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoWithAdditionalExecutorNotAutoRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEchoWithAdditionalExecutorNotAutoRead_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoWithAdditionalExecutorNotAutoRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEchoWithAdditionalExecutorNotAutoRead_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoWithAdditionalExecutorNotAutoRead0(sb, cb);
        }

        private async Task TestSimpleEchoWithAdditionalExecutorNotAutoRead0(ServerBootstrap sb, Bootstrap cb)
        {
            try
            {
                await TestSimpleEcho0(sb, cb, true, false, false);
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
        public Task TestSimpleEchoWithVoidPromise(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoWithVoidPromise0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEchoWithVoidPromise_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoWithVoidPromise0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEchoWithVoidPromise_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoWithVoidPromise0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEchoWithVoidPromise_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoWithVoidPromise0(sb, cb);
        }

        private async Task TestSimpleEchoWithVoidPromise0(ServerBootstrap sb, Bootstrap cb)
        {
            try
            {
                await TestSimpleEcho0(sb, cb, false, true, true);
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
        public Task TestSimpleEchoWithVoidPromiseNotAutoRead(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoWithVoidPromiseNotAutoRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEchoWithVoidPromiseNotAutoRead_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoWithVoidPromiseNotAutoRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEchoWithVoidPromiseNotAutoRead_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoWithVoidPromiseNotAutoRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEchoWithVoidPromiseNotAutoRead_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoWithVoidPromiseNotAutoRead0(sb, cb);
        }

        private async Task TestSimpleEchoWithVoidPromiseNotAutoRead0(ServerBootstrap sb, Bootstrap cb)
        {
            try
            {
                await TestSimpleEcho0(sb, cb, false, true, false);
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
        public Task TestSimpleEchoWithAdditionalExecutorAndVoidPromise(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoWithAdditionalExecutorAndVoidPromise0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEchoWithAdditionalExecutorAndVoidPromise_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoWithAdditionalExecutorAndVoidPromise0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEchoWithAdditionalExecutorAndVoidPromise_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoWithAdditionalExecutorAndVoidPromise0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSimpleEchoWithAdditionalExecutorAndVoidPromise_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSimpleEchoWithAdditionalExecutorAndVoidPromise0(sb, cb);
        }

        private async Task TestSimpleEchoWithAdditionalExecutorAndVoidPromise0(ServerBootstrap sb, Bootstrap cb)
        {
            try
            {
                await TestSimpleEcho0(sb, cb, true, true, true);
            }
            finally
            {
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        private async Task TestSimpleEcho0(
            ServerBootstrap sb, Bootstrap cb, bool additionalExecutor, bool voidPromise, bool autoRead)
        {
            EchoHandler sh = new EchoHandler(autoRead);
            EchoHandler ch = new EchoHandler(autoRead);

            if (additionalExecutor)
            {
                sb.ChildHandler(new ActionChannelInitializer<IChannel>(c =>
                {
                    c.Pipeline.AddLast(_group, sh);
                }));
                cb.Handler(new ActionChannelInitializer<IChannel>(c =>
                {
                    c.Pipeline.AddLast(_group, ch);
                }));
            }
            else
            {
                sb.ChildHandler(sh);
                sb.Handler(new ChannelHandlerAdapter());
                cb.Handler(ch);
            }
            sb.ChildOption(ChannelOption.AutoRead, autoRead);
            cb.Option(ChannelOption.AutoRead, autoRead);

            IChannel sc = await sb.BindAsync();
            IChannel cc = await cb.ConnectAsync(sc.LocalAddress);

            for (int i = 0; i < s_data.Length;)
            {
                int length = Math.Min(s_random.Next(1024 * 64), s_data.Length - i);
                IByteBuffer buf = Unpooled.WrappedBuffer(s_data, i, length);
                if (voidPromise)
                {
                    Assert.Equal(cc.VoidPromise().Task, cc.WriteAndFlushAsync(buf, cc.VoidPromise()));
                }
                else
                {
                    Assert.NotEqual(cc.VoidPromise().Task, cc.WriteAndFlushAsync(buf));
                }
                i += length;
            }

            while (ch._counter < s_data.Length)
            {
                if (sh._exception.Value != null)
                {
                    break;
                }
                if (ch._exception.Value != null)
                {
                    break;
                }

                Thread.Sleep(50);
            }

            while (sh._counter < s_data.Length)
            {
                if (sh._exception.Value != null)
                {
                    break;
                }
                if (ch._exception.Value != null)
                {
                    break;
                }

                Thread.Sleep(50);
            }

            await sh._channel.CloseAsync();
            await ch._channel.CloseAsync();
            await sc.CloseAsync();

            if (sh._exception.Value != null && !(sh._exception.Value is SocketException || (sh._exception.Value is ChannelException chexc && chexc.InnerException is OperationException) || sh._exception.Value is OperationException))
            {
                throw sh._exception.Value;
            }
            if (ch._exception.Value != null && !(ch._exception.Value is SocketException || (sh._exception.Value is ChannelException chexc1 && chexc1.InnerException is OperationException) || sh._exception.Value is OperationException))
            {
                throw ch._exception.Value;
            }
            if (sh._exception.Value != null)
            {
                throw sh._exception.Value;
            }
            if (ch._exception.Value != null)
            {
                throw ch._exception.Value;
            }
        }

        sealed class EchoHandler : SimpleChannelInboundHandler<IByteBuffer>
        {
            private readonly bool _autoRead;
            internal volatile IChannel _channel;
            internal AtomicReference<Exception> _exception = new AtomicReference<Exception>();
            internal volatile int _counter;

            public EchoHandler(bool autoRead)
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
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, IByteBuffer input)
            {
                byte[] actual = new byte[input.ReadableBytes];
                input.ReadBytes(actual);

                int lastIdx = _counter;
                for (int i = 0; i < actual.Length; i++)
                {
                    Assert.Equal(s_data[i + lastIdx], actual[i]);
                }

                if (_channel.Parent != null)
                {
                    _channel.WriteAsync(Unpooled.WrappedBuffer(actual));
                }

                _counter += actual.Length;
            }

            public override void ChannelReadComplete(IChannelHandlerContext ctx)
            {
                try
                {
                    ctx.Flush();
                }
                finally
                {
                    if (!_autoRead)
                    {
                        ctx.Read();
                    }
                }
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                if (_exception.CompareAndSet(null, cause))
                {
                    //cause.printStackTrace();
                    ctx.CloseAsync();
                }
            }
        }
    }
}
