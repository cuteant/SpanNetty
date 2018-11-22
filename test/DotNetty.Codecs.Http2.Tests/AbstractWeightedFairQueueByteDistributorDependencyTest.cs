
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Moq;

    public abstract class AbstractWeightedFairQueueByteDistributorDependencyTest
    {
        protected IHttp2Connection connection;
        protected WeightedFairQueueByteDistributor distributor;
        internal Dictionary<int, Http2TestUtil.TestStreamByteDistributorStreamState> stateMap =
            new Dictionary<int, Http2TestUtil.TestStreamByteDistributorStreamState>();

        protected Mock<IStreamByteDistributorWriter> writer;

        protected IHttp2Stream Stream(int streamId)
        {
            return this.connection.Stream(streamId);
        }

        protected void WriteAnswer(IHttp2Stream stream, int numBytes, bool closeIfNoFrame)
        {
            this.stateMap.TryGetValue(stream.Id, out var state);
            state.pendingBytes -= numBytes;
            state.hasFrame = state.pendingBytes > 0;
            state.isWriteAllowed = state.hasFrame;
            if (closeIfNoFrame && !state.hasFrame)
            {
                stream.Close();
            }
            distributor.UpdateStreamableBytes(state);
        }

        protected void InitState(int streamId, long streamableBytes, bool hasFrame)
        {
            this.InitState(streamId, streamableBytes, hasFrame, hasFrame);
        }

        protected void InitState(int streamId, long pendingBytes, bool hasFrame, bool isWriteAllowed)
        {
            IHttp2Stream stream = this.Stream(streamId);
            var state = new Http2TestUtil.TestStreamByteDistributorStreamState(stream, pendingBytes, hasFrame, isWriteAllowed);
            stateMap[streamId] = state;
            distributor.UpdateStreamableBytes(state);
        }

        protected void SetPriority(int streamId, int parent, int weight, bool exclusive)
        {
            distributor.UpdateDependencyTree(streamId, parent, (short)weight, exclusive);
        }
    }
}
