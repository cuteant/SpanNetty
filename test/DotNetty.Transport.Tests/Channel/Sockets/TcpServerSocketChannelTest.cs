namespace DotNetty.Transport.Tests.Channel.Sockets
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Tests.Common;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit;
    using Xunit.Abstractions;

    [Collection("Transport Tests")]
    public class TcpServerSocketChannelTest : TestBase
    {
        public TcpServerSocketChannelTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task TestIsActiveFalseAfterClose()
        {
            var serverSocketChannel = new TcpServerSocketChannel();
            IEventLoopGroup group = new MultithreadEventLoopGroup(1);
            try
            {
                await group.RegisterAsync(serverSocketChannel);
                await serverSocketChannel.BindAsync(new IPEndPoint(IPAddress.IPv6Loopback, 0));
                Assert.True(serverSocketChannel.IsActive);
                Assert.True(serverSocketChannel.IsOpen);
                await serverSocketChannel.CloseAsync();
                Assert.False(serverSocketChannel.IsOpen);
                Assert.False(serverSocketChannel.IsActive);
            }
            finally
            {
                await group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
            }
        }
    }
}