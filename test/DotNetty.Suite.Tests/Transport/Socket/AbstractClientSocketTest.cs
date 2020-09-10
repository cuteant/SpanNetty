namespace DotNetty.Suite.Tests.Transport.Socket
{
    using System.Collections.Generic;
    using System.Net;
    using DotNetty.Buffers;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using Xunit.Abstractions;

    public abstract class AbstractClientSocketTest : AbstractClientTestsuiteTest
    {
        public AbstractClientSocketTest(ITestOutputHelper output)
            : base(output)
        {
        }

        public static IEnumerable<object[]> GetAllocators()
        {
            foreach (var item in SocketTestPermutation.Allocators)
            {
                yield return new[] { item };
            }
        }

        protected override void Configure(Bootstrap cb, IByteBufferAllocator allocator)
        {
            cb.Option(ChannelOption.Allocator, allocator);
        }

        protected IPEndPoint NewSocketAddress()
        {
            return new IPEndPoint(IPAddress.IPv6Loopback, 0);
        }
    }
}
