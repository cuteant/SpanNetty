// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace WebSockets.Server
{
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Codecs.Http.WebSockets.Extensions.Compression;
    using DotNetty.Common;
    using DotNetty.Handlers;
    using DotNetty.Handlers.Timeout;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using DotNetty.Transport.Libuv;
    using Examples.Common;
    using System;
    using System.IO;
    using System.Net;
    using System.Runtime;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    class Program
    {
        private const string WEBSOCKET_PATH = "/websocket";

        static Program()
        {
            ResourceLeakDetector.Level = ResourceLeakDetector.DetectionLevel.Disabled;
        }

        static async Task Main(string[] args)
        {
            ExampleHelper.SetConsoleLogger();

            Console.WriteLine(
                $"\n{RuntimeInformation.OSArchitecture} {RuntimeInformation.OSDescription}"
                + $"\n{RuntimeInformation.ProcessArchitecture} {RuntimeInformation.FrameworkDescription}"
                + $"\nProcessor Count : {Environment.ProcessorCount}\n");

            bool useLibuv = ServerSettings.UseLibuv;
            Console.WriteLine($"Transport type : {(useLibuv ? "Libuv" : "Socket")}");

            string websocketPath = ExampleHelper.Configuration["path"];
            websocketPath = !string.IsNullOrEmpty(websocketPath) ? websocketPath : WEBSOCKET_PATH;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            }

            Console.WriteLine($"Server garbage collection : {(GCSettings.IsServerGC ? "Enabled" : "Disabled")}");
            Console.WriteLine($"Current latency mode for garbage collection: {GCSettings.LatencyMode}");
            Console.WriteLine("\n");

            IEventLoopGroup bossGroup;
            IEventLoopGroup workGroup;
            if (useLibuv)
            {
                var dispatcher = new DispatcherEventLoopGroup();
                bossGroup = dispatcher;
                workGroup = new WorkerEventLoopGroup(dispatcher);
            }
            else
            {
                bossGroup = new MultithreadEventLoopGroup(1);
                workGroup = new MultithreadEventLoopGroup();
            }

            X509Certificate2 tlsCertificate = null;
            if (ServerSettings.IsSsl)
            {
                tlsCertificate = new X509Certificate2(Path.Combine(ExampleHelper.ProcessDirectory, "dotnetty.com.pfx"), "password");
            }

            try
            {
                int port = ServerSettings.Port;
                IChannel bootstrapChannel = null;

                var bootstrap = new ServerBootstrap();
                bootstrap.Group(bossGroup, workGroup);

                if (useLibuv)
                {
                    bootstrap.Channel<TcpServerChannel>();
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                        || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        bootstrap
                            .Option(ChannelOption.SoReuseport, true)
                            .ChildOption(ChannelOption.SoReuseaddr, true);
                    }
                }
                else
                {
                    bootstrap.Channel<TcpServerSocketChannel>();
                }

                bootstrap
                    .Option(ChannelOption.SoBacklog, 8192)

                    //.Handler(new LoggingHandler("LSTN"))
                    .Handler(new ServerChannelRebindHandler(DoBind))

                    .ChildHandler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;
                        if (ServerSettings.IsSsl)
                        {
                            pipeline.AddLast(TlsHandler.Server(tlsCertificate, true));
                        }

                        pipeline.AddLast("idleStateHandler", new IdleStateHandler(0, 0, 120));

                        //pipeline.AddLast(new LoggingHandler("CONN"));
                        //pipeline.AddLast(new HttpRequestDecoder());
                        //pipeline.AddLast(new HttpResponseEncoder());
                        pipeline.AddLast(new HttpServerCodec());
                        pipeline.AddLast(new HttpObjectAggregator(65536));
                        pipeline.AddLast(new WebSocketServerCompressionHandler());
                        pipeline.AddLast(new WebSocketServerProtocolHandler(
                            websocketPath: websocketPath,
                            subprotocols: null,
                            allowExtensions: true,
                            maxFrameSize: 65536,
                            allowMaskMismatch: true,
                            checkStartsWith: false,
                            dropPongFrames: true,
                            enableUtf8Validator: false));
                        pipeline.AddLast(new WebSocketServerHttpHandler(websocketPath));
                        pipeline.AddLast(new WebSocketFrameAggregator(65536));
                        pipeline.AddLast(new WebSocketServerFrameHandler());
                    }));

                bootstrapChannel = await bootstrap.BindAsync(IPAddress.Loopback, port);

                async void DoBind()
                {
                    await bootstrapChannel.CloseAsync();
                    Console.WriteLine("rebind......");
                    var ch = await bootstrap.BindAsync(IPAddress.Loopback, port);
                    Console.WriteLine("rebind complate");
                    Interlocked.Exchange(ref bootstrapChannel, ch);
                }

                Console.WriteLine("Open your web browser and navigate to "
                    + $"{(ServerSettings.IsSsl ? "https" : "http")}"
                    + $"://127.0.0.1:{port}/");
                Console.WriteLine("Listening on "
                    + $"{(ServerSettings.IsSsl ? "wss" : "ws")}"
                    + $"://127.0.0.1:{port}/websocket");
                Console.ReadLine();

                await bootstrapChannel.CloseAsync();
                Console.WriteLine("close completion");
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            }
            finally
            {
                await workGroup.ShutdownGracefullyAsync();// (TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5));
                await bossGroup.ShutdownGracefullyAsync();// (TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5));
            }
        }
    }
}
