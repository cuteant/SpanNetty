
namespace Http2Tiles
{
    using DotNetty.Codecs.Http;
    using DotNetty.Handlers.Logging;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using DotNetty.Transport.Libuv;
    using Examples.Common;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    /// <summary>
    /// Demonstrates an http server using Netty to display a bunch of images, simulate
    /// latency and compare it against the http2 implementation.
    /// </summary>
    class HttpServer
    {
        public static readonly int PORT = int.Parse(ExampleHelper.Configuration["http-port"]);

        static readonly int MAX_CONTENT_LENGTH = 1024 * 100;

        readonly IEventLoopGroup bossGroup;
        readonly IEventLoopGroup workGroup;

        public HttpServer(IEventLoopGroup bossGroup, IEventLoopGroup workGroup)
        {
            this.bossGroup = bossGroup;
            this.workGroup = workGroup;
        }

        public Task<IChannel> StartAsync()
        {
            var bootstrap = new ServerBootstrap();
            bootstrap.Group(this.bossGroup, this.workGroup);

            if (ServerSettings.UseLibuv)
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
                .Option(ChannelOption.SoBacklog, 1024)
                //.Option(ChannelOption.Allocator, UnpooledByteBufferAllocator.Default)
                .Handler(new LoggingHandler("LSTN"))
                .ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new HttpRequestDecoder(),
                                        new HttpResponseEncoder(),
                                        new HttpObjectAggregator(MAX_CONTENT_LENGTH),
                                        new Http1RequestHandler());
                }));

            return bootstrap.BindAsync(IPAddress.Loopback, PORT);
        }
    }
}
