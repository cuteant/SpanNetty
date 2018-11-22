
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using Moq;
    using Xunit;

    public class DefaultHttp2ConnectionEncoderTest
    {
        private const int STREAM_ID = 2;
        private const int PUSH_STREAM_ID = 4;

        Mock<IHttp2RemoteFlowController> remoteFlow;
        Mock<IChannelHandlerContext> ctx;
        Mock<IChannel> channel;
        Mock<IChannelUnsafe> channelUnsafe;
        Mock<IChannelPipeline> pipeline;
        Mock<IHttp2FrameWriter> writer;
        Mock<IHttp2FrameWriterConfiguration> writerConfig;
        Mock<IHttp2FrameSizePolicy> frameSizePolicy;
        Mock<IHttp2LifecycleManager> lifecycleManager;

        private DefaultHttp2ConnectionEncoder encoder;
        private IHttp2Connection connection;
        private ArgumentCaptor<IHttp2RemoteFlowControlled> payloadCaptor;
        private List<string> writtenData;
        private List<int> writtenPadding;
        private bool streamClosed;

        public DefaultHttp2ConnectionEncoderTest()
        {
            this.remoteFlow = new Mock<IHttp2RemoteFlowController>();
            this.ctx = new Mock<IChannelHandlerContext>();
            this.channel = new Mock<IChannel>();
            this.channelUnsafe = new Mock<IChannelUnsafe>();
            this.pipeline = new Mock<IChannelPipeline>();
            this.writer = new Mock<IHttp2FrameWriter>();
            this.writerConfig = new Mock<IHttp2FrameWriterConfiguration>();
            this.frameSizePolicy = new Mock<IHttp2FrameSizePolicy>();
            this.lifecycleManager = new Mock<IHttp2LifecycleManager>();

            ChannelMetadata metadata = new ChannelMetadata(false, 16);
            this.channel.Setup(x => x.Active).Returns(true);
            this.channel.Setup(x => x.Pipeline).Returns(this.pipeline.Object);
            this.channel.Setup(x => x.Metadata).Returns(metadata);
            this.channel.Setup(x => x.Unsafe).Returns(this.channelUnsafe.Object);
            var config = new DefaultChannelConfiguration(this.channel.Object);
            this.channel.Setup(x => x.Configuration).Returns(config);
            this.writer.Setup(x => x.Configuration).Returns(this.writerConfig.Object);
            this.writerConfig.Setup(x => x.FrameSizePolicy).Returns(this.frameSizePolicy.Object);
            this.frameSizePolicy.Setup(x => x.MaxFrameSize).Returns(64);
            this.writer
                .Setup(x => x.WriteSettingsAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<Http2Settings>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, Http2Settings, IPromise>((c, s, p) =>
                {
                    p.Complete();
                    return p.Task;
                });
            this.writer
                .Setup(x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, Http2Error, IByteBuffer, IPromise>((c, id, err, buf, p) =>
                {
                    buf.Release();
                    p.Complete();
                    return p.Task;
                });
            this.writtenData = new List<string>();
            this.writtenPadding = new List<int>();
            this.writer
                .Setup(x => x.WriteDataAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<int>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, IByteBuffer, int, bool, IPromise>((c, id, data, padding, endOfStream, p) =>
                {
                    // Make sure we only receive stream closure on the last frame and that void promises
                    // are used for all writes except the last one.
                    if (this.streamClosed)
                    {
                        Assert.False(true, "Stream already closed");
                    }
                    else
                    {
                        this.streamClosed = endOfStream;
                    }
                    this.writtenPadding.Add(padding);
                    this.writtenData.Add(data.ToString(Encoding.UTF8));
                    // Release the buffer just as DefaultHttp2FrameWriter does
                    data.Release();
                    // Let the promise succeed to trigger listeners.
                    p.Complete();
                    return p.Task;
                });
            this.writer
                .Setup(x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<int>(),
                    It.IsAny<IHttp2Headers>(),
                    It.IsAny<int>(),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, IHttp2Headers, int, short, bool, int, bool, IPromise>((c, id, headers, streamDependency, weight, exclusive, padding, endOfStream, p) =>
                {
                    if (this.streamClosed)
                    {
                        Assert.False(true, "Stream already closed");
                    }
                    else
                    {
                        this.streamClosed = endOfStream;
                    }
                    p.Complete();
                    return p.Task;
                });
            this.payloadCaptor = new ArgumentCaptor<IHttp2RemoteFlowControlled>();
            this.remoteFlow
                .Setup(x => x.AddFlowControlled(
                    It.IsAny<IHttp2Stream>(),
                    It.Is<IHttp2RemoteFlowControlled>(v => this.payloadCaptor.Capture(v))))
                .Callback<IHttp2Stream, IHttp2RemoteFlowControlled>((s, f) => { });
            this.ctx.Setup(x => x.Allocator).Returns(UnpooledByteBufferAllocator.Default);
            this.ctx.Setup(x => x.Channel).Returns(this.channel.Object);
            this.ctx.Setup(x => x.NewPromise()).Returns(() => NewPromise());
            this.ctx.Setup(x => x.Flush()).Throws(new Exception("forbidden"));
            this.channel.Setup(x => x.Allocator).Returns(PooledByteBufferAllocator.Default);

            // Use a server-side connection so we can test server push.
            this.connection = new DefaultHttp2Connection(true);
            this.connection.Remote.FlowController = this.remoteFlow.Object;

            this.encoder = new DefaultHttp2ConnectionEncoder(this.connection, this.writer.Object);
            this.encoder.LifecycleManager(this.lifecycleManager.Object);
        }

        [Fact]
        public void DataWriteShouldSucceed()
        {
            this.CreateStream(STREAM_ID, false);
            var data = DummyData();
            var p = NewPromise();
            this.encoder.WriteDataAsync(this.ctx.Object, STREAM_ID, data, 0, true, p);
            Assert.Equal(8, this.payloadCaptor.GetValue().Size);
            this.payloadCaptor.GetValue().Write(this.ctx.Object, 8);
            Assert.Equal(0, this.payloadCaptor.GetValue().Size);
            Assert.Equal("abcdefgh", this.writtenData[0]);
            Assert.Equal(0, data.ReferenceCount);
            Assert.True(p.IsSuccess);
        }

        [Fact]
        public void DataFramesShouldMerge()
        {
            this.CreateStream(STREAM_ID, false);
            var data = (IByteBuffer)DummyData().Retain();

            var promise1 = NewPromise();
            this.encoder.WriteDataAsync(this.ctx.Object, STREAM_ID, data, 0, true, promise1);
            var promise2 = NewPromise();
            this.encoder.WriteDataAsync(this.ctx.Object, STREAM_ID, data, 0, true, promise2);

            // Now merge the two payloads.
            var capturedWrites = this.payloadCaptor.GetAllValues();
            var mergedPayload = capturedWrites[0];
            mergedPayload.Merge(this.ctx.Object, capturedWrites[1]);
            Assert.Equal(16, mergedPayload.Size);
            Assert.False(promise1.IsCompleted);
            Assert.False(promise2.IsCompleted);

            // Write the merged payloads and verify it was written correctly.
            mergedPayload.Write(this.ctx.Object, 16);
            Assert.Equal(0, mergedPayload.Size);
            Assert.Equal("abcdefghabcdefgh", this.writtenData[0]);
            Assert.Equal(0, data.ReferenceCount);
            Assert.True(promise1.IsSuccess);
            Assert.True(promise2.IsSuccess);
        }

        [Fact]
        public void DataFramesShouldMergeUseVoidPromise()
        {
            this.CreateStream(STREAM_ID, false);
            var data = (IByteBuffer)DummyData().Retain();

            var promise1 = Http2TestUtil.NewVoidPromise(this.channel.Object);
            this.encoder.WriteDataAsync(this.ctx.Object, STREAM_ID, data, 0, true, promise1);
            var promise2 = Http2TestUtil.NewVoidPromise(this.channel.Object);
            this.encoder.WriteDataAsync(this.ctx.Object, STREAM_ID, data, 0, true, promise2);

            // Now merge the two payloads.
            var capturedWrites = this.payloadCaptor.GetAllValues();
            var mergedPayload = capturedWrites[0];
            mergedPayload.Merge(this.ctx.Object, capturedWrites[1]);
            Assert.Equal(16, mergedPayload.Size);
            Assert.False(promise1.IsSuccess);
            Assert.False(promise2.IsSuccess);

            // Write the merged payloads and verify it was written correctly.
            mergedPayload.Write(this.ctx.Object, 16);
            Assert.Equal(0, mergedPayload.Size);
            Assert.Equal("abcdefghabcdefgh", this.writtenData[0]);
            Assert.Equal(0, data.ReferenceCount);

            // The promises won't be set since there are no listeners.
            Assert.False(promise1.IsSuccess);
            Assert.False(promise2.IsSuccess);
        }

        [Fact]
        public void DataFramesDontMergeWithHeaders()
        {
            this.CreateStream(STREAM_ID, false);
            var data = (IByteBuffer)DummyData().Retain();
            this.encoder.WriteDataAsync(this.ctx.Object, STREAM_ID, data, 0, false, NewPromise());
            this.remoteFlow.Setup(x => x.HasFlowControlled(It.IsAny<IHttp2Stream>())).Returns(true);
            this.encoder.WriteHeadersAsync(this.ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, true, NewPromise());
            var capturedWrites = this.payloadCaptor.GetAllValues();
            Assert.False(capturedWrites[0].Merge(this.ctx.Object, capturedWrites[1]));
        }

        [Fact]
        public void EmptyFrameShouldSplitPadding()
        {
            var data = Unpooled.Buffer(0);
            this.AssertSplitPaddingOnEmptyBuffer(data);
            Assert.Equal(0, data.ReferenceCount);
        }

        [Fact]
        public void WriteHeadersUsingVoidPromise()
        {
            var cause = new Http2RuntimeException("fake exception");
            this.writer
                .Setup(x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<IHttp2Headers>(),
                    It.IsAny<int>(),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, IHttp2Headers, int, short, bool, int, bool, IPromise>((c, id, headers, streamDependency, weight, exclusive, padding, endOfStream, p) =>
                {
                    Assert.False(p.IsVoid);
                    p.SetException(cause);
                    return p.Task;
                });
            this.CreateStream(STREAM_ID, false);
            // END_STREAM flag, so that a listener is added to the future.
            this.encoder.WriteHeadersAsync(this.ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, true, Http2TestUtil.NewVoidPromise(this.channel.Object));

            this.writer.Verify(x => x.WriteHeadersAsync(
                It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                It.Is<int>(v => v == STREAM_ID),
                It.IsAny<IHttp2Headers>(),
                It.IsAny<int>(),
                It.IsAny<short>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<IPromise>()));
            // When using a void promise, the error should be propagated via the channel pipeline.
            this.pipeline.Verify(x => x.FireExceptionCaught(It.Is<Exception>(ex => ex == cause)));
        }

        private void AssertSplitPaddingOnEmptyBuffer(IByteBuffer data)
        {
            this.CreateStream(STREAM_ID, false);
            this.frameSizePolicy.Setup(x => x.MaxFrameSize).Returns(5);
            var p = NewPromise();
            this.encoder.WriteDataAsync(this.ctx.Object, STREAM_ID, data, 10, true, p);
            Assert.Equal(10, this.payloadCaptor.GetValue().Size);
            this.payloadCaptor.GetValue().Write(this.ctx.Object, 10);
            // writer was called 2 times
            Assert.Single(this.writtenData);
            Assert.Equal("", this.writtenData[0]);
            Assert.Equal(10, (int)this.writtenPadding[0]);
            Assert.Equal(0, data.ReferenceCount);
            Assert.True(p.IsSuccess);
        }

        [Fact]
        public void HeadersWriteForUnknownStreamShouldCreateStream()
        {
            this.WriteAllFlowControlledFrames();
            int streamId = 6;
            var promise = NewPromise();
            this.encoder.WriteHeadersAsync(this.ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, false, promise);
            this.writer.Verify(x => x.WriteHeadersAsync(
                It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                It.Is<int>(v => v == streamId),
                It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                It.Is<int>(v => v == 0),
                It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                It.Is<bool>(v => v == false),
                It.Is<int>(v => v == 0),
                It.Is<bool>(v => v == false),
                It.Is<IPromise>(v => v == promise)));
            Assert.True(promise.IsSuccess);
        }

        [Fact]
        public void HeadersWriteShouldOpenStreamForPush()
        {
            this.WriteAllFlowControlledFrames();
            IHttp2Stream parent = this.CreateStream(STREAM_ID, false);
            this.ReservePushStream(PUSH_STREAM_ID, parent);

            var promise = NewPromise();
            this.encoder.WriteHeadersAsync(this.ctx.Object, PUSH_STREAM_ID, EmptyHttp2Headers.Instance, 0, false, promise);
            Assert.Equal(Http2StreamState.HalfClosedRemote, this.Stream(PUSH_STREAM_ID).State);
            this.writer.Verify(x => x.WriteHeadersAsync(
                It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                It.Is<int>(v => v == PUSH_STREAM_ID),
                It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                It.Is<int>(v => v == 0),
                It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                It.Is<bool>(v => v == false),
                It.Is<int>(v => v == 0),
                It.Is<bool>(v => v == false),
                It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void TrailersDoNotEndStreamThrows()
        {
            this.WriteAllFlowControlledFrames();
            int streamId = 6;
            var promise = NewPromise();
            this.encoder.WriteHeadersAsync(this.ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, false, promise);

            var promise2 = NewPromise();
            var future = this.encoder.WriteHeadersAsync(this.ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, false, promise2);
            Assert.True(future.IsCompleted);
            Assert.False(future.IsSuccess());

            this.writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.Is<IPromise>(v => v == promise)),
                Times.Once);
        }

        [Fact]
        public void TrailersDoNotEndStreamWithDataThrows()
        {
            this.WriteAllFlowControlledFrames();
            int streamId = 6;
            var promise = NewPromise();
            this.encoder.WriteHeadersAsync(this.ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, false, promise);

            IHttp2Stream stream = this.connection.Stream(streamId);
            this.remoteFlow.Setup(x => x.HasFlowControlled(It.Is<IHttp2Stream>(v => v == stream))).Returns(true);

            var promise2 = NewPromise();
            var future = this.encoder.WriteHeadersAsync(this.ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, false, promise2);
            Assert.True(future.IsCompleted);
            Assert.False(future.IsSuccess());

            this.writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.Is<IPromise>(v => v == promise)),
                Times.Once);
        }

        [Fact]
        public void TooManyHeadersNoEOSThrows()
        {
            this.TooManyHeadersThrows(false);
        }

        [Fact]
        public void TooManyHeadersEOSThrows()
        {
            this.TooManyHeadersThrows(true);
        }

        private void TooManyHeadersThrows(bool eos)
        {
            this.WriteAllFlowControlledFrames();
            int streamId = 6;
            var promise = NewPromise();
            this.encoder.WriteHeadersAsync(this.ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, false, promise);
            var promise2 = NewPromise();
            this.encoder.WriteHeadersAsync(this.ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, true, promise2);

            var promise3 = NewPromise();
            var future = this.encoder.WriteHeadersAsync(this.ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, eos, promise3);
            Assert.True(future.IsCompleted);
            Assert.False(future.IsSuccess());

            this.writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.Is<IPromise>(v => v == promise)),
                Times.Once);
            this.writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == promise2)),
                Times.Once);
        }

        [Fact]
        public void InfoHeadersAndTrailersAllowed()
        {
            this.InfoHeadersAndTrailers(true, 1);
        }

        [Fact]
        public void MultipleInfoHeadersAndTrailersAllowed()
        {
            this.InfoHeadersAndTrailers(true, 10);
        }

        [Fact]
        public void InfoHeadersAndTrailersNoEOSThrows()
        {
            this.InfoHeadersAndTrailers(false, 1);
        }

        [Fact]
        public void MultipleInfoHeadersAndTrailersNoEOSThrows()
        {
            this.InfoHeadersAndTrailers(false, 10);
        }

        private void InfoHeadersAndTrailers(bool eos, int infoHeaderCount)
        {
            this.WriteAllFlowControlledFrames();
            int streamId = 6;
            IHttp2Headers infoHeaders = InformationalHeaders();
            for (int i = 0; i < infoHeaderCount; ++i)
            {
                this.encoder.WriteHeadersAsync(this.ctx.Object, streamId, infoHeaders, 0, false, NewPromise());
            }
            var promise2 = NewPromise();
            this.encoder.WriteHeadersAsync(this.ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, false, promise2);

            var promise3 = NewPromise();
            var future = this.encoder.WriteHeadersAsync(this.ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, eos, promise3);
            Assert.True(future.IsCompleted);
            Assert.Equal(eos, future.IsSuccess());

            this.writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => v.Equals(infoHeaders)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.IsAny<IPromise>()),
                Times.Exactly(infoHeaderCount));
            this.writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.Is<IPromise>(v => v == promise2)),
                Times.Once);
            if (eos)
            {
                this.writer.Verify(
                    x => x.WriteHeadersAsync(
                        It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                        It.Is<int>(v => v == streamId),
                        It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                        It.Is<int>(v => v == 0),
                        It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                        It.Is<bool>(v => v == false),
                        It.Is<int>(v => v == 0),
                        It.Is<bool>(v => v == true),
                        It.Is<IPromise>(v => v == promise3)),
                    Times.Once);
            }
        }

        private static IHttp2Headers InformationalHeaders()
        {
            return new DefaultHttp2Headers
            {
                Status = HttpResponseStatus.Continue.CodeAsText
            };
        }

        [Fact]
        public void TooManyHeadersWithDataNoEOSThrows()
        {
            this.TooManyHeadersWithDataThrows(false);
        }

        [Fact]
        public void TooManyHeadersWithDataEOSThrows()
        {
            this.TooManyHeadersWithDataThrows(true);
        }

        private void TooManyHeadersWithDataThrows(bool eos)
        {
            this.WriteAllFlowControlledFrames();
            int streamId = 6;
            var promise = NewPromise();
            this.encoder.WriteHeadersAsync(this.ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, false, promise);

            var stream = this.connection.Stream(streamId);
            this.remoteFlow.Setup(x => x.HasFlowControlled(It.Is<IHttp2Stream>(v => v == stream))).Returns(true);

            var promise2 = NewPromise();
            this.encoder.WriteHeadersAsync(this.ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, true, promise2);

            var promise3 = NewPromise();
            var future = this.encoder.WriteHeadersAsync(this.ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, eos, promise3);
            Assert.True(future.IsCompleted);
            Assert.False(future.IsSuccess());

            this.writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.Is<IPromise>(v => v == promise)),
                Times.Once);
            this.writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == promise2)),
                Times.Once);
        }

        [Fact]
        public void InfoHeadersAndTrailersWithDataAllowed()
        {
            this.InfoHeadersAndTrailersWithData(true, 1);
        }

        [Fact]
        public void MultipleInfoHeadersAndTrailersWithDataAllowed()
        {
            this.InfoHeadersAndTrailersWithData(true, 10);
        }

        [Fact]
        public void InfoHeadersAndTrailersWithDataNoEOSThrows()
        {
            this.InfoHeadersAndTrailersWithData(false, 1);
        }

        [Fact]
        public void MultipleInfoHeadersAndTrailersWithDataNoEOSThrows()
        {
            this.InfoHeadersAndTrailersWithData(false, 10);
        }

        private void InfoHeadersAndTrailersWithData(bool eos, int infoHeaderCount)
        {
            this.WriteAllFlowControlledFrames();
            int streamId = 6;
            IHttp2Headers infoHeaders = InformationalHeaders();
            for (int i = 0; i < infoHeaderCount; ++i)
            {
                this.encoder.WriteHeadersAsync(this.ctx.Object, streamId, infoHeaders, 0, false, NewPromise());
            }

            IHttp2Stream stream = this.connection.Stream(streamId);
            this.remoteFlow.Setup(x => x.HasFlowControlled(It.Is<IHttp2Stream>(v => v == stream))).Returns(true);

            var promise2 = NewPromise();
            this.encoder.WriteHeadersAsync(this.ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, false, promise2);

            var promise3 = NewPromise();
            var future = this.encoder.WriteHeadersAsync(this.ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, eos, promise3);
            Assert.True(future.IsCompleted);
            Assert.Equal(eos, future.IsSuccess());

            this.writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => v.Equals(infoHeaders)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.IsAny<IPromise>()),
                Times.Exactly(infoHeaderCount));
            this.writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.Is<IPromise>(v => v == promise2)),
                Times.Once);
            if (eos)
            {
                this.writer.Verify(
                    x => x.WriteHeadersAsync(
                        It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                        It.Is<int>(v => v == streamId),
                        It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                        It.Is<int>(v => v == 0),
                        It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                        It.Is<bool>(v => v == false),
                        It.Is<int>(v => v == 0),
                        It.Is<bool>(v => v == true),
                        It.Is<IPromise>(v => v == promise3)),
                    Times.Once);
            }
        }

        [Fact]
        public void PushPromiseWriteAfterGoAwayReceivedShouldFail()
        {
            this.CreateStream(STREAM_ID, false);
            this.GoAwayReceived(0);
            var future = this.encoder.WritePushPromiseAsync(this.ctx.Object, STREAM_ID, PUSH_STREAM_ID, EmptyHttp2Headers.Instance, 0,
                    NewPromise());
            Assert.True(future.IsCompleted);
            Assert.False(future.IsSuccess());
        }

        [Fact]
        public void PushPromiseWriteShouldReserveStream()
        {
            this.CreateStream(STREAM_ID, false);
            var promise = NewPromise();
            this.encoder.WritePushPromiseAsync(this.ctx.Object, STREAM_ID, PUSH_STREAM_ID, EmptyHttp2Headers.Instance, 0, promise);
            Assert.Equal(Http2StreamState.ReservedLocal, this.Stream(PUSH_STREAM_ID).State);
            this.writer.Verify(
                x => x.WritePushPromiseAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<int>(v => v == PUSH_STREAM_ID),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void PriorityWriteAfterGoAwayShouldSucceed()
        {
            this.CreateStream(STREAM_ID, false);
            this.GoAwayReceived(int.MaxValue);
            var promise = NewPromise();
            this.encoder.WritePriorityAsync(this.ctx.Object, STREAM_ID, 0, (short)255, true, promise);
            this.writer.Verify(
                x => x.WritePriorityAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == 255),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void PriorityWriteShouldSetPriorityForStream()
        {
            var promise = NewPromise();
            short weight = 255;
            this.encoder.WritePriorityAsync(this.ctx.Object, STREAM_ID, 0, weight, true, promise);

            // Verify that this did NOT create a stream object.
            IHttp2Stream stream = this.Stream(STREAM_ID);
            Assert.Null(stream);

            this.writer.Verify(
                x => x.WritePriorityAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == 255),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void PriorityWriteOnPreviouslyExistingStreamShouldSucceed()
        {
            this.CreateStream(STREAM_ID, false).Close();
            var promise = NewPromise();
            short weight = 255;
            this.encoder.WritePriorityAsync(this.ctx.Object, STREAM_ID, 0, weight, true, promise);
            this.writer.Verify(
                x => x.WritePriorityAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == weight),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void PriorityWriteOnPreviouslyExistingParentStreamShouldSucceed()
        {
            int parentStreamId = STREAM_ID + 2;
            this.CreateStream(STREAM_ID, false);
            this.CreateStream(parentStreamId, false).Close();

            var promise = NewPromise();
            short weight = 255;
            this.encoder.WritePriorityAsync(this.ctx.Object, STREAM_ID, parentStreamId, weight, true, promise);
            this.writer.Verify(
                x => x.WritePriorityAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<int>(v => v == parentStreamId),
                    It.Is<short>(v => v == weight),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void RstStreamWriteForUnknownStreamShouldIgnore()
        {
            var promise = NewPromise();
            this.encoder.WriteRstStreamAsync(this.ctx.Object, 5, Http2Error.ProtocolError, promise);
            this.writer.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.Is<IPromise>(v => v == promise)),
                Times.Never());
        }

        [Fact]
        public void RstStreamShouldCloseStream()
        {
            // Create the stream and send headers.
            this.WriteAllFlowControlledFrames();
            this.encoder.WriteHeadersAsync(this.ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, true, NewPromise());

            // Now verify that a stream reset is performed.
            this.Stream(STREAM_ID);
            var promise = NewPromise();
            this.encoder.WriteRstStreamAsync(this.ctx.Object, STREAM_ID, Http2Error.ProtocolError, promise);
            this.lifecycleManager.Verify(
                x => x.ResetStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<Http2Error>(),
                    It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void PingWriteAfterGoAwayShouldSucceed()
        {
            var promise = NewPromise();
            this.GoAwayReceived(0);
            this.encoder.WritePingAsync(this.ctx.Object, false, 0L, promise);
            this.writer.Verify(
                x => x.WritePingAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<bool>(v => v == false),
                    It.Is<long>(v => v == 0L),
                    It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void PingWriteShouldSucceed()
        {
            var promise = NewPromise();
            this.encoder.WritePingAsync(this.ctx.Object, false, 0L, promise);
            this.writer.Verify(
                x => x.WritePingAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<bool>(v => v == false),
                    It.Is<long>(v => v == 0L),
                    It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void SettingsWriteAfterGoAwayShouldSucceed()
        {
            this.GoAwayReceived(0);
            var promise = NewPromise();
            this.encoder.WriteSettingsAsync(this.ctx.Object, new Http2Settings(), promise);
            this.writer.Verify(
                x => x.WriteSettingsAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<Http2Settings>(),
                    It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void SettingsWriteShouldNotUpdateSettings()
        {
            Http2Settings settings = new Http2Settings();
            settings.InitialWindowSize(100);
            settings.MaxConcurrentStreams(1000);
            settings.HeaderTableSize(2000);

            var promise = NewPromise();
            this.encoder.WriteSettingsAsync(this.ctx.Object, settings, promise);
            this.writer.Verify(
                x => x.WriteSettingsAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<Http2Settings>(v => v == settings),
                    It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void DataWriteShouldCreateHalfClosedStream()
        {
            this.WriteAllFlowControlledFrames();

            IHttp2Stream stream = this.CreateStream(STREAM_ID, false);
            var data = DummyData();
            var promise = NewPromise();
            this.encoder.WriteDataAsync(this.ctx.Object, STREAM_ID, (IByteBuffer)data.Retain(), 0, true, promise);
            Assert.True(promise.IsSuccess);
            this.remoteFlow.Verify(
                x => x.AddFlowControlled(
                    It.Is<IHttp2Stream>(v => v == stream),
                    It.IsAny<IHttp2RemoteFlowControlled>()));
            this.lifecycleManager.Verify(
                x => x.CloseStreamLocal(
                    It.Is<IHttp2Stream>(v => v == stream),
                    It.Is<Task>(v => v == promise.Task)));
            Assert.Equal(data.ToString(Encoding.UTF8), this.writtenData[0]);
            data.Release();
        }

        [Fact]
        public void HeadersWriteShouldHalfCloseStream()
        {
            this.WriteAllFlowControlledFrames();
            this.CreateStream(STREAM_ID, false);
            var promise = NewPromise();
            this.encoder.WriteHeadersAsync(this.ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, true, promise);

            Assert.True(promise.IsSuccess);
            this.lifecycleManager.Verify(
                x => x.CloseStreamLocal(
                    It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_ID)),
                    It.Is<Task>(v => v == promise.Task)));
        }

        [Fact]
        public void HeadersWriteShouldHalfClosePushStream()
        {
            this.WriteAllFlowControlledFrames();
            IHttp2Stream parent = this.CreateStream(STREAM_ID, false);
            IHttp2Stream stream = this.ReservePushStream(PUSH_STREAM_ID, parent);
            var promise = NewPromise();
            this.encoder.WriteHeadersAsync(this.ctx.Object, PUSH_STREAM_ID, EmptyHttp2Headers.Instance, 0, true, promise);
            Assert.Equal(Http2StreamState.HalfClosedRemote, stream.State);
            Assert.True(promise.IsSuccess);
            this.lifecycleManager.Verify(
                x => x.CloseStreamLocal(
                    It.Is<IHttp2Stream>(v => v == stream),
                    It.Is<Task>(v => v == promise.Task)));
        }

        [Fact]
        public void HeadersWriteShouldHalfCloseAfterOnError()
        {
            IPromise promise = NewPromise();
            var ex = new Http2RuntimeException();
            // Fake an encoding error, like HPACK's HeaderListSizeException
            this.writer
                .Setup(x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == promise)))
                .Returns<IChannelHandlerContext, int, IHttp2Headers, int, short, bool, int, bool, IPromise>((c, id, headers, streamDependency, weight, exclusive, padding, endOfStream, p) =>
                {
                    p.SetException(ex);
                    return p.Task;
                });

            this.WriteAllFlowControlledFrames();
            this.CreateStream(STREAM_ID, false);
            this.encoder.WriteHeadersAsync(this.ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, true, promise);

            Assert.True(promise.IsCompleted);
            Assert.False(promise.IsSuccess);
            //InOrder inOrder = inOrder(lifecycleManager);
            //inOrder.verify(lifecycleManager).onError(eq(ctx), eq(true), eq(ex));
            //inOrder.verify(lifecycleManager).closeStreamLocal(eq(stream(STREAM_ID)), eq(promise));
            this.lifecycleManager.Verify(
                x => x.OnError(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<bool>(v => v == true),
                    It.Is<Exception>(v => v == ex)));
            this.lifecycleManager.Verify(
                x => x.CloseStreamLocal(
                    It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_ID)),
                    It.Is<Task>(v => v == promise.Task)));
        }

        [Fact]
        public void EncoderDelegatesGoAwayToLifeCycleManager()
        {
            var promise = NewPromise();
            this.encoder.WriteGoAwayAsync(this.ctx.Object, STREAM_ID, Http2Error.InternalError, null, promise);
            this.lifecycleManager.Verify(
                x => x.GoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.InternalError),
                    It.Is<IByteBuffer>(v => v == null),
                    It.Is<IPromise>(v => v == promise)));
            this.writer.VerifyNoOtherCalls();
        }

        [Fact]
        public void DataWriteToClosedStreamShouldFail()
        {
            this.CreateStream(STREAM_ID, false).Close();
            var data = new Mock<IByteBuffer>();
            var promise = NewPromise();
            this.encoder.WriteDataAsync(this.ctx.Object, STREAM_ID, data.Object, 0, false, promise);
            Assert.True(promise.IsCompleted);
            Assert.False(promise.IsSuccess);
            Assert.IsType<ArgumentException>(promise.Task.Exception.InnerException);
            data.Verify(x => x.Release());
        }

        [Fact]
        public void DataWriteToHalfClosedLocalStreamShouldFail()
        {
            this.CreateStream(STREAM_ID, true);
            var data = new Mock<IByteBuffer>();
            var promise = NewPromise();
            this.encoder.WriteDataAsync(this.ctx.Object, STREAM_ID, data.Object, 0, false, promise);
            Assert.True(promise.IsCompleted);
            Assert.False(promise.IsSuccess);
            Assert.IsType<InvalidOperationException>(promise.Task.Exception.InnerException);
            data.Verify(x => x.Release());
        }

        [Fact]
        public void CanWriteDataFrameAfterGoAwaySent()
        {
            IHttp2Stream stream = this.CreateStream(STREAM_ID, false);
            this.connection.GoAwaySent(0, 0, Unpooled.Empty);
            var data = new Mock<IByteBuffer>();
            this.encoder.WriteDataAsync(this.ctx.Object, STREAM_ID, data.Object, 0, false, NewPromise());
            this.remoteFlow.Verify(
                x => x.AddFlowControlled(
                    It.Is<IHttp2Stream>(v => v == stream),
                    It.IsAny<IHttp2RemoteFlowControlled>()));
        }

        [Fact]
        public void CanWriteHeaderFrameAfterGoAwaySent()
        {
            this.WriteAllFlowControlledFrames();
            this.CreateStream(STREAM_ID, false);
            this.GoAwaySent(0);
            var promise = NewPromise();
            this.encoder.WriteHeadersAsync(this.ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, false, promise);
            this.writer.Verify(x => x.WriteHeadersAsync(
                It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                It.Is<int>(v => v == STREAM_ID),
                It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                It.Is<int>(v => v == 0),
                It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                It.Is<bool>(v => v == false),
                It.Is<int>(v => v == 0),
                It.Is<bool>(v => v == false),
                It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void CanWriteDataFrameAfterGoAwayReceived()
        {
            IHttp2Stream stream = this.CreateStream(STREAM_ID, false);
            this.GoAwayReceived(STREAM_ID);
            var data = new Mock<IByteBuffer>();
            this.encoder.WriteDataAsync(this.ctx.Object, STREAM_ID, data.Object, 0, false, NewPromise());
            this.remoteFlow.Verify(
                x => x.AddFlowControlled(
                    It.Is<IHttp2Stream>(v => v == stream),
                    It.IsAny<IHttp2RemoteFlowControlled>()));
        }

        [Fact]
        public void CanWriteHeaderFrameAfterGoAwayReceived()
        {
            this.WriteAllFlowControlledFrames();
            this.GoAwayReceived(STREAM_ID);
            var promise = NewPromise();
            this.encoder.WriteHeadersAsync(this.ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, false, promise);
            this.writer.Verify(x => x.WriteHeadersAsync(
                It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                It.Is<int>(v => v == STREAM_ID),
                It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                It.Is<int>(v => v == 0),
                It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                It.Is<bool>(v => v == false),
                It.Is<int>(v => v == 0),
                It.Is<bool>(v => v == false),
                It.Is<IPromise>(v => v == promise)));
        }

        private void WriteAllFlowControlledFrames()
        {
            this.remoteFlow
                .Setup(x => x.AddFlowControlled(
                    It.IsAny<IHttp2Stream>(),
                    It.Is<IHttp2RemoteFlowControlled>(v => this.payloadCaptor.Capture(v))))
                .Callback<IHttp2Stream, IHttp2RemoteFlowControlled>((s, f) =>
                {
                    f.Write(this.ctx.Object, int.MaxValue);
                    f.WriteComplete();
                });
        }

        private IHttp2Stream CreateStream(int streamId, bool halfClosed)
        {
            return this.connection.Local.CreateStream(streamId, halfClosed);
        }

        private IHttp2Stream ReservePushStream(int pushStreamId, IHttp2Stream parent)
        {
            return this.connection.Local.ReservePushStream(pushStreamId, parent);
        }

        private IHttp2Stream Stream(int streamId)
        {
            return this.connection.Stream(streamId);
        }

        private void GoAwayReceived(int lastStreamId)
        {
            this.connection.GoAwayReceived(lastStreamId, 0, Unpooled.Empty);
        }

        private void GoAwaySent(int lastStreamId)
        {
            this.connection.GoAwaySent(lastStreamId, 0, Unpooled.Empty);
        }

        private static IPromise NewPromise()
        {
            return new TaskCompletionSource();
        }

        private static IByteBuffer DummyData()
        {
            // The buffer is purposely 8 bytes so it will even work for a ping frame.
            return Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes("abcdefgh"));
        }
    }
}
