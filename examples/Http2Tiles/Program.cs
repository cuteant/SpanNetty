namespace Http2Tiles
{
    using DotNetty.Common;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv;
    using Examples.Common;
    using System;
    using System.Runtime;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

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
            Console.WriteLine($"Transport type : {(useLibuv ? "Libuv" : "Socket")}");

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            }

            Console.WriteLine($"Server garbage collection : {(GCSettings.IsServerGC ? "Enabled" : "Disabled")}");
            Console.WriteLine($"Current latency mode for garbage collection: {GCSettings.LatencyMode}");
            Console.WriteLine("\n");

            IEventLoopGroup bossGroup;
            IEventLoopGroup workGroup;
            IEventLoopGroup bossGroup2 = null;
            IEventLoopGroup workGroup2 = null;
            if (useLibuv)
            {
                var dispatcher = new DispatcherEventLoopGroup();
                bossGroup = dispatcher;
                workGroup = new WorkerEventLoopGroup(dispatcher);

                dispatcher = new DispatcherEventLoopGroup();
                bossGroup2 = dispatcher;
                workGroup2 = new WorkerEventLoopGroup(dispatcher);
            }
            else
            {
                bossGroup = new MultithreadEventLoopGroup(1);
                workGroup = new MultithreadEventLoopGroup();
            }

            IChannel http2Channel = null;
            IChannel httpChannel = null;

            try
            {
                Http2Server http2 = useLibuv ? new Http2Server(bossGroup2, workGroup2) : new Http2Server(bossGroup, workGroup);
                http2Channel = await http2.StartAsync();

                Console.WriteLine($"Open your web browser and navigate to http://127.0.0.1:{HttpServer.PORT}");
                HttpServer http = new HttpServer(bossGroup, workGroup);
                httpChannel = await http.StartAsync();

                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"{exception}");
                Console.ReadKey();
            }
            finally
            {
                if (http2Channel != null) { await http2Channel.CloseAsync(); }
                if (httpChannel != null) { await httpChannel.CloseAsync(); }

                if (workGroup2 != null) { await workGroup2.ShutdownGracefullyAsync(); }
                if (bossGroup2 != null) { await bossGroup2.ShutdownGracefullyAsync(); }
                await workGroup.ShutdownGracefullyAsync();
                await bossGroup.ShutdownGracefullyAsync();
            }
        }
    }
}
