
namespace DotNetty.Codecs.Http2.Tests
{
    public class WeightedFairQueueRemoteFlowControllerTest: DefaultHttp2RemoteFlowControllerTest
    {
        protected override IStreamByteDistributor NewDistributor(IHttp2Connection connection)
        {
            return new WeightedFairQueueByteDistributor(connection);
        }
    }
}
