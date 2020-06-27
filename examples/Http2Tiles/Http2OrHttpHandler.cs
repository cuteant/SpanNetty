#if NETCOREAPP_2_0_GREATER
namespace Http2Tiles
{
    using System;
    using System.Net.Security;
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http2;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Channels;

    public class Http2OrHttpHandler : ApplicationProtocolNegotiationHandler
    {
        const int MAX_CONTENT_LENGTH = 1024 * 100;

        public Http2OrHttpHandler()
            : base(SslApplicationProtocol.Http11)
        {
        }

        protected override void ConfigurePipeline(IChannelHandlerContext ctx, SslApplicationProtocol protocol)
        {
            if (SslApplicationProtocol.Http2.Equals(protocol))
            {
                ConfigureHttp2(ctx);
                return;
            }

            if (SslApplicationProtocol.Http11.Equals(protocol))
            {
                ConfigureHttp1(ctx);
                return;
            }

            throw new InvalidOperationException("unknown protocol: " + protocol);
        }

        private static void ConfigureHttp2(IChannelHandlerContext ctx)
        {
            var connection = new DefaultHttp2Connection(true);
            InboundHttp2ToHttpAdapter listener = new InboundHttp2ToHttpAdapterBuilder(connection)
            {
                IsPropagateSettings = true,
                IsValidateHttpHeaders = false,
                MaxContentLength = MAX_CONTENT_LENGTH
            }.Build();

            ctx.Pipeline.AddLast(new HttpToHttp2ConnectionHandlerBuilder()
            {
                FrameListener = listener,
                // FrameLogger = TilesHttp2ToHttpHandler.logger,
                Connection = connection
            }.Build());

            ctx.Pipeline.AddLast(new Http2RequestHandler());
        }

        private static void ConfigureHttp1(IChannelHandlerContext ctx)
        {
            ctx.Pipeline.AddLast(new HttpServerCodec(),
                                 new HttpObjectAggregator(MAX_CONTENT_LENGTH),
                                 new FallbackRequestHandler());
        }
    }
}
#endif
