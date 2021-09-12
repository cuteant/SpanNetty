
namespace Http2Tiles
{
    using DotNetty.Handlers.Logging;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using DotNetty.Transport.Libuv;
    using Examples.Common;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    /**
     * Demonstrates an Http2 server using Netty to display a bunch of images and
     * simulate latency. It is a Netty version of the <a href="https://http2.golang.org/gophertiles?latency=0">
     * Go lang HTTP2 tiles demo</a>.
     */
    public class Http2Server
    {
        public static readonly int PORT = int.Parse(ExampleHelper.Configuration["http2-port"]);

        readonly IEventLoopGroup _bossGroup;
        readonly IEventLoopGroup _workGroup;

        public Http2Server(IEventLoopGroup bossGroup, IEventLoopGroup workGroup)
        {
            _bossGroup = bossGroup;
            _workGroup = workGroup;
        }

        public Task<IChannel> StartAsync()
        {
            var bootstrap = new ServerBootstrap();
            bootstrap.Group(_bossGroup, _workGroup);

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

            var tlsCertificate = new X509Certificate2(Path.Combine(ExampleHelper.ProcessDirectory, "dotnetty.com.pfx"), "password");

            bootstrap
                .Option(ChannelOption.SoBacklog, 1024)
                //.Option(ChannelOption.Allocator, UnpooledByteBufferAllocator.Default)
                .Handler(new LoggingHandler("LSTN"))
                .ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    var tlsSettings = new ServerTlsSettings(tlsCertificate)
                    {
                        ApplicationProtocols = new List<SslApplicationProtocol>(new[]
                        {
                            SslApplicationProtocol.Http2,
                            SslApplicationProtocol.Http11
                        })
                    };
                    tlsSettings.AllowAnyClientCertificate();
                    ch.Pipeline.AddLast(new TlsHandler(tlsSettings));
                    ch.Pipeline.AddLast(new Http2OrHttpHandler());
                }));

            return bootstrap.BindAsync(IPAddress.Loopback, PORT);
        }
    }
}
