namespace Http2Helloworld.FrameClient
{
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http2;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using DotNetty.Transport.Libuv;
    using Examples.Common;
    using System;
    using System.IO;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    /// <summary>
    /// An HTTP2 client that allows you to send HTTP2 frames to a server using HTTP1-style approaches
    /// (via {@link io.netty.handler.codec.http2.InboundHttp2ToHttpAdapter}). Inbound and outbound
    /// frames are logged.
    /// 
    /// When run from the command-line, sends a single HEADERS frame to the server and gets back
    /// a "Hello World" response.
    /// See the ./http2/helloworld/frame/client/ example for a HTTP2 client example which does not use
    /// HTTP1-style objects and patterns.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            ExampleHelper.SetConsoleLogger();

            bool useLibuv = ClientSettings.UseLibuv;
            Console.WriteLine($"Transport type : {(useLibuv ? "Libuv" : "Socket")}");

            IEventLoopGroup group;
            if (useLibuv)
            {
                group = new EventLoopGroup();
            }
            else
            {
                group = new MultithreadEventLoopGroup();
            }

            X509Certificate2 cert = null;
            string targetHost = null;
            if (ClientSettings.IsSsl)
            {
                cert = new X509Certificate2(Path.Combine(ExampleHelper.ProcessDirectory, "dotnetty.com.pfx"), "password");
                targetHost = cert.GetNameInfo(X509NameType.DnsName, false);
            }

            try
            {
                var bootstrap = new Bootstrap();
                bootstrap
                    .Group(group)
                    .Option(ChannelOption.TcpNodelay, true)
                    .Option(ChannelOption.SoKeepalive, true);

                if (useLibuv)
                {
                    bootstrap.Channel<TcpChannel>();
                }
                else
                {
                    bootstrap.Channel<TcpSocketChannel>();
                }

                bootstrap.Handler(new Http2ClientFrameInitializer(cert, targetHost));

                IChannel channel = await bootstrap.ConnectAsync(new IPEndPoint(ClientSettings.Host, ClientSettings.Port));

                try
                {
                    Console.WriteLine($"Connected to [{ClientSettings.Host}:{ClientSettings.Port}");

                    Http2ClientStreamFrameResponseHandler streamFrameResponseHandler =
                           new Http2ClientStreamFrameResponseHandler();

                    Http2StreamChannelBootstrap streamChannelBootstrap = new Http2StreamChannelBootstrap(channel);
                    IHttp2StreamChannel streamChannel = await streamChannelBootstrap.OpenAsync();
                    streamChannel.Pipeline.AddLast(streamFrameResponseHandler);

                    // Send request (a HTTP/2 HEADERS frame - with ':method = GET' in this case)
                    var path = ExampleHelper.Configuration["path"];
                    HttpScheme scheme = ClientSettings.IsSsl ? HttpScheme.Https : HttpScheme.Http;
                    DefaultHttp2Headers headers = new DefaultHttp2Headers
                    {
                        Method = HttpMethod.Get.AsciiName,
                        Path = AsciiString.Of(path),
                        Scheme = scheme.Name
                    };
                    IHttp2HeadersFrame headersFrame = new DefaultHttp2HeadersFrame(headers);
                    await streamChannel.WriteAndFlushAsync(headersFrame);
                    Console.WriteLine($"Sent HTTP/2 GET request to {path}");

                    // Wait for the responses (or for the latch to expire), then clean up the connections
                    if (!streamFrameResponseHandler.ResponseSuccessfullyCompleted())
                    {
                        Console.WriteLine("Did not get HTTP/2 response in expected time.");
                    }

                    Console.WriteLine("Finished HTTP/2 request, will close the connection.");
                    Console.ReadKey();
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"{exception}");
                    Console.WriteLine("Press any key to exit");
                    Console.ReadKey();
                }
                finally
                {
                    // Wait until the connection is closed.
                    await channel.CloseAsync();
                }
            }
            finally
            {
                await group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
            }
        }
    }
}
