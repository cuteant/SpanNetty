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

    public class SocketReadPendingTest : AbstractSocketTest
    {
        public SocketReadPendingTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestReadPendingIsResetAfterEachRead(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestReadPendingIsResetAfterEachRead0(sb, cb, false, false);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestReadPendingIsResetAfterEachRead_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestReadPendingIsResetAfterEachRead0(sb, cb, false, true);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestReadPendingIsResetAfterEachRead_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestReadPendingIsResetAfterEachRead0(sb, cb, true, false);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestReadPendingIsResetAfterEachRead_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestReadPendingIsResetAfterEachRead0(sb, cb, true, true);
        }

        private async Task TestReadPendingIsResetAfterEachRead0(ServerBootstrap sb, Bootstrap cb, bool isLibuvServer, bool isLibuvClient)
        {
            IChannel serverChannel = null;
            IChannel clientChannel = null;
            try
            {
                ReadPendingInitializer serverInitializer = new ReadPendingInitializer(isLibuvServer);
                ReadPendingInitializer clientInitializer = new ReadPendingInitializer(isLibuvClient);
                sb.Option(ChannelOption.SoBacklog, 1024)
                  .Option(ChannelOption.AutoRead, true)
                  .ChildOption(ChannelOption.AutoRead, false)
                  // We intend to do 2 reads per read loop wakeup
                  .ChildOption(ChannelOption.RcvbufAllocator, new TestNumReadsRecvByteBufAllocator(2))
                  .ChildHandler(serverInitializer);

                serverChannel = await sb.BindAsync();

                cb.Option(ChannelOption.AutoRead, false)
                  // We intend to do 2 reads per read loop wakeup
                  .Option(ChannelOption.RcvbufAllocator, new TestNumReadsRecvByteBufAllocator(2))
                  .Handler(clientInitializer);
                clientChannel = await cb.ConnectAsync(serverChannel.LocalAddress);

                // 4 bytes means 2 read loops for TestNumReadsRecvByteBufAllocator
                await clientChannel.WriteAndFlushAsync(Unpooled.WrappedBuffer(new byte[4]));

                // 4 bytes means 2 read loops for TestNumReadsRecvByteBufAllocator
                Assert.True(serverInitializer._channelInitLatch.Wait(TimeSpan.FromSeconds(5)));
                await serverInitializer._channel.WriteAndFlushAsync(Unpooled.WrappedBuffer(new byte[4]));

                serverInitializer._channel.Read();
                serverInitializer._readPendingHandler.AssertAllRead();

                clientChannel.Read();
                clientInitializer._readPendingHandler.AssertAllRead();
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

        sealed class ReadPendingInitializer : ChannelInitializer<IChannel>
        {
            internal readonly ReadPendingReadHandler _readPendingHandler;
            internal readonly CountdownEvent _channelInitLatch;
            internal volatile IChannel _channel;

            public ReadPendingInitializer(bool isLibuv)
            {
                _channelInitLatch = new CountdownEvent(1);
                _readPendingHandler = new ReadPendingReadHandler(isLibuv);
            }

            protected override void InitChannel(IChannel ch)
            {
                _channel = ch;
                ch.Pipeline.AddLast(_readPendingHandler);
                _channelInitLatch.SafeSignal();
            }
        }

        sealed class ReadPendingReadHandler : ChannelHandlerAdapter
        {
            private readonly bool _isLibuv;
            private readonly AtomicInteger _count;
            private readonly CountdownEvent _latch;
            private readonly CountdownEvent _latch2;

            public ReadPendingReadHandler(bool isLibuv)
            {
                _isLibuv = isLibuv;
                _count = new AtomicInteger();
                _latch = new CountdownEvent(1);
                _latch2 = new CountdownEvent(2);
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                ReferenceCountUtil.Release(msg);
                if (_count.Increment() == 1)
                {
                    // Call read the first time, to ensure it is not reset the second time.
                    ctx.Read(); // Socket BeginRead 没有做重复调用检测
                }
            }

            public override void ChannelReadComplete(IChannelHandlerContext ctx)
            {
                _latch.SafeSignal();
                _latch2.SafeSignal();
            }

            public void AssertAllRead()
            {
                Assert.True(_latch.Wait(TimeSpan.FromSeconds(5)));
                // We should only do 1 read loop, because we only called read() on the first channelRead.
                //Assert.False(_latch2.Wait(TimeSpan.FromSeconds(1)));
                Assert.True(_latch2.Wait(TimeSpan.FromSeconds(1)));
                if (_isLibuv)
                {
                    Assert.Equal(2, _count.Value);
                }
                else
                {
                    Assert.Equal(4, _count.Value);
                }
            }
        }

        /// <summary>
        /// Designed to read a single byte at a time to control the number of reads done at a fine granularity.
        /// </summary>
        sealed class TestNumReadsRecvByteBufAllocator : IRecvByteBufAllocator
        {
            private readonly int _numReads;

            public TestNumReadsRecvByteBufAllocator(int numReads)
            {
                _numReads = numReads;
            }

            public IRecvByteBufAllocatorHandle NewHandle()
            {
                return new TestExtendedHandle(_numReads);
            }
        }

        sealed class TestExtendedHandle : IRecvByteBufAllocatorHandle
        {
            private readonly int _numReads;

            private int _attemptedBytesRead;
            private int _lastBytesRead;
            private int _numMessagesRead;

            public TestExtendedHandle(int numReads)
            {
                _numReads = numReads;
            }

            public int LastBytesRead { get => _lastBytesRead; set => _lastBytesRead = value; }
            public int AttemptedBytesRead { get => _attemptedBytesRead; set => _attemptedBytesRead = value; }

            public IByteBuffer Allocate(IByteBufferAllocator alloc)
            {
                return alloc.Buffer(Guess(), Guess());
            }

            public bool ContinueReading()
            {
                return _numMessagesRead < _numReads;
            }

            public int Guess()
            {
                return 1; // only ever allocate buffers of size 1 to ensure the number of reads is controlled.
            }

            public void IncMessagesRead(int numMessages)
            {
                _numMessagesRead += numMessages;
            }

            public void ReadComplete()
            {
                // Nothing needs to be done or adjusted after each read cycle is completed.
            }

            public void Reset(IChannelConfiguration config)
            {
                _numMessagesRead = 0;
            }
        }
    }
}
