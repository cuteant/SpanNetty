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

    public class SocketConditionalWritabilityTest : AbstractSocketTest
    {
        static SocketConditionalWritabilityTest()
        {
            DotNetty.Common.ResourceLeakDetector.Level = Common.ResourceLeakDetector.DetectionLevel.Disabled;
        }

        public SocketConditionalWritabilityTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestConditionalWritability(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestConditionalWritability0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestConditionalWritability_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestConditionalWritability0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestConditionalWritability_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestConditionalWritability0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestConditionalWritability_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestConditionalWritability0(sb, cb);
        }

        private async Task TestConditionalWritability0(ServerBootstrap sb, Bootstrap cb)
        {
            IChannel serverChannel = null;
            IChannel clientChannel = null;
            try
            {
                int expectedBytes = 100 * 1024 * 1024;
                int maxWriteChunkSize = 16 * 1024;
                CountdownEvent latch = new CountdownEvent(1);
                sb.ChildOption(ChannelOption.WriteBufferLowWaterMark, 8 * 1024);
                sb.ChildOption(ChannelOption.WriteBufferHighWaterMark, 16 * 1024);
                sb.ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new ChannelDuplexHandler0(expectedBytes, maxWriteChunkSize));
                }));

                serverChannel = await sb.BindAsync();

                cb.Handler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new ChannelInboundHandlerAdapter0(expectedBytes, latch));
                }));
                clientChannel = await cb.ConnectAsync(serverChannel.LocalAddress);
                latch.Wait();
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

        sealed class ChannelInboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            private readonly int _expectedBytes;
            private readonly CountdownEvent _latch;
            private int _totalRead;

            public ChannelInboundHandlerAdapter0(int expectedBytes, CountdownEvent latch)
            {
                _expectedBytes = expectedBytes;
                _latch = latch;
            }

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                ctx.WriteAndFlushAsync(ctx.Allocator.Buffer(1).WriteByte(0));
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                if (msg is IByteBuffer buf)
                {
                    _totalRead += buf.ReadableBytes;
                    if (_totalRead == _expectedBytes)
                    {
                        _latch.SafeSignal();
                    }
                }
                ReferenceCountUtil.Release(msg);
            }
        }

        sealed class ChannelDuplexHandler0 : ChannelDuplexHandler
        {
            private readonly int _expectedBytes;
            private readonly int _maxWriteChunkSize;
            private int _bytesWritten;

            public ChannelDuplexHandler0(int expectedBytes, int maxWriteChunkSize)
            {
                _expectedBytes = expectedBytes;
                _maxWriteChunkSize = maxWriteChunkSize;
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                ReferenceCountUtil.Release(msg);
                WriteRemainingBytes(ctx);
            }

            public override void Flush(IChannelHandlerContext ctx)
            {
                if (ctx.Channel.IsWritable)
                {
                    WriteRemainingBytes(ctx);
                }
                else
                {
                    ctx.Flush();
                }
            }

            public override void ChannelWritabilityChanged(IChannelHandlerContext ctx)
            {
                if (ctx.Channel.IsWritable)
                {
                    WriteRemainingBytes(ctx);
                }
                ctx.FireChannelWritabilityChanged();
            }

            private void WriteRemainingBytes(IChannelHandlerContext ctx)
            {
                while (ctx.Channel.IsWritable && _bytesWritten < _expectedBytes)
                {
                    int chunkSize = Math.Min(_expectedBytes - _bytesWritten, _maxWriteChunkSize);
                    _bytesWritten += chunkSize;
                    ctx.WriteAsync(ctx.Allocator.Buffer(chunkSize).WriteZero(chunkSize));
                }
                ctx.Flush();
            }
        }
    }
}
