
namespace DotNetty.Codecs.Http2.Tests
{
    public class UniformStreamByteDistributorFlowControllerTest : DefaultHttp2RemoteFlowControllerTest
    {
        protected override IStreamByteDistributor NewDistributor(IHttp2Connection connection)
        {
            return new UniformStreamByteDistributor(connection);
        }
    }
}
