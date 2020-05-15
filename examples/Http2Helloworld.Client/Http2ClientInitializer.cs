// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Http2Helloworld.Client
{
    using System;
    using System.Collections.Generic;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http2;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Channels;
    using DotNetty.Common.Internal.Logging;
    using Microsoft.Extensions.Logging;

    public class Http2ClientInitializer : ChannelInitializer<IChannel>
    {
        static readonly ILogger s_logger = InternalLoggerFactory.DefaultFactory.CreateLogger<Http2ClientInitializer>();
        static readonly IHttp2FrameLogger Logger = new Http2FrameMsLogger(LogLevel.Information, typeof(Http2ClientInitializer));

        readonly X509Certificate2 cert;
        readonly string targetHost;
        readonly int maxContentLength;

        HttpToHttp2ConnectionHandler connectionHandler;
        HttpResponseHandler responseHandler;
        Http2SettingsHandler settingsHandler;

        public Http2ClientInitializer(X509Certificate2 cert, string targetHost, int maxContentLength)
        {
            this.cert = cert;
            this.targetHost = targetHost;
            this.maxContentLength = maxContentLength;
        }

        protected override void InitChannel(IChannel channel)
        {
            IHttp2Connection connection = new DefaultHttp2Connection(false);
            this.connectionHandler = new HttpToHttp2ConnectionHandlerBuilder()
            {
                FrameListener = new DelegatingDecompressorFrameListener(
                    connection,
                    new InboundHttp2ToHttpAdapterBuilder(connection)
                    {
                        MaxContentLength = this.maxContentLength,
                        IsPropagateSettings = true
                    }.Build()),
                FrameLogger = Logger,
                Connection = connection
            }.Build();
            this.responseHandler = new HttpResponseHandler();
            this.settingsHandler = new Http2SettingsHandler(channel.NewPromise());
            if (this.cert != null)
            {
                this.ConfigureSsl(channel);
            }
            else
            {
                this.ConfigureClearText(channel);
            }
        }

        public HttpResponseHandler ResponseHandler => this.responseHandler;

        public Http2SettingsHandler SettingsHandler => this.settingsHandler;

        protected void ConfigureEndOfPipeline(IChannelPipeline pipeline)
        {
            pipeline.AddLast(this.settingsHandler, this.responseHandler);
        }

        /// <summary>
        /// Configure the pipeline for TLS NPN negotiation to HTTP/2.
        /// </summary>
        /// <param name="ch"></param>
        void ConfigureSsl(IChannel ch)
        {
            var pipeline = ch.Pipeline;
            pipeline.AddLast("tls", new TlsHandler(
                stream => new SslStream(stream, true, (sender, certificate, chain, errors) => true),
                new ClientTlsSettings(this.targetHost)
#if NETCOREAPP_2_0_GREATER
                {
                    ApplicationProtocols = new List<SslApplicationProtocol>(new[]
                    {
                        SslApplicationProtocol.Http2,
                        SslApplicationProtocol.Http11
                    })
                }
#endif
                ));

            // We must wait for the handshake to finish and the protocol to be negotiated before configuring
            // the HTTP/2 components of the pipeline.
#if NETCOREAPP_2_0_GREATER
            pipeline.AddLast(new ClientApplicationProtocolNegotiationHandler(this));
#else
            this.ConfigureClearText(ch);
#endif
        }

#if NETCOREAPP_2_0_GREATER
        sealed class ClientApplicationProtocolNegotiationHandler : ApplicationProtocolNegotiationHandler
        {
            readonly Http2ClientInitializer self;

            public ClientApplicationProtocolNegotiationHandler(Http2ClientInitializer self)
                : base(default(SslApplicationProtocol))
            {
                this.self = self;
            }

            protected override void ConfigurePipeline(IChannelHandlerContext ctx, SslApplicationProtocol protocol)
            {
                if (SslApplicationProtocol.Http2.Equals(protocol))
                {
                    var p = ctx.Pipeline;
                    p.AddLast(this.self.connectionHandler);
                    this.self.ConfigureEndOfPipeline(p);
                    return;
                }
                ctx.CloseAsync();
                throw new InvalidOperationException("unknown protocol: " + protocol);
            }
        }
#endif

        /// <summary>
        /// Configure the pipeline for a cleartext upgrade from HTTP to HTTP/2.
        /// </summary>
        /// <param name="ch"></param>
        void ConfigureClearText(IChannel ch)
        {
            HttpClientCodec sourceCodec = new HttpClientCodec();
            Http2ClientUpgradeCodec upgradeCodec = new Http2ClientUpgradeCodec(connectionHandler);
            HttpClientUpgradeHandler upgradeHandler = new HttpClientUpgradeHandler(sourceCodec, upgradeCodec, 65536);

            ch.Pipeline.AddLast(sourceCodec,
                                upgradeHandler,
                                new UpgradeRequestHandler(this),
                                new UserEventLogger());
        }

        /// <summary>
        /// A handler that triggers the cleartext upgrade to HTTP/2 by sending an initial HTTP request.
        /// </summary>
        sealed class UpgradeRequestHandler : ChannelHandlerAdapter
        {
            readonly Http2ClientInitializer self;

            public UpgradeRequestHandler(Http2ClientInitializer self) => this.self = self;

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                DefaultFullHttpRequest upgradeRequest =
                        new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/");
                ctx.WriteAndFlushAsync(upgradeRequest);

                ctx.FireChannelActive();

                // Done with this handler, remove it from the pipeline.
                ctx.Pipeline.Remove(this);

                this.self.ConfigureEndOfPipeline(ctx.Pipeline);
            }
        }

        /// <summary>
        /// Class that logs any User Events triggered on this channel.
        /// </summary>
        sealed class UserEventLogger : ChannelHandlerAdapter
        {
            public override void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
                s_logger.LogInformation("User Event Triggered: " + evt);
                context.FireUserEventTriggered(evt);
            }
        }
    }
}
