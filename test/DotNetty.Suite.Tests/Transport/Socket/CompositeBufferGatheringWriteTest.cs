namespace DotNetty.Suite.Tests.Transport.Socket
{
    using System;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using Xunit;
    using Xunit.Abstractions;

    public class CompositeBufferGatheringWriteTest : AbstractSocketTest
    {
        private const int EXPECTED_BYTES = 20;

        public CompositeBufferGatheringWriteTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSingleCompositeBufferWrite(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSingleCompositeBufferWrite0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSingleCompositeBufferWrite_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSingleCompositeBufferWrite0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSingleCompositeBufferWrite_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSingleCompositeBufferWrite0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestSingleCompositeBufferWrite_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestSingleCompositeBufferWrite0(sb, cb);
        }

        private async Task TestSingleCompositeBufferWrite0(ServerBootstrap sb, Bootstrap cb)
        {
            IChannel serverChannel = null;
            IChannel clientChannel = null;
            try
            {
                CountdownEvent latch = new CountdownEvent(1);
                AtomicReference<object> clientReceived = new AtomicReference<object>();
                sb.ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new ChannelInboundHandlerAdapter2());
                }));
                cb.Handler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new ChannelInboundHandlerAdapter3(latch, clientReceived));
                }));

                serverChannel = await sb.BindAsync();
                clientChannel = await cb.ConnectAsync(serverChannel.LocalAddress);

                IByteBuffer expected = NewCompositeBuffer(clientChannel.Allocator);
                latch.Wait();
                object received = clientReceived.Value;
                if (received is IByteBuffer actual)
                {
                    Assert.Equal(expected, actual);
                    expected.Release();
                    actual.Release();
                }
                else
                {
                    expected.Release();
                    throw (Exception)received;
                }
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

        sealed class ChannelInboundHandlerAdapter2 : ChannelHandlerAdapter
        {
            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                ctx.WriteAndFlushAsync(NewCompositeBuffer(ctx.Allocator)).CloseOnComplete(ctx.Channel);
            }
        }

        sealed class ChannelInboundHandlerAdapter3 : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _latch;
            private readonly AtomicReference<object> _clientReceived;
            private IByteBuffer _aggregator;

            public ChannelInboundHandlerAdapter3(CountdownEvent latch, AtomicReference<object> clientReceived)
            {
                _latch = latch;
                _clientReceived = clientReceived;
            }

            public override void HandlerAdded(IChannelHandlerContext ctx)
            {
                _aggregator = ctx.Allocator.Buffer(EXPECTED_BYTES);
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                try
                {
                    if (msg is IByteBuffer buf)
                    {
                        _aggregator.WriteBytes(buf);
                    }
                }
                finally
                {
                    ReferenceCountUtil.Release(msg);
                }
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                // IOException is fine as it will also close the channel and may just be a connection reset.
                if (!(cause is SocketException))
                {
                    _clientReceived.Value = cause;
                    _latch.SafeSignal();
                }
            }

            public override void ChannelInactive(IChannelHandlerContext context)
            {
                if (_clientReceived.CompareAndSet(null, _aggregator))
                {
                    try
                    {
                        Assert.Equal(EXPECTED_BYTES, _aggregator.ReadableBytes);
                    }
                    catch (Exception cause)
                    {
                        _aggregator.Release();
                        _aggregator = null;
                        _clientReceived.Value = cause;
                    }
                    finally
                    {
                        _latch.SafeSignal();
                    }
                }
            }
        }

        protected static void CompositeBufferPartialWriteDoesNotCorruptDataInitServerConfig(IChannelConfiguration config, int soSndBuf)
        {
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestCompositeBufferPartialWriteDoesNotCorruptData(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestCompositeBufferPartialWriteDoesNotCorruptData0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestCompositeBufferPartialWriteDoesNotCorruptData_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestCompositeBufferPartialWriteDoesNotCorruptData0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestCompositeBufferPartialWriteDoesNotCorruptData_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestCompositeBufferPartialWriteDoesNotCorruptData0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestCompositeBufferPartialWriteDoesNotCorruptData_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestCompositeBufferPartialWriteDoesNotCorruptData0(sb, cb);
        }

        private async Task TestCompositeBufferPartialWriteDoesNotCorruptData0(ServerBootstrap sb, Bootstrap cb)
        {
            // The scenario is the following:
            // Limit SO_SNDBUF so that a single buffer can be written, and part of a CompositeByteBuf at the same time.
            // We then write the single buffer, the CompositeByteBuf, and another single buffer and verify the data is not
            // corrupted when we read it on the other side.
            IChannel serverChannel = null;
            IChannel clientChannel = null;
            try
            {
                Random r = new Random();
                int soSndBuf = 1024;
                IByteBufferAllocator alloc = ByteBufferUtil.DefaultAllocator;
                IByteBuffer expectedContent = alloc.Buffer(soSndBuf * 2);
                expectedContent.WriteBytes(NewRandomBytes(expectedContent.WritableBytes, r));
                CountdownEvent latch = new CountdownEvent(1);
                AtomicReference<object> clientReceived = new AtomicReference<object>();
                sb.ChildOption(ChannelOption.SoSndbuf, soSndBuf)
                  .ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
                  {
                      ch.Pipeline.AddLast(new ChannelInboundHandlerAdapter0(expectedContent, latch, clientReceived, soSndBuf));
                  }));
                cb.Handler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new ChannelInboundHandlerAdapter1(expectedContent, latch, clientReceived));
                }));

                serverChannel = await sb.BindAsync();
                clientChannel = await cb.ConnectAsync(serverChannel.LocalAddress);

                latch.Wait();
                object received = clientReceived.Value;
                if (received is IByteBuffer)
                {
                    IByteBuffer actual = (IByteBuffer)received;
                    Assert.Equal(expectedContent, actual);
                    expectedContent.Release();
                    actual.Release();
                }
                else
                {
                    expectedContent.Release();
                    throw (Exception)received;
                }
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
            private readonly IByteBuffer _expectedContent;
            private readonly CountdownEvent _latch;
            private readonly AtomicReference<object> _clientReceived;
            private readonly int _soSndBuf;

            public ChannelInboundHandlerAdapter0(IByteBuffer expectedContent, CountdownEvent latch, AtomicReference<object> clientReceived, int soSndBuf)
            {
                _expectedContent = expectedContent;
                _latch = latch;
                _clientReceived = clientReceived;
                _soSndBuf = soSndBuf;
            }

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                CompositeBufferPartialWriteDoesNotCorruptDataInitServerConfig(ctx.Channel.Configuration, _soSndBuf);
                // First single write
                int offset = _soSndBuf - 100;
                ctx.WriteAsync(_expectedContent.RetainedSlice(_expectedContent.ReaderIndex, offset));

                // Build and write CompositeByteBuf
                CompositeByteBuffer compositeByteBuf = ctx.Allocator.CompositeBuffer();
                compositeByteBuf.AddComponent(true,
                        _expectedContent.RetainedSlice(_expectedContent.ReaderIndex + offset, 50));
                offset += 50;
                compositeByteBuf.AddComponent(true,
                        _expectedContent.RetainedSlice(_expectedContent.ReaderIndex + offset, 200));
                offset += 200;
                ctx.WriteAsync(compositeByteBuf);

                // Write a single buffer that is smaller than the second component of the CompositeByteBuf
                // above but small enough to fit in the remaining space allowed by the soSndBuf amount.
                ctx.WriteAsync(_expectedContent.RetainedSlice(_expectedContent.ReaderIndex + offset, 50));
                offset += 50;

                // Write the remainder of the content
                ctx.WriteAndFlushAsync(_expectedContent.RetainedSlice(_expectedContent.ReaderIndex + offset,
                        _expectedContent.ReadableBytes - _expectedContent.ReaderIndex - offset))
                        .CloseOnComplete(ctx.Channel);
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                // IOException is fine as it will also close the channel and may just be a connection reset.
                if (!(cause is SocketException))
                {
                    _clientReceived.Value = cause;
                    _latch.SafeSignal();
                }
            }
        }

        sealed class ChannelInboundHandlerAdapter1 : ChannelHandlerAdapter
        {
            private readonly IByteBuffer _expectedContent;
            private readonly CountdownEvent _latch;
            private readonly AtomicReference<object> _clientReceived;
            private IByteBuffer _aggregator;

            public ChannelInboundHandlerAdapter1(IByteBuffer expectedContent, CountdownEvent latch, AtomicReference<object> clientReceived)
            {
                _expectedContent = expectedContent;
                _latch = latch;
                _clientReceived = clientReceived;
            }

            public override void HandlerAdded(IChannelHandlerContext ctx)
            {
                _aggregator = ctx.Allocator.Buffer(_expectedContent.ReadableBytes);
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                try
                {
                    if (msg is IByteBuffer buf)
                    {
                        _aggregator.WriteBytes(buf);
                    }
                }
                finally
                {
                    ReferenceCountUtil.Release(msg);
                }
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                // IOException is fine as it will also close the channel and may just be a connection reset.
                if (!(cause is SocketException))
                {
                    _clientReceived.Value = cause;
                    _latch.SafeSignal();
                }
            }

            public override void ChannelInactive(IChannelHandlerContext ctx)
            {
                if (_clientReceived.CompareAndSet(null, _aggregator))
                {
                    try
                    {
                        Assert.Equal(_expectedContent.ReadableBytes, _aggregator.ReadableBytes);
                    }
                    catch (Exception cause)
                    {
                        _aggregator.Release();
                        _aggregator = null;
                        _clientReceived.Value = cause;
                    }
                    finally
                    {
                        _latch.SafeSignal();
                    }
                }
            }
        }

        private static IByteBuffer NewCompositeBuffer(IByteBufferAllocator alloc)
        {
            CompositeByteBuffer compositeByteBuf = alloc.CompositeBuffer();
            compositeByteBuf.AddComponent(true, alloc.DirectBuffer(4).WriteInt(100));
            compositeByteBuf.AddComponent(true, alloc.DirectBuffer(8).WriteLong(123));
            compositeByteBuf.AddComponent(true, alloc.DirectBuffer(8).WriteLong(456));
            Assert.Equal(EXPECTED_BYTES, compositeByteBuf.ReadableBytes);
            return compositeByteBuf;
        }

        private static byte[] NewRandomBytes(int size, Random r)
        {
            byte[] bytes = new byte[size];
            r.NextBytes(bytes);
            return bytes;
        }
    }
}
