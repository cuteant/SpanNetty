namespace DotNetty.Suite.Tests.Transport.Socket
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit;
    using Xunit.Abstractions;

    public class SocketHalfClosedTest : AbstractSocketTest
    {
        public SocketHalfClosedTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public void TestHalfClosureOnlyOneEventWhenAutoRead(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            TestHalfClosureOnlyOneEventWhenAutoRead0(sb, cb);
        }

        private void TestHalfClosureOnlyOneEventWhenAutoRead0(ServerBootstrap sb, Bootstrap cb)
        {
            IChannel serverChannel = null;
            try
            {
                cb.Option(ChannelOption.AllowHalfClosure, true)
                        .Option(ChannelOption.AutoRead, true);
                sb.ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new ChannelInboundHandlerAdapter4());
                }));

                AtomicInteger shutdownEventReceivedCounter = new AtomicInteger();
                AtomicInteger shutdownReadCompleteEventReceivedCounter = new AtomicInteger();

                cb.Handler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new ChannelInboundHandlerAdapter5(shutdownEventReceivedCounter, shutdownReadCompleteEventReceivedCounter));
                }));

                serverChannel = sb.BindAsync().GetAwaiter().GetResult();
                IChannel clientChannel = cb.ConnectAsync(serverChannel.LocalAddress).GetAwaiter().GetResult();
                clientChannel.CloseCompletion.GetAwaiter().GetResult();
                Assert.Equal(1, shutdownEventReceivedCounter.Value);
                Assert.Equal(1, shutdownReadCompleteEventReceivedCounter.Value);
            }
            finally
            {
                if (serverChannel != null)
                {
                    serverChannel.CloseAsync().GetAwaiter().GetResult();
                }
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        sealed class ChannelInboundHandlerAdapter4 : ChannelHandlerAdapter
        {
            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                ((IDuplexChannel)ctx.Channel).ShutdownOutputAsync();
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                ctx.CloseAsync();
            }
        }

        sealed class ChannelInboundHandlerAdapter5 : ChannelHandlerAdapter
        {
            private readonly AtomicInteger _shutdownEventReceivedCounter;
            private readonly AtomicInteger _shutdownReadCompleteEventReceivedCounter;

            public ChannelInboundHandlerAdapter5(AtomicInteger shutdownEventReceivedCounter, AtomicInteger shutdownReadCompleteEventReceivedCounter)
            {
                _shutdownEventReceivedCounter = shutdownEventReceivedCounter;
                _shutdownReadCompleteEventReceivedCounter = shutdownReadCompleteEventReceivedCounter;
            }

            public override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
            {
                if (evt == ChannelInputShutdownEvent.Instance)
                {
                    _shutdownEventReceivedCounter.Increment();
                }
                else if (evt == ChannelInputShutdownReadComplete.Instance)
                {
                    _shutdownReadCompleteEventReceivedCounter.Increment();
                    ctx.Executor.Schedule(() => ctx.CloseAsync(), TimeSpan.FromMilliseconds(100));
                }
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                ctx.CloseAsync();
            }
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public void TestAllDataReadAfterHalfClosure(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            TestAllDataReadAfterHalfClosure0(sb, cb);
        }

        private void TestAllDataReadAfterHalfClosure0(ServerBootstrap sb, Bootstrap cb)
        {
            try
            {
                TestAllDataReadAfterHalfClosure0(true, sb, cb);
                TestAllDataReadAfterHalfClosure0(false, sb, cb);
            }
            finally
            {
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        private static void TestAllDataReadAfterHalfClosure0(bool autoRead,
                                                            ServerBootstrap sb, Bootstrap cb)
        {
            const int totalServerBytesWritten = 1024 * 16;
            const int numReadsPerReadLoop = 2;
            CountdownEvent serverInitializedLatch = new CountdownEvent(1);
            CountdownEvent clientReadAllDataLatch = new CountdownEvent(1);
            CountdownEvent clientHalfClosedLatch = new CountdownEvent(1);
            AtomicInteger clientReadCompletes = new AtomicInteger();
            IChannel serverChannel = null;
            IChannel clientChannel = null;
            try
            {
                cb.Option(ChannelOption.AllowHalfClosure, true)
                    .Option(ChannelOption.AutoRead, autoRead)
                    .Option(ChannelOption.RcvbufAllocator, new TestNumReadsRecvByteBufAllocator(numReadsPerReadLoop));

                sb.ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new ChannelInboundHandlerAdapter2(totalServerBytesWritten, serverInitializedLatch));
                }));

                cb.Handler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new ChannelInboundHandlerAdapter3(autoRead, totalServerBytesWritten,
                        clientHalfClosedLatch, clientReadAllDataLatch, clientReadCompletes));
                }));

                serverChannel = sb.BindAsync().GetAwaiter().GetResult();
                clientChannel = cb.ConnectAsync(serverChannel.LocalAddress).GetAwaiter().GetResult();
                clientChannel.Read();

                serverInitializedLatch.Wait();
                clientReadAllDataLatch.Wait();
                clientHalfClosedLatch.Wait();
                Assert.True(totalServerBytesWritten / numReadsPerReadLoop + 10 > clientReadCompletes.Value,
                    "too many read complete events: " + clientReadCompletes.Value);
            }
            finally
            {
                if (clientChannel != null)
                {
                    clientChannel.CloseAsync().GetAwaiter().GetResult();
                }
                if (serverChannel != null)
                {
                    serverChannel.CloseAsync().GetAwaiter().GetResult();
                }
            }
        }

        class ChannelInboundHandlerAdapter2 : ChannelHandlerAdapter
        {
            private readonly int _totalServerBytesWritten;
            private readonly CountdownEvent _serverInitializedLatch;

            public ChannelInboundHandlerAdapter2(int totalServerBytesWritten, CountdownEvent serverInitializedLatch)
            {
                _totalServerBytesWritten = totalServerBytesWritten;
                _serverInitializedLatch = serverInitializedLatch;
            }

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                IByteBuffer buf = ctx.Allocator.Buffer(_totalServerBytesWritten);
                buf.SetWriterIndex(buf.Capacity);
                ctx.WriteAndFlushAsync(buf).ContinueWith(t =>
                {
                    ((IDuplexChannel)ctx.Channel).ShutdownOutputAsync();
                }, TaskContinuationOptions.ExecuteSynchronously);
                _serverInitializedLatch.SafeSignal();
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                ctx.CloseAsync();
            }
        }

        class ChannelInboundHandlerAdapter3 : ChannelHandlerAdapter
        {
            private readonly bool _autoRead;
            private readonly int _totalServerBytesWritten;
            private readonly CountdownEvent _clientHalfClosedLatch;
            private readonly CountdownEvent _clientReadAllDataLatch;
            private readonly AtomicInteger _clientReadCompletes;
            private int _bytesRead;

            public ChannelInboundHandlerAdapter3(bool autoRead, int totalServerBytesWritten,
                CountdownEvent clientHalfClosedLatch, CountdownEvent clientReadAllDataLatch, AtomicInteger clientReadCompletes)
            {
                _autoRead = autoRead;
                _totalServerBytesWritten = totalServerBytesWritten;
                _clientHalfClosedLatch = clientHalfClosedLatch;
                _clientReadAllDataLatch = clientReadAllDataLatch;
                _clientReadCompletes = clientReadCompletes;
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                IByteBuffer buf = (IByteBuffer)msg;
                _bytesRead += buf.ReadableBytes;
                buf.Release();
            }

            public override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
            {
                if (evt == ChannelInputShutdownEvent.Instance)
                {
                    _clientHalfClosedLatch.SafeSignal();
                }
                else if (evt == ChannelInputShutdownReadComplete.Instance)
                {
                    ctx.CloseAsync();
                }
            }

            public override void ChannelReadComplete(IChannelHandlerContext ctx)
            {
                _clientReadCompletes.Increment();
                if (_bytesRead == _totalServerBytesWritten)
                {
                    _clientReadAllDataLatch.SafeSignal();
                }
                if (!_autoRead)
                {
                    ctx.Read();
                }
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                ctx.CloseAsync();
            }
        }

        //[Theory]
        //[MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        [Fact(Skip = "TestAutoCloseFalseDoesShutdownOutput")]
        public void TestAutoCloseFalseDoesShutdownOutput(/*IByteBufferAllocator allocator*/)
        {
            // This test only works on Linux / BSD / MacOS as we assume some semantics that are not true for Windows.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) { return; }

            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, PooledByteBufferAllocator.Default);

            TestAutoCloseFalseDoesShutdownOutput0(sb, cb, true);
        }

        private void TestAutoCloseFalseDoesShutdownOutput0(ServerBootstrap sb, Bootstrap cb, bool supportHalfClosed)
        {
            try
            {
                TestAutoCloseFalseDoesShutdownOutput0(false, false, sb, cb);
                TestAutoCloseFalseDoesShutdownOutput0(false, true, sb, cb);
                if (supportHalfClosed)
                {
                    TestAutoCloseFalseDoesShutdownOutput0(true, false, sb, cb);
                    TestAutoCloseFalseDoesShutdownOutput0(true, true, sb, cb);
                }
            }
            finally
            {
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        private static void TestAutoCloseFalseDoesShutdownOutput0(bool allowHalfClosed,
                                                                 bool clientIsLeader,
                                                                 ServerBootstrap sb,
                                                                 Bootstrap cb)
        {
            const int expectedBytes = 100;
            CountdownEvent serverReadExpectedLatch = new CountdownEvent(1);
            CountdownEvent doneLatch = new CountdownEvent(1);
            AtomicReference<Exception> causeRef = new AtomicReference<Exception>();
            IChannel serverChannel = null;
            IChannel clientChannel = null;
            try
            {
                cb.Option(ChannelOption.AllowHalfClosure, allowHalfClosed)
                        .Option(ChannelOption.AutoClose, false)
                        .Option(ChannelOption.SoLinger, 0);
                sb.ChildOption(ChannelOption.AllowHalfClosure, allowHalfClosed)
                        .ChildOption(ChannelOption.AutoClose, false)
                        .ChildOption(ChannelOption.SoLinger, 0);

                SimpleChannelInboundHandler<IByteBuffer> leaderHandler = new AutoCloseFalseLeader(expectedBytes,
                       serverReadExpectedLatch, doneLatch, causeRef);
                SimpleChannelInboundHandler<IByteBuffer> followerHandler = new AutoCloseFalseFollower(expectedBytes,
                       serverReadExpectedLatch, doneLatch, causeRef);
                sb.ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(clientIsLeader ? followerHandler : leaderHandler);
                }));

                cb.Handler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(clientIsLeader ? leaderHandler : followerHandler);
                }));

                serverChannel = sb.BindAsync().GetAwaiter().GetResult();
                clientChannel = cb.ConnectAsync(serverChannel.LocalAddress).GetAwaiter().GetResult();

                doneLatch.Wait();
                Assert.Null(causeRef.Value);
            }
            finally
            {
                if (clientChannel != null)
                {
                    clientChannel.CloseAsync().GetAwaiter().GetResult();
                }
                if (serverChannel != null)
                {
                    serverChannel.CloseAsync().GetAwaiter().GetResult();
                }
            }
        }

        sealed class AutoCloseFalseFollower : SimpleChannelInboundHandler<IByteBuffer>
        {
            private readonly int _expectedBytes;
            private readonly CountdownEvent _followerCloseLatch;
            private readonly CountdownEvent _doneLatch;
            private readonly AtomicReference<Exception> _causeRef;
            private int _bytesRead;

            public AutoCloseFalseFollower(int expectedBytes, CountdownEvent followerCloseLatch, CountdownEvent doneLatch,
                AtomicReference<Exception> causeRef)
            {
                _expectedBytes = expectedBytes;
                _followerCloseLatch = followerCloseLatch;
                _doneLatch = doneLatch;
                _causeRef = causeRef;
            }

            public override void ChannelInactive(IChannelHandlerContext ctx)
            {
                CheckPrematureClose();
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                ctx.CloseAsync();
                CheckPrematureClose();
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, IByteBuffer msg)
            {
                _bytesRead += msg.ReadableBytes;
                if (_bytesRead >= _expectedBytes)
                {
                    // We write a reply and immediately close our end of the socket.
                    IByteBuffer buf = ctx.Allocator.Buffer(_expectedBytes);
                    buf.SetWriterIndex(buf.WriterIndex + _expectedBytes);
                    ctx.WriteAndFlushAsync(buf).ContinueWith(t =>
                    {
                        ctx.Channel.CloseAsync().ContinueWith(task =>
                        {
                            // This is a bit racy but there is no better way how to handle this in Java11.
                            // The problem is that on close() the underlying FD will not actually be closed directly
                            // but the close will be done after the Selector did process all events. Because of
                            // this we will need to give it a bit time to ensure the FD is actual closed before we
                            // count down the latch and try to write.
                            _followerCloseLatch.SafeSignal();
                            //ctx.Channel.EventLoop.Schedule(() => _followerCloseLatch.SafeSignal(), TimeSpan.FromMilliseconds(200));
                        }, TaskContinuationOptions.ExecuteSynchronously);
                    }, TaskContinuationOptions.ExecuteSynchronously);
                }
            }

            private void CheckPrematureClose()
            {
                if (_bytesRead < _expectedBytes)
                {
                    _causeRef.Value = new InvalidOperationException("follower premature close");
                    _doneLatch.SafeSignal();
                }
            }
        }

        sealed class AutoCloseFalseLeader : SimpleChannelInboundHandler<IByteBuffer>
        {
            private readonly int _expectedBytes;
            private readonly CountdownEvent _followerCloseLatch;
            private readonly CountdownEvent _doneLatch;
            private readonly AtomicReference<Exception> _causeRef;
            private int _bytesRead;
            private bool _seenOutputShutdown;

            public AutoCloseFalseLeader(int expectedBytes, CountdownEvent followerCloseLatch, CountdownEvent doneLatch,
                AtomicReference<Exception> causeRef)
            {
                _expectedBytes = expectedBytes;
                _followerCloseLatch = followerCloseLatch;
                _doneLatch = doneLatch;
                _causeRef = causeRef;
            }

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                IByteBuffer buf = ctx.Allocator.Buffer(_expectedBytes);
                buf.SetWriterIndex(buf.WriterIndex + _expectedBytes);
                ctx.WriteAndFlushAsync(buf.RetainedDuplicate());

                // We wait here to ensure that we write before we have a chance to process the outbound
                // shutdown event.
                _followerCloseLatch.Wait();

                // This write should fail, but we should still be allowed to read the peer's data
                ctx.WriteAndFlushAsync(buf).ContinueWith(t =>
                {
                    if (t.IsSuccess())
                    {
                        _causeRef.Value = new InvalidOperationException("second write should have failed!");
                        _doneLatch.SafeSignal();
                    }
                }, TaskContinuationOptions.ExecuteSynchronously);
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, IByteBuffer msg)
            {
                _bytesRead += msg.ReadableBytes;
                if (_bytesRead >= _expectedBytes)
                {
                    if (!_seenOutputShutdown)
                    {
                        _causeRef.Value = new InvalidOperationException(
                                nameof(ChannelOutputShutdownEvent) + " event was not seen");
                    }
                    _doneLatch.SafeSignal();
                }
            }

            public override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
            {
                if (evt is ChannelOutputShutdownEvent)
                {
                    _seenOutputShutdown = true;
                }
            }

            public override void ChannelInactive(IChannelHandlerContext ctx)
            {
                CheckPrematureClose();
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                ctx.CloseAsync();
                CheckPrematureClose();
            }

            private void CheckPrematureClose()
            {
                if (_bytesRead < _expectedBytes || !_seenOutputShutdown)
                {
                    _causeRef.Value = new InvalidOperationException("leader premature close");
                    _doneLatch.SafeSignal();
                }
            }
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public void TestAllDataReadClosure(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            TestAllDataReadClosure0(sb, cb, true);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public void TestAllDataReadClosure_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            TestAllDataReadClosure0(sb, cb, false);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public void TestAllDataReadClosure_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            TestAllDataReadClosure0(sb, cb, true);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public void TestAllDataReadClosure_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            TestAllDataReadClosure0(sb, cb, false);
        }

        private void TestAllDataReadClosure0(ServerBootstrap sb, Bootstrap cb, bool supportHalfClosed)
        {
            try
            {
                TestAllDataReadClosure0(true, false, sb, cb);
                if (supportHalfClosed) { TestAllDataReadClosure0(true, true, sb, cb); }
                TestAllDataReadClosure0(false, false, sb, cb);
                if (supportHalfClosed) { TestAllDataReadClosure0(false, true, sb, cb); }
            }
            finally
            {
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        private static void TestAllDataReadClosure0(bool autoRead, bool allowHalfClosed,
                                                   ServerBootstrap sb, Bootstrap cb)
        {
            const int totalServerBytesWritten = 1024 * 16;
            const int numReadsPerReadLoop = 2;
            CountdownEvent serverInitializedLatch = new CountdownEvent(1);
            CountdownEvent clientReadAllDataLatch = new CountdownEvent(1);
            CountdownEvent clientHalfClosedLatch = new CountdownEvent(1);
            AtomicInteger clientReadCompletes = new AtomicInteger();
            IChannel serverChannel = null;
            IChannel clientChannel = null;
            try
            {
                cb.Option(ChannelOption.AllowHalfClosure, allowHalfClosed)
                    .Option(ChannelOption.AutoRead, autoRead)
                    .Option(ChannelOption.RcvbufAllocator, new TestNumReadsRecvByteBufAllocator(numReadsPerReadLoop));

                sb.ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new ChannelInboundHandlerAdapter0(totalServerBytesWritten, serverInitializedLatch));
                }));

                cb.Handler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new ChannelInboundHandlerAdapter1(allowHalfClosed, autoRead, totalServerBytesWritten,
                        clientHalfClosedLatch, clientReadAllDataLatch, clientReadCompletes));
                }));

                serverChannel = sb.BindAsync().GetAwaiter().GetResult();
                clientChannel = cb.ConnectAsync(serverChannel.LocalAddress).GetAwaiter().GetResult();
                clientChannel.Read();

                serverInitializedLatch.Wait();
                clientReadAllDataLatch.Wait();
                clientHalfClosedLatch.Wait();
            }
            finally
            {
                if (clientChannel is object)
                {
                    clientChannel.CloseAsync().GetAwaiter().GetResult();
                }
                if (serverChannel is object)
                {
                    serverChannel.CloseAsync().GetAwaiter().GetResult();
                }
            }
        }

        class ChannelInboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            private readonly int _totalServerBytesWritten;
            private readonly CountdownEvent _serverInitializedLatch;

            public ChannelInboundHandlerAdapter0(int totalServerBytesWritten, CountdownEvent serverInitializedLatch)
            {
                _totalServerBytesWritten = totalServerBytesWritten;
                _serverInitializedLatch = serverInitializedLatch;
            }

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                IByteBuffer buf = ctx.Allocator.Buffer(_totalServerBytesWritten);
                buf.SetWriterIndex(buf.Capacity);
                ctx.WriteAndFlushAsync(buf).CloseOnComplete(ctx.Channel);
                _serverInitializedLatch.SafeSignal();
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                ctx.CloseAsync();
            }
        }

        class ChannelInboundHandlerAdapter1 : ChannelHandlerAdapter
        {
            private readonly bool _allowHalfClosed;
            private readonly bool _autoRead;
            private readonly int _totalServerBytesWritten;
            private readonly CountdownEvent _clientHalfClosedLatch;
            private readonly CountdownEvent _clientReadAllDataLatch;
            private readonly AtomicInteger _clientReadCompletes;
            private int _bytesRead;

            public ChannelInboundHandlerAdapter1(bool allowHalfClosed, bool autoRead, int totalServerBytesWritten,
                CountdownEvent clientHalfClosedLatch, CountdownEvent clientReadAllDataLatch, AtomicInteger clientReadCompletes)
            {
                _allowHalfClosed = allowHalfClosed;
                _autoRead = autoRead;
                _totalServerBytesWritten = totalServerBytesWritten;
                _clientHalfClosedLatch = clientHalfClosedLatch;
                _clientReadAllDataLatch = clientReadAllDataLatch;
                _clientReadCompletes = clientReadCompletes;
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                IByteBuffer buf = (IByteBuffer)msg;
                _bytesRead += buf.ReadableBytes;
                buf.Release();
            }

            public override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
            {
                if (evt == ChannelInputShutdownEvent.Instance && _allowHalfClosed)
                {
                    _clientHalfClosedLatch.SafeSignal();
                }
                else if (evt == ChannelInputShutdownReadComplete.Instance)
                {
                    ctx.CloseAsync();
                }
            }

            public override void ChannelInactive(IChannelHandlerContext ctx)
            {
                if (!_allowHalfClosed)
                {
                    _clientHalfClosedLatch.SafeSignal();
                }
            }

            public override void ChannelReadComplete(IChannelHandlerContext ctx)
            {
                _clientReadCompletes.Increment();
                if (_bytesRead == _totalServerBytesWritten)
                {
                    _clientReadAllDataLatch.SafeSignal();
                }
                if (!_autoRead)
                {
                    ctx.Read();
                }
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                ctx.CloseAsync();
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
