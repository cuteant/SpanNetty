#if NETCOREAPP_2_0_GREATER
namespace Http2Helloworld.Server
{
    using DotNetty.Codecs.Http;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Channels;
    using System;
    using System.Net.Security;

    public class Http2OrHttpHandler : ApplicationProtocolNegotiationHandler
    {
        const int MAX_CONTENT_LENGTH = 1024 * 100;

        public Http2OrHttpHandler()
            : base(SslApplicationProtocol.Http11)
        {
        }

        protected override void ConfigurePipeline(IChannelHandlerContext context, SslApplicationProtocol protocol)
        {
            if (SslApplicationProtocol.Http2.Equals(protocol))
            {
                context.Pipeline.AddLast(new HelloWorldHttp2HandlerBuilder().Build());
                return;
            }

            if (SslApplicationProtocol.Http11.Equals(protocol))
            {
                context.Pipeline.AddLast(new HttpServerCodec(),
                                     new HttpObjectAggregator(MAX_CONTENT_LENGTH),
                                     new HelloWorldHttp1Handler("ALPN Negotiation"));
                return;
            }

            throw new InvalidOperationException($"Unknown protocol: {protocol}");
        }
    }
}
#endif
