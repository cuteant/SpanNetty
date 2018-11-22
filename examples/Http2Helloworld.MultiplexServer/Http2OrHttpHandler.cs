// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP_2_0_GREATER
namespace Http2Helloworld.MultiplexServer
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
                ctx.Pipeline.AddLast(Http2MultiplexCodecBuilder.ForServer(new HelloWorldHttp2Handler()).Build());
                return;
            }

            if (SslApplicationProtocol.Http11.Equals(protocol))
            {
                ctx.Pipeline.AddLast(new HttpServerCodec(),
                                     new HttpObjectAggregator(MAX_CONTENT_LENGTH),
                                     new Http2Helloworld.Server.HelloWorldHttp1Handler("ALPN Negotiation"));
                return;
            }

            throw new InvalidOperationException("unknown protocol: " + protocol);
        }
    }
}
#endif
