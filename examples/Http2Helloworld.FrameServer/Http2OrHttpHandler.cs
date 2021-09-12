#if NETCOREAPP_2_0_GREATER
namespace Http2Helloworld.FrameServer
{
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http2;
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
                context.Pipeline.AddLast(Http2FrameCodecBuilder.ForServer().Build(), new HelloWorldHttp2Handler());
                return;
            }

            if (SslApplicationProtocol.Http11.Equals(protocol))
            {
                context.Pipeline.AddLast(new HttpServerCodec(),
                                     new HttpObjectAggregator(MAX_CONTENT_LENGTH),
                                     new Http2Helloworld.Server.HelloWorldHttp1Handler("ALPN Negotiation"));
                return;
            }

            throw new InvalidOperationException($"unknown protocol: {protocol}");
        }
    }
}
#endif
