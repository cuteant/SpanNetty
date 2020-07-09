namespace DotNetty.Transport.Tests.Channel.Group
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Groups;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit;

    [Collection("TransportTests")]
    public class DefaultChannelGroupTest
    {
        // Test for #1183
        [Fact]
        public async Task TestNotThrowBlockingOperationException()
        {
            IEventLoopGroup bossGroup = new MultithreadEventLoopGroup(1);
            IEventLoopGroup workerGroup = new MultithreadEventLoopGroup(1);


            IChannelGroup allChannels = new DefaultChannelGroup(null);

            ServerBootstrap b = new ServerBootstrap();
            b.Group(bossGroup, workerGroup);
            b.ChildHandler(new ChannelInboundHandlerAdapter0(allChannels));
            b.Channel<TcpServerSocketChannel>();

            var ch = await b.BindAsync(0);

            allChannels.Add(ch);
            await allChannels.CloseAsync();

            await Task.WhenAll(
                bossGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                workerGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5))
            );
            await bossGroup.TerminationCompletion;
            await workerGroup.TerminationCompletion;
        }

        class ChannelInboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            private readonly IChannelGroup _allChannels;

            public ChannelInboundHandlerAdapter0(IChannelGroup allChannels)
            {
                _allChannels = allChannels;
            }

            public override void ChannelActive(IChannelHandlerContext context)
            {
                _allChannels.Add(context.Channel);
            }
        }
    }
}