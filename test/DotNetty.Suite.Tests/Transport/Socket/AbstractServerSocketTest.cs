﻿namespace DotNetty.Suite.Tests.Transport.Socket
{
    using System.Collections.Generic;
    using System.Net;
    using DotNetty.Buffers;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using Xunit.Abstractions;

    public abstract class AbstractServerSocketTest : AbstractServerTestsuiteTest
    {
        public AbstractServerSocketTest(ITestOutputHelper output)
            : base(output)
        {
        }

        public static IEnumerable<object[]> GetAllocators()
        {
            foreach(var item in SocketTestPermutation.Allocators)
            {
                yield return new[] { item };
            }
        }

        protected override void Configure(ServerBootstrap sb, IByteBufferAllocator allocator)
        {
            sb.LocalAddress(NewSocketAddress());
            sb.Option(ChannelOption.Allocator, allocator);
            sb.ChildOption(ChannelOption.Allocator, allocator);
        }

        protected IPEndPoint NewSocketAddress()
        {
            return new IPEndPoint(IPAddress.IPv6Loopback, 0);
        }
    }
}
