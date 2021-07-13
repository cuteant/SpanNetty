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
    using DotNetty.Buffers;
    using System.Net;

    /// <summary>
    /// Configures the client pipeline to support HTTP/2 frames.
    /// </summary>
    public class Http2ClientInitializer : ChannelInitializer<IChannel>
    {
        static readonly ILogger s_logger = InternalLoggerFactory.DefaultFactory.CreateLogger<Http2ClientInitializer>();
        static readonly IHttp2FrameLogger Logger = new Http2FrameMsLogger(LogLevel.Information, typeof(Http2ClientInitializer));

        readonly X509Certificate2 _cert;
        readonly string _targetHost;
        readonly int _maxContentLength;

        HttpToHttp2ConnectionHandler _connectionHandler;
        HttpResponseHandler _responseHandler;
        Http2SettingsHandler _settingsHandler;

        public Http2ClientInitializer(X509Certificate2 cert, string targetHost, int maxContentLength)
        {
            _cert = cert;
            _targetHost = targetHost;
            _maxContentLength = maxContentLength;
        }

        protected override void InitChannel(IChannel channel)
        {
            IHttp2Connection connection = new DefaultHttp2Connection(false);
            _connectionHandler = new HttpToHttp2ConnectionHandlerBuilder()
            {
                FrameListener = new DelegatingDecompressorFrameListener(
                    connection,
                    new InboundHttp2ToHttpAdapterBuilder(connection)
                    {
                        MaxContentLength = _maxContentLength,
                        IsPropagateSettings = true
                    }.Build()),
                FrameLogger = Logger,
                Connection = connection
            }.Build();
            _responseHandler = new HttpResponseHandler();
            _settingsHandler = new Http2SettingsHandler(channel.NewPromise());
            if (_cert != null)
            {
                ConfigureSsl(channel);
            }
            else
            {
                ConfigureClearText(channel);
            }
        }

        public HttpResponseHandler ResponseHandler => _responseHandler;

        public Http2SettingsHandler SettingsHandler => _settingsHandler;

        protected void ConfigureEndOfPipeline(IChannelPipeline pipeline)
        {
            pipeline.AddLast(_settingsHandler, _responseHandler);
        }

        /// <summary>
        /// Configure the pipeline for TLS NPN negotiation to HTTP/2.
        /// </summary>
        /// <param name="ch"></param>
        void ConfigureSsl(IChannel ch)
        {
            var pipeline = ch.Pipeline;
            var tlsSettings = new ClientTlsSettings(_targetHost)
            {
                ApplicationProtocols = new List<SslApplicationProtocol>(new[]
                {
                    SslApplicationProtocol.Http2,
                    SslApplicationProtocol.Http11
                })
            }.AllowAnyServerCertificate();
            pipeline.AddLast("tls", new TlsHandler(tlsSettings));

            // We must wait for the handshake to finish and the protocol to be negotiated before configuring
            // the HTTP/2 components of the pipeline.
            pipeline.AddLast(new ClientApplicationProtocolNegotiationHandler(this));
        }

        sealed class ClientApplicationProtocolNegotiationHandler : ApplicationProtocolNegotiationHandler
        {
            readonly Http2ClientInitializer _self;

            public ClientApplicationProtocolNegotiationHandler(Http2ClientInitializer self)
                : base(default(SslApplicationProtocol))
            {
                _self = self;
            }

            protected override void ConfigurePipeline(IChannelHandlerContext ctx, SslApplicationProtocol protocol)
            {
                if (SslApplicationProtocol.Http2.Equals(protocol))
                {
                    var p = ctx.Pipeline;
                    p.AddLast(_self._connectionHandler);
                    _self.ConfigureEndOfPipeline(p);
                    return;
                }
                ctx.CloseAsync();
                throw new InvalidOperationException("unknown protocol: " + protocol);
            }
        }

        /// <summary>
        /// Configure the pipeline for a cleartext upgrade from HTTP to HTTP/2.
        /// </summary>
        /// <param name="ch"></param>
        void ConfigureClearText(IChannel ch)
        {
            HttpClientCodec sourceCodec = new HttpClientCodec();
            Http2ClientUpgradeCodec upgradeCodec = new Http2ClientUpgradeCodec(_connectionHandler);
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
            readonly Http2ClientInitializer _self;

            public UpgradeRequestHandler(Http2ClientInitializer self) => _self = self;

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                DefaultFullHttpRequest upgradeRequest =
                        new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get, "/", Unpooled.Empty);

                // Set HOST header as the remote peer may require it.
                var remote = (IPEndPoint)ctx.Channel.RemoteAddress;
                //String hostString = remote.getHostString();
                //if (hostString == null)
                //{
                //    hostString = remote.getAddress().getHostAddress();
                //}
                upgradeRequest.Headers.Set(HttpHeaderNames.Host, _self._targetHost + ':' + remote.Port);

                ctx.WriteAndFlushAsync(upgradeRequest);

                ctx.FireChannelActive();

                // Done with this handler, remove it from the pipeline.
                ctx.Pipeline.Remove(this);

                _self.ConfigureEndOfPipeline(ctx.Pipeline);
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
