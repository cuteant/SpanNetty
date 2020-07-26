namespace DotNetty.Transport.Tests.Channel.Sockets
{
    using System;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Tests.Common;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit;
    using Xunit.Abstractions;

    [Collection("Transport Tests")]
    public class TcpSocketChannelTest : TestBase
    {
        public TcpSocketChannelTest(ITestOutputHelper output)
            : base(output)
        {
        }

        /**
         * Reproduces the issue #1600
         */
        [Fact]
        public void TestFlushCloseReentrance()
        {
            var group = new MultithreadEventLoopGroup(1);
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            IChannel sc = null;
            try
            {
                var futures = new BlockingCollection<Task>();

                ServerBootstrap sb = new ServerBootstrap();
                sb.Group(group)
                    .Channel<TcpServerSocketChannel>()
                    .Option(ChannelOption.SoSndbuf, 1024)
                    .ChildHandler(new ChannelInboundHandlerAdapter1(futures));

                sc = sb.BindAsync(new IPEndPoint(IPAddress.Loopback, 0)).GetAwaiter().GetResult();
#if NET452
                socket.Connect(sc.LocalAddress);
#else
                socket.ConnectAsync(sc.LocalAddress).GetAwaiter().GetResult();
#endif

                byte[] tempArea = new byte[8192];
#if NETCOREAPP_2_0_GREATER || NETSTANDARD_2_0_GREATER
                Span<byte> buf = tempArea;
#endif
                while (true)
                {
#if NETCOREAPP_2_0_GREATER || NETSTANDARD_2_0_GREATER
                    var byteCount = socket.Receive(buf);
#else
                    var byteCount = socket.Receive(tempArea);
#endif
                    if (byteCount <= 0) { break; }

                    // Wait a little bit so that the write attempts are split into multiple flush attempts.
                    Thread.Sleep(10);
                }
                SocketEx.SafeClose(socket);

                Assert.Equal(3, futures.Count);
                var f1 = futures.Take();
                var f2 = futures.Take();
                var f3 = futures.Take();
                Assert.True(f1.IsSuccess());
                Assert.True(f1.IsCompleted);
                Assert.False(f2.IsSuccess());
                Assert.True(f2.IsCompleted);
                Assert.IsType<ClosedChannelException>(TaskUtil.Unwrap(f2.Exception));
                Assert.False(f3.IsSuccess());
                Assert.True(f3.IsCompleted);
                Assert.IsType<ClosedChannelException>(TaskUtil.Unwrap(f3.Exception));
            }
            finally
            {
                SocketEx.SafeClose(socket);
                sc?.CloseAsync().GetAwaiter().GetResult();
                group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
            }
        }

        class ChannelInboundHandlerAdapter1 : ChannelHandlerAdapter
        {
            private readonly BlockingCollection<Task> _futures;

            public ChannelInboundHandlerAdapter1(BlockingCollection<Task> futures)
            {
                _futures = futures;
            }

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                // Write a large enough data so that it is split into two loops.
                _futures.Add(ctx.WriteAsync(ctx.Allocator.Buffer().WriteZero(1048576)).CloseOnComplete(ctx.Channel));
                _futures.Add(ctx.WriteAsync(ctx.Allocator.Buffer().WriteZero(1048576)));
                ctx.Flush();
                _futures.Add(ctx.WriteAsync(ctx.Allocator.Buffer().WriteZero(1048576)));
                ctx.Flush();
            }
        }

#if NETCOREAPP_2_0_GREATER || NETSTANDARD_2_0_GREATER
        /**
         * Reproduces the issue #1679
         */
        [Fact]
        public void TestFlushAfterGatheredFlush()
        {
            var group = new MultithreadEventLoopGroup(1);
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            IChannel sc = null;
            try
            {
                ServerBootstrap sb = new ServerBootstrap();
                sb.Group(group)
                    .Channel<TcpServerSocketChannel>()
                    .ChildHandler(new ChannelInboundHandlerAdapter0());

                sc = sb.BindAsync(new IPEndPoint(IPAddress.Loopback, 0)).GetAwaiter().GetResult();
                socket.ConnectAsync(sc.LocalAddress).GetAwaiter().GetResult();
                byte[] tempArea = new byte[3];
                Span<byte> buf = tempArea;
                var totalCount = 0;
                do
                {
                    var byteCount = socket.Receive(buf);
                    if (byteCount < 0) { break; }
                    totalCount += byteCount;
                    if (totalCount < 3)
                    {
                        buf = buf.Slice(totalCount);
                    }
                } while (totalCount < 3);
                Assert.Equal("abc", Encoding.ASCII.GetString(tempArea));
            }
            finally
            {
                sc?.CloseAsync().GetAwaiter().GetResult();
                SocketEx.SafeClose(socket);
                group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
            }
        }
#endif

        class ChannelInboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                // Trigger a gathering write by writing two buffers.
                ctx.WriteAsync(Unpooled.WrappedBuffer(new byte[] { (byte)'a' }));
                var f = ctx.WriteAsync(Unpooled.WrappedBuffer(new byte[] { (byte)'b' }));
                f.ContinueWith(t =>
                {
                    // This message must be flushed
                    ctx.WriteAndFlushAsync(Unpooled.WrappedBuffer(new byte[] { (byte)'c' }));
                }, TaskContinuationOptions.ExecuteSynchronously);
                ctx.Flush();
            }
        }

        // Test for https://github.com/netty/netty/issues/4805
        [Fact]
        public Task TestChannelReRegisterReadSameEventLoop()
        {
            return TestChannelReRegisterRead(true);
        }

        [Fact]
        public Task TestChannelReRegisterReadDifferentEventLoop()
        {
            return TestChannelReRegisterRead(false);
        }

        private async Task TestChannelReRegisterRead(bool sameEventLoop)
        {
            var group = new MultithreadEventLoopGroup(2);
            var latch = new CountdownEvent(1);

            // Just some random bytes
            byte[] bytes = new byte[1024];
            (new Random()).NextBytes(bytes);

            IChannel sc = null;
            IChannel cc = null;
            ServerBootstrap bs = null;
            try
            {
                bs = new ServerBootstrap()
                    .Group(group)
                    .Channel<TcpServerSocketChannel>()
                    .ChildOption(ChannelOption.SoKeepalive, true)
                    .ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
                    {
                        var pipeline = ch.Pipeline;
                        pipeline.AddLast(new SimpleChannelInboundHandler0(group, latch, sameEventLoop));
                    }));

                sc = await bs.BindAsync(new IPEndPoint(IPAddress.IPv6Loopback, 0));
                var bc = new Bootstrap()
                    .Group(group)
                    .Channel<TcpSocketChannel>()
                    .Handler(new ChannelHandlerAdapter());
                cc = await bc.ConnectAsync(sc.LocalAddress);
                await cc.WriteAndFlushAsync(Unpooled.WrappedBuffer(bytes));
                latch.Wait();
            }
            finally
            {
                if (sc != null) { await sc.CloseAsync(); }
                if (cc != null) { await cc.CloseAsync(); }
                await group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
            }
        }

        sealed class SimpleChannelInboundHandler0 : SimpleChannelInboundHandler<IByteBuffer>
        {
            private readonly IEventLoopGroup _group;
            private readonly CountdownEvent _latch;
            private readonly bool _sameEventLoop;

            public SimpleChannelInboundHandler0(IEventLoopGroup group, CountdownEvent latch, bool sameEventLoop)
            {
                _group = group;
                _latch = latch;
                _sameEventLoop = sameEventLoop;
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, IByteBuffer msg)
            {
                // We was able to read something from the Channel after reregister.
                _latch.Signal();
            }

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                IEventLoop loop = _group.GetNext();
                if (_sameEventLoop)
                {
                    Deregister(ctx, loop);
                }
                else
                {
                    loop.Execute(() => Deregister(ctx, loop));
                }
            }

            private void Deregister(IChannelHandlerContext ctx, IEventLoop loop)
            {
                // As soon as the channel becomes active re-register it to another
                // EventLoop. After this is done we should still receive the data that
                // was written to the channel.
                ctx.DeregisterAsync().ContinueWith(t =>
                {
                    IChannel channel = ctx.Channel;
                    Assert.NotSame(loop, channel.EventLoop);
                    _group.GetNext().RegisterAsync(channel);
                }, TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        [Fact]
        public async Task TestShutdownOutputAndClose()
        {
            var group = new MultithreadEventLoopGroup(1);
            var bc = new Bootstrap();
            var bs = new ServerBootstrap()
                .Group(group)
                .Channel<TcpServerSocketChannel>()
                .ChildHandler(new ChannelHandlerAdapter());
            IChannel serverChannel = null;
            try
            {
                serverChannel = await bs.BindAsync(new IPEndPoint(IPAddress.IPv6Loopback, 0));

                bc.Group(group).Channel<TcpSocketChannel>().Handler(new ChannelHandlerAdapter());

                var channel = await bc.ConnectAsync(serverChannel.LocalAddress);
                await ((TcpSocketChannel)channel).ShutdownOutputAsync();
                await channel.CloseAsync();
            }
            finally
            {
                if (serverChannel is object) { await serverChannel.CloseAsync(); }
                await group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
            }
        }
    }
}