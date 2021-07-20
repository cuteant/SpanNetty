using System.Net;
using System.Text;
using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Codecs.Base64;
using DotNetty.Codecs.Http;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Xunit;
using HttpVersion = DotNetty.Codecs.Http.HttpVersion;

namespace DotNetty.Handlers.Proxy.Tests
{
    internal sealed class HttpProxyServer : ProxyServer
    {
        internal HttpProxyServer(bool useSsl, TestMode testMode, EndPoint destination)
            : base(useSsl, testMode, destination)
        {
        }

        internal HttpProxyServer(bool useSsl, TestMode testMode, EndPoint destination, string username, string password)
            : base(useSsl, testMode, destination, username, password)
        {
        }

        protected override void Configure(ISocketChannel ch)
        {
            var p = ch.Pipeline;
            switch (TestMode)
            {
                case TestMode.Intermediary:
                    p.AddLast(new HttpServerCodec());
                    p.AddLast(new HttpObjectAggregator(1));
                    p.AddLast(new HttpIntermediaryHandler(this));
                    break;
                case TestMode.Terminal:
                    p.AddLast(new HttpServerCodec());
                    p.AddLast(new HttpObjectAggregator(1));
                    p.AddLast(new HttpTerminalHandler(this));
                    break;
                case TestMode.Unresponsive:
                    p.AddLast(UnresponsiveHandler.Instance);
                    break;
            }
        }

        bool Authenticate(IChannelHandlerContext ctx, IFullHttpRequest req)
        {
            Assert.Equal(req.Method, HttpMethod.Connect);

            if (TestMode != TestMode.Intermediary)
                ctx.Pipeline.AddBefore(ctx.Name, "lineDecoder", new LineBasedFrameDecoder(64, false, true));

            ctx.Pipeline.Remove<HttpObjectAggregator>();
            ctx.Pipeline.Get<HttpServerCodec>().RemoveInboundHandler();

            var authzSuccess = false;
            if (Username != null)
            {
                if (req.Headers.TryGet(HttpHeaderNames.ProxyAuthorization, out var authz))
                {
                    var authzParts = authz.ToString().Split(' ');
                    var authzBuf64 = Unpooled.CopiedBuffer(authzParts[1], Encoding.ASCII);
                    var authzBuf = Base64.Decode(authzBuf64);

                    var expectedAuthz = Username + ':' + Password;
                    authzSuccess = "Basic".Equals(authzParts[0]) &&
                                   expectedAuthz.Equals(authzBuf.ToString(Encoding.ASCII));

                    authzBuf64.Release();
                    authzBuf.Release();
                }
            }
            else
            {
                authzSuccess = true;
            }

            return authzSuccess;
        }

        private sealed class HttpIntermediaryHandler : IntermediaryHandler
        {
            private readonly HttpProxyServer _server;

            public HttpIntermediaryHandler(HttpProxyServer server)
                : base(server)
            {
                _server = server;
            }

            protected override EndPoint IntermediaryDestination { get; set; }

            protected override bool HandleProxyProtocol(IChannelHandlerContext ctx, object msg)
            {
                var req = (IFullHttpRequest) msg;
                IFullHttpResponse res;
                if (!_server.Authenticate(ctx, req))
                {
                    res = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.Unauthorized);
                    res.Headers.Set(HttpHeaderNames.ContentLength, 0);
                }
                else
                {
                    res = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
                    var uri = req.Uri;
                    var lastColonPos = uri.LastIndexOf(':');
                    Assert.True(lastColonPos > 0);
                    IntermediaryDestination = new DnsEndPoint(uri.Substring(0, lastColonPos),
                        int.Parse(uri.Substring(lastColonPos + 1)));
                }

                ctx.WriteAsync(res);
                ctx.Pipeline.Get<HttpServerCodec>().RemoveOutboundHandler();
                return true;
            }
        }

        private sealed class HttpTerminalHandler : TerminalHandler
        {
            private readonly HttpProxyServer _server;

            public HttpTerminalHandler(HttpProxyServer server)
                : base(server)
            {
                _server = server;
            }

            protected override bool HandleProxyProtocol(IChannelHandlerContext ctx, object msg)
            {
                var req = (IFullHttpRequest) msg;
                IFullHttpResponse res;
                var sendGreeting = false;

                if (!_server.Authenticate(ctx, req))
                {
                    res = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.Unauthorized);
                    res.Headers.Set(HttpHeaderNames.ContentLength, 0);
                }
                else if (!req.Uri.Equals(((DnsEndPoint) _server.Destination).Host + ':' +
                                         ((DnsEndPoint) _server.Destination).Port))
                {
                    res = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.Forbidden);
                    res.Headers.Set(HttpHeaderNames.ContentLength, 0);
                }
                else
                {
                    res = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
                    sendGreeting = true;
                }

                ctx.WriteAsync(res);
                ctx.Pipeline.Get<HttpServerCodec>().RemoveOutboundHandler();

                if (sendGreeting) ctx.WriteAsync(Unpooled.CopiedBuffer("0\n", Encoding.ASCII));

                return true;
            }
        }
    }
}