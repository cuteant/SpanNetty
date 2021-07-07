namespace DotNetty.Suite.Tests.Transport.Socket
{
    using System;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;
    using Xunit;
    using Xunit.Abstractions;

    public class SocketFixedLengthEchoTest : AbstractSocketTest
    {
        static readonly Random s_random;
        static readonly byte[] s_data;

        static SocketFixedLengthEchoTest()
        {
            s_random = new Random();
            s_data = new byte[1048576];
            s_random.NextBytes(s_data);
        }

        public SocketFixedLengthEchoTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestFixedLengthEcho(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestFixedLengthEcho0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestFixedLengthEcho_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestFixedLengthEcho0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestFixedLengthEcho_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestFixedLengthEcho0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestFixedLengthEcho_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestFixedLengthEcho0(sb, cb);
        }

        private async Task TestFixedLengthEcho0(ServerBootstrap sb, Bootstrap cb)
        {
            try
            {
                await TestFixedLengthEcho0(sb, cb, true);
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
        public Task TestFixedLengthEchoNotAutoRead(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestFixedLengthEchoNotAutoRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestFixedLengthEchoNotAutoRead_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestFixedLengthEchoNotAutoRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestFixedLengthEchoNotAutoRead_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestFixedLengthEchoNotAutoRead0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestFixedLengthEchoNotAutoRead_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestFixedLengthEchoNotAutoRead0(sb, cb);
        }

        private async Task TestFixedLengthEchoNotAutoRead0(ServerBootstrap sb, Bootstrap cb)
        {
            try
            {
                await TestFixedLengthEcho0(sb, cb, false);
            }
            finally
            {
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        private static async Task TestFixedLengthEcho0(ServerBootstrap sb, Bootstrap cb, bool autoRead)
        {
            EchoHandler sh = new EchoHandler(autoRead);
            EchoHandler ch = new EchoHandler(autoRead);

            sb.ChildOption(ChannelOption.AutoRead, autoRead);
            sb.ChildHandler(new ActionChannelInitializer<IChannel>(sch =>
            {
                sch.Pipeline.AddLast("decoder", new FixedLengthFrameDecoder(1024));
                sch.Pipeline.AddAfter("decoder", "handler", sh);
            }));

            cb.Option(ChannelOption.AutoRead, autoRead);
            cb.Handler(new ActionChannelInitializer<IChannel>(sch =>
            {
                sch.Pipeline.AddLast("decoder", new FixedLengthFrameDecoder(1024));
                sch.Pipeline.AddAfter("decoder", "handler", ch);
            }));

            IChannel sc = await sb.BindAsync();
            IChannel cc = await cb.ConnectAsync(sc.LocalAddress);
            for (int i = 0; i < s_data.Length;)
            {
                int length = Math.Min(s_random.Next(1024 * 3), s_data.Length - i);
                cc.WriteAndFlushAsync(Unpooled.WrappedBuffer(s_data, i, length)).Ignore();
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
            internal readonly AtomicReference<Exception> _exception = new AtomicReference<Exception>();
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

            protected override void ChannelRead0(IChannelHandlerContext ctx, IByteBuffer msg)
            {
                Assert.Equal(1024, msg.ReadableBytes);

                byte[] actual = new byte[msg.ReadableBytes];
                msg.GetBytes(0, actual);

                int lastIdx = _counter;
                for (int i = 0; i < actual.Length; i++)
                {
                    Assert.Equal(s_data[i + lastIdx], actual[i]);
                }

                if (_channel.Parent != null)
                {
                    _channel.WriteAsync(msg.Retain());
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
                    ctx.CloseAsync();
                }
            }
        }
    }
}
