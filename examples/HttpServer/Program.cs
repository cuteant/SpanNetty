// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace HttpServer
{
    using System;
    using System.IO;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using DotNetty.Codecs.Http;
    using DotNetty.Common;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using DotNetty.Transport.Libuv;
    using Examples.Common;

    class Program
    {
        static Program()
        {
            ResourceLeakDetector.Level = ResourceLeakDetector.DetectionLevel.Disabled;
        }

        static async Task RunServerAsync()
        {
            bool useLibuv = ServerSettings.UseLibuv;
            Console.WriteLine("Transport type : " + (useLibuv ? "Libuv" : "Socket"));

            IEventLoopGroup group;
            IEventLoopGroup workGroup;
            if (useLibuv)
            {
                var dispatcherLoop = new DispatcherEventLoop();
                group = new MultithreadEventLoopGroup(_ => dispatcherLoop, 1);
                workGroup = new WorkerEventLoopGroup(dispatcherLoop);
            }
            else
            {
                group = new MultithreadEventLoopGroup(1);
                workGroup = new MultithreadEventLoopGroup();
            }

            X509Certificate2 tlsCertificate = null;
            if (ServerSettings.IsSsl)
            {
                tlsCertificate = new X509Certificate2(Path.Combine(ExampleHelper.ProcessDirectory, "dotnetty.com.pfx"), "password");
            }
            try
            {
                var bootstrap = new ServerBootstrap();
                bootstrap.Group(group, workGroup);

                if (useLibuv)
                {
                    bootstrap.Channel<TcpServerChannel>();
                }
                else
                {
                    bootstrap.Channel<TcpServerSocketChannel>();
                }

                bootstrap
                    .Option(ChannelOption.SoBacklog, 8192)
                    .Option(ChannelOption.SoReuseaddr, true)
                    .ChildHandler(
                        new ActionChannelInitializer<IChannel>(channel =>
                            {
                                IChannelPipeline pipeline = channel.Pipeline;
                                if (tlsCertificate != null)
                                {
                                    pipeline.AddLast(TlsHandler.Server(tlsCertificate));
                                }

                                pipeline.AddLast("encoder", new HttpResponseEncoder());
                                pipeline.AddLast("decoder", new HttpRequestDecoder(4096, 8192, 8192, false));
                                pipeline.AddLast("handler", new HelloServerHandler());
                            }))
                    .ChildOption(ChannelOption.SoReuseaddr, true);

                IChannel bootstrapChannel = await bootstrap.BindAsync(IPAddress.IPv6Any, ServerSettings.Port);

                Console.WriteLine($"Httpd started. Listening on {bootstrapChannel.LocalAddress}");
                Console.ReadLine();

                await bootstrapChannel.CloseAsync();
            }
            finally
            {
                group.ShutdownGracefullyAsync().Wait();
            }
        }

        static void Main() => RunServerAsync().Wait();
    }
}