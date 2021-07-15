namespace Http2Helloworld.FrameClient
{
    using System.Collections.Generic;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using DotNetty.Codecs.Http2;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Configures client pipeline to support HTTP/2 frames via {@link Http2FrameCodec} and {@link Http2MultiplexHandler}.
    /// </summary>
    public class Http2ClientFrameInitializer : ChannelInitializer<IChannel>
    {
        readonly X509Certificate2 _cert;
        readonly string _targetHost;

        public Http2ClientFrameInitializer(X509Certificate2 cert, string targetHost)
        {
            _cert = cert;
            _targetHost = targetHost;
        }

        protected override void InitChannel(IChannel ch)
        {
            var pipeline = ch.Pipeline;
            if (_cert is object)
            {
                var tlsSettings = new ClientTlsSettings(_targetHost)
                {
                    ApplicationProtocols = new List<SslApplicationProtocol>(new[]
                    {
                        SslApplicationProtocol.Http2,
                        SslApplicationProtocol.Http11
                    })
                }.AllowAnyServerCertificate();
                pipeline.AddLast("tls", new TlsHandler(tlsSettings));
            }
            var build = Http2FrameCodecBuilder.ForClient();
            build.InitialSettings = Http2Settings.DefaultSettings(); // this is the default, but shows it can be changed.
            Http2FrameCodec http2FrameCodec = build.Build();
            pipeline.AddLast(http2FrameCodec);
            pipeline.AddLast(new Http2MultiplexHandler(new SimpleChannelInboundHandler0()));
        }

        sealed class SimpleChannelInboundHandler0 : SimpleChannelInboundHandler<object>
        {
            protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
            {
                // NOOP (this is the handler for 'inbound' streams, which is not relevant in this example)
            }
        }
    }
}
