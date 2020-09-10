namespace DotNetty.Suite.Tests.Transport
{
    using DotNetty.Buffers;
    using DotNetty.Tests.Common;
    using DotNetty.Transport.Bootstrapping;
    using Xunit;
    using Xunit.Abstractions;

    [Collection("TestSuite")]
    public abstract class AbstractComboTestsuiteTest : TestBase
    {
        protected volatile Bootstrap _cb;
        protected volatile ServerBootstrap _sb;

        public AbstractComboTestsuiteTest(ITestOutputHelper output)
            : base(output)
        {
        }

        protected (ServerBootstrap sb, Bootstrap cb) NewBootstrap(IServerBootstrapFactory serverBootstrapFactory, IClientBootstrapFactory clientBootstrapFactory, IByteBufferAllocator allocator)
        {
            var sb = serverBootstrapFactory.NewInstance();
            var cb = clientBootstrapFactory.NewInstance();
            Configure(sb, cb, allocator);
            return (sb, cb);
        }

        protected abstract void Configure(ServerBootstrap sb, Bootstrap cb, IByteBufferAllocator allocator);
    }
}