using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DotNetty.Codecs.Http;
using DotNetty.Common.Concurrency;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Embedded;
using DotNetty.Transport.Channels.Local;
using Moq;
using Xunit;
using HttpVersion = DotNetty.Codecs.Http.HttpVersion;

namespace DotNetty.Handlers.Proxy.Tests
{
    /*
    public class HttpProxyHandlerTest
    {
        [Fact(Timeout = 5000)]
        public async Task TestHostname()
        {
            EndPoint socketAddress = new DnsEndPoint("localhost", 8080);
            TestInitialMessage(
                socketAddress,
                "localhost:8080",
                "localhost:8080",
                null,
                true);
        }

        [Fact(Timeout = 5000)]
        public async Task TestHostnameUnresolved()
        {
            EndPoint socketAddress = new DnsEndPoint("localhost", 8080);
            TestInitialMessage(
                socketAddress,
                "localhost:8080",
                "localhost:8080",
                null,
                true);
        }

        [Fact(Timeout = 5000)]
        public async Task TestHostHeaderWithHttpDefaultPort()
        {
            EndPoint socketAddress = new DnsEndPoint("localhost", 80);
            TestInitialMessage(socketAddress,
                "localhost:80",
                "localhost:80", null,
                false);
        }

        [Fact(Timeout = 5000)]
        public async Task TestHostHeaderWithHttpDefaultPortIgnored()
        {
            EndPoint socketAddress = new DnsEndPoint("localhost", 80);
            TestInitialMessage(
                socketAddress,
                "localhost:80",
                "localhost",
                null,
                true);
        }

        [Fact(Timeout = 5000)]
        public async Task TestHostHeaderWithHttpsDefaultPort()
        {
            EndPoint socketAddress = new DnsEndPoint("localhost", 443);
            TestInitialMessage(
                socketAddress,
                "localhost:443",
                "localhost:443",
                null,
                false);
        }

        [Fact(Timeout = 5000)]
        public async Task TestHostHeaderWithHttpsDefaultPortIgnored()
        {
            EndPoint socketAddress = new DnsEndPoint("localhost", 443);
            TestInitialMessage(
                socketAddress,
                "localhost:443",
                "localhost",
                null,
                true);
        }

        [Fact(Timeout = 5000)]
        public async Task TestIpv6()
        {
            EndPoint socketAddress = new IPEndPoint(IPAddress.Parse("::1"), 8080);
            TestInitialMessage(
                socketAddress,
                "[::1]:8080",
                "[::1]:8080",
                null,
                true);
        }

        [Fact(Timeout = 5000)]
        public async Task TestIpv6Unresolved()
        {
            EndPoint socketAddress = new DnsEndPoint("foo.bar", 8080);
            TestInitialMessage(
                socketAddress,
                "foo.bar:8080",
                "foo.bar:8080",
                null,
                true);
        }

        [Fact(Timeout = 5000)]
        public async Task TestIpv4()
        {
            EndPoint socketAddress = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 8080);
            TestInitialMessage(socketAddress,
                "10.0.0.1:8080",
                "10.0.0.1:8080",
                null,
                true);
        }

        [Fact(Timeout = 5000)]
        public async Task TestIpv4Unresolved()
        {
            EndPoint socketAddress = new DnsEndPoint("10.0.0.1", 8080);
            TestInitialMessage(
                socketAddress,
                "10.0.0.1:8080",
                "10.0.0.1:8080",
                null,
                true);
        }

        [Fact(Timeout = 5000)]
        public async Task TestCustomHeaders()
        {
            EndPoint socketAddress = new DnsEndPoint("10.0.0.1", 8080);
            TestInitialMessage(
                socketAddress,
                "10.0.0.1:8080",
                "10.0.0.1:8080",
                new DefaultHttpHeaders()
                    .Add(AsciiString.Of("CUSTOM_HEADER"), "CUSTOM_VALUE1")
                    .Add(AsciiString.Of("CUSTOM_HEADER"), "CUSTOM_VALUE2"),
                true);
        }

        [Fact(Timeout = 5000)]
        public async Task TestExceptionDuringConnect()
        {
            IEventLoopGroup group = null;
            IChannel serverChannel = null;
            IChannel clientChannel = null;
            try
            {
                group = new DefaultEventLoopGroup(1);
                var addr = new LocalAddress("a");
                var exception = new AtomicReference<Exception>();
                var sf =
                    new ServerBootstrap().Channel<LocalServerChannel>().Group(group).ChildHandler(
                        new ActionChannelInitializer<IChannel>(ch =>
                            {
                                ch.Pipeline.AddFirst(new HttpResponseEncoder());
                                var response = new DefaultFullHttpResponse(
                                    HttpVersion.Http11,
                                    HttpResponseStatus.BadGateway);
                                response.Headers.Add(AsciiString.Of("name"), "value");
                                response.Headers.Add(HttpHeaderNames.ContentLength, "0");
                                ch.WriteAndFlushAsync(response);
                            }
                        )).BindAsync(addr);
                serverChannel = sf.Result;

                var cf = new Bootstrap().Channel<LocalChannel>().Group(group).Handler(
                    new ActionChannelInitializer<IChannel>(ch =>
                    {
                        ch.Pipeline.AddFirst(new HttpProxyHandler(addr));
                        ch.Pipeline.AddLast(new ErrorCaptureHandler(exception));
                    })).ConnectAsync(new DnsEndPoint("localhost", 1234));

                clientChannel = cf.Result;
                clientChannel.CloseAsync().Wait();

                Assert.True(exception.Value is HttpProxyConnectException);
                var actual = (HttpProxyConnectException) exception.Value;
                Assert.NotNull(actual.Headers);
                Assert.Equal("value", actual.Headers.GetAsString(AsciiString.Of("name")));
            }
            finally
            {
                if (clientChannel != null) clientChannel.CloseAsync();
                if (serverChannel != null) serverChannel.CloseAsync();
                if (group != null) @group.ShutdownGracefullyAsync().Wait();
            }
        }

        private static void TestInitialMessage(EndPoint socketAddress,
            string expectedUrl,
            string expectedHostHeader,
            HttpHeaders headers,
            bool ignoreDefaultPortsInConnectHostHeader)
        {
            EndPoint proxyAddress = new IPEndPoint(IPAddress.Loopback, 8080);

            var promise = new TaskCompletionSource<object>();
            
            var channel = new Mock<IChannel>();
            
            var pipeline = new Mock<IChannelPipeline>();
            channel.Setup(c => c.Pipeline).Returns(pipeline.Object);
            
            var config = new Mock<IChannelConfiguration>();
            channel.SetupGet(c => c.Configuration).Returns(config.Object);
            
            var ctx = new Mock<IChannelHandlerContext>();
            ctx.SetupGet(c => c.Channel).Returns(channel.Object);
            var executor = new Mock<IEventExecutor>();
            ctx.Setup(c => c.Executor).Returns(executor.Object);
            ctx.Setup(c => c.ConnectAsync(proxyAddress, null)).Returns(promise.Task);

            var handler = new HttpProxyHandler(
                new IPEndPoint(IPAddress.Loopback, 8080),
                headers,
                ignoreDefaultPortsInConnectHostHeader);

            handler.HandlerAdded(ctx.Object);
            
            handler.ConnectAsync(ctx.Object, socketAddress, null);
            ctx.Verify(c => c.ConnectAsync(proxyAddress, null), Times.Once);

            handler.ChannelActive(ctx.Object);
            ctx.Verify(c => c.WriteAndFlushAsync(It.Is<IFullHttpRequest>(request =>
                request.ProtocolVersion.Equals(HttpVersion.Http11)
                && request.Uri == expectedUrl
                && request.Headers.GetAsString(HttpHeaderNames.Host) == expectedHostHeader
                && (headers == null || headers.Names().All(name => string.Join(",", headers.GetAllAsString(name)).Equals(string.Join(",",request.Headers.GetAllAsString(name))))))
            ));
        }

        [Fact(Timeout = 5000)]
        public async Task TestHttpClientCodecIsInvisible()
        {
            EmbeddedChannel channel =
                new InactiveEmbeddedChannel(new HttpProxyHandler(new IPEndPoint(IPAddress.Loopback, 8080)));
            Assert.NotNull(channel.Pipeline.Get<HttpProxyHandler>());
            Assert.Null(channel.Pipeline.Get<HttpClientCodec>());
        }

        class ErrorCaptureHandler : ChannelHandlerAdapter
        {
            private readonly AtomicReference<Exception> _exception;

            public ErrorCaptureHandler(AtomicReference<Exception> exception)
            {
                _exception = exception;
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                _exception.Value = exception;
            }
        }

        private class InactiveEmbeddedChannel : EmbeddedChannel
        {
            public InactiveEmbeddedChannel(params IChannelHandler[] handlers)
                : base(handlers)
            {
            }

            public override bool IsActive => false;
        }
    }*/
}