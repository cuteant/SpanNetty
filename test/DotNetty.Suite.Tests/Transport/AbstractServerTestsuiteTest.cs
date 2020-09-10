namespace DotNetty.Suite.Tests.Transport
{
    using DotNetty.Buffers;
    using DotNetty.Tests.Common;
    using DotNetty.Transport.Bootstrapping;
    using Xunit;
    using Xunit.Abstractions;

    [Collection("TestSuite")]
    public abstract class AbstractServerTestsuiteTest : TestBase
    {
        protected volatile ServerBootstrap _sb;

        public AbstractServerTestsuiteTest(ITestOutputHelper output)
            : base(output)
        {
        }

        protected ServerBootstrap NewBootstrap(IServerBootstrapFactory factory, IByteBufferAllocator allocator)
        {
            var bs = factory.NewInstance();
            Configure(bs, allocator);
            return bs;
        }

        protected abstract void Configure(ServerBootstrap sb, IByteBufferAllocator allocator);
    }
}