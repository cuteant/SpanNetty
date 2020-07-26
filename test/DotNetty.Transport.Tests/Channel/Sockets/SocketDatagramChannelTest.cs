namespace DotNetty.Transport.Tests.Channel.Sockets
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Tests.Common;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels.Groups;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit;
    using Xunit.Abstractions;

    [Collection("Transport Tests")]
    public class SocketDatagramChannelTest : TestBase
    {
        public SocketDatagramChannelTest(ITestOutputHelper output)
            : base(output)
        {
        }

        /**
         * Test try to reproduce issue #1335
         */
        [Fact]
        public async Task TestBindMultiple()
        {
            DefaultChannelGroup channelGroup = new DefaultChannelGroup();
            IEventLoopGroup group = new MultithreadEventLoopGroup(1);
            try
            {
                for (int i = 0; i < 100; i++)
                {
                    Bootstrap udpBootstrap = new Bootstrap();
                    udpBootstrap
                        .Group(group)
                        .Channel<SocketDatagramChannel>()
                        .Option(ChannelOption.SoBroadcast, true)
                        .Handler(new ChannelInboundHandlerAdapter0());
                    var datagramChannel = await udpBootstrap
                            .BindAsync(new IPEndPoint(IPAddress.Loopback, 0));
                    channelGroup.Add(datagramChannel);

                }
                Assert.Equal(100, channelGroup.Count);
            }
            finally
            {
                await channelGroup.CloseAsync();
                await group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
            }
        }

        class ChannelInboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                // Discard
                ReferenceCountUtil.Release(message);
            }
        }
    }
}