
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

        Mock<IHttp2RemoteFlowController> _remoteFlow;
        Mock<IChannelHandlerContext> _ctx;
        Mock<IChannel> _channel;
        Mock<IChannelUnsafe> _channelUnsafe;
        Mock<IChannelPipeline> _pipeline;
        Mock<IHttp2FrameWriter> _writer;
        Mock<IHttp2FrameWriterConfiguration> _writerConfig;
        Mock<IHttp2FrameSizePolicy> _frameSizePolicy;
        Mock<IHttp2LifecycleManager> _lifecycleManager;

        private DefaultHttp2ConnectionEncoder _encoder;
        private IHttp2Connection _connection;
        private ArgumentCaptor<IHttp2RemoteFlowControlled> _payloadCaptor;
        private List<string> _writtenData;
        private List<int> _writtenPadding;
        private bool _streamClosed;

        public DefaultHttp2ConnectionEncoderTest()
        {
            _remoteFlow = new Mock<IHttp2RemoteFlowController>();
            _ctx = new Mock<IChannelHandlerContext>();
            _channel = new Mock<IChannel>();
            _channelUnsafe = new Mock<IChannelUnsafe>();
            _pipeline = new Mock<IChannelPipeline>();
            _writer = new Mock<IHttp2FrameWriter>();
            _writerConfig = new Mock<IHttp2FrameWriterConfiguration>();
            _frameSizePolicy = new Mock<IHttp2FrameSizePolicy>();
            _lifecycleManager = new Mock<IHttp2LifecycleManager>();

            ChannelMetadata metadata = new ChannelMetadata(false, 16);
            _channel.Setup(x => x.Active).Returns(true);
            _channel.Setup(x => x.Pipeline).Returns(_pipeline.Object);
            _channel.Setup(x => x.Metadata).Returns(metadata);
            _channel.Setup(x => x.Unsafe).Returns(_channelUnsafe.Object);
            var config = new DefaultChannelConfiguration(_channel.Object);
            _channel.Setup(x => x.Configuration).Returns(config);
            _writer.Setup(x => x.Configuration).Returns(_writerConfig.Object);
            _writerConfig.Setup(x => x.FrameSizePolicy).Returns(_frameSizePolicy.Object);
            _frameSizePolicy.Setup(x => x.MaxFrameSize).Returns(64);
            _writer
                .Setup(x => x.WriteSettingsAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.IsAny<Http2Settings>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, Http2Settings, IPromise>((c, s, p) =>
                {
                    p.Complete();
                    return p.Task;
                });
            _writer
                .Setup(x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
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
            _writtenData = new List<string>();
            _writtenPadding = new List<int>();
            _writer
                .Setup(x => x.WriteDataAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.IsAny<int>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, IByteBuffer, int, bool, IPromise>((c, id, data, padding, endOfStream, p) =>
                {
                    // Make sure we only receive stream closure on the last frame and that void promises
                    // are used for all writes except the last one.
                    if (_streamClosed)
                    {
                        Assert.False(true, "Stream already closed");
                    }
                    else
                    {
                        _streamClosed = endOfStream;
                    }
                    _writtenPadding.Add(padding);
                    _writtenData.Add(data.ToString(Encoding.UTF8));
                    // Release the buffer just as DefaultHttp2FrameWriter does
                    data.Release();
                    // Let the promise succeed to trigger listeners.
                    p.Complete();
                    return p.Task;
                });
            _writer
                .Setup(x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
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
                    if (_streamClosed)
                    {
                        Assert.False(true, "Stream already closed");
                    }
                    else
                    {
                        _streamClosed = endOfStream;
                    }
                    p.Complete();
                    return p.Task;
                });
            _writer
                .Setup(x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.IsAny<int>(),
                    It.IsAny<IHttp2Headers>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, IHttp2Headers, int, bool, IPromise>((c, id, headers, streamDependency, endOfStream, p) =>
                {
                    if (_streamClosed)
                    {
                        Assert.False(true, "Stream already closed");
                    }
                    else
                    {
                        _streamClosed = endOfStream;
                    }
                    p.Complete();
                    return p.Task;
                });
            _payloadCaptor = new ArgumentCaptor<IHttp2RemoteFlowControlled>();
            _remoteFlow
                .Setup(x => x.AddFlowControlled(
                    It.IsAny<IHttp2Stream>(),
                    It.Is<IHttp2RemoteFlowControlled>(v => _payloadCaptor.Capture(v))))
                .Callback<IHttp2Stream, IHttp2RemoteFlowControlled>((s, f) => { });
            _ctx.Setup(x => x.Allocator).Returns(UnpooledByteBufferAllocator.Default);
            _ctx.Setup(x => x.Channel).Returns(_channel.Object);
            _ctx.Setup(x => x.NewPromise()).Returns(() => NewPromise());
            _ctx.Setup(x => x.Flush()).Throws(new Exception("forbidden"));
            _channel.Setup(x => x.Allocator).Returns(PooledByteBufferAllocator.Default);

            // Use a server-side connection so we can test server push.
            _connection = new DefaultHttp2Connection(true);
            _connection.Remote.FlowController = _remoteFlow.Object;

            _encoder = new DefaultHttp2ConnectionEncoder(_connection, _writer.Object);
            _encoder.LifecycleManager(_lifecycleManager.Object);
        }

        [Fact]
        public void DataWithEndOfStreamWriteShouldSignalThatFrameWasConsumedOnError()
        {
            DataWriteShouldSignalThatFrameWasConsumedOnError0(true);
        }

        [Fact]
        public void DataWriteShouldSignalThatFrameWasConsumedOnError()
        {
            DataWriteShouldSignalThatFrameWasConsumedOnError0(false);
        }

        private void DataWriteShouldSignalThatFrameWasConsumedOnError0(bool endOfStream)
        {
            CreateStream(STREAM_ID, false);
            IByteBuffer data = DummyData();
            IPromise p = NewPromise();
            _encoder.WriteDataAsync(_ctx.Object, STREAM_ID, data, 0, endOfStream, p);

            var controlled = _payloadCaptor.GetValue();
            Assert.Equal(8, controlled.Size);
            _payloadCaptor.GetValue().Write(_ctx.Object, 4);
            Assert.Equal(4, controlled.Size);

            var error = new InvalidOperationException();
            _payloadCaptor.GetValue().Error(_ctx.Object, error);
            _payloadCaptor.GetValue().Write(_ctx.Object, 8);
            Assert.Equal(0, controlled.Size);
            Assert.Equal("abcd", _writtenData[0]);
            Assert.Equal(0, data.ReferenceCount);
            Assert.Same(error, p.Task.Exception.InnerException);
        }

        [Fact]
        public void DataWriteShouldSucceed()
        {
            CreateStream(STREAM_ID, false);
            var data = DummyData();
            var p = NewPromise();
            _encoder.WriteDataAsync(_ctx.Object, STREAM_ID, data, 0, true, p);
            Assert.Equal(8, _payloadCaptor.GetValue().Size);
            _payloadCaptor.GetValue().Write(_ctx.Object, 8);
            Assert.Equal(0, _payloadCaptor.GetValue().Size);
            Assert.Equal("abcdefgh", _writtenData[0]);
            Assert.Equal(0, data.ReferenceCount);
            Assert.True(p.IsSuccess);
        }

        [Fact]
        public void DataFramesShouldMerge()
        {
            CreateStream(STREAM_ID, false);
            var data = (IByteBuffer)DummyData().Retain();

            var promise1 = NewPromise();
            _encoder.WriteDataAsync(_ctx.Object, STREAM_ID, data, 0, true, promise1);
            var promise2 = NewPromise();
            _encoder.WriteDataAsync(_ctx.Object, STREAM_ID, data, 0, true, promise2);

            // Now merge the two payloads.
            var capturedWrites = _payloadCaptor.GetAllValues();
            var mergedPayload = capturedWrites[0];
            mergedPayload.Merge(_ctx.Object, capturedWrites[1]);
            Assert.Equal(16, mergedPayload.Size);
            Assert.False(promise1.IsCompleted);
            Assert.False(promise2.IsCompleted);

            // Write the merged payloads and verify it was written correctly.
            mergedPayload.Write(_ctx.Object, 16);
            Assert.Equal(0, mergedPayload.Size);
            Assert.Equal("abcdefghabcdefgh", _writtenData[0]);
            Assert.Equal(0, data.ReferenceCount);
            Assert.True(promise1.IsSuccess);
            Assert.True(promise2.IsSuccess);
        }

        [Fact]
        public void DataFramesShouldMergeUseVoidPromise()
        {
            CreateStream(STREAM_ID, false);
            var data = (IByteBuffer)DummyData().Retain();

            var promise1 = Http2TestUtil.NewVoidPromise(_channel.Object);
            _encoder.WriteDataAsync(_ctx.Object, STREAM_ID, data, 0, true, promise1);
            var promise2 = Http2TestUtil.NewVoidPromise(_channel.Object);
            _encoder.WriteDataAsync(_ctx.Object, STREAM_ID, data, 0, true, promise2);

            // Now merge the two payloads.
            var capturedWrites = _payloadCaptor.GetAllValues();
            var mergedPayload = capturedWrites[0];
            mergedPayload.Merge(_ctx.Object, capturedWrites[1]);
            Assert.Equal(16, mergedPayload.Size);
            Assert.False(promise1.IsSuccess);
            Assert.False(promise2.IsSuccess);

            // Write the merged payloads and verify it was written correctly.
            mergedPayload.Write(_ctx.Object, 16);
            Assert.Equal(0, mergedPayload.Size);
            Assert.Equal("abcdefghabcdefgh", _writtenData[0]);
            Assert.Equal(0, data.ReferenceCount);

            // The promises won't be set since there are no listeners.
            Assert.False(promise1.IsSuccess);
            Assert.False(promise2.IsSuccess);
        }

        [Fact]
        public void DataFramesDontMergeWithHeaders()
        {
            CreateStream(STREAM_ID, false);
            var data = (IByteBuffer)DummyData().Retain();
            _encoder.WriteDataAsync(_ctx.Object, STREAM_ID, data, 0, false, NewPromise());
            _remoteFlow.Setup(x => x.HasFlowControlled(It.IsAny<IHttp2Stream>())).Returns(true);
            _encoder.WriteHeadersAsync(_ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, true, NewPromise());
            var capturedWrites = _payloadCaptor.GetAllValues();
            Assert.False(capturedWrites[0].Merge(_ctx.Object, capturedWrites[1]));
        }

        [Fact]
        public void EmptyFrameShouldSplitPadding()
        {
            var data = Unpooled.Buffer(0);
            AssertSplitPaddingOnEmptyBuffer(data);
            Assert.Equal(0, data.ReferenceCount);
        }

        [Fact]
        public void WriteHeadersUsingVoidPromise()
        {
            var cause = new Http2RuntimeException("fake exception");
            _writer
                .Setup(x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<IHttp2Headers>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, IHttp2Headers, int, bool, IPromise>((c, id, headers, streamDependency, endOfStream, p) =>
                {
                    Assert.False(p.IsVoid);
                    p.SetException(cause);
                    return p.Task;
                });
            CreateStream(STREAM_ID, false);
            // END_STREAM flag, so that a listener is added to the future.
            _encoder.WriteHeadersAsync(_ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, true, Http2TestUtil.NewVoidPromise(_channel.Object));

            _writer.Verify(x => x.WriteHeadersAsync(
                It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                It.Is<int>(v => v == STREAM_ID),
                It.IsAny<IHttp2Headers>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<IPromise>()));
            // When using a void promise, the error should be propagated via the channel pipeline.
            _pipeline.Verify(x => x.FireExceptionCaught(It.Is<Exception>(ex => ex == cause)));
        }

        private void AssertSplitPaddingOnEmptyBuffer(IByteBuffer data)
        {
            CreateStream(STREAM_ID, false);
            _frameSizePolicy.Setup(x => x.MaxFrameSize).Returns(5);
            var p = NewPromise();
            _encoder.WriteDataAsync(_ctx.Object, STREAM_ID, data, 10, true, p);
            Assert.Equal(10, _payloadCaptor.GetValue().Size);
            _payloadCaptor.GetValue().Write(_ctx.Object, 10);
            // writer was called 2 times
            Assert.Single(_writtenData);
            Assert.Equal("", _writtenData[0]);
            Assert.Equal(10, (int)_writtenPadding[0]);
            Assert.Equal(0, data.ReferenceCount);
            Assert.True(p.IsSuccess);
        }

        [Fact]
        public void HeadersWriteForUnknownStreamShouldCreateStream()
        {
            WriteAllFlowControlledFrames();
            int streamId = 6;
            var promise = NewPromise();
            _encoder.WriteHeadersAsync(_ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, false, promise);
            _writer.Verify(x => x.WriteHeadersAsync(
                It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                It.Is<int>(v => v == streamId),
                It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                It.Is<int>(v => v == 0),
                It.Is<bool>(v => v == false),
                It.Is<IPromise>(v => v == promise)));
            Assert.True(promise.IsSuccess);
        }

        [Fact]
        public void HeadersWriteShouldOpenStreamForPush()
        {
            WriteAllFlowControlledFrames();
            IHttp2Stream parent = CreateStream(STREAM_ID, false);
            ReservePushStream(PUSH_STREAM_ID, parent);

            var promise = NewPromise();
            _encoder.WriteHeadersAsync(_ctx.Object, PUSH_STREAM_ID, EmptyHttp2Headers.Instance, 0, false, promise);
            Assert.Equal(Http2StreamState.HalfClosedRemote, Stream(PUSH_STREAM_ID).State);
            _writer.Verify(x => x.WriteHeadersAsync(
                It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                It.Is<int>(v => v == PUSH_STREAM_ID),
                It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                It.Is<int>(v => v == 0),
                It.Is<bool>(v => v == false),
                It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void TrailersDoNotEndStreamThrows()
        {
            WriteAllFlowControlledFrames();
            int streamId = 6;
            var promise = NewPromise();
            _encoder.WriteHeadersAsync(_ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, false, promise);

            var promise2 = NewPromise();
            var future = _encoder.WriteHeadersAsync(_ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, false, promise2);
            Assert.True(future.IsCompleted);
            Assert.False(future.IsSuccess());

            _writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.Is<IPromise>(v => v == promise)),
                Times.Once);
        }

        [Fact]
        public void TrailersDoNotEndStreamWithDataThrows()
        {
            WriteAllFlowControlledFrames();
            int streamId = 6;
            var promise = NewPromise();
            _encoder.WriteHeadersAsync(_ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, false, promise);

            IHttp2Stream stream = _connection.Stream(streamId);
            _remoteFlow.Setup(x => x.HasFlowControlled(It.Is<IHttp2Stream>(v => v == stream))).Returns(true);

            var promise2 = NewPromise();
            var future = _encoder.WriteHeadersAsync(_ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, false, promise2);
            Assert.True(future.IsCompleted);
            Assert.False(future.IsSuccess());

            _writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.Is<IPromise>(v => v == promise)),
                Times.Once);
        }

        [Fact]
        public void TooManyHeadersNoEOSThrows()
        {
            TooManyHeadersThrows(false);
        }

        [Fact]
        public void TooManyHeadersEOSThrows()
        {
            TooManyHeadersThrows(true);
        }

        private void TooManyHeadersThrows(bool eos)
        {
            WriteAllFlowControlledFrames();
            int streamId = 6;
            var promise = NewPromise();
            _encoder.WriteHeadersAsync(_ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, false, promise);
            var promise2 = NewPromise();
            _encoder.WriteHeadersAsync(_ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, true, promise2);

            var promise3 = NewPromise();
            var future = _encoder.WriteHeadersAsync(_ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, eos, promise3);
            Assert.True(future.IsCompleted);
            Assert.False(future.IsSuccess());

            _writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.Is<IPromise>(v => v == promise)),
                Times.Once);
            _writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == promise2)),
                Times.Once);
        }

        [Fact]
        public void InfoHeadersAndTrailersAllowed()
        {
            InfoHeadersAndTrailers(true, 1);
        }

        [Fact]
        public void MultipleInfoHeadersAndTrailersAllowed()
        {
            InfoHeadersAndTrailers(true, 10);
        }

        [Fact]
        public void InfoHeadersAndTrailersNoEOSThrows()
        {
            InfoHeadersAndTrailers(false, 1);
        }

        [Fact]
        public void MultipleInfoHeadersAndTrailersNoEOSThrows()
        {
            InfoHeadersAndTrailers(false, 10);
        }

        private void InfoHeadersAndTrailers(bool eos, int infoHeaderCount)
        {
            WriteAllFlowControlledFrames();
            int streamId = 6;
            IHttp2Headers infoHeaders = InformationalHeaders();
            for (int i = 0; i < infoHeaderCount; ++i)
            {
                _encoder.WriteHeadersAsync(_ctx.Object, streamId, infoHeaders, 0, false, NewPromise());
            }
            var promise2 = NewPromise();
            _encoder.WriteHeadersAsync(_ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, false, promise2);

            var promise3 = NewPromise();
            var future = _encoder.WriteHeadersAsync(_ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, eos, promise3);
            Assert.True(future.IsCompleted);
            Assert.Equal(eos, future.IsSuccess());

            _writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => v.Equals(infoHeaders)),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.IsAny<IPromise>()),
                Times.Exactly(infoHeaderCount));
            _writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.Is<IPromise>(v => v == promise2)),
                Times.Once);
            if (eos)
            {
                _writer.Verify(
                    x => x.WriteHeadersAsync(
                        It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                        It.Is<int>(v => v == streamId),
                        It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
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
            TooManyHeadersWithDataThrows(false);
        }

        [Fact]
        public void TooManyHeadersWithDataEOSThrows()
        {
            TooManyHeadersWithDataThrows(true);
        }

        private void TooManyHeadersWithDataThrows(bool eos)
        {
            WriteAllFlowControlledFrames();
            int streamId = 6;
            var promise = NewPromise();
            _encoder.WriteHeadersAsync(_ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, false, promise);

            var stream = _connection.Stream(streamId);
            _remoteFlow.Setup(x => x.HasFlowControlled(It.Is<IHttp2Stream>(v => v == stream))).Returns(true);

            var promise2 = NewPromise();
            _encoder.WriteHeadersAsync(_ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, true, promise2);

            var promise3 = NewPromise();
            var future = _encoder.WriteHeadersAsync(_ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, eos, promise3);
            Assert.True(future.IsCompleted);
            Assert.False(future.IsSuccess());

            _writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.Is<IPromise>(v => v == promise)),
                Times.Once);
            _writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == promise2)),
                Times.Once);
        }

        [Fact]
        public void InfoHeadersAndTrailersWithDataAllowed()
        {
            InfoHeadersAndTrailersWithData(true, 1);
        }

        [Fact]
        public void MultipleInfoHeadersAndTrailersWithDataAllowed()
        {
            InfoHeadersAndTrailersWithData(true, 10);
        }

        [Fact]
        public void InfoHeadersAndTrailersWithDataNoEOSThrows()
        {
            InfoHeadersAndTrailersWithData(false, 1);
        }

        [Fact]
        public void MultipleInfoHeadersAndTrailersWithDataNoEOSThrows()
        {
            InfoHeadersAndTrailersWithData(false, 10);
        }

        private void InfoHeadersAndTrailersWithData(bool eos, int infoHeaderCount)
        {
            WriteAllFlowControlledFrames();
            int streamId = 6;
            IHttp2Headers infoHeaders = InformationalHeaders();
            for (int i = 0; i < infoHeaderCount; ++i)
            {
                _encoder.WriteHeadersAsync(_ctx.Object, streamId, infoHeaders, 0, false, NewPromise());
            }

            IHttp2Stream stream = _connection.Stream(streamId);
            _remoteFlow.Setup(x => x.HasFlowControlled(It.Is<IHttp2Stream>(v => v == stream))).Returns(true);

            var promise2 = NewPromise();
            _encoder.WriteHeadersAsync(_ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, false, promise2);

            var promise3 = NewPromise();
            var future = _encoder.WriteHeadersAsync(_ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, eos, promise3);
            Assert.True(future.IsCompleted);
            Assert.Equal(eos, future.IsSuccess());

            _writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => v.Equals(infoHeaders)),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.IsAny<IPromise>()),
                Times.Exactly(infoHeaderCount));
            _writer.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.Is<IPromise>(v => v == promise2)),
                Times.Once);
            if (eos)
            {
                _writer.Verify(
                    x => x.WriteHeadersAsync(
                        It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                        It.Is<int>(v => v == streamId),
                        It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                        It.Is<int>(v => v == 0),
                        It.Is<bool>(v => v == true),
                        It.Is<IPromise>(v => v == promise3)),
                    Times.Once);
            }
        }

        [Fact]
        public void PushPromiseWriteAfterGoAwayReceivedShouldFail()
        {
            CreateStream(STREAM_ID, false);
            GoAwayReceived(0);
            var future = _encoder.WritePushPromiseAsync(_ctx.Object, STREAM_ID, PUSH_STREAM_ID, EmptyHttp2Headers.Instance, 0,
                    NewPromise());
            Assert.True(future.IsCompleted);
            Assert.False(future.IsSuccess());
        }

        [Fact]
        public void PushPromiseWriteShouldReserveStream()
        {
            CreateStream(STREAM_ID, false);
            var promise = NewPromise();
            _encoder.WritePushPromiseAsync(_ctx.Object, STREAM_ID, PUSH_STREAM_ID, EmptyHttp2Headers.Instance, 0, promise);
            Assert.Equal(Http2StreamState.ReservedLocal, Stream(PUSH_STREAM_ID).State);
            _writer.Verify(
                x => x.WritePushPromiseAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<int>(v => v == PUSH_STREAM_ID),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void PriorityWriteAfterGoAwayShouldSucceed()
        {
            CreateStream(STREAM_ID, false);
            GoAwayReceived(int.MaxValue);
            var promise = NewPromise();
            _encoder.WritePriorityAsync(_ctx.Object, STREAM_ID, 0, (short)255, true, promise);
            _writer.Verify(
                x => x.WritePriorityAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
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
            _encoder.WritePriorityAsync(_ctx.Object, STREAM_ID, 0, weight, true, promise);

            // Verify that this did NOT create a stream object.
            IHttp2Stream stream = Stream(STREAM_ID);
            Assert.Null(stream);

            _writer.Verify(
                x => x.WritePriorityAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == 255),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void PriorityWriteOnPreviouslyExistingStreamShouldSucceed()
        {
            CreateStream(STREAM_ID, false).Close();
            var promise = NewPromise();
            short weight = 255;
            _encoder.WritePriorityAsync(_ctx.Object, STREAM_ID, 0, weight, true, promise);
            _writer.Verify(
                x => x.WritePriorityAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
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
            CreateStream(STREAM_ID, false);
            CreateStream(parentStreamId, false).Close();

            var promise = NewPromise();
            short weight = 255;
            _encoder.WritePriorityAsync(_ctx.Object, STREAM_ID, parentStreamId, weight, true, promise);
            _writer.Verify(
                x => x.WritePriorityAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
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
            _encoder.WriteRstStreamAsync(_ctx.Object, 5, Http2Error.ProtocolError, promise);
            _writer.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.Is<IPromise>(v => v == promise)),
                Times.Never());
        }

        [Fact]
        public void RstStreamShouldCloseStream()
        {
            // Create the stream and send headers.
            WriteAllFlowControlledFrames();
            _encoder.WriteHeadersAsync(_ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, true, NewPromise());

            // Now verify that a stream reset is performed.
            Stream(STREAM_ID);
            var promise = NewPromise();
            _encoder.WriteRstStreamAsync(_ctx.Object, STREAM_ID, Http2Error.ProtocolError, promise);
            _lifecycleManager.Verify(
                x => x.ResetStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<Http2Error>(),
                    It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void PingWriteAfterGoAwayShouldSucceed()
        {
            var promise = NewPromise();
            GoAwayReceived(0);
            _encoder.WritePingAsync(_ctx.Object, false, 0L, promise);
            _writer.Verify(
                x => x.WritePingAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<bool>(v => v == false),
                    It.Is<long>(v => v == 0L),
                    It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void PingWriteShouldSucceed()
        {
            var promise = NewPromise();
            _encoder.WritePingAsync(_ctx.Object, false, 0L, promise);
            _writer.Verify(
                x => x.WritePingAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<bool>(v => v == false),
                    It.Is<long>(v => v == 0L),
                    It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void SettingsWriteAfterGoAwayShouldSucceed()
        {
            GoAwayReceived(0);
            var promise = NewPromise();
            _encoder.WriteSettingsAsync(_ctx.Object, new Http2Settings(), promise);
            _writer.Verify(
                x => x.WriteSettingsAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
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
            _encoder.WriteSettingsAsync(_ctx.Object, settings, promise);
            _writer.Verify(
                x => x.WriteSettingsAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<Http2Settings>(v => v == settings),
                    It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void DataWriteShouldCreateHalfClosedStream()
        {
            WriteAllFlowControlledFrames();

            IHttp2Stream stream = CreateStream(STREAM_ID, false);
            var data = DummyData();
            var promise = NewPromise();
            _encoder.WriteDataAsync(_ctx.Object, STREAM_ID, (IByteBuffer)data.Retain(), 0, true, promise);
            Assert.True(promise.IsSuccess);
            _remoteFlow.Verify(
                x => x.AddFlowControlled(
                    It.Is<IHttp2Stream>(v => v == stream),
                    It.IsAny<IHttp2RemoteFlowControlled>()));
            _lifecycleManager.Verify(
                x => x.CloseStreamLocal(
                    It.Is<IHttp2Stream>(v => v == stream),
                    It.Is<Task>(v => v == promise.Task)));
            Assert.Equal(data.ToString(Encoding.UTF8), _writtenData[0]);
            data.Release();
        }

        [Fact]
        public void HeadersWriteShouldHalfCloseStream()
        {
            WriteAllFlowControlledFrames();
            CreateStream(STREAM_ID, false);
            var promise = NewPromise();
            _encoder.WriteHeadersAsync(_ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, true, promise);

            Assert.True(promise.IsSuccess);
            _lifecycleManager.Verify(
                x => x.CloseStreamLocal(
                    It.Is<IHttp2Stream>(v => v == Stream(STREAM_ID)),
                    It.Is<Task>(v => v == promise.Task)));
        }

        [Fact]
        public void HeadersWriteShouldHalfClosePushStream()
        {
            WriteAllFlowControlledFrames();
            IHttp2Stream parent = CreateStream(STREAM_ID, false);
            IHttp2Stream stream = ReservePushStream(PUSH_STREAM_ID, parent);
            var promise = NewPromise();
            _encoder.WriteHeadersAsync(_ctx.Object, PUSH_STREAM_ID, EmptyHttp2Headers.Instance, 0, true, promise);
            Assert.Equal(Http2StreamState.HalfClosedRemote, stream.State);
            Assert.True(promise.IsSuccess);
            _lifecycleManager.Verify(
                x => x.CloseStreamLocal(
                    It.Is<IHttp2Stream>(v => v == stream),
                    It.Is<Task>(v => v == promise.Task)));
        }

        [Fact]
        public void HeadersWriteShouldHalfCloseAfterOnErrorForPreCreatedStream()
        {
            IPromise promise = NewPromise();
            var ex = new Http2RuntimeException();
            // Fake an encoding error, like HPACK's HeaderListSizeException
            _writer
                .Setup(x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == promise)))
                .Returns<IChannelHandlerContext, int, IHttp2Headers, int, bool, IPromise>((c, id, headers, streamDependency, endOfStream, p) =>
                {
                    p.SetException(ex);
                    return p.Task;
                });

            WriteAllFlowControlledFrames();
            var stream = CreateStream(STREAM_ID, false);
            _encoder.WriteHeadersAsync(_ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, true, promise);

            Assert.True(promise.IsCompleted);
            Assert.False(promise.IsSuccess);
            Assert.False(stream.IsHeadersSent);
            //InOrder inOrder = inOrder(lifecycleManager);
            //inOrder.verify(lifecycleManager).onError(eq(ctx), eq(true), eq(ex));
            //inOrder.verify(lifecycleManager).closeStreamLocal(eq(stream(STREAM_ID)), eq(promise));
            _lifecycleManager.Verify(
                x => x.OnError(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<bool>(v => v == true),
                    It.Is<Exception>(v => v == ex)));
            _lifecycleManager.Verify(
                x => x.CloseStreamLocal(
                    It.Is<IHttp2Stream>(v => v == Stream(STREAM_ID)),
                    It.Is<Task>(v => v == promise.Task)));
        }

        [Fact]
        public void HeadersWriteShouldHalfCloseAfterOnErrorForImplicitlyCreatedStream()
        {
            IPromise promise = NewPromise();
            var ex = new Http2RuntimeException();
            // Fake an encoding error, like HPACK's HeaderListSizeException
            _writer
                .Setup(x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == promise)))
                .Returns<IChannelHandlerContext, int, IHttp2Headers, int, bool, IPromise>((c, id, headers, streamDependency, endOfStream, p) =>
                {
                    p.SetException(ex);
                    return p.Task;
                });

            WriteAllFlowControlledFrames();
            _encoder.WriteHeadersAsync(_ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, true, promise);

            Assert.True(promise.IsCompleted);
            Assert.False(promise.IsSuccess);
            Assert.False(Stream(STREAM_ID).IsHeadersSent);
            //InOrder inOrder = inOrder(lifecycleManager);
            //inOrder.verify(lifecycleManager).onError(eq(ctx), eq(true), eq(ex));
            //inOrder.verify(lifecycleManager).closeStreamLocal(eq(stream(STREAM_ID)), eq(promise));
            _lifecycleManager.Verify(
                x => x.OnError(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<bool>(v => v == true),
                    It.Is<Exception>(v => v == ex)));
            _lifecycleManager.Verify(
                x => x.CloseStreamLocal(
                    It.Is<IHttp2Stream>(v => v == Stream(STREAM_ID)),
                    It.Is<Task>(v => v == promise.Task)));
        }

        [Fact]
        public void EncoderDelegatesGoAwayToLifeCycleManager()
        {
            var promise = NewPromise();
            _encoder.WriteGoAwayAsync(_ctx.Object, STREAM_ID, Http2Error.InternalError, null, promise);
            _lifecycleManager.Verify(
                x => x.GoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.InternalError),
                    It.Is<IByteBuffer>(v => v == null),
                    It.Is<IPromise>(v => v == promise)));
            _writer.VerifyNoOtherCalls();
        }

        [Fact]
        public void DataWriteToClosedStreamShouldFail()
        {
            CreateStream(STREAM_ID, false).Close();
            var data = new Mock<IByteBuffer>();
            var promise = NewPromise();
            _encoder.WriteDataAsync(_ctx.Object, STREAM_ID, data.Object, 0, false, promise);
            Assert.True(promise.IsCompleted);
            Assert.False(promise.IsSuccess);
            Assert.IsType<ArgumentException>(promise.Task.Exception.InnerException);
            data.Verify(x => x.Release());
        }

        [Fact]
        public void DataWriteToHalfClosedLocalStreamShouldFail()
        {
            CreateStream(STREAM_ID, true);
            var data = new Mock<IByteBuffer>();
            var promise = NewPromise();
            _encoder.WriteDataAsync(_ctx.Object, STREAM_ID, data.Object, 0, false, promise);
            Assert.True(promise.IsCompleted);
            Assert.False(promise.IsSuccess);
            Assert.IsType<InvalidOperationException>(promise.Task.Exception.InnerException);
            data.Verify(x => x.Release());
        }

        [Fact]
        public void CanWriteDataFrameAfterGoAwaySent()
        {
            IHttp2Stream stream = CreateStream(STREAM_ID, false);
            _connection.GoAwaySent(0, 0, Unpooled.Empty);
            var data = new Mock<IByteBuffer>();
            _encoder.WriteDataAsync(_ctx.Object, STREAM_ID, data.Object, 0, false, NewPromise());
            _remoteFlow.Verify(
                x => x.AddFlowControlled(
                    It.Is<IHttp2Stream>(v => v == stream),
                    It.IsAny<IHttp2RemoteFlowControlled>()));
        }

        [Fact]
        public void CanWriteHeaderFrameAfterGoAwaySent()
        {
            WriteAllFlowControlledFrames();
            CreateStream(STREAM_ID, false);
            GoAwaySent(0);
            var promise = NewPromise();
            _encoder.WriteHeadersAsync(_ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, false, promise);
            _writer.Verify(x => x.WriteHeadersAsync(
                It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                It.Is<int>(v => v == STREAM_ID),
                It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                It.Is<int>(v => v == 0),
                It.Is<bool>(v => v == false),
                It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void CanWriteDataFrameAfterGoAwayReceived()
        {
            IHttp2Stream stream = CreateStream(STREAM_ID, false);
            GoAwayReceived(STREAM_ID);
            var data = new Mock<IByteBuffer>();
            _encoder.WriteDataAsync(_ctx.Object, STREAM_ID, data.Object, 0, false, NewPromise());
            _remoteFlow.Verify(
                x => x.AddFlowControlled(
                    It.Is<IHttp2Stream>(v => v == stream),
                    It.IsAny<IHttp2RemoteFlowControlled>()));
        }

        [Fact]
        public void CanWriteHeaderFrameAfterGoAwayReceived()
        {
            WriteAllFlowControlledFrames();
            GoAwayReceived(STREAM_ID);
            var promise = NewPromise();
            _encoder.WriteHeadersAsync(_ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, false, promise);
            _writer.Verify(x => x.WriteHeadersAsync(
                It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                It.Is<int>(v => v == STREAM_ID),
                It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                It.Is<int>(v => v == 0),
                It.Is<bool>(v => v == false),
                It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void HeadersWithNoPriority()
        {
            WriteAllFlowControlledFrames();
            int streamId = 6;
            var promise = NewPromise();
            _encoder.WriteHeadersAsync(_ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, false, promise);
            _writer.Verify(x => x.WriteHeadersAsync(
                It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                It.Is<int>(v => v == streamId),
                It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                It.Is<int>(v => v == 0),
                It.Is<bool>(v => v == false),
                It.Is<IPromise>(v => v == promise)));
        }

        [Fact]
        public void HeadersWithPriority()
        {
            WriteAllFlowControlledFrames();
            int streamId = 6;
            var promise = NewPromise();
            _encoder.WriteHeadersAsync(_ctx.Object, streamId, EmptyHttp2Headers.Instance, 10, Http2CodecUtil.DefaultPriorityWeight,
                    true, 1, false, promise);
            _writer.Verify(x => x.WriteHeadersAsync(
                It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                It.Is<int>(v => v == streamId),
                It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                It.Is<int>(v => v == 10),
                It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                It.Is<bool>(v => v == true),
                It.Is<int>(v => v == 1),
                It.Is<bool>(v => v == false),
                It.Is<IPromise>(v => v == promise)));
        }

        private void WriteAllFlowControlledFrames()
        {
            _remoteFlow
                .Setup(x => x.AddFlowControlled(
                    It.IsAny<IHttp2Stream>(),
                    It.Is<IHttp2RemoteFlowControlled>(v => _payloadCaptor.Capture(v))))
                .Callback<IHttp2Stream, IHttp2RemoteFlowControlled>((s, f) =>
                {
                    f.Write(_ctx.Object, int.MaxValue);
                    f.WriteComplete();
                });
        }

        private IHttp2Stream CreateStream(int streamId, bool halfClosed)
        {
            return _connection.Local.CreateStream(streamId, halfClosed);
        }

        private IHttp2Stream ReservePushStream(int pushStreamId, IHttp2Stream parent)
        {
            return _connection.Local.ReservePushStream(pushStreamId, parent);
        }

        private IHttp2Stream Stream(int streamId)
        {
            return _connection.Stream(streamId);
        }

        private void GoAwayReceived(int lastStreamId)
        {
            _connection.GoAwayReceived(lastStreamId, 0, Unpooled.Empty);
        }

        private void GoAwaySent(int lastStreamId)
        {
            _connection.GoAwaySent(lastStreamId, 0, Unpooled.Empty);
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
