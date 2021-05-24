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

    public class SocketAutoReadTest : AbstractSocketTest
    {
        public SocketAutoReadTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestAutoReadOffDuringReadOnlyReadsOneTime(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestAutoReadOffDuringReadOnlyReadsOneTime0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestAutoReadOffDuringReadOnlyReadsOneTime_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestAutoReadOffDuringReadOnlyReadsOneTime0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestAutoReadOffDuringReadOnlyReadsOneTime_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestAutoReadOffDuringReadOnlyReadsOneTime0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestAutoReadOffDuringReadOnlyReadsOneTime_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestAutoReadOffDuringReadOnlyReadsOneTime0(sb, cb);
        }

        private async Task TestAutoReadOffDuringReadOnlyReadsOneTime0(ServerBootstrap sb, Bootstrap cb)
        {
            try
            {
                await TestAutoReadOffDuringReadOnlyReadsOneTime0(true, sb, cb);
                await TestAutoReadOffDuringReadOnlyReadsOneTime0(false, sb, cb);
            }
            finally
            {
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        private static async Task TestAutoReadOffDuringReadOnlyReadsOneTime0(bool readOutsideEventLoopThread,
            ServerBootstrap sb, Bootstrap cb)
        {
            IChannel serverChannel = null;
            IChannel clientChannel = null;
            try
            {
                AutoReadInitializer serverInitializer = new AutoReadInitializer(!readOutsideEventLoopThread);
                AutoReadInitializer clientInitializer = new AutoReadInitializer(!readOutsideEventLoopThread);
                sb.Option(ChannelOption.SoBacklog, 1024)
                        .Option(ChannelOption.AutoRead, true)
                        .ChildOption(ChannelOption.AutoRead, true)
                        // We want to ensure that we attempt multiple individual read operations per read loop so we can
                        // test the auto read feature being turned off when data is first read.
                        .ChildOption(ChannelOption.RcvbufAllocator, new TestRecvByteBufAllocator())
                        .ChildHandler(serverInitializer);

                serverChannel = await sb.BindAsync();

                cb.Option(ChannelOption.AutoRead, true)
                        // We want to ensure that we attempt multiple individual read operations per read loop so we can
                        // test the auto read feature being turned off when data is first read.
                        .Option(ChannelOption.RcvbufAllocator, new TestRecvByteBufAllocator())
                        .Handler(clientInitializer);

                clientChannel = await cb.ConnectAsync(serverChannel.LocalAddress);

                // 3 bytes means 3 independent reads for TestRecvByteBufAllocator
                clientChannel.WriteAndFlushAsync(Unpooled.WrappedBuffer(new byte[3])).Ignore();
                serverInitializer._autoReadHandler.AssertSingleRead();

                // 3 bytes means 3 independent reads for TestRecvByteBufAllocator
                serverInitializer._channel.WriteAndFlushAsync(Unpooled.WrappedBuffer(new byte[3])).Ignore();
                clientInitializer._autoReadHandler.AssertSingleRead();

                if (readOutsideEventLoopThread)
                {
                    serverInitializer._channel.Read();
                }
                serverInitializer._autoReadHandler.AssertSingleReadSecondTry();

                if (readOutsideEventLoopThread)
                {
                    clientChannel.Read();
                }
                clientInitializer._autoReadHandler.AssertSingleReadSecondTry();
            }
            finally
            {
                if (clientChannel != null)
                {
                    await clientChannel.CloseAsync();
                }
                if (serverChannel != null)
                {
                    await serverChannel.CloseAsync();
                }
            }
        }

        class AutoReadInitializer : ChannelInitializer<IChannel>
        {
            internal readonly AutoReadHandler _autoReadHandler;
            internal volatile IChannel _channel;

            public AutoReadInitializer(bool readInEventLoop)
            {
                _autoReadHandler = new AutoReadHandler(readInEventLoop);
            }

            protected override void InitChannel(IChannel channel)
            {
                _channel = channel;
                channel.Pipeline.AddLast(_autoReadHandler);
            }
        }

        sealed class AutoReadHandler : ChannelHandlerAdapter
        {
            private readonly AtomicInteger _count = new AtomicInteger();
            private readonly CountdownEvent _latch = new CountdownEvent(1);
            private readonly CountdownEvent _latch2;
            private readonly bool _callRead;

            public AutoReadHandler(bool callRead)
            {
                _callRead = callRead;
                _latch2 = new CountdownEvent(callRead ? 3 : 2);
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                ReferenceCountUtil.Release(msg);
                if (_count.Increment() == 1)
                {
                    ctx.Channel.Configuration.IsAutoRead = false;
                }
                if (_callRead)
                {
                    // Test calling read in the EventLoop thread to ensure a read is eventually done.
                    ctx.Read();
                }
            }

            public override void ChannelReadComplete(IChannelHandlerContext ctx)
            {
                _latch.SafeSignal();
                _latch2.SafeSignal();
            }

            public void AssertSingleRead()
            {
                Assert.True(_latch.Wait(TimeSpan.FromSeconds(5)));
                Assert.True(_count.Value > 0);
            }

            public void AssertSingleReadSecondTry()
            {
                Assert.True(_latch2.Wait(TimeSpan.FromSeconds(5)));
                Assert.Equal(_callRead ? 3 : 2, _count.Value);
            }
        }

        /// <summary>
        /// Designed to keep reading as long as autoread is enabled.
        /// </summary>
        sealed class TestRecvByteBufAllocator : IRecvByteBufAllocator
        {
            public IRecvByteBufAllocatorHandle NewHandle()
            {
                return new TestExtendedHandle();
            }
        }

        sealed class TestExtendedHandle : IRecvByteBufAllocatorHandle
        {
            private IChannelConfiguration _config;
            private int _attemptedBytesRead;
            private int _lastBytesRead;

            public int LastBytesRead { get => _lastBytesRead; set => _lastBytesRead = value; }
            public int AttemptedBytesRead { get => _attemptedBytesRead; set => _attemptedBytesRead = value; }

            public IByteBuffer Allocate(IByteBufferAllocator alloc)
            {
                return alloc.Buffer(Guess(), Guess());
            }

            public bool ContinueReading()
            {
                return _config.IsAutoRead;
            }

            public int Guess()
            {
                return 1; // only ever allocate buffers of size 1 to ensure the number of reads is controlled.
            }

            public void IncMessagesRead(int numMessages)
            {
                // No need to track the number of messages read because it is not used.
            }

            public void ReadComplete()
            {
                // Nothing needs to be done or adjusted after each read cycle is completed.
            }

            public void Reset(IChannelConfiguration config)
            {
                _config = config;
            }
        }
    }
}
