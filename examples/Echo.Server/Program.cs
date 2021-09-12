// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Echo.Server
{
    using DotNetty.Codecs;
    using DotNetty.Common;
    using DotNetty.Handlers.Logging;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using DotNetty.Transport.Libuv;
    using Examples.Common;
    using System;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    class Program
    {
        static async Task Main(string[] args)
        {
            ExampleHelper.SetConsoleLogger();

            IEventLoopGroup bossGroup;
            IEventLoopGroup workerGroup;

            if (ServerSettings.UseLibuv)
            {
                ResourceLeakDetector.Level = ResourceLeakDetector.DetectionLevel.Disabled;
                var dispatcher = new DispatcherEventLoopGroup();
                bossGroup = dispatcher;
                workerGroup = new WorkerEventLoopGroup(dispatcher);
            }
            else
            {
                bossGroup = new MultithreadEventLoopGroup(1);
                workerGroup = new MultithreadEventLoopGroup();
            }

            X509Certificate2 tlsCertificate = null;
            if (ServerSettings.IsSsl)
            {
                tlsCertificate = new X509Certificate2("dotnetty.com.pfx", "password");
            }

            var serverHandler = new EchoServerHandler();

            try
            {
                var bootstrap = new ServerBootstrap();
                bootstrap.Group(bossGroup, workerGroup);

                if (ServerSettings.UseLibuv)
                {
                    bootstrap.Channel<TcpServerChannel>();
                }
                else
                {
                    bootstrap.Channel<TcpServerSocketChannel>();
                }

                bootstrap
                  .Option(ChannelOption.SoBacklog, 100)
                  .Handler(new LoggingHandler())
                  .ChildHandler(new ActionChannelInitializer<IChannel>(channel =>
                  {
                      IChannelPipeline pipeline = channel.Pipeline;
                      if (tlsCertificate != null)
                      {
                          pipeline.AddLast("tls", TlsHandler.Server(tlsCertificate));
                      }
                      pipeline.AddLast(new LoggingHandler("SRV-CONN"));
                      pipeline.AddLast("framing-enc", new LengthFieldPrepender2(2));
                      pipeline.AddLast("framing-dec", new LengthFieldBasedFrameDecoder2(ushort.MaxValue, 0, 2, 0, 2));

                      pipeline.AddLast("echo", serverHandler);
                  }));

                IChannel boundChannel = await bootstrap.BindAsync(ServerSettings.Port);

                Console.ReadLine();

                await boundChannel.CloseAsync();
            }
            finally
            {
                await Task.WhenAll(
                    bossGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
                    workerGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)));
            }
        }
    }
}
