
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;
    using Moq;
    using Xunit;

    public class DefaultHttp2LocalFlowControllerTest : IDisposable
    {
        private static readonly int STREAM_ID = 1;

        private DefaultHttp2LocalFlowController controller;

        private Mock<IHttp2FrameWriter> _frameWriter;
        private Mock<IChannelHandlerContext> _ctx;
        private Mock<IEventExecutor> _executor;
        private Mock<IPromise> _promise;

        private DefaultHttp2Connection _connection;

        public DefaultHttp2LocalFlowControllerTest()
        {
            _frameWriter = new Mock<IHttp2FrameWriter>();
            _ctx = new Mock<IChannelHandlerContext>();
            _executor = new Mock<IEventExecutor>();
            _promise = new Mock<IPromise>();

            SetupChannelHandlerContext(false);
            _ctx.Setup(x => x.Executor.InEventLoop).Returns(true);

            InitController(false);
        }

        private void SetupChannelHandlerContext(bool allowFlush)
        {
            _ctx.Reset();

            _ctx.Setup(x => x.NewPromise()).Returns(_promise.Object);
            _ctx.Setup(x => x.NewPromise(It.IsAny<object>())).Returns(_promise.Object);
            if (allowFlush)
            {
                _ctx.Setup(x => x.Flush()).Returns(() =>
                {
                    return _ctx.Object;
                });
            }
            else
            {
                _ctx.Setup(x => x.Flush()).Throws(new Exception("forbidden"));
            }
            _ctx.Setup(x => x.Executor).Returns(_executor.Object);
        }

        public void Dispose()
        {
        }

        [Fact]
        public void DataFrameShouldBeAccepted()
        {
            ReceiveFlowControlledFrame(STREAM_ID, 10, 0, false);
            VerifyWindowUpdateNotSent();
        }

        [Fact]
        public void WindowUpdateShouldSendOnceBytesReturned()
        {
            int dataSize = (int)(Http2CodecUtil.DefaultWindowSize * DefaultHttp2LocalFlowController.DefaultWindowUpdateRatio) + 1;
            ReceiveFlowControlledFrame(STREAM_ID, dataSize, 0, false);

            // Return only a few bytes and verify that the WINDOW_UPDATE hasn't been sent.
            Assert.False(ConsumeBytes(STREAM_ID, 10));
            VerifyWindowUpdateNotSent(STREAM_ID);
            VerifyWindowUpdateNotSent(Http2CodecUtil.ConnectionStreamId);

            // Return the rest and verify the WINDOW_UPDATE is sent.
            Assert.True(ConsumeBytes(STREAM_ID, dataSize - 10));
            VerifyWindowUpdateSent(STREAM_ID, dataSize);
            VerifyWindowUpdateSent(Http2CodecUtil.ConnectionStreamId, dataSize);
            _frameWriter.VerifyNoOtherCalls();
        }

        [Fact]
        public void ConnectionWindowShouldAutoRefillWhenDataReceived()
        {
            // Reconfigure controller to auto-refill the connection window.
            InitController(true);

            int dataSize = (int)(Http2CodecUtil.DefaultWindowSize * DefaultHttp2LocalFlowController.DefaultWindowUpdateRatio) + 1;
            ReceiveFlowControlledFrame(STREAM_ID, dataSize, 0, false);
            // Verify that we immediately refill the connection window.
            VerifyWindowUpdateSent(Http2CodecUtil.ConnectionStreamId, dataSize);

            // Return only a few bytes and verify that the WINDOW_UPDATE hasn't been sent for the stream.
            Assert.False(ConsumeBytes(STREAM_ID, 10));
            VerifyWindowUpdateNotSent(STREAM_ID);

            // Return the rest and verify the WINDOW_UPDATE is sent for the stream.
            Assert.True(ConsumeBytes(STREAM_ID, dataSize - 10));
            VerifyWindowUpdateSent(STREAM_ID, dataSize);
            _frameWriter.VerifyNoOtherCalls();
        }

        [Fact]
        public void ConnectionFlowControlExceededShouldThrow()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                // Window exceeded because of the padding.
                ReceiveFlowControlledFrame(STREAM_ID, Http2CodecUtil.DefaultWindowSize, 1, true);
            });
        }

        [Fact]
        public void WindowUpdateShouldNotBeSentAfterEndOfStream()
        {
            int dataSize = (int)(Http2CodecUtil.DefaultWindowSize * DefaultHttp2LocalFlowController.DefaultWindowUpdateRatio) + 1;

            // Set end-of-stream on the frame, so no window update will be sent for the stream.
            ReceiveFlowControlledFrame(STREAM_ID, dataSize, 0, true);
            VerifyWindowUpdateNotSent(Http2CodecUtil.ConnectionStreamId);
            VerifyWindowUpdateNotSent(STREAM_ID);

            Assert.True(ConsumeBytes(STREAM_ID, dataSize));
            VerifyWindowUpdateSent(Http2CodecUtil.ConnectionStreamId, dataSize);
            VerifyWindowUpdateNotSent(STREAM_ID);
        }

        [Fact]
        public void WindowUpdateShouldNotBeSentAfterStreamIsClosedForUnconsumedBytes()
        {
            int dataSize = (int)(Http2CodecUtil.DefaultWindowSize * DefaultHttp2LocalFlowController.DefaultWindowUpdateRatio) + 1;

            // Don't set end-of-stream on the frame as we want to verify that we not return the unconsumed bytes in this
            // case once the stream was closed,
            ReceiveFlowControlledFrame(STREAM_ID, dataSize, 0, false);
            VerifyWindowUpdateNotSent(Http2CodecUtil.ConnectionStreamId);
            VerifyWindowUpdateNotSent(STREAM_ID);

            // Close the stream
            IHttp2Stream stream = _connection.Stream(STREAM_ID);
            stream.Close();
            Assert.Equal(Http2StreamState.Closed, stream.State);
            Assert.Null(_connection.Stream(STREAM_ID));

            // The window update for the connection should made it through but not the update for the already closed
            // stream
            VerifyWindowUpdateSent(Http2CodecUtil.ConnectionStreamId, dataSize);
            VerifyWindowUpdateNotSent(STREAM_ID);
        }

        [Fact]
        public void WindowUpdateShouldBeWrittenWhenStreamIsClosedAndFlushed()
        {
            int dataSize = (int)(Http2CodecUtil.DefaultWindowSize * DefaultHttp2LocalFlowController.DefaultWindowUpdateRatio) + 1;

            SetupChannelHandlerContext(true);

            ReceiveFlowControlledFrame(STREAM_ID, dataSize, 0, false);
            VerifyWindowUpdateNotSent(Http2CodecUtil.ConnectionStreamId);
            VerifyWindowUpdateNotSent(STREAM_ID);

            _connection.Stream(STREAM_ID).Close();

            VerifyWindowUpdateSent(Http2CodecUtil.ConnectionStreamId, dataSize);

            // Verify we saw one flush.
            _ctx.Verify(x => x.Flush());
        }

        [Fact]
        public void HalfWindowRemainingShouldUpdateAllWindows()
        {
            int dataSize = (int)(Http2CodecUtil.DefaultWindowSize * DefaultHttp2LocalFlowController.DefaultWindowUpdateRatio) + 1;
            int initialWindowSize = Http2CodecUtil.DefaultWindowSize;
            int windowDelta = GetWindowDelta(initialWindowSize, initialWindowSize, dataSize);

            // Don't set end-of-stream so we'll get a window update for the stream as well.
            ReceiveFlowControlledFrame(STREAM_ID, dataSize, 0, false);
            Assert.True(ConsumeBytes(STREAM_ID, dataSize));
            VerifyWindowUpdateSent(Http2CodecUtil.ConnectionStreamId, windowDelta);
            VerifyWindowUpdateSent(STREAM_ID, windowDelta);
        }

        [Fact]
        public void InitialWindowUpdateShouldAllowMoreFrames()
        {
            // Send a frame that takes up the entire window.
            int initialWindowSize = Http2CodecUtil.DefaultWindowSize;
            ReceiveFlowControlledFrame(STREAM_ID, initialWindowSize, 0, false);
            Assert.Equal(0, Window(STREAM_ID));
            Assert.Equal(0, Window(Http2CodecUtil.ConnectionStreamId));
            ConsumeBytes(STREAM_ID, initialWindowSize);
            Assert.Equal(initialWindowSize, Window(STREAM_ID));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, Window(Http2CodecUtil.ConnectionStreamId));

            // Update the initial window size to allow another frame.
            int newInitialWindowSize = 2 * initialWindowSize;
            controller.SetInitialWindowSize(newInitialWindowSize);
            Assert.Equal(newInitialWindowSize, Window(STREAM_ID));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, Window(Http2CodecUtil.ConnectionStreamId));

            // Clear any previous calls to the writer.
            _frameWriter.Reset();

            // Send the next frame and verify that the expected window updates were sent.
            ReceiveFlowControlledFrame(STREAM_ID, initialWindowSize, 0, false);
            Assert.True(ConsumeBytes(STREAM_ID, initialWindowSize));
            int delta = newInitialWindowSize - initialWindowSize;
            VerifyWindowUpdateSent(STREAM_ID, delta);
            VerifyWindowUpdateSent(Http2CodecUtil.ConnectionStreamId, delta);
        }

        [Fact]
        public void ConnectionWindowShouldAdjustWithMultipleStreams()
        {
            int newStreamId = 3;
            _connection.Local.CreateStream(newStreamId, false);

            try
            {
                Assert.Equal(Http2CodecUtil.DefaultWindowSize, Window(STREAM_ID));
                Assert.Equal(Http2CodecUtil.DefaultWindowSize, Window(Http2CodecUtil.ConnectionStreamId));

                // Test that both stream and connection window are updated (or not updated) together
                int data1 = (int)(Http2CodecUtil.DefaultWindowSize * DefaultHttp2LocalFlowController.DefaultWindowUpdateRatio) + 1;
                ReceiveFlowControlledFrame(STREAM_ID, data1, 0, false);
                VerifyWindowUpdateNotSent(STREAM_ID);
                VerifyWindowUpdateNotSent(Http2CodecUtil.ConnectionStreamId);
                Assert.Equal(Http2CodecUtil.DefaultWindowSize - data1, Window(STREAM_ID));
                Assert.Equal(Http2CodecUtil.DefaultWindowSize - data1, Window(Http2CodecUtil.ConnectionStreamId));
                Assert.True(ConsumeBytes(STREAM_ID, data1));
                VerifyWindowUpdateSent(STREAM_ID, data1);
                VerifyWindowUpdateSent(Http2CodecUtil.ConnectionStreamId, data1);

                _frameWriter.Reset();

                // Create a scenario where data is depleted from multiple streams, but not enough data
                // to generate a window update on those streams. The amount will be enough to generate
                // a window update for the connection stream.
                --data1;
                int data2 = data1 >> 1;
                ReceiveFlowControlledFrame(STREAM_ID, data1, 0, false);
                ReceiveFlowControlledFrame(newStreamId, data1, 0, false);
                VerifyWindowUpdateNotSent(STREAM_ID);
                VerifyWindowUpdateNotSent(newStreamId);
                VerifyWindowUpdateNotSent(Http2CodecUtil.ConnectionStreamId);
                Assert.Equal(Http2CodecUtil.DefaultWindowSize - data1, Window(STREAM_ID));
                Assert.Equal(Http2CodecUtil.DefaultWindowSize - data1, Window(newStreamId));
                Assert.Equal(Http2CodecUtil.DefaultWindowSize - (data1 << 1), Window(Http2CodecUtil.ConnectionStreamId));
                Assert.False(ConsumeBytes(STREAM_ID, data1));
                Assert.True(ConsumeBytes(newStreamId, data2));
                VerifyWindowUpdateNotSent(STREAM_ID);
                VerifyWindowUpdateNotSent(newStreamId);
                VerifyWindowUpdateSent(Http2CodecUtil.ConnectionStreamId, data1 + data2);
                Assert.Equal(Http2CodecUtil.DefaultWindowSize - data1, Window(STREAM_ID));
                Assert.Equal(Http2CodecUtil.DefaultWindowSize - data1, Window(newStreamId));
                Assert.Equal(Http2CodecUtil.DefaultWindowSize - (data1 - data2), Window(Http2CodecUtil.ConnectionStreamId));
            }
            finally
            {
                _connection.Stream(newStreamId).Close();
            }
        }

        [Fact]
        public void CloseShouldConsumeBytes()
        {
            ReceiveFlowControlledFrame(STREAM_ID, 10, 0, false);
            Assert.Equal(10, controller.UnconsumedBytes(_connection.ConnectionStream));
            Stream(STREAM_ID).Close();
            Assert.Equal(0, controller.UnconsumedBytes(_connection.ConnectionStream));
        }

        [Fact]
        public void CloseShouldNotConsumeConnectionWindowWhenAutoRefilled()
        {
            // Reconfigure controller to auto-refill the connection window.
            InitController(true);

            ReceiveFlowControlledFrame(STREAM_ID, 10, 0, false);
            Assert.Equal(0, controller.UnconsumedBytes(_connection.ConnectionStream));
            Stream(STREAM_ID).Close();
            Assert.Equal(0, controller.UnconsumedBytes(_connection.ConnectionStream));
        }

        [Fact]
        public void DataReceivedForClosedStreamShouldImmediatelyConsumeBytes()
        {
            IHttp2Stream stream = Stream(STREAM_ID);
            stream.Close();
            ReceiveFlowControlledFrame(stream, 10, 0, false);
            Assert.Equal(0, controller.UnconsumedBytes(_connection.ConnectionStream));
        }

        [Fact]
        public void DataReceivedForNullStreamShouldImmediatelyConsumeBytes()
        {
            ReceiveFlowControlledFrame(null, 10, 0, false);
            Assert.Equal(0, controller.UnconsumedBytes(_connection.ConnectionStream));
        }

        [Fact]
        public void ConsumeBytesForNullStreamShouldIgnore()
        {
            controller.ConsumeBytes(null, 10);
            Assert.Equal(0, controller.UnconsumedBytes(_connection.ConnectionStream));
        }

        [Fact]
        public void GlobalRatioShouldImpactStreams()
        {
            float ratio = 0.6f;
            controller.WindowUpdateRatio(ratio);
            TestRatio(ratio, Http2CodecUtil.DefaultWindowSize << 1, 3, false);
        }

        [Fact]
        public void StreamlRatioShouldImpactStreams()
        {
            float ratio = 0.6f;
            TestRatio(ratio, Http2CodecUtil.DefaultWindowSize << 1, 3, true);
        }

        [Fact]
        public void ConsumeBytesForZeroNumBytesShouldIgnore()
        {
            Assert.False(controller.ConsumeBytes(_connection.Stream(STREAM_ID), 0));
        }

        [Fact]
        public void ConsumeBytesForNegativeNumBytesShouldFail()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                Assert.False(controller.ConsumeBytes(_connection.Stream(STREAM_ID), -1));
            });
        }

        private void TestRatio(float ratio, int newDefaultWindowSize, int newStreamId, bool setStreamRatio)
        {
            int delta = newDefaultWindowSize - Http2CodecUtil.DefaultWindowSize;
            controller.IncrementWindowSize(Stream(0), delta);
            IHttp2Stream stream = _connection.Local.CreateStream(newStreamId, false);
            if (setStreamRatio)
            {
                controller.WindowUpdateRatio(stream, ratio);
            }
            controller.IncrementWindowSize(stream, delta);
            _frameWriter.Reset();
            try
            {
                int data1 = (int)(newDefaultWindowSize * ratio) + 1;
                int data2 = (int)(Http2CodecUtil.DefaultWindowSize * DefaultHttp2LocalFlowController.DefaultWindowUpdateRatio) >> 1;
                ReceiveFlowControlledFrame(STREAM_ID, data2, 0, false);
                ReceiveFlowControlledFrame(newStreamId, data1, 0, false);
                VerifyWindowUpdateNotSent(STREAM_ID);
                VerifyWindowUpdateNotSent(newStreamId);
                VerifyWindowUpdateNotSent(Http2CodecUtil.ConnectionStreamId);
                Assert.Equal(Http2CodecUtil.DefaultWindowSize - data2, Window(STREAM_ID));
                Assert.Equal(newDefaultWindowSize - data1, Window(newStreamId));
                Assert.Equal(newDefaultWindowSize - data2 - data1, Window(Http2CodecUtil.ConnectionStreamId));
                Assert.False(ConsumeBytes(STREAM_ID, data2));
                Assert.True(ConsumeBytes(newStreamId, data1));
                VerifyWindowUpdateNotSent(STREAM_ID);
                VerifyWindowUpdateSent(newStreamId, data1);
                VerifyWindowUpdateSent(Http2CodecUtil.ConnectionStreamId, data1 + data2);
                Assert.Equal(Http2CodecUtil.DefaultWindowSize - data2, Window(STREAM_ID));
                Assert.Equal(newDefaultWindowSize, Window(newStreamId));
                Assert.Equal(newDefaultWindowSize, Window(Http2CodecUtil.ConnectionStreamId));
            }
            finally
            {
                _connection.Stream(newStreamId).Close();
            }
        }

        private static int GetWindowDelta(int initialSize, int windowSize, int dataSize)
        {
            int newWindowSize = windowSize - dataSize;
            return initialSize - newWindowSize;
        }

        private void ReceiveFlowControlledFrame(int streamId, int dataSize, int padding,
                                                bool endOfStream)
        {
            ReceiveFlowControlledFrame(Stream(streamId), dataSize, padding, endOfStream);
        }

        private void ReceiveFlowControlledFrame(IHttp2Stream stream, int dataSize, int padding, bool endOfStream)
        {
            var buf = DummyData(dataSize);
            try
            {
                controller.ReceiveFlowControlledFrame(stream, buf, padding, endOfStream);
            }
            finally
            {
                buf.Release();
            }
        }

        private static IByteBuffer DummyData(int size)
        {
            var buffer = Unpooled.Buffer(size);
            buffer.SetWriterIndex(size);
            return buffer;
        }

        private bool ConsumeBytes(int streamId, int numBytes)
        {
            return controller.ConsumeBytes(Stream(streamId), numBytes);
        }

        private void VerifyWindowUpdateSent(int streamId, int windowSizeIncrement)
        {
            _frameWriter.Verify(x => x.WriteWindowUpdateAsync(
                It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                It.Is<int>(v => v == streamId),
                It.Is<int>(v => v == windowSizeIncrement),
                It.Is<IPromise>(v => v == _promise.Object)));
        }

        private void VerifyWindowUpdateNotSent(int streamId)
        {
            _frameWriter.Verify(x => x.WriteWindowUpdateAsync(
                It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                It.Is<int>(v => v == streamId), It.IsAny<int>(),
                It.Is<IPromise>(v => v == _promise.Object)), Times.Never());
        }

        private void VerifyWindowUpdateNotSent()
        {
            _frameWriter.Verify(x => x.WriteWindowUpdateAsync(It.IsAny<IChannelHandlerContext>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IPromise>()), Times.Never());
        }

        private int Window(int streamId)
        {
            return controller.GetWindowSize(Stream(streamId));
        }

        private IHttp2Stream Stream(int streamId)
        {
            return _connection.Stream(streamId);
        }

        private void InitController(bool autoRefillConnectionWindow)
        {
            _connection = new DefaultHttp2Connection(false);
            controller = new DefaultHttp2LocalFlowController(_connection, DefaultHttp2LocalFlowController.DefaultWindowUpdateRatio, autoRefillConnectionWindow);
            controller.FrameWriter(_frameWriter.Object);
            _connection.Local.FlowController = controller;
            _connection.Local.CreateStream(STREAM_ID, false);
            controller.SetChannelHandlerContext(_ctx.Object);
        }

    }
}
