
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Collections.Generic;
    using Moq;
    using Xunit;

    public class UniformStreamByteDistributorTest
    {
        private const int CHUNK_SIZE = Http2CodecUtil.DefaultMinAllocationChunk;

        private const int STREAM_A = 1;
        private const int STREAM_B = 3;
        private const int STREAM_C = 5;
        private const int STREAM_D = 7;

        private IHttp2Connection connection;
        private UniformStreamByteDistributor distributor;
        private Dictionary<int, Http2TestUtil.TestStreamByteDistributorStreamState> stateMap;

        private Mock<IStreamByteDistributorWriter> writer;

        public UniformStreamByteDistributorTest()
        {
            this.writer = new Mock<IStreamByteDistributorWriter>();
            stateMap = new Dictionary<int, Http2TestUtil.TestStreamByteDistributorStreamState>();
            this.connection = new DefaultHttp2Connection(false);
            this.distributor = new UniformStreamByteDistributor(this.connection);

            // Assume we always write all the allocated bytes.
            this.ResetWriter();

            connection.Local.CreateStream(STREAM_A, false);
            connection.Local.CreateStream(STREAM_B, false);
            IHttp2Stream streamC = connection.Local.CreateStream(STREAM_C, false);
            IHttp2Stream streamD = connection.Local.CreateStream(STREAM_D, false);
            this.SetPriority(streamC.Id, STREAM_A, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamD.Id, STREAM_A, Http2CodecUtil.DefaultPriorityWeight, false);
        }

        protected void WriteAnswer(IHttp2Stream stream, int numBytes)
        {
            this.stateMap.TryGetValue(stream.Id, out var state);
            state.pendingBytes -= numBytes;
            state.hasFrame = state.pendingBytes > 0;
            distributor.UpdateStreamableBytes(state);
        }

        private void ResetWriter()
        {
            this.writer.Reset();
            this.writer.Setup(x => x.Write(It.IsAny<IHttp2Stream>(), It.IsAny<int>()))
                       .Callback<IHttp2Stream, int>((stream, numBytes) => this.WriteAnswer(stream, numBytes));
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
            this.writer.VerifyNoOtherCalls();

            Assert.False(this.Write(10));
            this.writer.VerifyNoOtherCalls();
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
                .Callback<IHttp2Stream, int>((stream, numBytes) => { });
            this.Write(10);
            this.VerifyWrite(STREAM_A, 1);
            this.VerifyWrite(STREAM_B, 2);
            this.VerifyWrite(STREAM_C, 3);
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
            this.InitState(STREAM_A, CHUNK_SIZE, true);
            this.InitState(STREAM_B, CHUNK_SIZE, true);
            this.InitState(STREAM_C, CHUNK_SIZE, true);
            this.InitState(STREAM_D, CHUNK_SIZE, true);

            // Only write 3 * chunkSize, so that we'll only write to the first 3 streams.
            int written = 3 * CHUNK_SIZE;
            Assert.True(this.Write(written));
            Assert.Equal(CHUNK_SIZE, this.CaptureWrite(STREAM_A));
            Assert.Equal(CHUNK_SIZE, this.CaptureWrite(STREAM_B));
            Assert.Equal(CHUNK_SIZE, this.CaptureWrite(STREAM_C));
            this.writer.VerifyNoOtherCalls();

            this.ResetWriter();

            // Now write again and verify that the last stream is written to.
            Assert.False(this.Write(CHUNK_SIZE));
            Assert.Equal(CHUNK_SIZE, this.CaptureWrite(STREAM_D));
            this.writer.VerifyNoOtherCalls();
        }

        [Fact]
        public void StreamWithMoreDataShouldBeEnqueuedAfterWrite()
        {
            // Give the stream a bunch of data.
            this.InitState(STREAM_A, 2 * CHUNK_SIZE, true);

            // Write only part of the data.
            Assert.True(this.Write(CHUNK_SIZE));
            Assert.Equal(CHUNK_SIZE, this.CaptureWrite(STREAM_A));
            this.writer.VerifyNoOtherCalls();

            this.ResetWriter();

            // Now write the rest of the data.
            Assert.False(this.Write(CHUNK_SIZE));
            Assert.Equal(CHUNK_SIZE, this.CaptureWrite(STREAM_A));
            this.writer.VerifyNoOtherCalls();
        }

        [Fact]
        public void EmptyFrameAtHeadIsWritten()
        {
            this.InitState(STREAM_A, 10, true);
            this.InitState(STREAM_B, 0, true);
            this.InitState(STREAM_C, 0, true);
            this.InitState(STREAM_D, 10, true);

            Assert.True(this.Write(10));
            this.VerifyWrite(STREAM_A, 10);
            this.VerifyWrite(STREAM_B, 0);
            this.VerifyWrite(STREAM_C, 0);
            this.writer.VerifyNoOtherCalls();
        }

        [Fact]
        public void StreamWindowExhaustedDoesNotWrite()
        {
            this.InitState(STREAM_A, 0, true, false);
            this.InitState(STREAM_B, 0, true);
            this.InitState(STREAM_C, 0, true);
            this.InitState(STREAM_D, 0, true, false);

            Assert.False(this.Write(10));
            this.VerifyWrite(STREAM_B, 0);
            this.VerifyWrite(STREAM_C, 0);
            this.writer.VerifyNoOtherCalls();
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

        private IHttp2Stream Stream(int streamId)
        {
            return this.connection.Stream(streamId);
        }

        private void InitState(int streamId, long streamableBytes, bool hasFrame)
        {
            this.InitState(streamId, streamableBytes, hasFrame, hasFrame);
        }

        private void InitState(int streamId, long pendingBytes, bool hasFrame, bool isWriteAllowed)
        {
            IHttp2Stream stream = this.Stream(streamId);
            var state = new Http2TestUtil.TestStreamByteDistributorStreamState(stream, pendingBytes, hasFrame, isWriteAllowed);
            stateMap[streamId] = state;
            distributor.UpdateStreamableBytes(state);
        }

        private void SetPriority(int streamId, int parent, int weight, bool exclusive)
        {
            distributor.UpdateDependencyTree(streamId, parent, (short)weight, exclusive);
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

        private int CaptureWrite(int streamId)
        {
            var captor = new ArgumentCaptor<int>();
            this.writer.Verify(
                x => x.Write(
                    It.Is<IHttp2Stream>(v => ReferenceEquals(v, this.Stream(streamId))),
                    It.Is<int>(v => captor.Capture(v))));
            return captor.GetValue();
        }
    }
}
