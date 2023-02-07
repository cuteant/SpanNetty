namespace Http2Helloworld.Client
{
    using DotNetty.Buffers;
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
    using System.Text;
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
                    .Option(ChannelOption.TcpNodelay, true);

                if (useLibuv)
                {
                    bootstrap.Channel<TcpChannel>();
                }
                else
                {
                    bootstrap.Channel<TcpSocketChannel>();
                }

                Http2ClientInitializer initializer = new Http2ClientInitializer(cert, targetHost, int.MaxValue);
                bootstrap.Handler(initializer);

                IChannel channel = await bootstrap.ConnectAsync(new IPEndPoint(ClientSettings.Host, ClientSettings.Port));

                try
                {
                    Console.WriteLine($"Connected to [{ClientSettings.Host}:{ClientSettings.Port}]");

                    // Wait for the HTTP/2 upgrade to occur.
                    Http2SettingsHandler http2SettingsHandler = initializer.SettingsHandler;
                    await http2SettingsHandler.AwaitSettings(TimeSpan.FromSeconds(5));

                    HttpResponseHandler responseHandler = initializer.ResponseHandler;
                    int streamId = 3;
                    HttpScheme scheme = ClientSettings.IsSsl ? HttpScheme.Https : HttpScheme.Http;
                    AsciiString hostName = new AsciiString(ClientSettings.Host.ToString() + ':' + ClientSettings.Port);
                    Console.WriteLine("Sending request(s)...");

                    var url = ExampleHelper.Configuration["url"];
                    var url2 = ExampleHelper.Configuration["url2"];
                    var url2Data = ExampleHelper.Configuration["url2data"];

                    if (!string.IsNullOrEmpty(url))
                    {
                        // Create a simple GET request.
                        IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get, url, Unpooled.Empty);
                        request.Headers.Add(HttpHeaderNames.Host, hostName);
                        request.Headers.Add(HttpConversionUtil.ExtensionHeaderNames.Scheme, scheme.Name);
                        request.Headers.Add(HttpHeaderNames.AcceptEncoding, HttpHeaderValues.Gzip);
                        request.Headers.Add(HttpHeaderNames.AcceptEncoding, HttpHeaderValues.Deflate);
                        responseHandler.Put(streamId, channel.WriteAsync(request), channel.NewPromise());
                        streamId += 2;
                    }

                    if (!string.IsNullOrEmpty(url2))
                    {
                        // Create a simple POST request with a body.
                        IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Post, url2,
                                Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes(url2Data)));
                        request.Headers.Add(HttpHeaderNames.Host, hostName);
                        request.Headers.Add(HttpConversionUtil.ExtensionHeaderNames.Scheme, scheme.Name);
                        request.Headers.Add(HttpHeaderNames.AcceptEncoding, HttpHeaderValues.Gzip);
                        request.Headers.Add(HttpHeaderNames.AcceptEncoding, HttpHeaderValues.Deflate);
                        responseHandler.Put(streamId, channel.WriteAsync(request), channel.NewPromise());
                    }

                    channel.Flush();
                    await responseHandler.AwaitResponses(TimeSpan.FromSeconds(5));
                    Console.WriteLine("Finished HTTP/2 request(s)");
                    Console.ReadKey();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception.ToString());
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
