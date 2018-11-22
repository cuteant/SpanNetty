
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
        private StreamBufferingEncoder encoder;
        private IHttp2Connection connection;

        private Mock<IHttp2FrameWriter> writer;
        private Mock<IChannelHandlerContext> ctx;
        private Mock<IChannel> channel;
        private Mock<IChannelUnsafe> channelUnsafe;
        private Mock<IChannelConfiguration> config;
        private Mock<IEventExecutor> executor;

        public StreamBufferingEncoderTest()
        {
            this.writer = new Mock<IHttp2FrameWriter>();
            this.ctx = new Mock<IChannelHandlerContext>();
            this.channel = new Mock<IChannel>();
            this.channelUnsafe = new Mock<IChannelUnsafe>();
            this.config = new Mock<IChannelConfiguration>();
            this.executor = new Mock<IEventExecutor>();

            var configuration = new Mock<IHttp2FrameWriterConfiguration>();
            var frameSizePolicy = new Mock<IHttp2FrameSizePolicy>();
            this.writer.Setup(x => x.Configuration).Returns(configuration.Object);
            configuration.Setup(x => x.FrameSizePolicy).Returns(frameSizePolicy.Object);
            frameSizePolicy.Setup(x => x.MaxFrameSize).Returns(Http2CodecUtil.DefaultMaxFrameSize);
            this.writer
                .Setup(x => x.WriteDataAsync(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, IByteBuffer, int, bool, IPromise>((c, i, buf, x, y, p) => SuccessAnswer(buf));
            this.writer
                .Setup(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, Http2Error, IPromise>((x, y, z, p) => SuccessAnswer());
            this.writer
                .Setup(x => x.WriteGoAwayAsync(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, Http2Error, IByteBuffer, IPromise>((c, i, e, buf, p) => SuccessAnswer(buf));

            this.connection = new DefaultHttp2Connection(false);
            this.connection.Remote.FlowController = new DefaultHttp2RemoteFlowController(this.connection);
            this.connection.Local.FlowController = new DefaultHttp2LocalFlowController(this.connection).FrameWriter(this.writer.Object);

            var defaultEncoder = new DefaultHttp2ConnectionEncoder(this.connection, this.writer.Object);
            this.encoder = new StreamBufferingEncoder(defaultEncoder);
            var decoder = new DefaultHttp2ConnectionDecoder(this.connection, this.encoder, new Mock<IHttp2FrameReader>().Object);
            var builder = new Http2ConnectionHandlerBuilder() { FrameListener = new Mock<IHttp2FrameListener>().Object };
            var handler = builder.Codec(decoder, this.encoder).Build();

            // Set LifeCycleManager on encoder and decoder
            this.ctx.Setup(x => x.Channel).Returns(this.channel.Object);
            this.ctx.Setup(x => x.Allocator).Returns(UnpooledByteBufferAllocator.Default);
            this.channel.Setup(x => x.Allocator).Returns(UnpooledByteBufferAllocator.Default);
            this.executor.Setup(x => x.InEventLoop).Returns(true);
            this.ctx.Setup(x => x.NewPromise()).Returns(() => NewPromise());
            this.ctx.Setup(x => x.Executor).Returns(this.executor.Object);
            this.channel.Setup(x => x.Active).Returns(false);
            this.channel.Setup(x => x.Configuration).Returns(this.config.Object);
            this.channel.Setup(x => x.IsWritable).Returns(true);
            this.channel.Setup(x => x.BytesBeforeUnwritable).Returns(long.MaxValue);
            this.config.Setup(x => x.WriteBufferHighWaterMark).Returns(int.MaxValue);
            this.config.Setup(x => x.MessageSizeEstimator).Returns(DefaultMessageSizeEstimator.Default);
            ChannelMetadata metadata = new ChannelMetadata(false, 16);
            this.channel.Setup(x => x.Metadata).Returns(metadata);
            this.channel.Setup(x => x.Unsafe).Returns(this.channelUnsafe.Object);
            handler.HandlerAdded(this.ctx.Object);
        }

        public void Dispose()
        {
            // Close and release any buffered frames.
            this.encoder.Close();
        }

        [Fact(Skip = "not yet supported")]
        public void MultipleWritesToActiveStream()
        {
            this.encoder.WriteSettingsAckAsync(this.ctx.Object, NewPromise());
            this.EncoderWriteHeaders(3, NewPromise());
            Assert.Equal(0, this.encoder.NumBufferedStreams());
            var data = Data();
            int expectedBytes = data.ReadableBytes * 3;
            this.encoder.WriteDataAsync(this.ctx.Object, 3, data, 0, false, NewPromise());
            this.encoder.WriteDataAsync(this.ctx.Object, 3, Data(), 0, false, NewPromise());
            this.encoder.WriteDataAsync(this.ctx.Object, 3, Data(), 0, false, NewPromise());
            this.EncoderWriteHeaders(3, NewPromise());

            this.WriteVerifyWriteHeaders(Times.Exactly(2), 3);
            // Contiguous data writes are coalesced
            var bufCaptor = new ArgumentCaptor<IByteBuffer>();
            this.writer.Verify(
                x => x.WriteDataAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
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
            this.encoder.WriteSettingsAckAsync(this.ctx.Object, NewPromise());
            this.SetMaxConcurrentStreams(1);

            this.EncoderWriteHeaders(3, NewPromise());
            Assert.Equal(0, this.encoder.NumBufferedStreams());

            // This one gets buffered.
            this.EncoderWriteHeaders(5, NewPromise());
            Assert.Equal(1, this.connection.NumActiveStreams);
            Assert.Equal(1, this.encoder.NumBufferedStreams());

            // Now prevent us from creating another stream.
            this.SetMaxConcurrentStreams(0);

            // Close the previous stream.
            this.connection.Stream(3).Close();

            // Ensure that no streams are currently active and that only the HEADERS from the first
            // stream were written.
            this.WriteVerifyWriteHeaders(Times.Once(), 3);
            this.WriteVerifyWriteHeaders(Times.Never(), 5);
            Assert.Equal(0, this.connection.NumActiveStreams);
            Assert.Equal(1, this.encoder.NumBufferedStreams());
        }

        [Fact]
        public void AlternatingWritesToActiveAndBufferedStreams()
        {
            this.encoder.WriteSettingsAckAsync(this.ctx.Object, NewPromise());
            this.SetMaxConcurrentStreams(1);

            this.EncoderWriteHeaders(3, NewPromise());
            Assert.Equal(0, this.encoder.NumBufferedStreams());

            this.EncoderWriteHeaders(5, NewPromise());
            Assert.Equal(1, this.connection.NumActiveStreams);
            Assert.Equal(1, this.encoder.NumBufferedStreams());

            this.encoder.WriteDataAsync(this.ctx.Object, 3, Unpooled.Empty, 0, false, NewPromise());
            this.WriteVerifyWriteHeaders(Times.Once(), 3);
            this.encoder.WriteDataAsync(this.ctx.Object, 5, Unpooled.Empty, 0, false, NewPromise());
            this.writer.Verify(
                x => x.WriteDataAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == 5),
                    It.IsAny<IByteBuffer>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.IsAny<IPromise>()), Times.Never());
        }

        [Fact]
        public void BufferingNewStreamFailsAfterGoAwayReceived()
        {
            this.encoder.WriteSettingsAckAsync(this.ctx.Object, NewPromise());
            this.SetMaxConcurrentStreams(0);
            this.connection.GoAwayReceived(1, (Http2Error)8, Unpooled.Empty);

            var promise = NewPromise();
            this.EncoderWriteHeaders(3, promise);
            Assert.Equal(0, this.encoder.NumBufferedStreams());
            Assert.True(promise.IsCompleted);
            Assert.False(promise.IsSuccess);
        }

        [Fact]
        public void ReceivingGoAwayFailsBufferedStreams()
        {
            this.encoder.WriteSettingsAckAsync(this.ctx.Object, NewPromise());
            this.SetMaxConcurrentStreams(5);

            int streamId = 3;
            List<Task> futures = new List<Task>();
            for (int i = 0; i < 9; i++)
            {
                futures.Add(this.EncoderWriteHeaders(streamId, NewPromise()));
                streamId += 2;
            }
            Assert.Equal(4, this.encoder.NumBufferedStreams());

            this.connection.GoAwayReceived(11, (Http2Error)8, Unpooled.Empty);

            Assert.Equal(5, this.connection.NumActiveStreams);
            int failCount = 0;
            foreach (Task f in futures)
            {
                if (!f.IsSuccess())
                {
                    failCount++;
                }
            }
            Assert.Equal(9, failCount);
            Assert.Equal(0, this.encoder.NumBufferedStreams());
        }

        [Fact]
        public void SendingGoAwayShouldNotFailStreams()
        {
            this.encoder.WriteSettingsAckAsync(this.ctx.Object, NewPromise());
            this.SetMaxConcurrentStreams(1);

            this.writer
                .Setup(x => x.WriteHeadersAsync(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<IHttp2Headers>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, IHttp2Headers, int, bool, IPromise>((c, i, h, x, y, p) => SuccessAnswer());

            this.writer
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

            Task f1 = this.EncoderWriteHeaders(3, NewPromise());
            Assert.Equal(0, this.encoder.NumBufferedStreams());
            Task f2 = this.EncoderWriteHeaders(5, NewPromise());
            Assert.Equal(1, this.encoder.NumBufferedStreams());
            Task f3 = this.EncoderWriteHeaders(7, NewPromise());
            Assert.Equal(2, this.encoder.NumBufferedStreams());

            var empty = Unpooled.Buffer(0);
            this.encoder.WriteGoAwayAsync(this.ctx.Object, 3, Http2Error.Cancel, empty, NewPromise());

            Assert.Equal(1, this.connection.NumActiveStreams);
            Assert.Equal(2, this.encoder.NumBufferedStreams());
            Assert.False(f1.IsCompleted);
            Assert.False(f2.IsCompleted);
            Assert.False(f3.IsCompleted);
        }

        [Fact]
        public void EndStreamDoesNotFailBufferedStream()
        {
            this.encoder.WriteSettingsAckAsync(this.ctx.Object, NewPromise());
            this.SetMaxConcurrentStreams(0);

            this.EncoderWriteHeaders(3, NewPromise());
            Assert.Equal(1, this.encoder.NumBufferedStreams());

            this.encoder.WriteDataAsync(this.ctx.Object, 3, Unpooled.Empty, 0, true, NewPromise());

            Assert.Equal(0, this.connection.NumActiveStreams);
            Assert.Equal(1, this.encoder.NumBufferedStreams());

            // Simulate that we received a SETTINGS frame which
            // increased MAX_CONCURRENT_STREAMS to 1.
            this.SetMaxConcurrentStreams(1);
            this.encoder.WriteSettingsAckAsync(this.ctx.Object, NewPromise());

            Assert.Equal(1, this.connection.NumActiveStreams);
            Assert.Equal(0, this.encoder.NumBufferedStreams());
            Assert.Equal(Http2StreamState.HalfClosedLocal, this.connection.Stream(3).State);
        }

        [Fact]
        public void RstStreamClosesBufferedStream()
        {
            this.encoder.WriteSettingsAckAsync(this.ctx.Object, NewPromise());
            this.SetMaxConcurrentStreams(0);

            this.EncoderWriteHeaders(3, NewPromise());
            Assert.Equal(1, this.encoder.NumBufferedStreams());

            var rstStreamPromise = NewPromise();
            this.encoder.WriteRstStreamAsync(this.ctx.Object, 3, Http2Error.Cancel, rstStreamPromise);
            Assert.True(rstStreamPromise.IsSuccess);
            Assert.Equal(0, this.encoder.NumBufferedStreams());
        }

        [Fact]
        public void BufferUntilActiveStreamsAreReset()
        {
            this.encoder.WriteSettingsAckAsync(this.ctx.Object, NewPromise());
            this.SetMaxConcurrentStreams(1);

            this.EncoderWriteHeaders(3, NewPromise());
            Assert.Equal(0, this.encoder.NumBufferedStreams());
            this.EncoderWriteHeaders(5, NewPromise());
            Assert.Equal(1, this.encoder.NumBufferedStreams());
            this.EncoderWriteHeaders(7, NewPromise());
            Assert.Equal(2, this.encoder.NumBufferedStreams());

            this.WriteVerifyWriteHeaders(Times.Once(), 3);
            this.WriteVerifyWriteHeaders(Times.Never(), 5);
            this.WriteVerifyWriteHeaders(Times.Never(), 7);

            this.encoder.WriteRstStreamAsync(this.ctx.Object, 3, Http2Error.Cancel, NewPromise());
            this.connection.Remote.FlowController.WritePendingBytes();
            this.WriteVerifyWriteHeaders(Times.Once(), 5);
            this.WriteVerifyWriteHeaders(Times.Never(), 7);
            Assert.Equal(1, this.connection.NumActiveStreams);
            Assert.Equal(1, this.encoder.NumBufferedStreams());

            this.encoder.WriteRstStreamAsync(this.ctx.Object, 5, Http2Error.Cancel, NewPromise());
            this.connection.Remote.FlowController.WritePendingBytes();
            this.WriteVerifyWriteHeaders(Times.Once(), 7);
            Assert.Equal(1, this.connection.NumActiveStreams);
            Assert.Equal(0, this.encoder.NumBufferedStreams());

            this.encoder.WriteRstStreamAsync(this.ctx.Object, 7, Http2Error.Cancel, NewPromise());
            Assert.Equal(0, this.connection.NumActiveStreams);
            Assert.Equal(0, this.encoder.NumBufferedStreams());
        }

        [Fact]
        public void BufferUntilMaxStreamsIncreased()
        {
            this.encoder.WriteSettingsAckAsync(this.ctx.Object, NewPromise());
            this.SetMaxConcurrentStreams(2);

            this.EncoderWriteHeaders(3, NewPromise());
            this.EncoderWriteHeaders(5, NewPromise());
            this.EncoderWriteHeaders(7, NewPromise());
            this.EncoderWriteHeaders(9, NewPromise());
            Assert.Equal(2, this.encoder.NumBufferedStreams());

            this.WriteVerifyWriteHeaders(Times.Once(), 3);
            this.WriteVerifyWriteHeaders(Times.Once(), 5);
            this.WriteVerifyWriteHeaders(Times.Never(), 7);
            this.WriteVerifyWriteHeaders(Times.Never(), 9);

            // Simulate that we received a SETTINGS frame which
            // increased MAX_CONCURRENT_STREAMS to 5.
            this.SetMaxConcurrentStreams(5);
            this.encoder.WriteSettingsAckAsync(this.ctx.Object, NewPromise());

            Assert.Equal(0, this.encoder.NumBufferedStreams());
            this.WriteVerifyWriteHeaders(Times.Once(), 7);
            this.WriteVerifyWriteHeaders(Times.Once(), 9);

            this.EncoderWriteHeaders(11, NewPromise());

            this.WriteVerifyWriteHeaders(Times.Once(), 11);

            Assert.Equal(5, this.connection.Local.NumActiveStreams);
        }

        [Fact]
        public void BufferUntilSettingsReceived()
        {
            int initialLimit = Http2CodecUtil.SmallestMaxConcurrentStreams;
            int numStreams = initialLimit * 2;
            for (int ix = 0, nextStreamId = 3; ix < numStreams; ++ix, nextStreamId += 2)
            {
                this.EncoderWriteHeaders(nextStreamId, NewPromise());
                if (ix < initialLimit)
                {
                    this.WriteVerifyWriteHeaders(Times.Once(), nextStreamId);
                }
                else
                {
                    this.WriteVerifyWriteHeaders(Times.Never(), nextStreamId);
                }
            }
            Assert.Equal(numStreams / 2, this.encoder.NumBufferedStreams());

            // Simulate that we received a SETTINGS frame.
            this.SetMaxConcurrentStreams(initialLimit * 2);

            Assert.Equal(0, this.encoder.NumBufferedStreams());
            Assert.Equal(numStreams, this.connection.Local.NumActiveStreams);
        }

        [Fact]
        public void BufferUntilSettingsReceivedWithNoMaxConcurrentStreamValue()
        {
            int initialLimit = Http2CodecUtil.SmallestMaxConcurrentStreams;
            int numStreams = initialLimit * 2;
            for (int ix = 0, nextStreamId = 3; ix < numStreams; ++ix, nextStreamId += 2)
            {
                this.EncoderWriteHeaders(nextStreamId, NewPromise());
                if (ix < initialLimit)
                {
                    this.WriteVerifyWriteHeaders(Times.Once(), nextStreamId);
                }
                else
                {
                    this.WriteVerifyWriteHeaders(Times.Never(), nextStreamId);
                }
            }
            Assert.Equal(numStreams / 2, this.encoder.NumBufferedStreams());

            // Simulate that we received an empty SETTINGS frame.
            this.encoder.RemoteSettings(new Http2Settings());

            Assert.Equal(0, this.encoder.NumBufferedStreams());
            Assert.Equal(numStreams, this.connection.Local.NumActiveStreams);
        }

        [Fact]
        public void ExhaustedStreamsDoNotBuffer()
        {
            // Write the highest possible stream ID for the client.
            // This will cause the next stream ID to be negative.
            this.EncoderWriteHeaders(int.MaxValue, NewPromise());

            // Disallow any further streams.
            this.SetMaxConcurrentStreams(0);

            // Simulate numeric overflow for the next stream ID.
            Task f = this.EncoderWriteHeaders(-1, NewPromise());

            // Verify that the write fails.
            Assert.NotNull(f.Exception);
        }

        [Fact]
        public void ClosedBufferedStreamReleasesByteBuf()
        {
            this.encoder.WriteSettingsAckAsync(this.ctx.Object, NewPromise());
            this.SetMaxConcurrentStreams(0);
            var data = new Mock<IByteBuffer>();
            Task f1 = this.EncoderWriteHeaders(3, NewPromise());
            Assert.Equal(1, this.encoder.NumBufferedStreams());
            Task f2 = this.encoder.WriteDataAsync(this.ctx.Object, 3, data.Object, 0, false, NewPromise());

            var rstPromise = new Mock<IPromise>();
            this.encoder.WriteRstStreamAsync(this.ctx.Object, 3, Http2Error.Cancel, rstPromise.Object);

            Assert.Equal(0, this.encoder.NumBufferedStreams());
            rstPromise.Verify(x => x.Complete());
            Assert.True(f1.IsSuccess());
            Assert.True(f2.IsSuccess());
            data.Verify(x => x.Release());
        }

        [Fact]
        public void CloseShouldCancelAllBufferedStreams()
        {
            this.encoder.WriteSettingsAckAsync(this.ctx.Object, NewPromise());
            this.connection.Local.SetMaxActiveStreams(0);

            Task f1 = this.EncoderWriteHeaders(3, NewPromise());
            Task f2 = this.EncoderWriteHeaders(5, NewPromise());
            Task f3 = this.EncoderWriteHeaders(7, NewPromise());

            this.encoder.Close();
            Assert.NotNull(f1.Exception);
            Assert.NotNull(f2.Exception);
            Assert.NotNull(f3.Exception);
        }

        [Fact]
        public void HeadersAfterCloseShouldImmediatelyFail()
        {
            this.encoder.WriteSettingsAckAsync(this.ctx.Object, NewPromise());
            this.encoder.Close();

            var f = this.EncoderWriteHeaders(3, NewPromise());
            Assert.NotNull(f.Exception);
        }

        private void SetMaxConcurrentStreams(int newValue)
        {
            try
            {
                this.encoder.RemoteSettings(new Http2Settings().MaxConcurrentStreams(newValue));
                // Flush the remote flow controller to write data
                this.encoder.FlowController.WritePendingBytes();
            }
            catch (Http2Exception e)
            {
                throw new Http2RuntimeException(e.Message, e);
            }
        }

        private Task EncoderWriteHeaders(int streamId, IPromise promise)
        {
            this.encoder.WriteHeadersAsync(this.ctx.Object, streamId, new DefaultHttp2Headers(), 0, Http2CodecUtil.DefaultPriorityWeight,
                false, 0, false, promise);
            try
            {
                this.encoder.FlowController.WritePendingBytes();
                return promise.Task;
            }
            catch (Http2Exception e)
            {
                throw new Http2RuntimeException(e.Message, e);
            }
        }

        private void WriteVerifyWriteHeaders(Times mode, int streamId)
        {
            this.writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
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
            return new TaskCompletionSource();
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
