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

    public class SocketCancelWriteTest : AbstractSocketTest
    {
        public SocketCancelWriteTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestCancelWrite(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestCancelWrite0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestCancelWrite_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestCancelWrite0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestCancelWrite_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestCancelWrite0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestCancelWrite_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestCancelWrite0(sb, cb);
        }

        private async Task TestCancelWrite0(ServerBootstrap sb, Bootstrap cb)
        {
            TestHandler sh = new TestHandler();
            TestHandler ch = new TestHandler();
            IByteBuffer a = Unpooled.Buffer().WriteByte('a');
            IByteBuffer b = Unpooled.Buffer().WriteByte('b');
            IByteBuffer c = Unpooled.Buffer().WriteByte('c');
            IByteBuffer d = Unpooled.Buffer().WriteByte('d');
            IByteBuffer e = Unpooled.Buffer().WriteByte('e');

            cb.Handler(ch);
            sb.ChildHandler(sh);

            IChannel sc = await sb.BindAsync();
            IChannel cc = await cb.ConnectAsync(sc.LocalAddress);

            var promise = new DefaultPromise();
            Assert.True(promise.TrySetCanceled());
            var f = cc.WriteAsync(a, promise);
            await cc.WriteAndFlushAsync(b);
            cc.WriteAsync(c).Ignore();
            promise = new DefaultPromise();
            Assert.True(promise.TrySetCanceled());
            var f2 = cc.WriteAsync(d, promise);
            await cc.WriteAndFlushAsync(e);

            while (sh._counter < 3)
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

            Task.WaitAll(
                sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));

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
            Assert.Equal(0, ch._counter);
            Assert.True(Unpooled.WrappedBuffer(new byte[] { (byte)'b', (byte)'c', (byte)'e' }).Equals(sh._received));
        }

        sealed class TestHandler : SimpleChannelInboundHandler<IByteBuffer>
        {
            internal volatile IChannel _channel;
            internal readonly AtomicReference<Exception> _exception = new AtomicReference<Exception>();
            internal volatile int _counter;
            internal readonly IByteBuffer _received = Unpooled.Buffer();

            public override void ChannelActive(IChannelHandlerContext context)
            {
                _channel = context.Channel;
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, IByteBuffer msg)
            {
                _counter += msg.ReadableBytes;
                _received.WriteBytes(msg);
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
