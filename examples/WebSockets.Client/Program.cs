// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace WebSockets.Client
{
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Codecs.Http.WebSockets.Extensions.Compression;
    using DotNetty.Handlers.Logging;
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
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    class Program
    {
        private const string WEBSOCKET_PATH = "/websocket";

        static async Task Main(string[] args)
        {
            var builder = new UriBuilder
            {
                Scheme = ClientSettings.IsSsl ? "wss" : "ws",
                Host = ClientSettings.Host.ToString(),
                Port = ClientSettings.Port,
            };

            string path = ExampleHelper.Configuration["path"];
            builder.Path = !string.IsNullOrEmpty(path) ? path : WEBSOCKET_PATH;

            Uri uri = builder.Uri;
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

                // Connect with V13 (RFC 6455 aka HyBi-17). You can change it to V08 or V00.
                // If you change it to V00, ping is not supported and remember to change
                // HttpResponseDecoder to WebSocketHttpResponseDecoder in the pipeline.
                WebSocketClientHandler handler =
                    new WebSocketClientHandler(
                            WebSocketClientHandshakerFactory.NewHandshaker(
                                    uri, WebSocketVersion.V13, null, true, new DefaultHttpHeaders()));

                bootstrap.Handler(new ActionChannelInitializer<IChannel>(channel =>
                {
                    IChannelPipeline pipeline = channel.Pipeline;
                    if (cert != null)
                    {
                        pipeline.AddLast("tls", new TlsHandler(new ClientTlsSettings(targetHost).AllowAnyServerCertificate()));
                    }

                    pipeline.AddLast("idleStateHandler", new IdleStateHandler(0, 0, 60));

                    pipeline.AddLast(new LoggingHandler("CONN"));
                    pipeline.AddLast(
                        new HttpClientCodec(),
                        new HttpObjectAggregator(8192),
                        WebSocketClientCompressionHandler.Instance,
                        //new WebSocketClientProtocolHandler(
                        //    webSocketUrl: uri,
                        //    version: WebSocketVersion.V13,
                        //    subprotocol: null,
                        //    allowExtensions: true,
                        //    customHeaders: new DefaultHttpHeaders(),
                        //    maxFramePayloadLength: 65536,
                        //    handleCloseFrames: true,
                        //    performMasking: false,
                        //    allowMaskMismatch: true,
                        //    enableUtf8Validator: false),
                        new WebSocketFrameAggregator(65536),
                        handler);
                }));

                try
                {
                    IChannel channel = await bootstrap.ConnectAsync(new IPEndPoint(ClientSettings.Host, ClientSettings.Port));
                    await handler.HandshakeCompletion;

                    Console.WriteLine("WebSocket handshake completed.\n");
                    Console.WriteLine("\t[bye]:Quit \n\t [ping]:Send ping frame\n\t Enter any text and Enter: Send text frame");

                    while (true)
                    {
                        string msg = Console.ReadLine();
                        if (msg == null)
                        {
                            break;
                        }
                        msg = msg.ToLowerInvariant();

                        switch (msg)
                        {
                            case "bye":
                                await channel.WriteAndFlushAsync(new CloseWebSocketFrame());
                                goto CloseLable;

                            case "ping":
                                var ping = new PingWebSocketFrame(Unpooled.WrappedBuffer(new byte[] { 8, 1, 8, 1 }));
                                await channel.WriteAndFlushAsync(ping);
                                break;

                            case "this is a test":
                                await channel.WriteAndFlushManyAsync(
                                    new TextWebSocketFrame(false, "this "),
                                    new ContinuationWebSocketFrame(false, "is "),
                                    new ContinuationWebSocketFrame(false, "a "),
                                    new ContinuationWebSocketFrame(true, "test")
                                );
                                break;

                            case "this is a error":
                                await channel.WriteAndFlushAsync(new TextWebSocketFrame(false, "this "));
                                await channel.WriteAndFlushAsync(new ContinuationWebSocketFrame(false, "is "));
                                await channel.WriteAndFlushAsync(new ContinuationWebSocketFrame(false, "a "));
                                await channel.WriteAndFlushAsync(new TextWebSocketFrame(true, "error"));
                                break;

                            default:
                                await channel.WriteAndFlushAsync(new TextWebSocketFrame(msg));
                                break;
                        }
                    }
                    CloseLable:
                    await channel.CloseAsync();
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"{exception}");
                    Console.WriteLine("Press any key to exit");
                    Console.ReadKey();
                }
            }
            finally
            {
                await group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
            }
        }
    }
}
