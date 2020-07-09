namespace DotNetty.Transport.Tests.Channel.Local
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Local;
    using Xunit;

    public class LocalTransportThreadModelTest2
    {
        private const string LOCAL_CHANNEL = nameof(LocalTransportThreadModelTest2);
        private const int c_messageCountPerRun = 4;

        [Fact]
        public async Task TestSocketReuse()
        {
            ServerBootstrap serverBootstrap = new ServerBootstrap();
            LocalHandler serverHandler = new LocalHandler("SERVER");
            serverBootstrap
                .Group(new DefaultEventLoopGroup(1), new DefaultEventLoopGroup())
                .Channel<LocalServerChannel>()
                .ChildHandler(serverHandler);
            Bootstrap clientBootstrap = new Bootstrap();
            LocalHandler clientHandler = new LocalHandler("CLIENT");
            clientBootstrap
                .Group(new DefaultEventLoopGroup())
                .Channel<LocalChannel>()
                .RemoteAddress(new LocalAddress(LOCAL_CHANNEL)).Handler(clientHandler);

            await serverBootstrap.BindAsync(new LocalAddress(LOCAL_CHANNEL));

            int count = 100;
            for (int i = 1; i < count + 1; i++)
            {
                var ch = await clientBootstrap.ConnectAsync();

                // SPIN until we get what we are looking for.
                int target = i * c_messageCountPerRun;
                while (serverHandler.Count.Value != target || clientHandler.Count.Value != target)
                {
                    Thread.Sleep(50);
                }
                Close(ch, clientHandler);
            }

            Assert.Equal(count * 2 * c_messageCountPerRun, serverHandler.Count.Value + clientHandler.Count.Value);

            Task.WaitAll(
                serverBootstrap.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                serverBootstrap.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                clientBootstrap.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5))
            );
        }

        private void Close(IChannel localChannel, LocalHandler localRegistrationHandler)
        {
            // we want to make sure we actually shutdown IN the event loop
            if (localChannel.EventLoop.InEventLoop)
            {
                // Wait until all messages are flushed before closing the channel.
                if (localRegistrationHandler.LastWriteFuture != null)
                {
                    localRegistrationHandler.LastWriteFuture.GetAwaiter().GetResult();
                }

                localChannel.CloseAsync();
                return;
            }

            localChannel.EventLoop.Execute(() => Close(localChannel, localRegistrationHandler));

            // Wait until the connection is closed or the connection attempt fails.
            localChannel.CloseCompletion.GetAwaiter().GetResult();
        }

        class LocalHandler : ChannelHandlerAdapter
        {
            private readonly string _name;

            public volatile Task LastWriteFuture;
            public readonly AtomicInteger Count = new AtomicInteger(0);

            public LocalHandler(string name) => _name = name;

            public override bool IsSharable => true;

            public override void ChannelActive(IChannelHandlerContext context)
            {
                for (int i = 0; i < c_messageCountPerRun; i++)
                {
                    LastWriteFuture = context.Channel.WriteAsync(_name + ' ' + i);
                }
                context.Channel.Flush();
            }

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                Count.Increment();
                ReferenceCountUtil.Release(message);
            }
        }
    }
}