
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using Moq;
    using Xunit;

    public abstract class DefaultHttp2RemoteFlowControllerTest
    {
        private const int STREAM_A = 1;
        private const int STREAM_B = 3;
        private const int STREAM_C = 5;
        private const int STREAM_D = 7;


        private DefaultHttp2RemoteFlowController controller;

        private Mock<IChannelHandlerContext> ctx;
        private Mock<IChannel> channel;
        private Mock<IChannelConfiguration> config;
        private Mock<IEventExecutor> executor;
        private Mock<IPromise> promise;
        private Mock<IHttp2RemoteFlowControllerListener> listener;

        private DefaultHttp2Connection connection;

        public DefaultHttp2RemoteFlowControllerTest()
        {
            this.ctx = new Mock<IChannelHandlerContext>();
            this.channel = new Mock<IChannel>();
            this.config = new Mock<IChannelConfiguration>();
            this.executor = new Mock<IEventExecutor>();
            this.promise = new Mock<IPromise>();
            this.listener = new Mock<IHttp2RemoteFlowControllerListener>();

            this.ctx.Setup(x => x.NewPromise()).Returns(this.promise.Object);
            this.ctx.Setup(x => x.Flush()).Throws(new Exception("forbidden"));
            this.SetChannelWritability(true);
            this.channel.Setup(x => x.Configuration).Returns(this.config.Object);
            this.executor.Setup(x => x.InEventLoop).Returns(true);

            this.InitConnectionAndController();

            this.ResetCtx();
            // This is intentionally left out of initConnectionAndController so it can be tested below.
            this.controller.SetChannelHandlerContext(this.ctx.Object);
            this.AssertWritabilityChanged(1, true);
            this.listener.Reset();
        }

        protected abstract IStreamByteDistributor NewDistributor(IHttp2Connection connection);

        [Fact]
        public void InitialWindowSizeShouldOnlyChangeStreams()
        {
            this.controller.SetInitialWindowSize(0);
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(Http2CodecUtil.ConnectionStreamId));
            Assert.Equal(0, this.Window(STREAM_A));
            Assert.Equal(0, this.Window(STREAM_B));
            Assert.Equal(0, this.Window(STREAM_C));
            Assert.Equal(0, this.Window(STREAM_D));
            this.AssertWritabilityChanged(1, false);
        }

        [Fact]
        public void WindowUpdateShouldChangeConnectionWindow()
        {
            this.IncrementWindowSize(Http2CodecUtil.ConnectionStreamId, 100);
            Assert.Equal(Http2CodecUtil.DefaultWindowSize + 100, this.Window(Http2CodecUtil.ConnectionStreamId));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(STREAM_A));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(STREAM_B));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(STREAM_C));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(STREAM_D));
            this.listener.VerifyNoOtherCalls();
        }

        [Fact]
        public void WindowUpdateShouldChangeStreamWindow()
        {
            this.IncrementWindowSize(STREAM_A, 100);
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(Http2CodecUtil.ConnectionStreamId));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize + 100, this.Window(STREAM_A));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(STREAM_B));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(STREAM_C));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(STREAM_D));
            this.listener.VerifyNoOtherCalls();
        }

        [Fact]
        public void PayloadSmallerThanWindowShouldBeWrittenImmediately()
        {
            FakeFlowControlled data = new FakeFlowControlled(5);
            this.SendData(STREAM_A, data);
            data.AssertNotWritten();
            this.listener.VerifyNoOtherCalls();
            this.controller.WritePendingBytes();
            data.AssertFullyWritten();
            this.listener.VerifyNoOtherCalls();
        }

        [Fact]
        public void EmptyPayloadShouldBeWrittenImmediately()
        {
            FakeFlowControlled data = new FakeFlowControlled(0);
            this.SendData(STREAM_A, data);
            data.AssertNotWritten();
            this.controller.WritePendingBytes();
            data.AssertFullyWritten();
            this.listener.VerifyNoOtherCalls();
        }

        [Fact]
        public void UnflushedPayloadsShouldBeDroppedOnCancel()
        {
            FakeFlowControlled data = new FakeFlowControlled(5);
            IHttp2Stream streamA = this.Stream(STREAM_A);
            this.SendData(STREAM_A, data);
            streamA.Close();
            this.controller.WritePendingBytes();
            data.AssertNotWritten();
            this.controller.WritePendingBytes();
            data.AssertNotWritten();
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == streamA)), Times.Once);
            Assert.False(this.controller.IsWritable(streamA));
        }

        [Fact]
        public void PayloadsShouldMerge()
        {
            this.controller.SetInitialWindowSize(15);
            FakeFlowControlled data1 = new FakeFlowControlled(5, true);
            FakeFlowControlled data2 = new FakeFlowControlled(10, true);
            this.SendData(STREAM_A, data1);
            this.SendData(STREAM_A, data2);
            data1.AssertNotWritten();
            data1.AssertNotWritten();
            data2.AssertMerged();
            this.controller.WritePendingBytes();
            data1.AssertFullyWritten();
            data2.AssertNotWritten();
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Once);
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
        }

        [Fact]
        public void FlowControllerCorrectlyAccountsForBytesWithMerge()
        {
            this.controller.SetInitialWindowSize(112); // This must be more than the total merged frame size 110
            FakeFlowControlled data1 = new FakeFlowControlled(5, 2, true);
            FakeFlowControlled data2 = new FakeFlowControlled(5, 100, true);
            this.SendData(STREAM_A, data1);
            this.SendData(STREAM_A, data2);
            data1.AssertNotWritten();
            data1.AssertNotWritten();
            data2.AssertMerged();
            this.controller.WritePendingBytes();
            data1.AssertFullyWritten();
            data2.AssertNotWritten();
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Never());
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_A)));
        }

        [Fact]
        public void StalledStreamShouldQueuePayloads()
        {
            this.controller.SetInitialWindowSize(0);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Once);
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
            this.listener.Reset();

            FakeFlowControlled data = new FakeFlowControlled(15);
            FakeFlowControlled moreData = new FakeFlowControlled(0);
            this.SendData(STREAM_A, data);
            this.controller.WritePendingBytes();
            data.AssertNotWritten();
            this.SendData(STREAM_A, moreData);
            this.controller.WritePendingBytes();
            moreData.AssertNotWritten();
            this.listener.VerifyNoOtherCalls();
        }

        [Fact]
        public void QueuedPayloadsReceiveErrorOnStreamClose()
        {
            this.controller.SetInitialWindowSize(0);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Once);
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
            this.listener.Reset();

            FakeFlowControlled data = new FakeFlowControlled(15);
            FakeFlowControlled moreData = new FakeFlowControlled(0);
            this.SendData(STREAM_A, data);
            this.controller.WritePendingBytes();
            data.AssertNotWritten();
            this.SendData(STREAM_A, moreData);
            this.controller.WritePendingBytes();
            moreData.AssertNotWritten();

            this.connection.Stream(STREAM_A).Close();
            data.AssertError(Http2Error.StreamClosed);
            moreData.AssertError(Http2Error.StreamClosed);
            this.listener.VerifyNoOtherCalls();
        }

        [Fact]
        public void PayloadLargerThanWindowShouldWritePartial()
        {
            this.controller.SetInitialWindowSize(5);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Never());
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_A)));
            this.listener.Reset();

            FakeFlowControlled data = new FakeFlowControlled(10);
            this.SendData(STREAM_A, data);
            this.controller.WritePendingBytes();
            // Verify that a partial frame of 5 remains to be sent
            data.AssertPartiallyWritten(5);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Once);
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
            this.listener.VerifyNoOtherCalls();
        }

        [Fact]
        public void WindowUpdateAndFlushShouldTriggerWrite()
        {
            this.controller.SetInitialWindowSize(10);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Never());
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_A)));

            FakeFlowControlled data = new FakeFlowControlled(20);
            FakeFlowControlled moreData = new FakeFlowControlled(10);
            this.SendData(STREAM_A, data);
            this.SendData(STREAM_A, moreData);
            this.controller.WritePendingBytes();
            data.AssertPartiallyWritten(10);
            moreData.AssertNotWritten();
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Once);
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
            this.listener.Reset();
            this.ResetCtx();

            // Update the window and verify that the rest of data and some of moreData are written
            this.IncrementWindowSize(STREAM_A, 15);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Never());
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
            this.listener.Reset();

            this.controller.WritePendingBytes();

            data.AssertFullyWritten();
            moreData.AssertPartiallyWritten(5);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Never());
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));

            Assert.Equal(Http2CodecUtil.DefaultWindowSize - 25, this.Window(Http2CodecUtil.ConnectionStreamId));
            Assert.Equal(0, this.Window(STREAM_A));
            Assert.Equal(10, this.Window(STREAM_B));
            Assert.Equal(10, this.Window(STREAM_C));
            Assert.Equal(10, this.Window(STREAM_D));
        }

        [Fact]
        public void InitialWindowUpdateShouldSendPayload()
        {
            this.IncrementWindowSize(Http2CodecUtil.ConnectionStreamId, -this.Window(Http2CodecUtil.ConnectionStreamId) + 10);
            this.AssertWritabilityChanged(0, true);
            this.listener.Reset();

            this.controller.SetInitialWindowSize(0);
            this.AssertWritabilityChanged(1, false);
            this.listener.Reset();

            FakeFlowControlled data = new FakeFlowControlled(10);
            this.SendData(STREAM_A, data);
            this.controller.WritePendingBytes();
            data.AssertNotWritten();

            // Verify that the entire frame was sent.
            this.controller.SetInitialWindowSize(10);
            data.AssertFullyWritten();
            this.AssertWritabilityChanged(0, false);
        }

        [Fact]
        public void SuccessiveSendsShouldNotInteract()
        {
            // Collapse the connection window to force queueing.
            this.IncrementWindowSize(Http2CodecUtil.ConnectionStreamId, -this.Window(Http2CodecUtil.ConnectionStreamId));
            Assert.Equal(0, this.Window(Http2CodecUtil.ConnectionStreamId));
            this.AssertWritabilityChanged(1, false);
            this.listener.Reset();

            FakeFlowControlled dataA = new FakeFlowControlled(10);
            // Queue data for stream A and allow most of it to be written.
            this.SendData(STREAM_A, dataA);
            this.controller.WritePendingBytes();
            dataA.AssertNotWritten();
            this.IncrementWindowSize(Http2CodecUtil.ConnectionStreamId, 8);
            this.AssertWritabilityChanged(0, false);
            this.listener.Reset();

            this.controller.WritePendingBytes();
            dataA.AssertPartiallyWritten(8);
            Assert.Equal(65527, this.Window(STREAM_A));
            Assert.Equal(0, this.Window(Http2CodecUtil.ConnectionStreamId));
            this.AssertWritabilityChanged(0, false);
            this.listener.Reset();

            // Queue data for stream B and allow the rest of A and all of B to be written.
            FakeFlowControlled dataB = new FakeFlowControlled(10);
            this.SendData(STREAM_B, dataB);
            this.controller.WritePendingBytes();
            dataB.AssertNotWritten();
            this.IncrementWindowSize(Http2CodecUtil.ConnectionStreamId, 12);
            this.AssertWritabilityChanged(0, false);
            this.listener.Reset();

            this.controller.WritePendingBytes();
            Assert.Equal(0, this.Window(Http2CodecUtil.ConnectionStreamId));
            this.AssertWritabilityChanged(0, false);

            // Verify the rest of A is written.
            dataA.AssertFullyWritten();
            Assert.Equal(65525, this.Window(STREAM_A));

            dataB.AssertFullyWritten();
            Assert.Equal(65525, this.Window(STREAM_B));
            this.listener.VerifyNoOtherCalls();
        }

        [Fact]
        public void NegativeWindowShouldNotThrowException()
        {
            int initWindow = 20;
            int secondWindowSize = 10;
            this.controller.SetInitialWindowSize(initWindow);
            this.AssertWritabilityChanged(0, true);
            this.listener.Reset();

            FakeFlowControlled data1 = new FakeFlowControlled(initWindow);
            FakeFlowControlled data2 = new FakeFlowControlled(5);

            // Deplete the stream A window to 0
            this.SendData(STREAM_A, data1);
            this.controller.WritePendingBytes();
            data1.AssertFullyWritten();
            Assert.True(this.Window(Http2CodecUtil.ConnectionStreamId) > 0);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Once);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_B))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_C))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_D))), Times.Never());
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_B)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_C)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_D)));
            this.listener.Reset();

            // Make the window size for stream A negative
            this.controller.SetInitialWindowSize(initWindow - secondWindowSize);
            Assert.Equal(-secondWindowSize, this.Window(STREAM_A));
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_B))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_C))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_D))), Times.Never());
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_B)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_C)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_D)));
            this.listener.Reset();

            // Queue up a write. It should not be written now because the window is negative
            this.SendData(STREAM_A, data2);
            this.controller.WritePendingBytes();
            data2.AssertNotWritten();
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_B))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_C))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_D))), Times.Never());
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_B)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_C)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_D)));
            this.listener.Reset();

            // Open the window size back up a bit (no send should happen)
            this.IncrementWindowSize(STREAM_A, 5);
            this.controller.WritePendingBytes();
            Assert.Equal(-5, this.Window(STREAM_A));
            data2.AssertNotWritten();
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_B))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_C))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_D))), Times.Never());
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_B)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_C)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_D)));
            this.listener.Reset();

            // Open the window size back up a bit (no send should happen)
            this.IncrementWindowSize(STREAM_A, 5);
            this.controller.WritePendingBytes();
            Assert.Equal(0, this.Window(STREAM_A));
            data2.AssertNotWritten();
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_B))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_C))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_D))), Times.Never());
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_B)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_C)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_D)));
            this.listener.Reset();

            // Open the window size back up and allow the write to happen
            this.IncrementWindowSize(STREAM_A, 5);
            this.controller.WritePendingBytes();
            data2.AssertFullyWritten();
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_B))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_C))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_D))), Times.Never());
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_B)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_C)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_D)));
        }

        [Fact]
        public void InitialWindowUpdateShouldSendEmptyFrame()
        {
            this.controller.SetInitialWindowSize(0);
            this.AssertWritabilityChanged(1, false);
            this.listener.Reset();

            // First send a frame that will get buffered.
            FakeFlowControlled data = new FakeFlowControlled(10, false);
            this.SendData(STREAM_A, data);
            this.controller.WritePendingBytes();
            data.AssertNotWritten();

            // Now send an empty frame on the same stream and verify that it's also buffered.
            FakeFlowControlled data2 = new FakeFlowControlled(0, false);
            this.SendData(STREAM_A, data2);
            this.controller.WritePendingBytes();
            data2.AssertNotWritten();

            // Re-expand the window and verify that both frames were sent.
            this.controller.SetInitialWindowSize(10);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_B))), Times.Once);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_C))), Times.Once);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_D))), Times.Once);
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_B)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_C)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_D)));

            data.AssertFullyWritten();
            data2.AssertFullyWritten();
        }

        [Fact]
        public void InitialWindowUpdateShouldSendPartialFrame()
        {
            this.controller.SetInitialWindowSize(0);
            this.AssertWritabilityChanged(1, false);
            this.listener.Reset();

            FakeFlowControlled data = new FakeFlowControlled(10);
            this.SendData(STREAM_A, data);
            this.controller.WritePendingBytes();
            data.AssertNotWritten();

            // Verify that a partial frame of 5 was sent.
            this.controller.SetInitialWindowSize(5);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_B))), Times.Once);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_C))), Times.Once);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_D))), Times.Once);
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_B)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_C)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_D)));

            data.AssertPartiallyWritten(5);
        }

        [Fact]
        public void ConnectionWindowUpdateShouldSendFrame()
        {
            // Set the connection window size to zero.
            this.ExhaustStreamWindow(Http2CodecUtil.ConnectionStreamId);
            this.AssertWritabilityChanged(1, false);
            this.listener.Reset();

            FakeFlowControlled data = new FakeFlowControlled(10);
            this.SendData(STREAM_A, data);
            this.controller.WritePendingBytes();
            data.AssertNotWritten();
            this.AssertWritabilityChanged(0, false);
            this.listener.Reset();

            // Verify that the entire frame was sent.
            this.IncrementWindowSize(Http2CodecUtil.ConnectionStreamId, 10);
            this.AssertWritabilityChanged(0, false);
            this.listener.Reset();
            data.AssertNotWritten();

            this.controller.WritePendingBytes();
            data.AssertFullyWritten();
            this.AssertWritabilityChanged(0, false);
            Assert.Equal(0, this.Window(Http2CodecUtil.ConnectionStreamId));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize - 10, this.Window(STREAM_A));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(STREAM_B));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(STREAM_C));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(STREAM_D));
        }

        [Fact]
        public void ConnectionWindowUpdateShouldSendPartialFrame()
        {
            // Set the connection window size to zero.
            this.ExhaustStreamWindow(Http2CodecUtil.ConnectionStreamId);
            this.AssertWritabilityChanged(1, false);
            this.listener.Reset();

            FakeFlowControlled data = new FakeFlowControlled(10);
            this.SendData(STREAM_A, data);
            this.controller.WritePendingBytes();
            data.AssertNotWritten();

            // Verify that a partial frame of 5 was sent.
            this.IncrementWindowSize(Http2CodecUtil.ConnectionStreamId, 5);
            data.AssertNotWritten();
            this.AssertWritabilityChanged(0, false);
            this.listener.Reset();

            this.controller.WritePendingBytes();
            data.AssertPartiallyWritten(5);
            this.AssertWritabilityChanged(0, false);
            Assert.Equal(0, this.Window(Http2CodecUtil.ConnectionStreamId));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize - 5, this.Window(STREAM_A));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(STREAM_B));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(STREAM_C));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(STREAM_D));
        }

        [Fact]
        public void StreamWindowUpdateShouldSendFrame()
        {
            // Set the stream window size to zero.
            this.ExhaustStreamWindow(STREAM_A);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Once);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_B))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_C))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_D))), Times.Never());
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_B)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_C)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_D)));
            this.listener.Reset();

            FakeFlowControlled data = new FakeFlowControlled(10);
            this.SendData(STREAM_A, data);
            this.controller.WritePendingBytes();
            data.AssertNotWritten();

            // Verify that the entire frame was sent.
            this.IncrementWindowSize(STREAM_A, 10);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_B))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_C))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_D))), Times.Never());
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_B)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_C)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_D)));
            this.listener.Reset();

            data.AssertNotWritten();
            this.controller.WritePendingBytes();
            data.AssertFullyWritten();
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_B))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_C))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_D))), Times.Never());
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_B)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_C)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_D)));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize - 10, this.Window(Http2CodecUtil.ConnectionStreamId));
            Assert.Equal(0, this.Window(STREAM_A));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(STREAM_B));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(STREAM_C));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(STREAM_D));
        }

        [Fact]
        public void StreamWindowUpdateShouldSendPartialFrame()
        {
            // Set the stream window size to zero.
            this.ExhaustStreamWindow(STREAM_A);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Once);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_B))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_C))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_D))), Times.Never());
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_B)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_C)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_D)));
            this.listener.Reset();

            FakeFlowControlled data = new FakeFlowControlled(10);
            this.SendData(STREAM_A, data);
            this.controller.WritePendingBytes();
            data.AssertNotWritten();

            // Verify that a partial frame of 5 was sent.
            this.IncrementWindowSize(STREAM_A, 5);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_B))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_C))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_D))), Times.Never());
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_B)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_C)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_D)));
            this.listener.Reset();

            data.AssertNotWritten();
            this.controller.WritePendingBytes();
            data.AssertPartiallyWritten(5);
            Assert.Equal(Http2CodecUtil.DefaultWindowSize - 5, this.Window(Http2CodecUtil.ConnectionStreamId));
            Assert.Equal(0, this.Window(STREAM_A));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(STREAM_B));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(STREAM_C));
            Assert.Equal(Http2CodecUtil.DefaultWindowSize, this.Window(STREAM_D));
        }

        [Fact]
        public void FlowControlledWriteThrowsAnException()
        {
            var flowControlled = MockedFlowControlledThatThrowsOnWrite();
            IHttp2Stream stream = this.Stream(STREAM_A);
            flowControlled
                .Setup(x => x.Error(It.IsAny<IChannelHandlerContext>(), It.IsAny<Exception>()))
                .Callback<IChannelHandlerContext, Exception>((ctx, ex) => stream.CloseLocalSide());

            int windowBefore = this.Window(STREAM_A);

            this.controller.AddFlowControlled(stream, flowControlled.Object);
            this.controller.WritePendingBytes();

            flowControlled.Verify(x => x.Write(It.IsAny<IChannelHandlerContext>(), It.IsAny<int>()), Times.AtLeastOnce);
            flowControlled.Verify(x => x.Error(It.IsAny<IChannelHandlerContext>(), It.IsAny<Exception>()));
            flowControlled.Verify(x => x.WriteComplete(), Times.Never());

            Assert.Equal(90, windowBefore - this.Window(STREAM_A));
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Once);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_B))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_C))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_D))), Times.Never());
            Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_B)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_C)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_D)));
        }

        [Fact]
        public void FlowControlledWriteAndErrorThrowAnException()
        {
            var flowControlled = MockedFlowControlledThatThrowsOnWrite();
            IHttp2Stream stream = this.Stream(STREAM_A);
            var fakeException = new Http2RuntimeException("error failed");
            flowControlled
                .Setup(x => x.Error(It.IsAny<IChannelHandlerContext>(), It.IsAny<Exception>()))
                .Throws(fakeException);

            int windowBefore = this.Window(STREAM_A);

            try
            {
                this.controller.AddFlowControlled(stream, flowControlled.Object);
                this.controller.WritePendingBytes();
                Assert.False(true);
            }
            catch (Http2Exception e)
            {
                Assert.Same(fakeException, e.InnerException);
            }
            catch (Exception)
            {
                Assert.False(true);
            }

            flowControlled.Verify(x => x.Write(It.IsAny<IChannelHandlerContext>(), It.IsAny<int>()), Times.AtLeastOnce);
            flowControlled.Verify(x => x.Error(It.IsAny<IChannelHandlerContext>(), It.IsAny<Exception>()));
            flowControlled.Verify(x => x.WriteComplete(), Times.Never());

            Assert.Equal(90, windowBefore - this.Window(STREAM_A));
            this.listener.VerifyNoOtherCalls();
        }

        [Fact]
        public void FlowControlledWriteCompleteThrowsAnException()
        {
            var flowControlled = new Mock<IHttp2RemoteFlowControlled>();
            IHttp2Stream streamA = this.Stream(STREAM_A);
            AtomicInteger size = new AtomicInteger(150);
            flowControlled.Setup(x => x.Size).Returns(() => size.Value);
            flowControlled
                .Setup(x => x.Write(It.IsAny<IChannelHandlerContext>(), It.IsAny<int>()))
                .Callback<IChannelHandlerContext, int>((ctx, v) => size.AddAndGet(-50));

            IHttp2Stream stream = this.Stream(STREAM_A);
            flowControlled
                .Setup(x => x.WriteComplete())
                .Throws(new Http2RuntimeException("writeComplete failed"));

            int windowBefore = this.Window(STREAM_A);

            this.controller.AddFlowControlled(stream, flowControlled.Object);
            this.controller.WritePendingBytes();

            flowControlled.Verify(x => x.Write(It.IsAny<IChannelHandlerContext>(), It.IsAny<int>()), Times.Exactly(3));
            flowControlled.Verify(x => x.Error(It.IsAny<IChannelHandlerContext>(), It.IsAny<Exception>()), Times.Never());
            flowControlled.Verify(x => x.WriteComplete());

            Assert.Equal(150, windowBefore - this.Window(STREAM_A));
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == streamA)), Times.Once);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_B))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_C))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_D))), Times.Never());
            Assert.False(this.controller.IsWritable(streamA));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_B)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_C)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_D)));
        }

        [Fact]
        public void CloseStreamInFlowControlledError()
        {
            var flowControlled = new Mock<IHttp2RemoteFlowControlled>();
            IHttp2Stream stream = this.Stream(STREAM_A);
            flowControlled.Setup(x => x.Size).Returns(100);
            flowControlled
                .Setup(x => x.Write(It.IsAny<IChannelHandlerContext>(), It.IsAny<int>()))
                .Throws(new Http2RuntimeException("write failed"));
            flowControlled
                .Setup(x => x.Error(It.IsAny<IChannelHandlerContext>(), It.IsAny<Exception>()))
                .Callback<IChannelHandlerContext, Exception>((ctx, err) => stream.Close());

            this.controller.AddFlowControlled(stream, flowControlled.Object);
            this.controller.WritePendingBytes();

            flowControlled.Verify(x => x.Write(It.IsAny<IChannelHandlerContext>(), It.IsAny<int>()));
            flowControlled.Verify(x => x.Error(It.IsAny<IChannelHandlerContext>(), It.IsAny<Exception>()));
            flowControlled.Verify(x => x.WriteComplete(), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == stream)), Times.Once);
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_B))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_C))), Times.Never());
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_D))), Times.Never());
            Assert.False(this.controller.IsWritable(stream));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_B)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_C)));
            Assert.True(this.controller.IsWritable(this.Stream(STREAM_D)));
        }

        [Fact]
        public void NonWritableChannelDoesNotAttemptToWrite()
        {
            // Start the channel as not writable and exercise the public methods of the flow controller
            // making sure no frames are written.
            this.SetChannelWritability(false);
            this.AssertWritabilityChanged(1, false);
            this.listener.Reset();
            FakeFlowControlled dataA = new FakeFlowControlled(1);
            FakeFlowControlled dataB = new FakeFlowControlled(1);
            IHttp2Stream stream = this.Stream(STREAM_A);

            this.controller.AddFlowControlled(stream, dataA);
            this.controller.WritePendingBytes();
            dataA.AssertNotWritten();

            this.controller.IncrementWindowSize(stream, 100);
            this.controller.WritePendingBytes();
            dataA.AssertNotWritten();

            this.controller.AddFlowControlled(stream, dataB);
            this.controller.WritePendingBytes();
            dataA.AssertNotWritten();
            dataB.AssertNotWritten();
            this.AssertWritabilityChanged(0, false);

            // Now change the channel to writable and make sure frames are written.
            this.SetChannelWritability(true);
            this.AssertWritabilityChanged(1, true);
            this.controller.WritePendingBytes();
            dataA.AssertFullyWritten();
            dataB.AssertFullyWritten();
        }

        [Fact]
        public void ContextShouldSendQueuedFramesWhenSet()
        {
            // Re-initialize the controller so we can ensure the context hasn't been set yet.
            this.InitConnectionAndController();

            FakeFlowControlled dataA = new FakeFlowControlled(1);
            IHttp2Stream stream = this.Stream(STREAM_A);

            // Queue some frames
            this.controller.AddFlowControlled(stream, dataA);
            dataA.AssertNotWritten();

            this.controller.IncrementWindowSize(stream, 100);
            dataA.AssertNotWritten();

            this.AssertWritabilityChanged(0, false);

            // Set the controller
            this.controller.SetChannelHandlerContext(this.ctx.Object);
            dataA.AssertFullyWritten();

            this.AssertWritabilityChanged(1, true);
        }

        [Fact]
        public void InitialWindowSizeWithNoContextShouldNotThrow()
        {
            // Re-initialize the controller so we can ensure the context hasn't been set yet.
            this.InitConnectionAndController();

            // This should not throw.
            this.controller.SetInitialWindowSize(1024 * 100);

            FakeFlowControlled dataA = new FakeFlowControlled(1);
            IHttp2Stream stream = this.Stream(STREAM_A);

            // Queue some frames
            this.controller.AddFlowControlled(stream, dataA);
            dataA.AssertNotWritten();

            // Set the controller
            this.controller.SetChannelHandlerContext(this.ctx.Object);
            dataA.AssertFullyWritten();
        }

        //[Fact]//(expected = AssertionError_class)
        //public void InvalidParentStreamIdThrows()
        //{
        //    this.controller.UpdateDependencyTree(STREAM_D, -1, Http2CodecUtil.DefaultPriorityWeight, true);
        //}

        //[Fact]//(expected = AssertionError_class)
        //public void InvalidChildStreamIdThrows()
        //{
        //    this.controller.UpdateDependencyTree(-1, STREAM_D, Http2CodecUtil.DefaultPriorityWeight, true);
        //}

        //[Fact]//(expected = AssertionError_class)
        //public void ConnectionChildStreamIdThrows()
        //{
        //    this.controller.UpdateDependencyTree(0, STREAM_D, Http2CodecUtil.DefaultPriorityWeight, true);
        //}

        //[Fact]//(expected = AssertionError_class)
        //public void InvalidWeightTooSmallThrows()
        //{
        //    this.controller.UpdateDependencyTree(STREAM_A, STREAM_D, (short)(Http2CodecUtil.MinWeight - 1), true);
        //}

        //[Fact]//(expected = AssertionError_class)
        //public void InvalidWeightTooBigThrows()
        //{
        //    this.controller.UpdateDependencyTree(STREAM_A, STREAM_D, (short)(Http2CodecUtil.MaxWeight + 1), true);
        //}

        //[Fact]//(expected = AssertionError_class)
        //public void DependencyOnSelfThrows()
        //{
        //    this.controller.UpdateDependencyTree(STREAM_A, STREAM_A, Http2CodecUtil.DefaultPriorityWeight, true);
        //}

        private void InitConnectionAndController()
        {
            this.connection = new DefaultHttp2Connection(false);
            this.controller = new DefaultHttp2RemoteFlowController(this.connection, NewDistributor(this.connection), this.listener.Object);
            this.connection.Remote.FlowController = controller;

            this.connection.Local.CreateStream(STREAM_A, false);
            this.connection.Local.CreateStream(STREAM_B, false);
            var streamC = this.connection.Local.CreateStream(STREAM_C, false);
            var streamD = this.connection.Local.CreateStream(STREAM_D, false);
            controller.UpdateDependencyTree(streamC.Id, STREAM_A, Http2CodecUtil.DefaultPriorityWeight, false);
            controller.UpdateDependencyTree(streamD.Id, STREAM_A, Http2CodecUtil.DefaultPriorityWeight, false);
        }

        private void AssertWritabilityChanged(int amt, bool writable)
        {
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_A))), Times.Exactly(amt));
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_B))), Times.Exactly(amt));
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_C))), Times.Exactly(amt));
            this.listener.Verify(x => x.WritabilityChanged(It.Is<IHttp2Stream>(v => v == this.Stream(STREAM_D))), Times.Exactly(amt));
            if (writable)
            {
                Assert.True(this.controller.IsWritable(this.Stream(STREAM_A)));
                Assert.True(this.controller.IsWritable(this.Stream(STREAM_B)));
                Assert.True(this.controller.IsWritable(this.Stream(STREAM_C)));
                Assert.True(this.controller.IsWritable(this.Stream(STREAM_D)));
            }
            else
            {
                Assert.False(this.controller.IsWritable(this.Stream(STREAM_A)));
                Assert.False(this.controller.IsWritable(this.Stream(STREAM_B)));
                Assert.False(this.controller.IsWritable(this.Stream(STREAM_C)));
                Assert.False(this.controller.IsWritable(this.Stream(STREAM_D)));
            }
        }

        private static Mock<IHttp2RemoteFlowControlled> MockedFlowControlledThatThrowsOnWrite()
        {
            var flowControlled = new Mock<IHttp2RemoteFlowControlled>();
            flowControlled.Setup(x => x.Size).Returns(100);
            flowControlled
                .Setup(x => x.Write(It.IsAny<IChannelHandlerContext>(), It.IsAny<int>()))
                .Callback<IChannelHandlerContext, int>((ctx, allowBytes) =>
                {
                    flowControlled.Setup(x => x.Size).Returns(10);
                    throw new Http2RuntimeException("Write failed");
                });
            return flowControlled;
        }

        private void SendData(int streamId, FakeFlowControlled data)
        {
            IHttp2Stream stream = this.Stream(streamId);
            controller.AddFlowControlled(stream, data);
        }

        private void ExhaustStreamWindow(int streamId)
        {
            this.IncrementWindowSize(streamId, -this.Window(streamId));
        }

        private int Window(int streamId)
        {
            return controller.GetWindowSize(this.Stream(streamId));
        }

        private void IncrementWindowSize(int streamId, int delta)
        {
            controller.IncrementWindowSize(this.Stream(streamId), delta);
        }

        private IHttp2Stream Stream(int streamId)
        {
            return this.connection.Stream(streamId);
        }

        private void ResetCtx()
        {
            this.ctx.Reset();
            this.ctx.Setup(x => x.Channel).Returns(this.channel.Object);
            this.ctx.Setup(x => x.Executor).Returns(this.executor.Object);
        }

        private void SetChannelWritability(bool isWritable)
        {
            this.channel.Setup(x => x.BytesBeforeUnwritable).Returns(() => isWritable ? long.MaxValue : 0);
            this.channel.Setup(x => x.IsWritable).Returns(isWritable);
            if (this.controller != null)
            {
                this.controller.ChannelWritabilityChanged();
            }
        }

        sealed class FakeFlowControlled : IHttp2RemoteFlowControlled
        {
            private int currentPadding;
            private int currentPayloadSize;
            private int originalPayloadSize;
            private int originalPadding;
            private bool writeCalled;
            private readonly bool mergeable;
            private bool merged;

            private Exception t;

            public FakeFlowControlled(int size)
                : this(size, false)
            {
            }

            public FakeFlowControlled(int size, bool mergeable)
                : this(size, 0, mergeable)
            {
            }

            public FakeFlowControlled(int payloadSize, int padding, bool mergeable)
            {
                this.currentPayloadSize = this.originalPayloadSize = payloadSize;
                this.currentPadding = this.originalPadding = padding;
                this.mergeable = mergeable;
            }

            public int Size => this.currentPayloadSize + this.currentPadding;

            private int OriginalSize => this.originalPayloadSize + this.originalPadding;

            public void Error(IChannelHandlerContext ctx, Exception cause)
            {
                this.t = cause;
            }

            public bool Merge(IChannelHandlerContext ctx, IHttp2RemoteFlowControlled next)
            {
                if (mergeable && next is FakeFlowControlled ffcNext)
                {
                    this.originalPayloadSize += ffcNext.originalPayloadSize;
                    this.currentPayloadSize += ffcNext.originalPayloadSize;
                    this.currentPadding = this.originalPadding = Math.Max(this.originalPadding, ffcNext.originalPadding);
                    ffcNext.merged = true;
                    return true;
                }
                return false;
            }

            public void Write(IChannelHandlerContext ctx, int allowedBytes)
            {
                if (allowedBytes <= 0 && this.Size != 0)
                {
                    // Write has been called but no data can be written
                    return;
                }
                this.writeCalled = true;
                int written = Math.Min(this.Size, allowedBytes);
                if (written > this.currentPayloadSize)
                {
                    written -= this.currentPayloadSize;
                    this.currentPayloadSize = 0;
                    this.currentPadding -= written;
                }
                else
                {
                    this.currentPayloadSize -= written;
                }
            }

            public void WriteComplete()
            {
            }

            public int Written()
            {
                return this.OriginalSize - this.Size;
            }

            public void AssertNotWritten()
            {
                Assert.False(this.writeCalled);
            }

            public void AssertPartiallyWritten(int expectedWritten)
            {
                AssertPartiallyWritten(expectedWritten, 0);
            }

            public void AssertPartiallyWritten(int expectedWritten, int delta)
            {
                Assert.True(writeCalled);
                var actual = this.Written();
                Assert.True(expectedWritten == actual || (expectedWritten == actual + delta) || (expectedWritten == actual - delta));
            }

            public void AssertFullyWritten()
            {
                Assert.True(writeCalled);
                Assert.Equal(0, currentPayloadSize);
                Assert.Equal(0, currentPadding);
            }

            public bool AssertMerged()
            {
                return this.merged;
            }

            public void AssertError(Http2Error? error)
            {
                Assert.NotNull(this.t);
                if (error != null)
                {
                    Assert.Equal(error.Value, ((Http2Exception)t).Error);
                }
            }
        }
    }
}
