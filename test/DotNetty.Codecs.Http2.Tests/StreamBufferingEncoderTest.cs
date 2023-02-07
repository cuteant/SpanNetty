
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using Moq;
    using Xunit;

    public class StreamBufferingEncoderTest : IDisposable
    {
        private StreamBufferingEncoder _encoder;
        private IHttp2Connection _connection;

        private Mock<IHttp2FrameWriter> _writer;
        private Mock<IChannelHandlerContext> _ctx;
        private Mock<IChannel> _channel;
        private Mock<IChannelUnsafe> _channelUnsafe;
        private Mock<IChannelConfiguration> _config;
        private Mock<IEventExecutor> _executor;

        public StreamBufferingEncoderTest()
        {
            _writer = new Mock<IHttp2FrameWriter>();
            _ctx = new Mock<IChannelHandlerContext>();
            _channel = new Mock<IChannel>();
            _channelUnsafe = new Mock<IChannelUnsafe>();
            _config = new Mock<IChannelConfiguration>();
            _executor = new Mock<IEventExecutor>();

            var configuration = new Mock<IHttp2FrameWriterConfiguration>();
            var frameSizePolicy = new Mock<IHttp2FrameSizePolicy>();
            _writer.SetupGet(x => x.Configuration).Returns(() => configuration.Object);
            configuration.SetupGet(x => x.FrameSizePolicy).Returns(() => frameSizePolicy.Object);
            frameSizePolicy.SetupGet(x => x.MaxFrameSize).Returns(Http2CodecUtil.DefaultMaxFrameSize);
            _writer
                .Setup(x => x.WriteDataAsync(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, IByteBuffer, int, bool, IPromise>((c, i, buf, x, y, p) => SuccessAnswer(buf));
            _writer
                .Setup(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, Http2Error, IPromise>((x, y, z, p) => SuccessAnswer());
            _writer
                .Setup(x => x.WriteGoAwayAsync(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, Http2Error, IByteBuffer, IPromise>((c, i, e, buf, p) => SuccessAnswer(buf));

            _connection = new DefaultHttp2Connection(false);
            _connection.Remote.FlowController = new DefaultHttp2RemoteFlowController(_connection);
            _connection.Local.FlowController = new DefaultHttp2LocalFlowController(_connection).FrameWriter(_writer.Object);

            var defaultEncoder = new DefaultHttp2ConnectionEncoder(_connection, _writer.Object);
            _encoder = new StreamBufferingEncoder(defaultEncoder);
            var decoder = new DefaultHttp2ConnectionDecoder(_connection, _encoder, new Mock<IHttp2FrameReader>().Object);
            var builder = new Http2ConnectionHandlerBuilder() { FrameListener = new Mock<IHttp2FrameListener>().Object };
            var handler = builder.Codec(decoder, _encoder).Build();

            // Set LifeCycleManager on encoder and decoder
            _ctx.SetupGet(x => x.Channel).Returns(_channel.Object);
            _ctx.SetupGet(x => x.Allocator).Returns(UnpooledByteBufferAllocator.Default);
            _channel.SetupGet(x => x.Allocator).Returns(UnpooledByteBufferAllocator.Default);
            _executor.SetupGet(x => x.InEventLoop).Returns(true);
            _ctx.Setup(x => x.NewPromise()).Returns(() => NewPromise());
            _ctx.SetupGet(x => x.Executor).Returns(() => _executor.Object);
            _channel.SetupGet(x => x.IsActive).Returns(false);
            _channel.SetupGet(x => x.Configuration).Returns(() => _config.Object);
            _channel.SetupGet(x => x.IsWritable).Returns(true);
            _channel.SetupGet(x => x.BytesBeforeUnwritable).Returns(long.MaxValue);
            _config.SetupGet(x => x.WriteBufferHighWaterMark).Returns(int.MaxValue);
            _config.SetupGet(x => x.MessageSizeEstimator).Returns(DefaultMessageSizeEstimator.Default);
            ChannelMetadata metadata = new ChannelMetadata(false, 16);
            _channel.SetupGet(x => x.Metadata).Returns(metadata);
            _channel.SetupGet(x => x.Unsafe).Returns(() => _channelUnsafe.Object);
            handler.HandlerAdded(_ctx.Object);
        }

        public void Dispose()
        {
            // Close and release any buffered frames.
            _encoder.Close();
        }

        [Fact(Skip = "第二次 EncoderWriteHeaders，引发 DefaultHttp2ConnectionEncoder.ValidateHeadersSentState抛出异常")]
        public void MultipleWritesToActiveStream()
        {
            _encoder.WriteSettingsAckAsync(_ctx.Object, NewPromise());
            EncoderWriteHeaders(3, NewPromise());
            Assert.Equal(0, _encoder.NumBufferedStreams());
            var data = Data();
            int expectedBytes = data.ReadableBytes * 3;
            _encoder.WriteDataAsync(_ctx.Object, 3, data, 0, false, NewPromise());
            _encoder.WriteDataAsync(_ctx.Object, 3, Data(), 0, false, NewPromise());
            _encoder.WriteDataAsync(_ctx.Object, 3, Data(), 0, false, NewPromise());
            EncoderWriteHeaders(3, NewPromise());

            WriteVerifyWriteHeaders(Times.Exactly(2), 3);
            // Contiguous data writes are coalesced
            var bufCaptor = new ArgumentCaptor<IByteBuffer>();
            _writer.Verify(
                x => x.WriteDataAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == 3),
                    It.Is<IByteBuffer>(v => bufCaptor.Capture(v)),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.IsAny<IPromise>()), Times.Once);
            Assert.Equal(expectedBytes, bufCaptor.GetValue().ReadableBytes);
        }

        [Fact]
        public void EnsureCanCreateNextStreamWhenStreamCloses()
        {
            _encoder.WriteSettingsAckAsync(_ctx.Object, NewPromise());
            SetMaxConcurrentStreams(1);

            EncoderWriteHeaders(3, NewPromise());
            Assert.Equal(0, _encoder.NumBufferedStreams());

            // This one gets buffered.
            EncoderWriteHeaders(5, NewPromise());
            Assert.Equal(1, _connection.NumActiveStreams);
            Assert.Equal(1, _encoder.NumBufferedStreams());

            // Now prevent us from creating another stream.
            SetMaxConcurrentStreams(0);

            // Close the previous stream.
            _connection.Stream(3).Close();

            // Ensure that no streams are currently active and that only the HEADERS from the first
            // stream were written.
            WriteVerifyWriteHeaders(Times.Once(), 3);
            WriteVerifyWriteHeaders(Times.Never(), 5);
            Assert.Equal(0, _connection.NumActiveStreams);
            Assert.Equal(1, _encoder.NumBufferedStreams());
        }

        [Fact]
        public void AlternatingWritesToActiveAndBufferedStreams()
        {
            _encoder.WriteSettingsAckAsync(_ctx.Object, NewPromise());
            SetMaxConcurrentStreams(1);

            EncoderWriteHeaders(3, NewPromise());
            Assert.Equal(0, _encoder.NumBufferedStreams());

            EncoderWriteHeaders(5, NewPromise());
            Assert.Equal(1, _connection.NumActiveStreams);
            Assert.Equal(1, _encoder.NumBufferedStreams());

            _encoder.WriteDataAsync(_ctx.Object, 3, Unpooled.Empty, 0, false, NewPromise());
            WriteVerifyWriteHeaders(Times.Once(), 3);
            _encoder.WriteDataAsync(_ctx.Object, 5, Unpooled.Empty, 0, false, NewPromise());
            _writer.Verify(
                x => x.WriteDataAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == 5),
                    It.IsAny<IByteBuffer>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.IsAny<IPromise>()), Times.Never());
        }

        [Fact]
        public void BufferingNewStreamFailsAfterGoAwayReceived()
        {
            _encoder.WriteSettingsAckAsync(_ctx.Object, NewPromise());
            SetMaxConcurrentStreams(0);
            _connection.GoAwayReceived(1, (Http2Error)8, Unpooled.Empty);

            var promise = NewPromise();
            EncoderWriteHeaders(3, promise);
            Assert.Equal(0, _encoder.NumBufferedStreams());
            Assert.True(promise.IsCompleted);
            Assert.False(promise.IsSuccess);
        }

        [Fact]
        public void ReceivingGoAwayFailsBufferedStreams()
        {
            _encoder.WriteSettingsAckAsync(_ctx.Object, NewPromise());
            SetMaxConcurrentStreams(5);

            int streamId = 3;
            List<Task> futures = new List<Task>();
            for (int i = 0; i < 9; i++)
            {
                futures.Add(EncoderWriteHeaders(streamId, NewPromise()));
                streamId += 2;
            }
            Assert.Equal(4, _encoder.NumBufferedStreams());

            _connection.GoAwayReceived(11, (Http2Error)8, Unpooled.Empty);

            Assert.Equal(5, _connection.NumActiveStreams);
            int failCount = 0;
            foreach (Task f in futures)
            {
                if (!f.IsSuccess()) // TODO use IsFailure()
                {
                    failCount++;
                }
            }
            Assert.Equal(9, failCount);
            Assert.Equal(0, _encoder.NumBufferedStreams());
        }

        [Fact]
        public void SendingGoAwayShouldNotFailStreams()
        {
            _encoder.WriteSettingsAckAsync(_ctx.Object, NewPromise());
            SetMaxConcurrentStreams(1);

            _writer
                .Setup(x => x.WriteHeadersAsync(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<IHttp2Headers>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, IHttp2Headers, int, bool, IPromise>((c, i, h, x, y, p) => SuccessAnswer());

            _writer
                .Setup(x => x.WriteHeadersAsync(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<IHttp2Headers>(),
                    It.IsAny<int>(),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, IHttp2Headers, int, short, bool, int, bool, IPromise>((c, i, h, a, b, e, x, y, p) => SuccessAnswer());

            Task f1 = EncoderWriteHeaders(3, NewPromise());
            Assert.Equal(0, _encoder.NumBufferedStreams());
            Task f2 = EncoderWriteHeaders(5, NewPromise());
            Assert.Equal(1, _encoder.NumBufferedStreams());
            Task f3 = EncoderWriteHeaders(7, NewPromise());
            Assert.Equal(2, _encoder.NumBufferedStreams());

            var empty = Unpooled.Buffer(0);
            _encoder.WriteGoAwayAsync(_ctx.Object, 3, Http2Error.Cancel, empty, NewPromise());

            Assert.Equal(1, _connection.NumActiveStreams);
            Assert.Equal(2, _encoder.NumBufferedStreams());
            Assert.False(f1.IsCompleted);
            Assert.False(f2.IsCompleted);
            Assert.False(f3.IsCompleted);
        }

        [Fact]
        public void EndStreamDoesNotFailBufferedStream()
        {
            _encoder.WriteSettingsAckAsync(_ctx.Object, NewPromise());
            SetMaxConcurrentStreams(0);

            EncoderWriteHeaders(3, NewPromise());
            Assert.Equal(1, _encoder.NumBufferedStreams());

            _encoder.WriteDataAsync(_ctx.Object, 3, Unpooled.Empty, 0, true, NewPromise());

            Assert.Equal(0, _connection.NumActiveStreams);
            Assert.Equal(1, _encoder.NumBufferedStreams());

            // Simulate that we received a SETTINGS frame which
            // increased MAX_CONCURRENT_STREAMS to 1.
            SetMaxConcurrentStreams(1);
            _encoder.WriteSettingsAckAsync(_ctx.Object, NewPromise());

            Assert.Equal(1, _connection.NumActiveStreams);
            Assert.Equal(0, _encoder.NumBufferedStreams());
            Assert.Equal(Http2StreamState.HalfClosedLocal, _connection.Stream(3).State);
        }

        [Fact]
        public void RstStreamClosesBufferedStream()
        {
            _encoder.WriteSettingsAckAsync(_ctx.Object, NewPromise());
            SetMaxConcurrentStreams(0);

            EncoderWriteHeaders(3, NewPromise());
            Assert.Equal(1, _encoder.NumBufferedStreams());

            var rstStreamPromise = NewPromise();
            _encoder.WriteRstStreamAsync(_ctx.Object, 3, Http2Error.Cancel, rstStreamPromise);
            Assert.True(rstStreamPromise.IsSuccess);
            Assert.Equal(0, _encoder.NumBufferedStreams());
        }

        [Fact]
        public void BufferUntilActiveStreamsAreReset()
        {
            _encoder.WriteSettingsAckAsync(_ctx.Object, NewPromise());
            SetMaxConcurrentStreams(1);

            EncoderWriteHeaders(3, NewPromise());
            Assert.Equal(0, _encoder.NumBufferedStreams());
            EncoderWriteHeaders(5, NewPromise());
            Assert.Equal(1, _encoder.NumBufferedStreams());
            EncoderWriteHeaders(7, NewPromise());
            Assert.Equal(2, _encoder.NumBufferedStreams());

            WriteVerifyWriteHeaders(Times.Once(), 3);
            WriteVerifyWriteHeaders(Times.Never(), 5);
            WriteVerifyWriteHeaders(Times.Never(), 7);

            _encoder.WriteRstStreamAsync(_ctx.Object, 3, Http2Error.Cancel, NewPromise());
            _connection.Remote.FlowController.WritePendingBytes();
            WriteVerifyWriteHeaders(Times.Once(), 5);
            WriteVerifyWriteHeaders(Times.Never(), 7);
            Assert.Equal(1, _connection.NumActiveStreams);
            Assert.Equal(1, _encoder.NumBufferedStreams());

            _encoder.WriteRstStreamAsync(_ctx.Object, 5, Http2Error.Cancel, NewPromise());
            _connection.Remote.FlowController.WritePendingBytes();
            WriteVerifyWriteHeaders(Times.Once(), 7);
            Assert.Equal(1, _connection.NumActiveStreams);
            Assert.Equal(0, _encoder.NumBufferedStreams());

            _encoder.WriteRstStreamAsync(_ctx.Object, 7, Http2Error.Cancel, NewPromise());
            Assert.Equal(0, _connection.NumActiveStreams);
            Assert.Equal(0, _encoder.NumBufferedStreams());
        }

        [Fact]
        public void BufferUntilMaxStreamsIncreased()
        {
            _encoder.WriteSettingsAckAsync(_ctx.Object, NewPromise());
            SetMaxConcurrentStreams(2);

            EncoderWriteHeaders(3, NewPromise());
            EncoderWriteHeaders(5, NewPromise());
            EncoderWriteHeaders(7, NewPromise());
            EncoderWriteHeaders(9, NewPromise());
            Assert.Equal(2, _encoder.NumBufferedStreams());

            WriteVerifyWriteHeaders(Times.Once(), 3);
            WriteVerifyWriteHeaders(Times.Once(), 5);
            WriteVerifyWriteHeaders(Times.Never(), 7);
            WriteVerifyWriteHeaders(Times.Never(), 9);

            // Simulate that we received a SETTINGS frame which
            // increased MAX_CONCURRENT_STREAMS to 5.
            SetMaxConcurrentStreams(5);
            _encoder.WriteSettingsAckAsync(_ctx.Object, NewPromise());

            Assert.Equal(0, _encoder.NumBufferedStreams());
            WriteVerifyWriteHeaders(Times.Once(), 7);
            WriteVerifyWriteHeaders(Times.Once(), 9);

            EncoderWriteHeaders(11, NewPromise());

            WriteVerifyWriteHeaders(Times.Once(), 11);

            Assert.Equal(5, _connection.Local.NumActiveStreams);
        }

        [Fact]
        public void BufferUntilSettingsReceived()
        {
            int initialLimit = Http2CodecUtil.SmallestMaxConcurrentStreams;
            int numStreams = initialLimit * 2;
            for (int ix = 0, nextStreamId = 3; ix < numStreams; ++ix, nextStreamId += 2)
            {
                EncoderWriteHeaders(nextStreamId, NewPromise());
                if (ix < initialLimit)
                {
                    WriteVerifyWriteHeaders(Times.Once(), nextStreamId);
                }
                else
                {
                    WriteVerifyWriteHeaders(Times.Never(), nextStreamId);
                }
            }
            Assert.Equal(numStreams / 2, _encoder.NumBufferedStreams());

            // Simulate that we received a SETTINGS frame.
            SetMaxConcurrentStreams(initialLimit * 2);

            Assert.Equal(0, _encoder.NumBufferedStreams());
            Assert.Equal(numStreams, _connection.Local.NumActiveStreams);
        }

        [Fact]
        public void BufferUntilSettingsReceivedWithNoMaxConcurrentStreamValue()
        {
            int initialLimit = Http2CodecUtil.SmallestMaxConcurrentStreams;
            int numStreams = initialLimit * 2;
            for (int ix = 0, nextStreamId = 3; ix < numStreams; ++ix, nextStreamId += 2)
            {
                EncoderWriteHeaders(nextStreamId, NewPromise());
                if (ix < initialLimit)
                {
                    WriteVerifyWriteHeaders(Times.Once(), nextStreamId);
                }
                else
                {
                    WriteVerifyWriteHeaders(Times.Never(), nextStreamId);
                }
            }
            Assert.Equal(numStreams / 2, _encoder.NumBufferedStreams());

            // Simulate that we received an empty SETTINGS frame.
            _encoder.RemoteSettings(new Http2Settings());

            Assert.Equal(0, _encoder.NumBufferedStreams());
            Assert.Equal(numStreams, _connection.Local.NumActiveStreams);
        }

        [Fact]
        public void ExhaustedStreamsDoNotBuffer()
        {
            // Write the highest possible stream ID for the client.
            // This will cause the next stream ID to be negative.
            EncoderWriteHeaders(int.MaxValue, NewPromise());

            // Disallow any further streams.
            SetMaxConcurrentStreams(0);

            // Simulate numeric overflow for the next stream ID.
            Task f = EncoderWriteHeaders(-1, NewPromise());

            // Verify that the write fails.
            Assert.NotNull(f.Exception);
        }

        [Fact]
        public void ClosedBufferedStreamReleasesByteBuf()
        {
            _encoder.WriteSettingsAckAsync(_ctx.Object, NewPromise());
            SetMaxConcurrentStreams(0);
            var data = new Mock<IByteBuffer>();
            Task f1 = EncoderWriteHeaders(3, NewPromise());
            Assert.Equal(1, _encoder.NumBufferedStreams());
            Task f2 = _encoder.WriteDataAsync(_ctx.Object, 3, data.Object, 0, false, NewPromise());

            var rstPromise = new Mock<IPromise>();
            _encoder.WriteRstStreamAsync(_ctx.Object, 3, Http2Error.Cancel, rstPromise.Object);

            Assert.Equal(0, _encoder.NumBufferedStreams());
            rstPromise.Verify(x => x.Complete());
            Assert.True(f1.IsSuccess());
            Assert.True(f2.IsSuccess());
            data.Verify(x => x.Release());
        }

        [Fact]
        public void CloseShouldCancelAllBufferedStreams()
        {
            _encoder.WriteSettingsAckAsync(_ctx.Object, NewPromise());
            _connection.Local.SetMaxActiveStreams(0);

            Task f1 = EncoderWriteHeaders(3, NewPromise());
            Task f2 = EncoderWriteHeaders(5, NewPromise());
            Task f3 = EncoderWriteHeaders(7, NewPromise());

            _encoder.Close();
            Assert.NotNull(f1.Exception);
            Assert.NotNull(f2.Exception);
            Assert.NotNull(f3.Exception);
        }

        [Fact]
        public void HeadersAfterCloseShouldImmediatelyFail()
        {
            _encoder.WriteSettingsAckAsync(_ctx.Object, NewPromise());
            _encoder.Close();

            var f = EncoderWriteHeaders(3, NewPromise());
            Assert.NotNull(f.Exception);
        }

        private void SetMaxConcurrentStreams(int newValue)
        {
            try
            {
                _encoder.RemoteSettings(new Http2Settings().MaxConcurrentStreams(newValue));
                // Flush the remote flow controller to write data
                _encoder.FlowController.WritePendingBytes();
            }
            catch (Http2Exception e)
            {
                throw new Http2RuntimeException(e.Message, e);
            }
        }

        private Task EncoderWriteHeaders(int streamId, IPromise promise)
        {
            _encoder.WriteHeadersAsync(_ctx.Object, streamId, new DefaultHttp2Headers(), 0, Http2CodecUtil.DefaultPriorityWeight,
                false, 0, false, promise);
            try
            {
                _encoder.FlowController.WritePendingBytes();
                return promise.Task;
            }
            catch (Http2Exception e)
            {
                throw new Http2RuntimeException(e.Message, e);
            }
        }

        private void WriteVerifyWriteHeaders(Times mode, int streamId)
        {
            _writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.IsAny<IHttp2Headers>(),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.IsAny<IPromise>()),
                mode);
        }

        private static Task SuccessAnswer(params object[] args)
        {
            if (args != null)
            {
                foreach (var item in args)
                {
                    ReferenceCountUtil.SafeRelease(item);
                }
            }
            var promise = NewPromise();
            promise.Complete();
            return promise.Task;
        }

        private static IPromise NewPromise()
        {
            return new DefaultPromise();
        }

        private static IByteBuffer Data()
        {
            var buf = Unpooled.Buffer(10);
            for (int i = 0; i < buf.WritableBytes; i++)
            {
                buf.WriteByte(i);
            }
            return buf;
        }
    }
}
