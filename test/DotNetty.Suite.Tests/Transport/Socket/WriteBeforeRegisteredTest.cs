namespace DotNetty.Suite.Tests.Transport.Socket
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using Xunit;
    using Xunit.Abstractions;

    public class WriteBeforeRegisteredTest : AbstractClientSocketTest
    {
        public WriteBeforeRegisteredTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public void WriteBeforeConnect(IByteBufferAllocator allocator)
        {
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(cb, allocator);

            TestWriteBeforeConnect(cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public void LibuvWriteBeforeConnect(IByteBufferAllocator allocator)
        {
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(cb, allocator);

            TestWriteBeforeConnect(cb);
        }

        private void TestWriteBeforeConnect(Bootstrap cb)
        {
            TestHandler h = new TestHandler(Output);
            IChannel ch = null;
            ServerBootstrap b = DefaultServerBootstrapFactory.Instance.NewInstance().ChildHandler(new ChannelHandlerAdapter());
            try
            {
                var sch = b.BindAsync(NewSocketAddress()).GetAwaiter().GetResult();
                ch = cb.Handler(h).ConnectAsync(sch.LocalAddress).GetAwaiter().GetResult();
                ch.WriteAndFlushAsync(Unpooled.WrappedBuffer(new byte[] { 1 }));
            }
            finally
            {
                if (ch != null)
                {
                    ch.CloseAsync();
                }
                Task.WaitAll(
                    b.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    b.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        class TestHandler : ChannelHandlerAdapter
        {
            private readonly ITestOutputHelper _output;

            public TestHandler(ITestOutputHelper output)
            {
                _output = output;
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                _output.WriteLine(cause.ToString());
            }
        }
    }
}
