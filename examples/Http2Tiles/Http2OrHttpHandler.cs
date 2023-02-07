#if NETCOREAPP_2_0_GREATER
namespace Http2Tiles
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
                ConfigureHttp2(context);
                return;
            }

            if (SslApplicationProtocol.Http11.Equals(protocol))
            {
                ConfigureHttp1(context);
                return;
            }

            throw new InvalidOperationException($"Unknown protocol: {protocol}");
        }

        private static void ConfigureHttp2(IChannelHandlerContext context)
        {
            var connection = new DefaultHttp2Connection(true);
            InboundHttp2ToHttpAdapter listener = new InboundHttp2ToHttpAdapterBuilder(connection)
            {
                IsPropagateSettings = true,
                IsValidateHttpHeaders = false,
                MaxContentLength = MAX_CONTENT_LENGTH
            }.Build();

            context.Pipeline.AddLast(new HttpToHttp2ConnectionHandlerBuilder()
            {
                FrameListener = listener,
                // FrameLogger = TilesHttp2ToHttpHandler.logger,
                Connection = connection
            }.Build());

            context.Pipeline.AddLast(new Http2RequestHandler());
        }

        private static void ConfigureHttp1(IChannelHandlerContext context)
        {
            context.Pipeline.AddLast(new HttpServerCodec(),
                                 new HttpObjectAggregator(MAX_CONTENT_LENGTH),
                                 new FallbackRequestHandler());
        }
    }
}
#endif
