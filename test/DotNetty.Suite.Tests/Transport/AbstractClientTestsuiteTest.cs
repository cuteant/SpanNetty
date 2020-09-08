namespace DotNetty.Suite.Tests.Transport
{
    using DotNetty.Buffers;
    using DotNetty.Tests.Common;
    using DotNetty.Transport.Bootstrapping;
    using Xunit;
    using Xunit.Abstractions;

    [Collection("TestSuite")]
    public abstract class AbstractClientTestsuiteTest : TestBase
    {
        protected volatile Bootstrap _cb;

        public AbstractClientTestsuiteTest(ITestOutputHelper output)
            : base(output)
        {
        }

        protected Bootstrap NewBootstrap(IClientBootstrapFactory factory, IByteBufferAllocator allocator)
        {
            var cb = factory.NewInstance();
            Configure(cb, allocator);
            return cb;
        }

        protected abstract void Configure(Bootstrap cb, IByteBufferAllocator allocator);
    }
}