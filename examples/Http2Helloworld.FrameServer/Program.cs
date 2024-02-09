namespace Http2Helloworld.FrameServer
{
    using DotNetty.Common;
    using DotNetty.Handlers.Logging;
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
    using System.Threading.Tasks;

    /// <summary>
    /// An HTTP/2 Server that responds to requests with a Hello World. Once started, you can test the
    /// server with the example client.
    /// 
    /// <para>This example is making use of the "multiplexing" http2 API, where streams are mapped to child
    /// Channels. This API is very experimental and incomplete.</para>
    /// </summary>
    class Program
    {
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
            Console.WriteLine("Transport type : " + (useLibuv ? "Libuv" : "Socket"));

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
                    .Handler(new LoggingHandler("LSTN"))
                    //.Handler(new ServerChannelRebindHandler(DoBind))
                    .ChildHandler(new Http2ServerInitializer(tlsCertificate));

                bootstrapChannel = await bootstrap.BindAsync(IPAddress.Loopback, port);

                //async void DoBind()
                //{
                //    await bootstrapChannel.CloseAsync();
                //    Console.WriteLine("rebind......");
                //    var ch = await bootstrap.BindAsync(IPAddress.Loopback, port);
                //    Console.WriteLine("rebind complate");
                //    Interlocked.Exchange(ref bootstrapChannel, ch);
                //}

                Console.WriteLine("Open your HTTP/2-enabled web browser and navigate to " +
                        (ServerSettings.IsSsl ? "https" : "http") + "://127.0.0.1:" + ServerSettings.Port + '/');

                Console.WriteLine("Press any key to exit");
                Console.ReadKey();

                await bootstrapChannel.CloseAsync();
            }
            finally
            {
                await workGroup.ShutdownGracefullyAsync(); // (TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5));
                await bossGroup.ShutdownGracefullyAsync(); // (TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5));
            }
        }
    }
}
