
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

        private Mock<IHttp2FrameWriter> frameWriter;
        private Mock<IChannelHandlerContext> ctx;
        private Mock<IEventExecutor> executor;
        private Mock<IPromise> promise;

        private DefaultHttp2Connection connection;

        public DefaultHttp2LocalFlowControllerTest()
        {
            this.frameWriter = new Mock<IHttp2FrameWriter>();
            this.ctx = new Mock<IChannelHandlerContext>();
            this.executor = new Mock<IEventExecutor>();
            this.promise = new Mock<IPromise>();

            this.ctx.Setup(x => x.NewPromise()).Returns(this.promise.Object);
            this.ctx.Setup(x => x.NewPromise(It.IsAny<object>())).Returns(this.promise.Object);
            this.ctx.Setup(x => x.Flush()).Throws(new Exception("forbidden"));
            this.ctx.Setup(x => x.Executor).Returns(this.executor.Object);
            this.ctx.Setup(x => x.Executor.InEventLoop).Returns(true);

            this.InitController(false);
        }

        public void Dispose()
        {
        }

        [Fact]
        public void DataFrameShouldBeAccepted()
        {
            this.ReceiveFlowControlledFrame(STREAM_ID, 10, 0, false);
            this.VerifyWindowUpdateNotSent();
        }

        [Fact]
        public void WindowUpdateShouldSendOnceBytesReturned()
        {
            int dataSize = (int)(Http2CodecUtil.DefaultWindowSize * DefaultHttp2LocalFlowController.DefaultWindowUpdateRatio) + 1;
            this.ReceiveFlowControlledFrame(STREAM_ID, dataSize, 0, false);

            // Return only a few bytes and verify that the WINDOW_UPDATE hasn't been sent.
            Assert.False(this.ConsumeBytes(STREAM_ID, 10));
            this.VerifyWindowUpdateNotSent(STREAM_ID);
            this.VerifyWindowUpdateNotSent(Http2CodecUtil.ConnectionStreamId);

            // Return the rest and verify the WINDOW_UPDATE is sent.
            Assert.True(this.ConsumeBytes(STREAM_ID, dataSize - 10));
            this.VerifyWindowUpdateSent(STREAM_ID, dataSize);
            this.VerifyWindowUpdateSent(Http2CodecUtil.ConnectionStreamId, dataSize);
            this.frameWriter.VerifyNoOtherCalls();
        }

        [Fact]
        public void ConnectionWindowShouldAutoRefillWhenDataReceived()
        {
            // Reconfigure controller to auto-refill the connection window.
            this.InitController(true);

            int dataSize = (int)(Http2CodecUtil.DefaultWindowSize * DefaultHttp2LocalFlowController.DefaultWindowUpdateRatio) + 1;
            this.ReceiveFlowControlledFrame(STREAM_ID, dataSize, 0, false);
            // Verify that we immediately refill the connection window.
            this.VerifyWindowUpdateSent(Http2CodecUtil.ConnectionStreamId, dataSize);

            // Return only a few bytes and verify that the WINDOW_UPDATE hasn't been sent for the stream.
            Assert.False(this.ConsumeBytes(STREAM_ID, 10));
            this.VerifyWindowUpdateNotSent(STREAM_ID);

            // Return the rest and verify the WINDOW_UPDATE is sent for the stream.
            Assert.True(this.ConsumeBytes(STREAM_ID, dataSize - 10));
            this.VerifyWindowUpdateSent(STREAM_ID, dataSize);
            this.frameWriter.VerifyNoOtherCalls();
        }

        [Fact]
        public void ConnectionFlowControlExceededShouldThrow()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                // Window exceeded because of the padding.
                this.ReceiveFlowControlledFrame(STREAM_ID, Http2CodecUtil.DefaultWindowSize, 1, true);
            });
        }

        [Fact]
        public void WindowUpdateShouldNotBeSentAfterEndOfStream()
        {
            int dataSize = (int)(Http2CodecUtil.DefaultWindowSize * DefaultHttp2LocalFlowController.DefaultWindowUpdateRatio) + 1;

            // Set end-of-stream on the frame, so no window update will be sent for the stream.
            this.ReceiveFlowControlledFrame(STREAM_ID, dataSize, 0, true);
            this.VerifyWindowUpdateNotSent(Http2CodecUtil.ConnectionStreamId);
            this.VerifyWindowUpdateNotSent(STREAM_ID);

            Assert.True(this.ConsumeBytes(STREAM_ID, dataSize));
            this.VerifyWindowUpdateSent(Http2CodecUtil.ConnectionStreamId, dataSize);
            this.VerifyWindowUpdateNotSent(STREAM_ID);
        }

        [Fact]
        public void HalfWindowRemainingShouldUpdateAllWindows()
        {
            int dataSize = (int)(Http2CodecUtil.DefaultWindowSize * DefaultHttp2LocalFlowController.DefaultWindowUpdateRatio) + 1;
            int initialWindowSize = Http2CodecUtil.DefaultWindowSize;
            int windowDelta = GetWindowDelta(initialWindowSize, initialWindowSize, dataSize);

            // Don't set end-of-stream so we'll get a window update for the stream as well.
            this.ReceiveFlowControlledFrame(STREAM_ID, dataSize, 0, false);
            Assert.True(this.ConsumeBytes(STREAM_ID, dataSize));
            this.VerifyWindowUpdateSent(Http2CodecUtil.ConnectionStreamId, windowDelta);
            this.VerifyWindowUpdateSent(STREAM_ID, windowDelta);
        }

        [Fact]
        public void InitialWindowUpdateShouldAllowMoreFrames()
        {
            // Send a frame that takes up the entire window.
            int initialWindowSize = Http2CodecUtil.DefaultWindowSize;
            this.ReceiveFlowControlledFrame(STREAM_ID, initialWindowSize, 0, false);
            Assert.Equal(0, this.Window(STREAM_ID));
            Assert.Equal(0, this.Window(Http2CodecUtil.ConnectionStreamId));
            this.ConsumeBytes(STREAM_ID, initialWindowSize);
            Assert.Equal(initialWindowSize, this.Window(STREAM_ID));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(Http2CodecUtil.ConnectionStreamId));

            // Update the initial window size to allow another frame.
            int newInitialWindowSize = 2 * initialWindowSize;
            this.controller.SetInitialWindowSize(newInitialWindowSize);
            Assert.Equal(newInitialWindowSize, this.Window(STREAM_ID));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(Http2CodecUtil.ConnectionStreamId));

            // Clear any previous calls to the writer.
            this.frameWriter.Reset();

            // Send the next frame and verify that the expected window updates were sent.
            this.ReceiveFlowControlledFrame(STREAM_ID, initialWindowSize, 0, false);
            Assert.True(this.ConsumeBytes(STREAM_ID, initialWindowSize));
            int delta = newInitialWindowSize - initialWindowSize;
            this.VerifyWindowUpdateSent(STREAM_ID, delta);
            this.VerifyWindowUpdateSent(Http2CodecUtil.ConnectionStreamId, delta);
        }

        [Fact]
        public void ConnectionWindowShouldAdjustWithMultipleStreams()
        {
            int newStreamId = 3;
            connection.Local.CreateStream(newStreamId, false);

            try
            {
                Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(STREAM_ID));
                Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(Http2CodecUtil.ConnectionStreamId));

                // Test that both stream and connection window are updated (or not updated) together
                int data1 = (int)(Http2CodecUtil.DefaultWindowSize * DefaultHttp2LocalFlowController.DefaultWindowUpdateRatio) + 1;
                this.ReceiveFlowControlledFrame(STREAM_ID, data1, 0, false);
                this.VerifyWindowUpdateNotSent(STREAM_ID);
                this.VerifyWindowUpdateNotSent(Http2CodecUtil.ConnectionStreamId);
                Assert.Equal(Http2CodecUtil.DefaultWindowSize - data1, this.Window(STREAM_ID));
                Assert.Equal(Http2CodecUtil.DefaultWindowSize - data1, this.Window(Http2CodecUtil.ConnectionStreamId));
                Assert.True(this.ConsumeBytes(STREAM_ID, data1));
                this.VerifyWindowUpdateSent(STREAM_ID, data1);
                this.VerifyWindowUpdateSent(Http2CodecUtil.ConnectionStreamId, data1);

                this.frameWriter.Reset();

                // Create a scenario where data is depleted from multiple streams, but not enough data
                // to generate a window update on those streams. The amount will be enough to generate
                // a window update for the connection stream.
                --data1;
                int data2 = data1 >> 1;
                this.ReceiveFlowControlledFrame(STREAM_ID, data1, 0, false);
                this.ReceiveFlowControlledFrame(newStreamId, data1, 0, false);
                this.VerifyWindowUpdateNotSent(STREAM_ID);
                this.VerifyWindowUpdateNotSent(newStreamId);
                this.VerifyWindowUpdateNotSent(Http2CodecUtil.ConnectionStreamId);
                Assert.Equal(Http2CodecUtil.DefaultWindowSize - data1, this.Window(STREAM_ID));
                Assert.Equal(Http2CodecUtil.DefaultWindowSize - data1, this.Window(newStreamId));
                Assert.Equal(Http2CodecUtil.DefaultWindowSize - (data1 << 1), this.Window(Http2CodecUtil.ConnectionStreamId));
                Assert.False(this.ConsumeBytes(STREAM_ID, data1));
                Assert.True(this.ConsumeBytes(newStreamId, data2));
                this.VerifyWindowUpdateNotSent(STREAM_ID);
                this.VerifyWindowUpdateNotSent(newStreamId);
                this.VerifyWindowUpdateSent(Http2CodecUtil.ConnectionStreamId, data1 + data2);
                Assert.Equal(Http2CodecUtil.DefaultWindowSize - data1, this.Window(STREAM_ID));
                Assert.Equal(Http2CodecUtil.DefaultWindowSize - data1, this.Window(newStreamId));
                Assert.Equal(Http2CodecUtil.DefaultWindowSize - (data1 - data2), this.Window(Http2CodecUtil.ConnectionStreamId));
            }
            finally
            {
                connection.Stream(newStreamId).Close();
            }
        }

        [Fact]
        public void CloseShouldConsumeBytes()
        {
            this.ReceiveFlowControlledFrame(STREAM_ID, 10, 0, false);
            Assert.Equal(10, controller.UnconsumedBytes(connection.ConnectionStream));
            this.Stream(STREAM_ID).Close();
            Assert.Equal(0, controller.UnconsumedBytes(connection.ConnectionStream));
        }

        [Fact]
        public void CloseShouldNotConsumeConnectionWindowWhenAutoRefilled()
        {
            // Reconfigure controller to auto-refill the connection window.
            this.InitController(true);

            this.ReceiveFlowControlledFrame(STREAM_ID, 10, 0, false);
            Assert.Equal(0, controller.UnconsumedBytes(connection.ConnectionStream));
            this.Stream(STREAM_ID).Close();
            Assert.Equal(0, controller.UnconsumedBytes(connection.ConnectionStream));
        }

        [Fact]
        public void DataReceivedForClosedStreamShouldImmediatelyConsumeBytes()
        {
            IHttp2Stream stream = this.Stream(STREAM_ID);
            stream.Close();
            this.ReceiveFlowControlledFrame(stream, 10, 0, false);
            Assert.Equal(0, controller.UnconsumedBytes(connection.ConnectionStream));
        }

        [Fact]
        public void DataReceivedForNullStreamShouldImmediatelyConsumeBytes()
        {
            this.ReceiveFlowControlledFrame(null, 10, 0, false);
            Assert.Equal(0, controller.UnconsumedBytes(connection.ConnectionStream));
        }

        [Fact]
        public void ConsumeBytesForNullStreamShouldIgnore()
        {
            controller.ConsumeBytes(null, 10);
            Assert.Equal(0, controller.UnconsumedBytes(connection.ConnectionStream));
        }

        [Fact]
        public void GlobalRatioShouldImpactStreams()
        {
            float ratio = 0.6f;
            controller.WindowUpdateRatio(ratio);
            this.TestRatio(ratio, Http2CodecUtil.DefaultWindowSize << 1, 3, false);
        }

        [Fact]
        public void StreamlRatioShouldImpactStreams()
        {
            float ratio = 0.6f;
            this.TestRatio(ratio, Http2CodecUtil.DefaultWindowSize << 1, 3, true);
        }

        [Fact]
        public void ConsumeBytesForZeroNumBytesShouldIgnore()
        {
            Assert.False(controller.ConsumeBytes(connection.Stream(STREAM_ID), 0));
        }

        [Fact]
        public void ConsumeBytesForNegativeNumBytesShouldFail()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                Assert.False(controller.ConsumeBytes(connection.Stream(STREAM_ID), -1));
            });
        }

        private void TestRatio(float ratio, int newDefaultWindowSize, int newStreamId, bool setStreamRatio)
        {
            int delta = newDefaultWindowSize - Http2CodecUtil.DefaultWindowSize;
            this.controller.IncrementWindowSize(this.Stream(0), delta);
            IHttp2Stream stream = this.connection.Local.CreateStream(newStreamId, false);
            if (setStreamRatio)
            {
                this.controller.WindowUpdateRatio(stream, ratio);
            }
            this.controller.IncrementWindowSize(stream, delta);
            this.frameWriter.Reset();
            try
            {
                int data1 = (int)(newDefaultWindowSize * ratio) + 1;
                int data2 = (int)(Http2CodecUtil.DefaultWindowSize * DefaultHttp2LocalFlowController.DefaultWindowUpdateRatio) >> 1;
                this.ReceiveFlowControlledFrame(STREAM_ID, data2, 0, false);
                this.ReceiveFlowControlledFrame(newStreamId, data1, 0, false);
                this.VerifyWindowUpdateNotSent(STREAM_ID);
                this.VerifyWindowUpdateNotSent(newStreamId);
                this.VerifyWindowUpdateNotSent(Http2CodecUtil.ConnectionStreamId);
                Assert.Equal(Http2CodecUtil.DefaultWindowSize - data2, this.Window(STREAM_ID));
                Assert.Equal(newDefaultWindowSize - data1, this.Window(newStreamId));
                Assert.Equal(newDefaultWindowSize - data2 - data1, this.Window(Http2CodecUtil.ConnectionStreamId));
                Assert.False(this.ConsumeBytes(STREAM_ID, data2));
                Assert.True(this.ConsumeBytes(newStreamId, data1));
                this.VerifyWindowUpdateNotSent(STREAM_ID);
                this.VerifyWindowUpdateSent(newStreamId, data1);
                this.VerifyWindowUpdateSent(Http2CodecUtil.ConnectionStreamId, data1 + data2);
                Assert.Equal(Http2CodecUtil.DefaultWindowSize - data2, this.Window(STREAM_ID));
                Assert.Equal(newDefaultWindowSize, this.Window(newStreamId));
                Assert.Equal(newDefaultWindowSize, this.Window(Http2CodecUtil.ConnectionStreamId));
            }
            finally
            {
                this.connection.Stream(newStreamId).Close();
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
            this.ReceiveFlowControlledFrame(this.Stream(streamId), dataSize, padding, endOfStream);
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
            return this.controller.ConsumeBytes(this.Stream(streamId), numBytes);
        }

        private void VerifyWindowUpdateSent(int streamId, int windowSizeIncrement)
        {
            this.frameWriter.Verify(x => x.WriteWindowUpdateAsync(
                It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                It.Is<int>(v => v == streamId),
                It.Is<int>(v => v == windowSizeIncrement),
                It.Is<IPromise>(v => v == this.promise.Object)));
        }

        private void VerifyWindowUpdateNotSent(int streamId)
        {
            this.frameWriter.Verify(x => x.WriteWindowUpdateAsync(
                It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                It.Is<int>(v => v == streamId), It.IsAny<int>(),
                It.Is<IPromise>(v => v == this.promise.Object)), Times.Never());
        }

        private void VerifyWindowUpdateNotSent()
        {
            this.frameWriter.Verify(x => x.WriteWindowUpdateAsync(It.IsAny<IChannelHandlerContext>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IPromise>()), Times.Never());
        }

        private int Window(int streamId)
        {
            return this.controller.GetWindowSize(this.Stream(streamId));
        }

        private IHttp2Stream Stream(int streamId)
        {
            return this.connection.Stream(streamId);
        }

        private void InitController(bool autoRefillConnectionWindow)
        {
            this.connection = new DefaultHttp2Connection(false);
            this.controller = new DefaultHttp2LocalFlowController(this.connection, DefaultHttp2LocalFlowController.DefaultWindowUpdateRatio, autoRefillConnectionWindow);
            this.controller.FrameWriter(this.frameWriter.Object);
            this.connection.Local.FlowController = this.controller;
            this.connection.Local.CreateStream(STREAM_ID, false);
            this.controller.SetChannelHandlerContext(this.ctx.Object);
        }

    }
}
