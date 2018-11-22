
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using Moq;
    using Xunit;

    public class WeightedFairQueueByteDistributorTest : AbstractWeightedFairQueueByteDistributorDependencyTest
    {
        private const int STREAM_A = 1;
        private const int STREAM_B = 3;
        private const int STREAM_C = 5;
        private const int STREAM_D = 7;
        private const int STREAM_E = 9;
        private const int ALLOCATION_QUANTUM = 100;

        public WeightedFairQueueByteDistributorTest()
        {
            this.writer = new Moq.Mock<IStreamByteDistributorWriter>();

            // Assume we always write all the allocated bytes.
            this.writer.Setup(x => x.Write(It.IsAny<IHttp2Stream>(), It.IsAny<int>()))
                       .Callback<IHttp2Stream, int>((stream, numBytes) => this.WriteAnswer(stream, numBytes, false));

            this.Setup(-1);
        }

        private void Setup(int maxStateOnlySize)
        {
            this.connection = new DefaultHttp2Connection(false);
            this.distributor = maxStateOnlySize >= 0 ? new WeightedFairQueueByteDistributor(connection, maxStateOnlySize)
                                                : new WeightedFairQueueByteDistributor(connection);
            this.distributor.AllocationQuantum(ALLOCATION_QUANTUM);

            this.connection.Local.CreateStream(STREAM_A, false);
            this.connection.Local.CreateStream(STREAM_B, false);
            IHttp2Stream streamC = this.connection.Local.CreateStream(STREAM_C, false);
            IHttp2Stream streamD = this.connection.Local.CreateStream(STREAM_D, false);
            this.SetPriority(streamC.Id, STREAM_A, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamD.Id, STREAM_A, Http2CodecUtil.DefaultPriorityWeight, false);
        }

        /**
         * In this test, we block B such that it has no frames. We distribute enough bytes for all streams and stream B
         * should be preserved in the priority queue structure until it has no "active" children, but it should not be
         * doubly added to stream 0.
         *
         * <pre>
         *         0
         *         |
         *         A
         *         |
         *        [B]
         *         |
         *         C
         *         |
         *         D
         * </pre>
         *
         * After the write:
         * <pre>
         *         0
         * </pre>
         */
        [Fact]
        public void WriteWithNonActiveStreamShouldNotDobuleAddToPriorityQueue()
        {
            this.InitState(STREAM_A, 400, true);
            this.InitState(STREAM_B, 500, true);
            this.InitState(STREAM_C, 600, true);
            this.InitState(STREAM_D, 700, true);

            this.SetPriority(STREAM_B, STREAM_A, Http2CodecUtil.DefaultPriorityWeight, true);
            this.SetPriority(STREAM_D, STREAM_C, Http2CodecUtil.DefaultPriorityWeight, true);

            // Block B, but it should still remain in the queue/tree structure.
            this.InitState(STREAM_B, 0, false);

            // Get the streams before the write, because they may be be closed.
            IHttp2Stream streamA = this.Stream(STREAM_A);
            IHttp2Stream streamB = this.Stream(STREAM_B);
            IHttp2Stream streamC = this.Stream(STREAM_C);
            IHttp2Stream streamD = this.Stream(STREAM_D);

            this.writer.Reset();
            this.writer.Setup(x => x.Write(It.IsAny<IHttp2Stream>(), It.IsAny<int>()))
                       .Callback<IHttp2Stream, int>((stream, numBytes) => this.WriteAnswer(stream, numBytes, true));

            Assert.False(this.Write(400 + 600 + 700));
            Assert.Equal(400, this.CaptureWrites(streamA));
            this.VerifyNeverWrite(streamB);
            Assert.Equal(600, this.CaptureWrites(streamC));
            Assert.Equal(700, this.CaptureWrites(streamD));
        }

        [Fact]
        public void BytesUnassignedAfterProcessing()
        {
            this.InitState(STREAM_A, 1, true);
            this.InitState(STREAM_B, 2, true);
            this.InitState(STREAM_C, 3, true);
            this.InitState(STREAM_D, 4, true);

            Assert.False(this.Write(10));
            this.VerifyWrite(STREAM_A, 1);
            this.VerifyWrite(STREAM_B, 2);
            this.VerifyWrite(STREAM_C, 3);
            this.VerifyWrite(STREAM_D, 4);

            Assert.False(this.Write(10));
            this.VerifyAnyWrite(STREAM_A, 1);
            this.VerifyAnyWrite(STREAM_B, 1);
            this.VerifyAnyWrite(STREAM_C, 1);
            this.VerifyAnyWrite(STREAM_D, 1);
        }

        [Fact]
        public void ConnectionErrorForWriterException()
        {
            this.InitState(STREAM_A, 1, true);
            this.InitState(STREAM_B, 2, true);
            this.InitState(STREAM_C, 3, true);
            this.InitState(STREAM_D, 4, true);

            Exception fakeException = new Http2RuntimeException("Fake exception");
            this.writer
                .Setup(x => x.Write(
                    It.Is<IHttp2Stream>(v => ReferenceEquals(v, this.Stream(STREAM_C))),
                    It.Is<int>(v => v == 3)))
                .Throws(fakeException);

            try
            {
                this.Write(10);
                Assert.False(true, "Expected an exception");
            }
            catch (Http2Exception e)
            {
                Assert.False(Http2Exception.IsStreamError(e));
                Assert.Equal(Http2Error.InternalError, e.Error);
                Assert.Same(fakeException, e.InnerException);
            }

            this.VerifyWrite(Times.AtMost(1), STREAM_A, 1);
            this.VerifyWrite(Times.AtMost(1), STREAM_B, 2);
            this.VerifyWrite(STREAM_C, 3);
            this.VerifyWrite(Times.AtMost(1), STREAM_D, 4);

            this.writer
                .Setup(x => x.Write(
                    It.Is<IHttp2Stream>(v => ReferenceEquals(v, this.Stream(STREAM_C))),
                    It.Is<int>(v => v == 3)))
                .Callback<IHttp2Stream, int>((stream, numBytes) => this.WriteAnswer(stream, numBytes, false));
            Assert.False(this.Write(10));
            this.VerifyWrite(STREAM_A, 1);
            this.VerifyWrite(STREAM_B, 2);
            this.VerifyWrite(Times.Exactly(2), STREAM_C, 3);
            this.VerifyWrite(STREAM_D, 4);
        }

        /**
         * In this test, we verify that each stream is allocated a minimum chunk size. When bytes
         * run out, the remaining streams will be next in line for the next iteration.
         */
        [Fact]
        public void MinChunkShouldBeAllocatedPerStream()
        {
            // Re-assign weights.
            this.SetPriority(STREAM_A, 0, (short)50, false);
            this.SetPriority(STREAM_B, 0, (short)200, false);
            this.SetPriority(STREAM_C, STREAM_A, (short)100, false);
            this.SetPriority(STREAM_D, STREAM_A, (short)100, false);

            // Update the streams.
            this.InitState(STREAM_A, ALLOCATION_QUANTUM, true);
            this.InitState(STREAM_B, ALLOCATION_QUANTUM, true);
            this.InitState(STREAM_C, ALLOCATION_QUANTUM, true);
            this.InitState(STREAM_D, ALLOCATION_QUANTUM, true);

            // Only write 3 * chunkSize, so that we'll only write to the first 3 streams.
            int written = 3 * ALLOCATION_QUANTUM;
            Assert.True(this.Write(written));
            Assert.Equal(ALLOCATION_QUANTUM, this.CaptureWrites(STREAM_A));
            Assert.Equal(ALLOCATION_QUANTUM, this.CaptureWrites(STREAM_B));
            Assert.Equal(ALLOCATION_QUANTUM, this.CaptureWrites(STREAM_C));
            this.VerifyWrite(Times.AtMost(1), STREAM_D, 0);

            // Now write again and verify that the last stream is written to.
            Assert.False(this.Write(ALLOCATION_QUANTUM));
            Assert.Equal(ALLOCATION_QUANTUM, this.CaptureWrites(STREAM_A));
            Assert.Equal(ALLOCATION_QUANTUM, this.CaptureWrites(STREAM_B));
            Assert.Equal(ALLOCATION_QUANTUM, this.CaptureWrites(STREAM_C));
            Assert.Equal(ALLOCATION_QUANTUM, this.CaptureWrites(STREAM_D));
        }

        /**
         * In this test, we verify that the highest priority frame which has 0 bytes to send, but an empty frame is able
         * to send that empty frame.
         *
         * <pre>
         *         0
         *        / \
         *       A   B
         *      / \
         *     C   D
         * </pre>
         *
         * After the tree shift:
         *
         * <pre>
         *         0
         *         |
         *         A
         *         |
         *         B
         *        / \
         *       C   D
         * </pre>
         */
        [Fact]
        public void EmptyFrameAtHeadIsWritten()
        {
            this.InitState(STREAM_A, 0, true);
            this.InitState(STREAM_B, 0, true);
            this.InitState(STREAM_C, 0, true);
            this.InitState(STREAM_D, 10, true);

            this.SetPriority(STREAM_B, STREAM_A, Http2CodecUtil.DefaultPriorityWeight, true);

            Assert.False(this.Write(10));
            this.VerifyWrite(STREAM_A, 0);
            this.VerifyWrite(STREAM_B, 0);
            this.VerifyWrite(STREAM_C, 0);
            this.VerifyWrite(STREAM_D, 10);
        }

        /**
         * In this test, we block A which allows bytes to be written by C and D. Here's a view of the tree (stream A is
         * blocked).
         *
         * <pre>
         *         0
         *        / \
         *      [A]  B
         *      / \
         *     C   D
         * </pre>
         */
        [Fact]
        public void BlockedStreamNoDataShouldSpreadDataToChildren()
        {
            BlockedStreamShouldSpreadDataToChildren(false);
        }

        /**
         * In this test, we block A and also give it an empty data frame to send.
         * All bytes should be delegated to by C and D. Here's a view of the tree (stream A is blocked).
         *
         * <pre>
         *           0
         *         /   \
         *      [A](0)  B
         *      / \
         *     C   D
         * </pre>
         */
        [Fact]
        public void BlockedStreamWithDataAndNotAllowedToSendShouldSpreadDataToChildren()
        {
            // A cannot stream.
            this.InitState(STREAM_A, 0, true, false);
            BlockedStreamShouldSpreadDataToChildren(false);
        }

        /**
         * In this test, we allow A to send, but expect the flow controller will only write to the stream 1 time.
         * This is because we give the stream a chance to write its empty frame 1 time, and the stream will not
         * be written to again until a update stream is called.
         *
         * <pre>
         *         0
         *        / \
         *       A   B
         *      / \
         *     C   D
         * </pre>
         */
        [Fact]
        public void StreamWithZeroFlowControlWindowAndDataShouldWriteOnlyOnce()
        {
            this.InitState(STREAM_A, 0, true, true);
            BlockedStreamShouldSpreadDataToChildren(true);

            // Make sure if we call update stream again, A should write 1 more time.
            this.InitState(STREAM_A, 0, true, true);
            Assert.False(this.Write(1));
            this.VerifyWrite(Times.Exactly(2), STREAM_A, 0);

            // Try to write again, but since no initState A should not write again
            Assert.False(this.Write(1));
            this.VerifyWrite(Times.Exactly(2), STREAM_A, 0);
        }

        private void BlockedStreamShouldSpreadDataToChildren(bool streamAShouldWriteZero)
        {
            this.InitState(STREAM_B, 10, true);
            this.InitState(STREAM_C, 10, true);
            this.InitState(STREAM_D, 10, true);

            // Write up to 10 bytes.
            Assert.True(this.Write(10));

            if (streamAShouldWriteZero)
            {
                this.VerifyWrite(STREAM_A, 0);
            }
            else
            {
                this.VerifyNeverWrite(STREAM_A);
            }
            this.VerifyWrite(Times.AtMost(1), STREAM_C, 0);
            this.VerifyWrite(Times.AtMost(1), STREAM_D, 0);

            // B is entirely written
            this.VerifyWrite(STREAM_B, 10);

            // Now test that writes get delegated from A (which is blocked) to its children
            Assert.True(this.Write(5));
            if (streamAShouldWriteZero)
            {
                this.VerifyWrite(Times.Exactly(1), STREAM_A, 0);
            }
            else
            {
                this.VerifyNeverWrite(STREAM_A);
            }
            this.VerifyWrite(STREAM_D, 5);
            this.VerifyWrite(Times.AtMost(1), STREAM_C, 0);

            Assert.True(this.Write(5));
            if (streamAShouldWriteZero)
            {
                this.VerifyWrite(Times.Exactly(1), STREAM_A, 0);
            }
            else
            {
                this.VerifyNeverWrite(STREAM_A);
            }
            Assert.Equal(10, this.CaptureWrites(STREAM_C) + this.CaptureWrites(STREAM_D));

            Assert.True(this.Write(5));
            Assert.False(this.Write(5));
            if (streamAShouldWriteZero)
            {
                this.VerifyWrite(Times.Exactly(1), STREAM_A, 0);
            }
            else
            {
                this.VerifyNeverWrite(STREAM_A);
            }
            this.VerifyWrite(Times.Exactly(2), STREAM_C, 5);
            this.VerifyWrite(Times.Exactly(2), STREAM_D, 5);
        }

        /**
         * In this test, we block B which allows all bytes to be written by A. A should not share the data with its children
         * since it's not blocked.
         *
         * <pre>
         *         0
         *        / \
         *       A  [B]
         *      / \
         *     C   D
         * </pre>
         */
        [Fact]
        public void ChildrenShouldNotSendDataUntilParentBlocked()
        {
            // B cannot stream.
            this.InitState(STREAM_A, 10, true);
            this.InitState(STREAM_C, 10, true);
            this.InitState(STREAM_D, 10, true);

            // Write up to 10 bytes.
            Assert.True(this.Write(10));

            // A is assigned all of the bytes.
            this.VerifyWrite(STREAM_A, 10);
            this.VerifyNeverWrite(STREAM_B);
            this.VerifyWrite(Times.AtMost(1), STREAM_C, 0);
            this.VerifyWrite(Times.AtMost(1), STREAM_D, 0);
        }

        /**
         * In this test, we block B which allows all bytes to be written by A. Once A is complete, it will spill over the
         * remaining of its portion to its children.
         *
         * <pre>
         *         0
         *        / \
         *       A  [B]
         *      / \
         *     C   D
         * </pre>
         */
        [Fact]
        public void ParentShouldWaterFallDataToChildren()
        {
            // B cannot stream.
            this.InitState(STREAM_A, 5, true);
            this.InitState(STREAM_C, 10, true);
            this.InitState(STREAM_D, 10, true);

            // Write up to 10 bytes.
            Assert.True(this.Write(10));

            this.VerifyWrite(STREAM_A, 5);
            this.VerifyNeverWrite(STREAM_B);
            this.VerifyWrite(STREAM_C, 5);
            this.VerifyNeverWrite(STREAM_D);

            Assert.False(this.Write(15));
            this.VerifyAnyWrite(STREAM_A, 1);
            this.VerifyNeverWrite(STREAM_B);
            this.VerifyWrite(Times.Exactly(2), STREAM_C, 5);
            this.VerifyWrite(STREAM_D, 10);
        }

        /**
         * In this test, we verify re-prioritizing a stream. We start out with B blocked:
         *
         * <pre>
         *         0
         *        / \
         *       A  [B]
         *      / \
         *     C   D
         * </pre>
         *
         * We then re-prioritize D so that it's directly off of the connection and verify that A and D split the written
         * bytes between them.
         *
         * <pre>
         *           0
         *          /|\
         *        /  |  \
         *       A  [B]  D
         *      /
         *     C
         * </pre>
         */
        [Fact]
        public void ReprioritizeShouldAdjustOutboundFlow()
        {
            // B cannot stream.
            this.InitState(STREAM_A, 10, true);
            this.InitState(STREAM_C, 10, true);
            this.InitState(STREAM_D, 10, true);

            // Re-prioritize D as a direct child of the connection.
            this.SetPriority(STREAM_D, 0, Http2CodecUtil.DefaultPriorityWeight, false);

            Assert.True(this.Write(10));

            this.VerifyWrite(STREAM_A, 10);
            this.VerifyNeverWrite(STREAM_B);
            this.VerifyNeverWrite(STREAM_C);
            this.VerifyWrite(Times.AtMost(1), STREAM_D, 0);

            Assert.False(this.Write(20));
            this.VerifyAnyWrite(STREAM_A, 1);
            this.VerifyNeverWrite(STREAM_B);
            this.VerifyWrite(STREAM_C, 10);
            this.VerifyWrite(STREAM_D, 10);
        }

        /**
         * Test that the maximum allowed amount the flow controller allows to be sent is always fully allocated if
         * the streams have at least this much data to send. See https://github.com/netty/netty/issues/4266.
         * <pre>
         *            0
         *          / | \
         *        /   |   \
         *      A(0) B(0) C(0)
         *     /
         *    D(> allowed to send in 1 allocation attempt)
         * </pre>
         */
        [Fact]
        public void UnstreamableParentsShouldFeedHungryChildren()
        {
            // Setup the priority tree.
            this.SetPriority(STREAM_A, 0, (short)32, false);
            this.SetPriority(STREAM_B, 0, (short)16, false);
            this.SetPriority(STREAM_C, 0, (short)16, false);
            this.SetPriority(STREAM_D, STREAM_A, (short)16, false);

            int writableBytes = 100;

            // Send enough so it can not be completely written out
            int expectedUnsentAmount = 1;
            this.InitState(STREAM_D, writableBytes + expectedUnsentAmount, true);

            Assert.True(this.Write(writableBytes));
            this.VerifyWrite(STREAM_D, writableBytes);

            Assert.False(this.Write(expectedUnsentAmount));
            this.VerifyWrite(STREAM_D, expectedUnsentAmount);
        }

        /**
         * In this test, we root all streams at the connection, and then verify that data is split appropriately based on
         * weight (all available data is the same).
         *
         * <pre>
         *           0
         *        / / \ \
         *       A B   C D
         * </pre>
         */
        [Fact]
        public void WriteShouldPreferHighestWeight()
        {
            // Root the streams at the connection and assign weights.
            this.SetPriority(STREAM_A, 0, (short)50, false);
            this.SetPriority(STREAM_B, 0, (short)200, false);
            this.SetPriority(STREAM_C, 0, (short)100, false);
            this.SetPriority(STREAM_D, 0, (short)100, false);

            this.InitState(STREAM_A, 1000, true);
            this.InitState(STREAM_B, 1000, true);
            this.InitState(STREAM_C, 1000, true);
            this.InitState(STREAM_D, 1000, true);

            // Set allocation quantum to 1 so it is easier to see the ratio of total bytes written between each stream.
            distributor.AllocationQuantum(1);
            Assert.True(this.Write(1000));

            Assert.Equal(100, this.CaptureWrites(STREAM_A));
            Assert.Equal(450, this.CaptureWrites(STREAM_B));
            Assert.Equal(225, this.CaptureWrites(STREAM_C));
            Assert.Equal(225, this.CaptureWrites(STREAM_D));
        }

        /**
         * In this test, we root all streams at the connection, block streams C and D, and then verify that data is
         * prioritized toward stream B which has a higher weight than stream A.
         * 
         * We also verify that the amount that is written is not uniform, and not always the allocation quantum.
         *
         * <pre>
         *            0
         *        / /  \  \
         *       A B   [C] [D]
         * </pre>
         */
        [Fact]
        public void WriteShouldFavorPriority()
        {
            // Root the streams at the connection and assign weights.
            this.SetPriority(STREAM_A, 0, (short)50, false);
            this.SetPriority(STREAM_B, 0, (short)200, false);
            this.SetPriority(STREAM_C, 0, (short)100, false);
            this.SetPriority(STREAM_D, 0, (short)100, false);

            this.InitState(STREAM_A, 1000, true);
            this.InitState(STREAM_B, 1000, true);
            this.InitState(STREAM_C, 1000, false);
            this.InitState(STREAM_D, 1000, false);

            // Set allocation quantum to 1 so it is easier to see the ratio of total bytes written between each stream.
            distributor.AllocationQuantum(1);

            Assert.True(this.Write(100));
            Assert.Equal(20, this.CaptureWrites(STREAM_A));
            this.VerifyWrite(Times.Exactly(20), STREAM_A, 1);
            Assert.Equal(80, this.CaptureWrites(STREAM_B));
            this.VerifyWrite(Times.Exactly(0), STREAM_B, 1);
            this.VerifyNeverWrite(STREAM_C);
            this.VerifyNeverWrite(STREAM_D);

            Assert.True(this.Write(100));
            Assert.Equal(40, this.CaptureWrites(STREAM_A));
            this.VerifyWrite(Times.Exactly(40), STREAM_A, 1);
            Assert.Equal(160, this.CaptureWrites(STREAM_B));
            this.VerifyWrite(Times.AtMost(1), STREAM_B, 1);
            this.VerifyNeverWrite(STREAM_C);
            this.VerifyNeverWrite(STREAM_D);

            Assert.True(this.Write(1050));
            Assert.Equal(250, this.CaptureWrites(STREAM_A));
            this.VerifyWrite(Times.Exactly(250), STREAM_A, 1);
            Assert.Equal(1000, this.CaptureWrites(STREAM_B));
            this.VerifyWrite(Times.AtMost(2), STREAM_B, 1);
            this.VerifyNeverWrite(STREAM_C);
            this.VerifyNeverWrite(STREAM_D);

            Assert.False(this.Write(750));
            Assert.Equal(1000, this.CaptureWrites(STREAM_A));
            this.VerifyWrite(Times.Exactly(1), STREAM_A, 750);
            Assert.Equal(1000, this.CaptureWrites(STREAM_B));
            this.VerifyWrite(Times.Exactly(0), STREAM_B, 0);
            this.VerifyNeverWrite(STREAM_C);
            this.VerifyNeverWrite(STREAM_D);
        }

        /**
         * In this test, we root all streams at the connection, and then verify that data is split equally among the stream,
         * since they all have the same weight.
         *
         * <pre>
         *           0
         *        / / \ \
         *       A B   C D
         * </pre>
         */
        [Fact]
        public void SamePriorityShouldDistributeBasedOnData()
        {
            // Root the streams at the connection with the same weights.
            this.SetPriority(STREAM_A, 0, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(STREAM_B, 0, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(STREAM_C, 0, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(STREAM_D, 0, Http2CodecUtil.DefaultPriorityWeight, false);

            this.InitState(STREAM_A, 400, true);
            this.InitState(STREAM_B, 500, true);
            this.InitState(STREAM_C, 0, true);
            this.InitState(STREAM_D, 700, true);

            // Set allocation quantum to 1 so it is easier to see the ratio of total bytes written between each stream.
            distributor.AllocationQuantum(1);
            Assert.True(this.Write(999));

            Assert.Equal(333, this.CaptureWrites(STREAM_A));
            Assert.Equal(333, this.CaptureWrites(STREAM_B));
            this.VerifyWrite(Times.Exactly(1), STREAM_C, 0);
            Assert.Equal(333, this.CaptureWrites(STREAM_D));
        }

        /**
         * In this test, we call distribute with 0 bytes and verify that all streams with 0 bytes are written.
         *
         * <pre>
         *         0
         *        / \
         *       A   B
         *      / \
         *     C   D
         * </pre>
         *
         * After the tree shift:
         *
         * <pre>
         *         0
         *         |
         *        [A]
         *         |
         *         B
         *        / \
         *       C   D
         * </pre>
         */
        [Fact]
        public void ZeroDistributeShouldWriteAllZeroFrames()
        {
            this.InitState(STREAM_A, 400, false);
            this.InitState(STREAM_B, 0, true);
            this.InitState(STREAM_C, 0, true);
            this.InitState(STREAM_D, 0, true);

            this.SetPriority(STREAM_B, STREAM_A, Http2CodecUtil.DefaultPriorityWeight, true);

            Assert.False(this.Write(0));
            this.VerifyNeverWrite(STREAM_A);
            this.VerifyWrite(STREAM_B, 0);
            this.VerifyAnyWrite(STREAM_B, 1);
            this.VerifyWrite(STREAM_C, 0);
            this.VerifyAnyWrite(STREAM_C, 1);
            this.VerifyWrite(STREAM_D, 0);
            this.VerifyAnyWrite(STREAM_D, 1);
        }

        /**
         * In this test, we call distribute with 100 bytes which is the total amount eligible to be written, and also have
         * streams with 0 bytes to write. All of these streams should be written with a single call to distribute.
         *
         * <pre>
         *         0
         *        / \
         *       A   B
         *      / \
         *     C   D
         * </pre>
         *
         * After the tree shift:
         *
         * <pre>
         *         0
         *         |
         *        [A]
         *         |
         *         B
         *        / \
         *       C   D
         * </pre>
         */
        [Fact]
        public void NonZeroDistributeShouldWriteAllZeroFramesIfAllEligibleDataIsWritten()
        {
            this.InitState(STREAM_A, 400, false);
            this.InitState(STREAM_B, 100, true);
            this.InitState(STREAM_C, 0, true);
            this.InitState(STREAM_D, 0, true);

            this.SetPriority(STREAM_B, STREAM_A, Http2CodecUtil.DefaultPriorityWeight, true);

            Assert.False(this.Write(100));
            this.VerifyNeverWrite(STREAM_A);
            this.VerifyWrite(STREAM_B, 100);
            this.VerifyAnyWrite(STREAM_B, 1);
            this.VerifyWrite(STREAM_C, 0);
            this.VerifyAnyWrite(STREAM_C, 1);
            this.VerifyWrite(STREAM_D, 0);
            this.VerifyAnyWrite(STREAM_D, 1);
        }

        /**
         * In this test, we shift the priority tree and verify priority bytes for each subtree are correct
         *
         * <pre>
         *         0
         *        / \
         *       A   B
         *      / \
         *     C   D
         * </pre>
         *
         * After the tree shift:
         *
         * <pre>
         *         0
         *         |
         *         A
         *         |
         *         B
         *        / \
         *       C   D
         * </pre>
         */
        [Fact]
        public void BytesDistributedWithRestructureShouldBeCorrect()
        {
            this.InitState(STREAM_A, 400, true);
            this.InitState(STREAM_B, 500, true);
            this.InitState(STREAM_C, 600, true);
            this.InitState(STREAM_D, 700, true);

            this.SetPriority(STREAM_B, STREAM_A, Http2CodecUtil.DefaultPriorityWeight, true);

            Assert.True(this.Write(500));
            Assert.Equal(400, this.CaptureWrites(STREAM_A));
            this.VerifyWrite(STREAM_B, 100);
            this.VerifyNeverWrite(STREAM_C);
            this.VerifyNeverWrite(STREAM_D);

            Assert.True(this.Write(400));
            Assert.Equal(400, this.CaptureWrites(STREAM_A));
            Assert.Equal(500, this.CaptureWrites(STREAM_B));
            this.VerifyWrite(Times.AtMost(1), STREAM_C, 0);
            this.VerifyWrite(Times.AtMost(1), STREAM_D, 0);

            Assert.False(this.Write(1300));
            Assert.Equal(400, this.CaptureWrites(STREAM_A));
            Assert.Equal(500, this.CaptureWrites(STREAM_B));
            Assert.Equal(600, this.CaptureWrites(STREAM_C));
            Assert.Equal(700, this.CaptureWrites(STREAM_D));
        }

        /**
         * In this test, we add a node to the priority tree and verify
         *
         * <pre>
         *         0
         *        / \
         *       A   B
         *      / \
         *     C   D
         * </pre>
         *
         * After the tree shift:
         *
         * <pre>
         *         0
         *        / \
         *       A   B
         *       |
         *       E
         *      / \
         *     C   D
         * </pre>
         */
        [Fact]
        public void BytesDistributedWithAdditionShouldBeCorrect()
        {
            IHttp2Stream streamE = connection.Local.CreateStream(STREAM_E, false);
            this.SetPriority(streamE.Id, STREAM_A, Http2CodecUtil.DefaultPriorityWeight, true);

            // Send a bunch of data on each stream.
            this.InitState(STREAM_A, 400, true);
            this.InitState(STREAM_B, 500, true);
            this.InitState(STREAM_C, 600, true);
            this.InitState(STREAM_D, 700, true);
            this.InitState(STREAM_E, 900, true);

            Assert.True(this.Write(900));
            Assert.Equal(400, this.CaptureWrites(STREAM_A));
            Assert.Equal(500, this.CaptureWrites(STREAM_B));
            this.VerifyNeverWrite(STREAM_C);
            this.VerifyNeverWrite(STREAM_D);
            this.VerifyWrite(Times.AtMost(1), STREAM_E, 0);

            Assert.True(this.Write(900));
            Assert.Equal(400, this.CaptureWrites(STREAM_A));
            Assert.Equal(500, this.CaptureWrites(STREAM_B));
            this.VerifyWrite(Times.AtMost(1), STREAM_C, 0);
            this.VerifyWrite(Times.AtMost(1), STREAM_D, 0);
            Assert.Equal(900, this.CaptureWrites(STREAM_E));

            Assert.False(this.Write(1301));
            Assert.Equal(400, this.CaptureWrites(STREAM_A));
            Assert.Equal(500, this.CaptureWrites(STREAM_B));
            Assert.Equal(600, this.CaptureWrites(STREAM_C));
            Assert.Equal(700, this.CaptureWrites(STREAM_D));
            Assert.Equal(900, this.CaptureWrites(STREAM_E));
        }

        /**
         * In this test, we close an internal stream in the priority tree.
         *
         * <pre>
         *         0
         *        / \
         *       A   B
         *      / \
         *     C   D
         * </pre>
         *
         * After the close:
         * <pre>
         *          0
         *        / | \
         *       C  D  B
         * </pre>
         */
        [Fact]
        public void BytesDistributedShouldBeCorrectWithInternalStreamClose()
        {
            this.InitState(STREAM_A, 400, true);
            this.InitState(STREAM_B, 500, true);
            this.InitState(STREAM_C, 600, true);
            this.InitState(STREAM_D, 700, true);

            this.Stream(STREAM_A).Close();

            Assert.True(this.Write(500));
            this.VerifyNeverWrite(STREAM_A);
            Assert.Equal(500, this.CaptureWrites(STREAM_B) + this.CaptureWrites(STREAM_C) + this.CaptureWrites(STREAM_D));

            Assert.False(this.Write(1300));
            this.VerifyNeverWrite(STREAM_A);
            Assert.Equal(500, this.CaptureWrites(STREAM_B));
            Assert.Equal(600, this.CaptureWrites(STREAM_C));
            Assert.Equal(700, this.CaptureWrites(STREAM_D));
        }

        /**
         * In this test, we close a leaf stream in the priority tree and verify distribution.
         *
         * <pre>
         *         0
         *        / \
         *       A   B
         *      / \
         *     C   D
         * </pre>
         *
         * After the close:
         * <pre>
         *         0
         *        / \
         *       A   B
         *       |
         *       D
         * </pre>
         */
        [Fact]
        public void BytesDistributedShouldBeCorrectWithLeafStreamClose()
        {
            this.InitState(STREAM_A, 400, true);
            this.InitState(STREAM_B, 500, true);
            this.InitState(STREAM_C, 600, true);
            this.InitState(STREAM_D, 700, true);

            this.Stream(STREAM_C).Close();

            Assert.True(this.Write(900));
            Assert.Equal(400, this.CaptureWrites(STREAM_A));
            Assert.Equal(500, this.CaptureWrites(STREAM_B));
            this.VerifyNeverWrite(STREAM_C);
            this.VerifyWrite(Times.AtMost(1), STREAM_D, 0);

            Assert.False(this.Write(700));
            Assert.Equal(400, this.CaptureWrites(STREAM_A));
            Assert.Equal(500, this.CaptureWrites(STREAM_B));
            this.VerifyNeverWrite(STREAM_C);
            Assert.Equal(700, this.CaptureWrites(STREAM_D));
        }


        [Fact]
        public void ActiveStreamDependentOnNewNonActiveStreamGetsQuantum()
        {
            this.Setup(0);
            this.InitState(STREAM_D, 700, true);
            this.SetPriority(STREAM_D, STREAM_E, Http2CodecUtil.DefaultPriorityWeight, true);

            Assert.False(this.Write(700));
            Assert.Equal(700, this.CaptureWrites(STREAM_D));
        }

        [Fact]
        public void StreamWindowLargerThanIntDoesNotInfiniteLoop()
        {
            this.InitState(STREAM_A, int.MaxValue + 1L, true, true);
            Assert.True(this.Write(int.MaxValue));
            this.VerifyWrite(STREAM_A, int.MaxValue);
            Assert.False(this.Write(1));
            this.VerifyWrite(STREAM_A, 1);
        }

        private bool Write(int numBytes)
        {
            return this.distributor.Distribute(numBytes, this.writer.Object);
        }

        private void VerifyWrite(int streamId, int numBytes)
        {
            this.writer.Verify(
                x => x.Write(
                    It.Is<IHttp2Stream>(v => ReferenceEquals(v, this.Stream(streamId))),
                    It.Is<int>(v => v == numBytes)));
        }

        private void VerifyWrite(Times mode, int streamId, int numBytes)
        {
            this.writer.Verify(
                x => x.Write(
                    It.Is<IHttp2Stream>(v => ReferenceEquals(v, this.Stream(streamId))),
                    It.Is<int>(v => v == numBytes)),
                mode);
        }

        private void VerifyAnyWrite(int streamId, int times)
        {
            this.writer.Verify(
                x => x.Write(
                    It.Is<IHttp2Stream>(v => ReferenceEquals(v, this.Stream(streamId))),
                    It.IsAny<int>()),
                Times.Exactly(times));
        }

        private void VerifyNeverWrite(int streamId)
        {
            VerifyNeverWrite(this.Stream(streamId));
        }

        private void VerifyNeverWrite(IHttp2Stream stream)
        {
            this.writer.Verify(
                x => x.Write(
                    It.Is<IHttp2Stream>(v => ReferenceEquals(v, stream)),
                    It.IsAny<int>()),
                Times.Never());
        }

        private int CaptureWrites(int streamId)
        {
            return CaptureWrites(this.Stream(streamId));
        }

        private int CaptureWrites(IHttp2Stream stream)
        {
            var captor = new ArgumentCaptor<int>();
            this.writer.Verify(
                x => x.Write(
                    It.Is<IHttp2Stream>(v => ReferenceEquals(v, stream)),
                    It.Is<int>(v => captor.Capture(v))),
                Times.AtLeastOnce());
            int total = 0;
            foreach (var x in captor.GetAllValues())
            {
                total += x;
            }
            return total;
        }
    }
}
