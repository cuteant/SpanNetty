// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Echo.Client
{
    using DotNetty.Codecs;
    using DotNetty.Handlers.Logging;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Examples.Common;
    using System;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    class Program
    {
        static async Task Main(string[] args)
        {
            ExampleHelper.SetConsoleLogger();

            var group = new MultithreadEventLoopGroup();

            X509Certificate2 cert = null;
            string targetHost = null;
            if (ClientSettings.IsSsl)
            {
                cert = new X509Certificate2("dotnetty.com.pfx", "password");
                targetHost = cert.GetNameInfo(X509NameType.DnsName, false);
            }

            try
            {
                var bootstrap = new Bootstrap();
                bootstrap
                    .Group(group)
                    .Channel<TcpSocketChannel>()
                    .Option(ChannelOption.TcpNodelay, true)
                    .Handler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;

                        if (cert != null)
                        {
                            pipeline.AddLast("tls", new TlsHandler(stream => new SslStream(stream, true, (sender, certificate, chain, errors) => true), new ClientTlsSettings(targetHost)));
                        }
                        pipeline.AddLast(new LoggingHandler());
                        pipeline.AddLast("framing-enc", new LengthFieldPrepender2(2));
                        pipeline.AddLast("framing-dec", new LengthFieldBasedFrameDecoder2(ushort.MaxValue, 0, 2, 0, 2));

                        pipeline.AddLast("echo", new EchoClientHandler());
                    }));

                IChannel clientChannel = await bootstrap.ConnectAsync(new IPEndPoint(ClientSettings.Host, ClientSettings.Port));

                Console.ReadLine();

                await clientChannel.CloseAsync();
            }
            finally
            {
                await group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
            }
        }
    }
}
