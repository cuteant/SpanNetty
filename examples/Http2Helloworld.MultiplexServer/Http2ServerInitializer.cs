namespace Http2Helloworld.MultiplexServer
{
    using System;
    using System.Collections.Generic;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http2;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Channels;
    using Microsoft.Extensions.Logging;

    public class Http2ServerInitializer : ChannelInitializer<IChannel>
    {
        static readonly ILogger s_logger = InternalLoggerFactory.DefaultFactory.CreateLogger<Http2ServerInitializer>();

        static readonly HttpServerUpgradeCodecFactory UpgradeCodecFactory = new HttpServerUpgradeCodecFactory();

        readonly X509Certificate2 tlsCertificate;
        readonly int maxHttpContentLength;

        public Http2ServerInitializer(X509Certificate2 tlsCertificate)
            : this(tlsCertificate, 16 * 1024)
        {
        }

        public Http2ServerInitializer(X509Certificate2 tlsCertificate, int maxHttpContentLength)
        {
            if (maxHttpContentLength < 0)
            {
                throw new ArgumentException("maxHttpContentLength (expected >= 0): " + maxHttpContentLength);
            }
            this.tlsCertificate = tlsCertificate;
            this.maxHttpContentLength = maxHttpContentLength;
        }

        protected override void InitChannel(IChannel channel)
        {
            if (tlsCertificate != null)
            {
                this.ConfigureSsl(channel);
            }
            else
            {
                this.ConfigureClearText(channel);
            }
        }

        /**
         * Configure the pipeline for TLS NPN negotiation to HTTP/2.
         */
        void ConfigureSsl(IChannel ch)
        {
            var tlsSettings = new ServerTlsSettings(this.tlsCertificate)
            {
                ApplicationProtocols = new List<SslApplicationProtocol>(new[]
                {
                    SslApplicationProtocol.Http2,
                    SslApplicationProtocol.Http11
                })
            };
            //tlsSettings.AllowAnyClientCertificate();
            ch.Pipeline.AddLast(new TlsHandler(tlsSettings));
            ch.Pipeline.AddLast(new Http2OrHttpHandler());
        }

        void ConfigureClearText(IChannel ch)
        {
            IChannelPipeline p = ch.Pipeline;
            HttpServerCodec sourceCodec = new HttpServerCodec();

            p.AddLast(sourceCodec);
            p.AddLast(new HttpServerUpgradeHandler(sourceCodec, UpgradeCodecFactory));
            p.AddLast(new HttpMessageHandler(this.maxHttpContentLength));

            p.AddLast(new UserEventLogger());
        }

        sealed class HttpServerUpgradeCodecFactory : HttpServerUpgradeHandler.IUpgradeCodecFactory
        {
            public HttpServerUpgradeHandler.IUpgradeCodec NewUpgradeCodec(ICharSequence protocol)
            {
                if (AsciiString.ContentEquals(Http2CodecUtil.HttpUpgradeProtocolName, protocol))
                {
                    return new Http2ServerUpgradeCodec(
                        Http2FrameCodecBuilder.ForServer().Build(),
                        new Http2MultiplexHandler(new HelloWorldHttp2Handler()));
                }
                else
                {
                    return null;
                }
            }
        }

        sealed class HttpMessageHandler : SimpleChannelInboundHandler2<IHttpMessage>
        {
            readonly int maxHttpContentLength;

            public HttpMessageHandler(int maxHttpContentLength) => this.maxHttpContentLength = maxHttpContentLength;

            protected override void ChannelRead0(IChannelHandlerContext ctx, IHttpMessage msg)
            {
                // If this handler is hit then no upgrade has been attempted and the client is just talking HTTP.
                s_logger.LogInformation("Directly talking: " + msg.ProtocolVersion + " (no upgrade was attempted)");
                IChannelPipeline pipeline = ctx.Pipeline;
                pipeline.AddAfter(ctx.Name, null, new Http2Helloworld.Server.HelloWorldHttp1Handler("Direct. No Upgrade Attempted."));
                pipeline.Replace(this, null, new HttpObjectAggregator(this.maxHttpContentLength));
                ctx.FireChannelRead(ReferenceCountUtil.Retain(msg));
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
