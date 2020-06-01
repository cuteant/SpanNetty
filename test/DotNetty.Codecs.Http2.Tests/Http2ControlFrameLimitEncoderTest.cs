
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using Moq;
    using Xunit;

    public class Http2ControlFrameLimitEncoderTest : IDisposable
    {
        private readonly Http2ControlFrameLimitEncoder _encoder;
        private readonly Mock<IHttp2FrameWriter> _writer;
        private readonly Mock<IChannelHandlerContext> _ctx;
        private readonly Mock<IChannel> _channel;
        private readonly Mock<IChannelUnsafe> _unsafe;
        private readonly Mock<IChannelConfiguration> _config;
        private readonly Mock<IEventExecutor> _executor;
        private int _numWrites;
        private Deque<IPromise> _goAwayPromises = new Deque<IPromise>();

        public Http2ControlFrameLimitEncoderTest()
        {
            _writer = new Mock<IHttp2FrameWriter>();
            _ctx = new Mock<IChannelHandlerContext>();
            _channel = new Mock<IChannel>();
            _unsafe = new Mock<IChannelUnsafe>();
            _config = new Mock<IChannelConfiguration>();
            _executor = new Mock<IEventExecutor>();

            _numWrites = 0;

            var configuration = new Mock<IHttp2FrameWriterConfiguration>();
            var frameSizePolicy = new Mock<IHttp2FrameSizePolicy>();
            _writer.SetupGet(x => x.Configuration).Returns(configuration.Object);
            configuration.SetupGet(x => x.FrameSizePolicy).Returns(frameSizePolicy.Object);
            frameSizePolicy.SetupGet(x => x.MaxFrameSize).Returns(Http2CodecUtil.DefaultMaxFrameSize);

            _writer
                .Setup(x => x.WriteRstStreamAsync(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, Http2Error, IPromise>((ctx, streamId, errorCode, p) =>
                {
                    return HandlePromise(p, 3).Task;
                });
            _writer
                .Setup(x => x.WriteSettingsAckAsync(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, IPromise>((ctx, p) =>
                {
                    return HandlePromise(p, 1).Task;
                });
            _writer
                .Setup(x => x.WritePingAsync(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<bool>(),
                    It.IsAny<long>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, bool, long, IPromise>((ctx, ack, data, p) =>
                {
                    var promise = HandlePromise(p, 3);
                    if (ack == false)
                    {
                        promise.TryComplete();
                    }
                    return promise.Task;
                });
            _writer
                .Setup(x => x.WriteGoAwayAsync(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, Http2Error, IByteBuffer, IPromise>((ctx, streamId, errCode, debugData, p) =>
                {
                    ReferenceCountUtil.Release(debugData);
                    _goAwayPromises.AddToBack(p);
                    return p.Task;
                });
            IHttp2Connection connection = new DefaultHttp2Connection(false);
            connection.Remote.FlowController = new DefaultHttp2RemoteFlowController(connection);
            connection.Local.FlowController = new DefaultHttp2LocalFlowController(connection).FrameWriter(_writer.Object);

            DefaultHttp2ConnectionEncoder defaultEncoder =
                    new DefaultHttp2ConnectionEncoder(connection, _writer.Object);
            _encoder = new Http2ControlFrameLimitEncoder(defaultEncoder, 2);
            DefaultHttp2ConnectionDecoder decoder =
                    new DefaultHttp2ConnectionDecoder(connection, _encoder, (new Mock<IHttp2FrameReader>()).Object);
            var builder = new Http2ConnectionHandlerBuilder();
            builder.FrameListener = (new Mock<IHttp2FrameListener>()).Object;
            Http2ConnectionHandler handler = builder.Codec(decoder, _encoder).Build();

            // Set LifeCycleManager on _encoder and decoder
            _ctx.SetupGet(x => x.Channel).Returns(_channel.Object);
            _ctx.SetupGet(x => x.Allocator).Returns(UnpooledByteBufferAllocator.Default);
            _channel.SetupGet(x => x.Allocator).Returns(UnpooledByteBufferAllocator.Default);
            _executor.SetupGet(x => x.InEventLoop).Returns(true);
            _ctx.Setup(x => x.NewPromise()).Returns(() => NewPromise());
            _ctx.SetupGet(x => x.Executor).Returns(_executor.Object);
            _channel.SetupGet(x => x.Active).Returns(false);
            _channel.SetupGet(x => x.Configuration).Returns(_config.Object);
            _channel.SetupGet(x => x.IsWritable).Returns(true);
            _channel.SetupGet(x => x.BytesBeforeUnwritable).Returns(long.MaxValue);
            _config.SetupGet(x => x.WriteBufferHighWaterMark).Returns(int.MaxValue);
            _config.SetupGet(x => x.MessageSizeEstimator).Returns(DefaultMessageSizeEstimator.Default);
            ChannelMetadata metadata = new ChannelMetadata(false, 16);
            _channel.SetupGet(x => x.Metadata).Returns(metadata);
            _channel.SetupGet(x => x.Unsafe).Returns(_unsafe.Object);
            handler.HandlerAdded(_ctx.Object);
        }

        public void Dispose()
        {
            // Close and release any buffered frames.
            _encoder.Close();

            // Notify all goAway ChannelPromise instances now as these will also release the retained ByteBuf for the
            // debugData.
            while (_goAwayPromises.TryRemoveFromFront(out var p))
            {
                p.Complete();
            }
        }

        [Fact]
        public void TestLimitSettingsAck()
        {
            var ctx = _ctx.Object;
            Assert.False(_encoder.WriteSettingsAckAsync(ctx, NewPromise()).IsCompleted);
            // The second write is always marked as success by our mock, which means it will also not be queued and so
            // not count to the number of queued frames.
            Assert.True(_encoder.WriteSettingsAckAsync(ctx, NewPromise()).IsSuccess());
            Assert.False(_encoder.WriteSettingsAckAsync(ctx, NewPromise()).IsCompleted);

            VerifyFlushAndClose(0, false);

            Assert.False(_encoder.WriteSettingsAckAsync(ctx, NewPromise()).IsCompleted);
            Assert.False(_encoder.WriteSettingsAckAsync(ctx, NewPromise()).IsCompleted);

            VerifyFlushAndClose(1, true);
        }

        [Fact]
        public void TestLimitPingAck()
        {
            var ctx = _ctx.Object;
            Assert.False(_encoder.WritePingAsync(ctx, true, 8, NewPromise()).IsCompleted);
            // The second write is always marked as success by our mock, which means it will also not be queued and so
            // not count to the number of queued frames.
            Assert.True(_encoder.WritePingAsync(ctx, true, 8, NewPromise()).IsSuccess());
            Assert.False(_encoder.WritePingAsync(ctx, true, 8, NewPromise()).IsCompleted);

            VerifyFlushAndClose(0, false);

            Assert.False(_encoder.WritePingAsync(ctx, true, 8, NewPromise()).IsCompleted);
            Assert.False(_encoder.WritePingAsync(ctx, true, 8, NewPromise()).IsCompleted);

            VerifyFlushAndClose(1, true);
        }

        [Fact]
        public void TestNotLimitPing()
        {
            var ctx = _ctx.Object;
            Assert.True(_encoder.WritePingAsync(ctx, false, 8, NewPromise()).IsSuccess());
            Assert.True(_encoder.WritePingAsync(ctx, false, 8, NewPromise()).IsSuccess());
            Assert.True(_encoder.WritePingAsync(ctx, false, 8, NewPromise()).IsSuccess());
            Assert.True(_encoder.WritePingAsync(ctx, false, 8, NewPromise()).IsSuccess());

            VerifyFlushAndClose(0, false);
        }

        [Fact]
        public void TestLimitRst()
        {
            var ctx = _ctx.Object;
            Assert.False(_encoder.WriteRstStreamAsync(ctx, 1, Http2Error.Cancel, NewPromise()).IsCompleted);
            // The second write is always marked as success by our mock, which means it will also not be queued and so
            // not count to the number of queued frames.
            Assert.True(_encoder.WriteRstStreamAsync(ctx, 1, Http2Error.Cancel, NewPromise()).IsSuccess());
            Assert.False(_encoder.WriteRstStreamAsync(ctx, 1, Http2Error.Cancel, NewPromise()).IsCompleted);

            VerifyFlushAndClose(0, false);

            Assert.False(_encoder.WriteRstStreamAsync(ctx, 1, Http2Error.Cancel, NewPromise()).IsCompleted);
            Assert.False(_encoder.WriteRstStreamAsync(ctx, 1, Http2Error.Cancel, NewPromise()).IsCompleted);

            VerifyFlushAndClose(1, true);
        }

        [Fact]
        public void TestLimit()
        {
            var ctx = _ctx.Object;
            Assert.False(_encoder.WriteRstStreamAsync(ctx, 1, Http2Error.Cancel, NewPromise()).IsCompleted);
            // The second write is always marked as success by our mock, which means it will also not be queued and so
            // not count to the number of queued frames.
            Assert.True(_encoder.WritePingAsync(ctx, false, 8, NewPromise()).IsSuccess());
            Assert.False(_encoder.WritePingAsync(ctx, true, 8, NewPromise()).IsSuccess());

            VerifyFlushAndClose(0, false);

            Assert.False(_encoder.WriteSettingsAckAsync(ctx, NewPromise()).IsCompleted);
            Assert.False(_encoder.WriteRstStreamAsync(ctx, 1, Http2Error.Cancel, NewPromise()).IsCompleted);
            Assert.False(_encoder.WritePingAsync(ctx, true, 8, NewPromise()).IsSuccess());

            VerifyFlushAndClose(1, true);
        }

        private IPromise HandlePromise(IPromise promise, int promiseIdx)
        {
            if (++_numWrites == 2)
            {
                promise.Complete();
            }
            return promise;
        }

        private void VerifyFlushAndClose(int invocations, bool failed)
        {
            _ctx.Verify(
                x => x.Flush(),
                invocations <= 0 ? Times.Never() : Times.AtLeast(invocations));
            _ctx.Verify(
                x => x.CloseAsync(),
                invocations <= 0 ? Times.Never() : Times.AtLeast(invocations));
            if (failed)
            {
                _writer.Verify(
                    x => x.WriteGoAwayAsync(
                        It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                        It.Is<int>(v => v == 0),
                        It.Is<Http2Error>(v => v == Http2Error.EnhanceYourCalm),
                        It.IsAny<IByteBuffer>(),
                        It.IsAny<IPromise>()),
                    Times.Once);
            }
        }

        private IPromise NewPromise()
        {
            return new TaskCompletionSource();
        }
    }
}
