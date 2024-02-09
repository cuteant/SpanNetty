
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Handlers.Tls;
    using DotNetty.Tests.Common;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Local;
    using DotNetty.Transport.Channels.Sockets;
    using DotNetty.Transport.Libuv;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    public sealed class LibuvHttpToHttp2ConnectionHandlerTest : AbstractHttpToHttp2ConnectionHandlerTest
    {
        static LibuvHttpToHttp2ConnectionHandlerTest()
        {
            DotNetty.Common.ResourceLeakDetector.Level = Common.ResourceLeakDetector.DetectionLevel.Disabled;
        }

        public LibuvHttpToHttp2ConnectionHandlerTest(ITestOutputHelper output) : base(output) { }

        protected override void SetupServerBootstrap(ServerBootstrap bootstrap)
        {
            var dispatcher = new DispatcherEventLoopGroup();
            var bossGroup = dispatcher;
            var workGroup = new WorkerEventLoopGroup(dispatcher);
            bootstrap.Group(bossGroup, workGroup)
                     .Channel<TcpServerChannel>();
            //bootstrap.Handler(new DotNetty.Handlers.Logging.LoggingHandler("LSTN"));
        }

        protected override void SetupBootstrap(Bootstrap bootstrap)
        {
            bootstrap.Group(new EventLoopGroup()).Channel<TcpChannel>();
        }
    }

    public sealed class SocketHttpToHttp2ConnectionHandlerTest : AbstractHttpToHttp2ConnectionHandlerTest
    {
        public SocketHttpToHttp2ConnectionHandlerTest(ITestOutputHelper output) : base(output) { }

        protected override void SetupServerBootstrap(ServerBootstrap bootstrap)
        {
            bootstrap.Group(new MultithreadEventLoopGroup(1), new MultithreadEventLoopGroup())
                     .Channel<TcpServerSocketChannel>();
        }

        protected override void SetupBootstrap(Bootstrap bootstrap)
        {
            bootstrap.Group(new MultithreadEventLoopGroup()).Channel<TcpSocketChannel>();
        }
    }

    public sealed class LocalHttpToHttp2ConnectionHandlerTest : AbstractHttpToHttp2ConnectionHandlerTest
    {
        public LocalHttpToHttp2ConnectionHandlerTest(ITestOutputHelper output) : base(output) { }

        protected override void SetupServerBootstrap(ServerBootstrap bootstrap)
        {
            bootstrap.Group(new DefaultEventLoopGroup(1), new DefaultEventLoopGroup())
                     .Channel<LocalServerChannel>();
        }

        protected override void SetupBootstrap(Bootstrap bootstrap)
        {
            bootstrap.Group(new DefaultEventLoopGroup()).Channel<LocalChannel>();
        }

        protected override void StartBootstrap()
        {
            this.serverChannel = this.sb.BindAsync(new LocalAddress("HttpToHttp2ConnectionHandlerTest")).GetAwaiter().GetResult();

            var ccf = this.cb.ConnectAsync(this.serverChannel.LocalAddress);
            this.clientChannel = ccf.GetAwaiter().GetResult();
        }
    }

    /**
     * Testing the {@link HttpToHttp2ConnectionHandler} for {@link IFullHttpRequest} objects into HTTP/2 frames
     */
    [Collection("BootstrapEnv")]
    public abstract class AbstractHttpToHttp2ConnectionHandlerTest : TestBase, IDisposable
    {
        private const int WAIT_TIME_SECONDS = 5;

        private Mock<IHttp2FrameListener> clientListener;
        private Mock<IHttp2FrameListener> serverListener;

        protected ServerBootstrap sb;
        protected Bootstrap cb;
        protected IChannel serverChannel;
        private volatile IChannel serverConnectedChannel;
        protected IChannel clientChannel;
        private CountdownEvent requestLatch;
        private CountdownEvent serverSettingsAckLatch;
        private CountdownEvent trailersLatch;
        private Http2TestUtil.FrameCountDown serverFrameCountDown;

        public AbstractHttpToHttp2ConnectionHandlerTest(ITestOutputHelper output)
            : base(output)
        {
            this.clientListener = new Mock<IHttp2FrameListener>();
            this.serverListener = new Mock<IHttp2FrameListener>();
        }

        public void Dispose()
        {
            if (this.clientChannel != null)
            {
                this.clientChannel.CloseAsync().GetAwaiter().GetResult();
                this.clientChannel = null;
            }
            if (this.serverChannel != null)
            {
                this.serverChannel.CloseAsync().GetAwaiter().GetResult();
                this.serverChannel = null;
            }
            var serverConnectedChannel = this.serverConnectedChannel;
            if (serverConnectedChannel != null && serverConnectedChannel.IsActive)
            {
                serverConnectedChannel.CloseAsync().GetAwaiter().GetResult();
                this.serverConnectedChannel = null;
            }
            try
            {
                Task.WaitAll(
                    this.sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    this.sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    this.cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
            catch
            {
                // Ignore RejectedExecutionException(on Azure DevOps)
            }
        }

        [Fact]
        public void HeadersOnlyRequest()
        {
            this.BootstrapEnv(2, 1, 0);
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get,
                "http://my-user_name@www.example.org:5555/example");
            HttpHeaders httpHeaders = request.Headers;
            httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 5);
            httpHeaders.Set(HttpHeaderNames.Host, "my-user_name@www.example.org:5555");
            httpHeaders.Set(HttpConversionUtil.ExtensionHeaderNames.Scheme, "http");
            httpHeaders.Add(AsciiString.Of("foo"), AsciiString.Of("goo"));
            httpHeaders.Add(AsciiString.Of("foo"), AsciiString.Of("goo2"));
            httpHeaders.Add(AsciiString.Of("foo2"), AsciiString.Of("goo2"));
            var http2Headers = new DefaultHttp2Headers()
            {
                Method = new AsciiString("GET"),
                Path = new AsciiString("/example"),
                Authority = new AsciiString("www.example.org:5555"),
                Scheme = new AsciiString("http")
            };
            http2Headers.Add(new AsciiString("foo"), new AsciiString("goo"));
            http2Headers.Add(new AsciiString("foo"), new AsciiString("goo2"));
            http2Headers.Add(new AsciiString("foo2"), new AsciiString("goo2"));

            var writePromise = this.NewPromise();
            this.VerifyHeadersOnly(http2Headers, writePromise, this.clientChannel.WriteAndFlushAsync(request, writePromise));
        }

        [Fact]
        public void MultipleCookieEntriesAreCombined()
        {
            this.BootstrapEnv(2, 1, 0);
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get,
                "http://my-user_name@www.example.org:5555/example");
            HttpHeaders httpHeaders = request.Headers;
            httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 5);
            httpHeaders.Set(HttpHeaderNames.Host, "my-user_name@www.example.org:5555");
            httpHeaders.Set(HttpConversionUtil.ExtensionHeaderNames.Scheme, "http");
            httpHeaders.Set(HttpHeaderNames.Cookie, "a=b; c=d; e=f");
            var http2Headers = new DefaultHttp2Headers()
            {
                Method = new AsciiString("GET"),
                Path = new AsciiString("/example"),
                Authority = new AsciiString("www.example.org:5555"),
                Scheme = new AsciiString("http")
            };
            http2Headers.Add(HttpHeaderNames.Cookie, new AsciiString("a=b"));
            http2Headers.Add(HttpHeaderNames.Cookie, new AsciiString("c=d"));
            http2Headers.Add(HttpHeaderNames.Cookie, new AsciiString("e=f"));

            var writePromise = this.NewPromise();
            this.VerifyHeadersOnly(http2Headers, writePromise, this.clientChannel.WriteAndFlushAsync(request, writePromise));
        }



        [Fact]
        public void OriginFormRequestTargetHandled()
        {
            this.BootstrapEnv(2, 1, 0);
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get, "/where?q=now&f=then#section1");
            HttpHeaders httpHeaders = request.Headers;
            httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 5);
            httpHeaders.Set(HttpConversionUtil.ExtensionHeaderNames.Scheme, "http");
            var http2Headers = new DefaultHttp2Headers()
            {
                Method = new AsciiString("GET"),
                Path = new AsciiString("/where?q=now&f=then#section1"),
                Scheme = new AsciiString("http")
            };

            var writePromise = this.NewPromise();
            this.VerifyHeadersOnly(http2Headers, writePromise, this.clientChannel.WriteAndFlushAsync(request, writePromise));
        }

        [Fact]
        public void OriginFormRequestTargetHandledFromUrlencodedUri()
        {
            this.BootstrapEnv(2, 1, 0);
            IFullHttpRequest request = new DefaultFullHttpRequest(
                   DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get, "/where%2B0?q=now%2B0&f=then%2B0#section1%2B0");
            HttpHeaders httpHeaders = request.Headers;
            httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 5);
            httpHeaders.Set(HttpConversionUtil.ExtensionHeaderNames.Scheme, "http");
            var http2Headers = new DefaultHttp2Headers()
            {
                Method = new AsciiString("GET"),
                Path = new AsciiString("/where%2B0?q=now%2B0&f=then%2B0#section1%2B0"),
                Scheme = new AsciiString("http")
            };

            var writePromise = this.NewPromise();
            this.VerifyHeadersOnly(http2Headers, writePromise, this.clientChannel.WriteAndFlushAsync(request, writePromise));
        }

        [Fact]
        public void AbsoluteFormRequestTargetHandledFromHeaders()
        {
            this.BootstrapEnv(2, 1, 0);
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get, "/pub/WWW/TheProject.html");
            HttpHeaders httpHeaders = request.Headers;
            httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 5);
            httpHeaders.Set(HttpHeaderNames.Host, "foouser@www.example.org:5555");
            httpHeaders.Set(HttpConversionUtil.ExtensionHeaderNames.Path, "ignored_path");
            httpHeaders.Set(HttpConversionUtil.ExtensionHeaderNames.Scheme, "https");
            var http2Headers = new DefaultHttp2Headers()
            {
                Method = new AsciiString("GET"),
                Path = new AsciiString("/pub/WWW/TheProject.html"),
                Authority = new AsciiString("www.example.org:5555"),
                Scheme = new AsciiString("https")
            };

            var writePromise = this.NewPromise();
            this.VerifyHeadersOnly(http2Headers, writePromise, this.clientChannel.WriteAndFlushAsync(request, writePromise));
        }

        [Fact]
        public void AbsoluteFormRequestTargetHandledFromRequestTargetUri()
        {
            this.BootstrapEnv(2, 1, 0);
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get,
                   "http://foouser@www.example.org:5555/pub/WWW/TheProject.html");
            HttpHeaders httpHeaders = request.Headers;
            httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 5);
            var http2Headers = new DefaultHttp2Headers()
            {
                Method = new AsciiString("GET"),
                Path = new AsciiString("/pub/WWW/TheProject.html"),
                Authority = new AsciiString("www.example.org:5555"),
                Scheme = new AsciiString("http")
            };

            var writePromise = this.NewPromise();
            this.VerifyHeadersOnly(http2Headers, writePromise, this.clientChannel.WriteAndFlushAsync(request, writePromise));
        }

        [Fact]
        public void AuthorityFormRequestTargetHandled()
        {
            this.BootstrapEnv(2, 1, 0);
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Connect, "http://www.example.com:80");
            HttpHeaders httpHeaders = request.Headers;
            httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 5);
            var http2Headers = new DefaultHttp2Headers()
            {
                Method = new AsciiString("CONNECT"),
                Path = new AsciiString("/"),
                //Authority = new AsciiString("www.example.com:80"), // Uri 忽略默认端口 80
                Authority = new AsciiString("www.example.com"),
                Scheme = new AsciiString("http")
            };

            var writePromise = this.NewPromise();
            this.VerifyHeadersOnly(http2Headers, writePromise, this.clientChannel.WriteAndFlushAsync(request, writePromise));
        }

        [Fact]
        public void AsterikFormRequestTargetHandled()
        {
            this.BootstrapEnv(2, 1, 0);
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Options, "*");
            HttpHeaders httpHeaders = request.Headers;
            httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 5);
            httpHeaders.Set(HttpHeaderNames.Host, "www.example.com:80");
            httpHeaders.Set(HttpConversionUtil.ExtensionHeaderNames.Scheme, "http");
            var http2Headers = new DefaultHttp2Headers()
            {
                Method = new AsciiString("OPTIONS"),
                Path = new AsciiString("*"),
                Authority = new AsciiString("www.example.com:80"),
                Scheme = new AsciiString("http")
            };

            var writePromise = this.NewPromise();
            this.VerifyHeadersOnly(http2Headers, writePromise, this.clientChannel.WriteAndFlushAsync(request, writePromise));
        }

        [Fact]
        public void HostIPv6FormRequestTargetHandled()
        {
            // Valid according to
            // https://tools.ietf.org/html/rfc7230#section-2.7.1 -> https://tools.ietf.org/html/rfc3986#section-3.2.2
            this.BootstrapEnv(2, 1, 0);
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get, "/");
            HttpHeaders httpHeaders = request.Headers;
            httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 5);
            httpHeaders.Set(HttpHeaderNames.Host, "[::1]:80");
            httpHeaders.Set(HttpConversionUtil.ExtensionHeaderNames.Scheme, "http");
            var http2Headers = new DefaultHttp2Headers()
            {
                Method = new AsciiString("GET"),
                Path = new AsciiString("/"),
                Authority = new AsciiString("[::1]:80"),
                Scheme = new AsciiString("http")
            };

            var writePromise = this.NewPromise();
            this.VerifyHeadersOnly(http2Headers, writePromise, this.clientChannel.WriteAndFlushAsync(request, writePromise));
        }

        [Fact]
        public void HostFormRequestTargetHandled()
        {
            this.BootstrapEnv(2, 1, 0);
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get, "/");
            HttpHeaders httpHeaders = request.Headers;
            httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 5);
            httpHeaders.Set(HttpHeaderNames.Host, "localhost:80");
            httpHeaders.Set(HttpConversionUtil.ExtensionHeaderNames.Scheme, "http");
            var http2Headers = new DefaultHttp2Headers()
            {
                Method = new AsciiString("GET"),
                Path = new AsciiString("/"),
                Authority = new AsciiString("localhost:80"),
                Scheme = new AsciiString("http")
            };

            var writePromise = this.NewPromise();
            this.VerifyHeadersOnly(http2Headers, writePromise, this.clientChannel.WriteAndFlushAsync(request, writePromise));
        }

        [Fact]
        public void HostIPv4FormRequestTargetHandled()
        {
            this.BootstrapEnv(2, 1, 0);
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get, "/");
            HttpHeaders httpHeaders = request.Headers;
            httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 5);
            httpHeaders.Set(HttpHeaderNames.Host, "1.2.3.4:80");
            httpHeaders.Set(HttpConversionUtil.ExtensionHeaderNames.Scheme, "http");
            var http2Headers = new DefaultHttp2Headers()
            {
                Method = new AsciiString("GET"),
                Path = new AsciiString("/"),
                Authority = new AsciiString("1.2.3.4:80"),
                Scheme = new AsciiString("http")
            };

            var writePromise = this.NewPromise();
            this.VerifyHeadersOnly(http2Headers, writePromise, this.clientChannel.WriteAndFlushAsync(request, writePromise));
        }

        [Fact]
        public void NoSchemeRequestTargetHandled()
        {
            this.BootstrapEnv(2, 1, 0);
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get, "/");
            HttpHeaders httpHeaders = request.Headers;
            httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 5);
            httpHeaders.Set(HttpHeaderNames.Host, "localhost");
            var writePromise = this.NewPromise();
            var writeFuture = this.clientChannel.WriteAndFlushAsync(request, writePromise);

            Task.WaitAny(writePromise.Task, Task.Delay(TimeSpan.FromSeconds(WAIT_TIME_SECONDS)));
            //Assert.True(writePromise.Task.Wait(TimeSpan.FromSeconds(WAIT_TIME_SECONDS)));

            Assert.True(writePromise.IsCompleted);
            Assert.False(writePromise.IsSuccess);
            Assert.True(writeFuture.IsCompleted);
            Assert.False(writeFuture.IsSuccess());
        }

        [Fact]
        public void RequestWithBody()
        {
            string text = "foooooogoooo";
            List<string> receivedBuffers = new List<string>();
            this.serverListener
                .Setup(x => x.OnDataRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 3),
                    It.IsAny<IByteBuffer>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true)))
                .Returns<IChannelHandlerContext, int, IByteBuffer, int, bool>((ctx, id, buf, p, e) =>
                {
                    lock (receivedBuffers)
                    {
                        receivedBuffers.Add(buf.ToString(Encoding.UTF8));
                    }
                    return 0;
                });
            this.BootstrapEnv(3, 1, 0);
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Post,
                   "http://your_user-name123@www.example.org:5555/example",
                   Unpooled.CopiedBuffer(text, Encoding.UTF8));
            HttpHeaders httpHeaders = request.Headers;
            httpHeaders.Set(HttpHeaderNames.Host, "www.example-origin.org:5555");
            httpHeaders.Add(AsciiString.Of("foo"), AsciiString.Of("goo"));
            httpHeaders.Add(AsciiString.Of("foo"), AsciiString.Of("goo2"));
            httpHeaders.Add(AsciiString.Of("foo2"), AsciiString.Of("goo2"));
            var http2Headers = new DefaultHttp2Headers()
            {
                Method = new AsciiString("POST"),
                Path = new AsciiString("/example"),
                Authority = new AsciiString("www.example-origin.org:5555"),
                Scheme = new AsciiString("http")
            };
            http2Headers.Add(new AsciiString("foo"), new AsciiString("goo"));
            http2Headers.Add(new AsciiString("foo"), new AsciiString("goo2"));
            http2Headers.Add(new AsciiString("foo2"), new AsciiString("goo2"));
            var writePromise = this.NewPromise();
            var writeFuture = this.clientChannel.WriteAndFlushAsync(request, writePromise);

            Assert.True(writePromise.Task.Wait(TimeSpan.FromSeconds(WAIT_TIME_SECONDS)));
            Assert.True(writePromise.IsSuccess);
            Assert.True(writeFuture.Wait(TimeSpan.FromSeconds(WAIT_TIME_SECONDS)));
            Assert.True(writeFuture.IsSuccess());
            this.AwaitRequests();
            this.serverListener.Verify(
                x => x.OnHeadersRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 3),
                    It.Is<IHttp2Headers>(v => v.Equals(http2Headers)),
                    It.Is<int>(v => v == 0),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false)));
            this.serverListener.Verify(
                x => x.OnDataRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 3),
                    It.IsAny<IByteBuffer>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true)));
            lock (receivedBuffers)
            {
                Assert.Single(receivedBuffers);
                Assert.Equal(text, receivedBuffers[0]);
            }
        }

        [Fact]
        public void RequestWithBodyAndTrailingHeaders()
        {
            string text = "foooooogoooo";
            List<string> receivedBuffers = new List<string>();
            this.serverListener
                .Setup(x => x.OnDataRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 3),
                    It.IsAny<IByteBuffer>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false)))
                .Returns<IChannelHandlerContext, int, IByteBuffer, int, bool>((ctx, id, buf, p, e) =>
                {
                    lock (receivedBuffers)
                    {
                        receivedBuffers.Add(buf.ToString(Encoding.UTF8));
                    }
                    return 0;
                });
            this.BootstrapEnv(4, 1, 1);
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Post,
                   "http://your_user-name123@www.example.org:5555/example",
                   Unpooled.CopiedBuffer(text, Encoding.UTF8));
            HttpHeaders httpHeaders = request.Headers;
            httpHeaders.Set(HttpHeaderNames.Host, "www.example.org:5555");
            httpHeaders.Add(AsciiString.Of("foo"), AsciiString.Of("goo"));
            httpHeaders.Add(AsciiString.Of("foo"), AsciiString.Of("goo2"));
            httpHeaders.Add(AsciiString.Of("foo2"), AsciiString.Of("goo2"));
            var http2Headers = new DefaultHttp2Headers()
            {
                Method = new AsciiString("POST"),
                Path = new AsciiString("/example"),
                Authority = new AsciiString("www.example.org:5555"),
                Scheme = new AsciiString("http")
            };
            http2Headers.Add(new AsciiString("foo"), new AsciiString("goo"));
            http2Headers.Add(new AsciiString("foo"), new AsciiString("goo2"));
            http2Headers.Add(new AsciiString("foo2"), new AsciiString("goo2"));

            request.TrailingHeaders.Add(AsciiString.Of("trailing"), AsciiString.Of("bar"));

            IHttp2Headers http2TrailingHeaders = new DefaultHttp2Headers
            {
                { new AsciiString("trailing"), new AsciiString("bar") }
            };

            var writePromise = this.NewPromise();
            var writeFuture = this.clientChannel.WriteAndFlushAsync(request, writePromise);

            Assert.True(writePromise.Task.Wait(TimeSpan.FromSeconds(WAIT_TIME_SECONDS)));
            Assert.True(writePromise.IsSuccess);
            Assert.True(writeFuture.Wait(TimeSpan.FromSeconds(WAIT_TIME_SECONDS)));
            Assert.True(writeFuture.IsSuccess());
            this.AwaitRequests();
            this.serverListener.Verify(
                x => x.OnHeadersRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 3),
                    It.Is<IHttp2Headers>(v => v.Equals(http2Headers)),
                    It.Is<int>(v => v == 0),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false)));
            this.serverListener.Verify(
                x => x.OnDataRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 3),
                    It.IsAny<IByteBuffer>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false)));
            this.serverListener.Verify(
                x => x.OnHeadersRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 3),
                    It.Is<IHttp2Headers>(v => v.Equals(http2TrailingHeaders)),
                    It.Is<int>(v => v == 0),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true)));
            lock (receivedBuffers)
            {
                Assert.Single(receivedBuffers);
                Assert.Equal(text, receivedBuffers[0]);
            }
        }

        [Fact]
        public void ChunkedRequestWithBodyAndTrailingHeaders()
        {
            string text = "foooooo";
            string text2 = "goooo";
            List<string> receivedBuffers = new List<string>();
            this.serverListener
                .Setup(x => x.OnDataRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 3),
                    It.IsAny<IByteBuffer>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false)))
                .Returns<IChannelHandlerContext, int, IByteBuffer, int, bool>((ctx, id, buf, p, e) =>
                {
                    lock (receivedBuffers)
                    {
                        receivedBuffers.Add(buf.ToString(Encoding.UTF8));
                    }
                    return 0;
                });
            this.BootstrapEnv(4, 1, 1);
            IHttpRequest request = new DefaultHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Post,
                   "http://your_user-name123@www.example.org:5555/example");
            HttpHeaders httpHeaders = request.Headers;
            httpHeaders.Set(HttpHeaderNames.Host, "www.example.org:5555");
            httpHeaders.Add(HttpHeaderNames.TransferEncoding, "chunked");
            httpHeaders.Add(AsciiString.Of("foo"), AsciiString.Of("goo"));
            httpHeaders.Add(AsciiString.Of("foo"), AsciiString.Of("goo2"));
            httpHeaders.Add(AsciiString.Of("foo2"), AsciiString.Of("goo2"));
            var http2Headers = new DefaultHttp2Headers()
            {
                Method = new AsciiString("POST"),
                Path = new AsciiString("/example"),
                Authority = new AsciiString("www.example.org:5555"),
                Scheme = new AsciiString("http")
            };
            http2Headers.Add(new AsciiString("foo"), new AsciiString("goo"));
            http2Headers.Add(new AsciiString("foo"), new AsciiString("goo2"));
            http2Headers.Add(new AsciiString("foo2"), new AsciiString("goo2"));

            DefaultHttpContent httpContent = new DefaultHttpContent(Unpooled.CopiedBuffer(text, Encoding.UTF8));
            ILastHttpContent lastHttpContent = new DefaultLastHttpContent(Unpooled.CopiedBuffer(text2, Encoding.UTF8));

            lastHttpContent.TrailingHeaders.Add(AsciiString.Of("trailing"), AsciiString.Of("bar"));

            IHttp2Headers http2TrailingHeaders = new DefaultHttp2Headers
            {
                { new AsciiString("trailing"), new AsciiString("bar") }
            };

            var writePromise = this.NewPromise();
            var writeFuture = this.clientChannel.WriteAsync(request, writePromise);
            var contentPromise = this.NewPromise();
            var contentFuture = this.clientChannel.WriteAsync(httpContent, contentPromise);
            var lastContentPromise = this.NewPromise();
            var lastContentFuture = this.clientChannel.WriteAsync(lastHttpContent, lastContentPromise);

            this.clientChannel.Flush();

            Assert.True(writePromise.Task.Wait(TimeSpan.FromSeconds(WAIT_TIME_SECONDS)));
            Assert.True(writePromise.IsSuccess);
            Assert.True(writeFuture.Wait(TimeSpan.FromSeconds(WAIT_TIME_SECONDS)));
            Assert.True(writeFuture.IsSuccess());

            Assert.True(contentPromise.Task.Wait(TimeSpan.FromSeconds(WAIT_TIME_SECONDS)));
            Assert.True(contentPromise.IsSuccess);
            Assert.True(contentFuture.Wait(TimeSpan.FromSeconds(WAIT_TIME_SECONDS)));
            Assert.True(contentFuture.IsSuccess());

            Assert.True(lastContentPromise.Task.Wait(TimeSpan.FromSeconds(WAIT_TIME_SECONDS)));
            Assert.True(lastContentPromise.IsSuccess);
            Assert.True(lastContentFuture.Wait(TimeSpan.FromSeconds(WAIT_TIME_SECONDS)));
            Assert.True(lastContentFuture.IsSuccess());

            this.AwaitRequests();
            this.serverListener.Verify(
                x => x.OnHeadersRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 3),
                    It.Is<IHttp2Headers>(v => v.Equals(http2Headers)),
                    It.Is<int>(v => v == 0),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false)));
            this.serverListener.Verify(
                x => x.OnDataRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 3),
                    It.IsAny<IByteBuffer>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false)));
            this.serverListener.Verify(
                x => x.OnHeadersRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 3),
                    It.Is<IHttp2Headers>(v => v.Equals(http2TrailingHeaders)),
                    It.Is<int>(v => v == 0),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true)));
            lock (receivedBuffers)
            {
                Assert.Single(receivedBuffers);
                Assert.Equal(text + text2, receivedBuffers[0]);
            }
        }

        protected abstract void SetupServerBootstrap(ServerBootstrap bootstrap);

        protected abstract void SetupBootstrap(Bootstrap bootstrap);

        protected virtual void SetInitialServerChannelPipeline(IChannel ch)
        {
            this.serverConnectedChannel = ch;
            var p = ch.Pipeline;
            this.serverFrameCountDown =
                    new Http2TestUtil.FrameCountDown(this.serverListener.Object, this.serverSettingsAckLatch,
                            this.requestLatch, null, this.trailersLatch);
            //p.AddLast(new DotNetty.Handlers.Logging.LoggingHandler("CONN"));
            p.AddLast((new HttpToHttp2ConnectionHandlerBuilder()
            {
                IsServer = true,
                FrameListener = this.serverFrameCountDown
            }).Build());
        }

        protected virtual void SetInitialChannelPipeline(IChannel ch)
        {
            var p = ch.Pipeline;
            p.AddLast((new HttpToHttp2ConnectionHandlerBuilder()
            {
                IsServer = false,
                FrameListener = this.clientListener.Object,
                GracefulShutdownTimeout = TimeSpan.Zero
            }).Build());
        }

        protected virtual void StartBootstrap()
        {
            var loopback = IPAddress.IPv6Loopback;
            this.serverChannel = this.sb.BindAsync(loopback, Port).GetAwaiter().GetResult();

            var port = ((IPEndPoint)this.serverChannel.LocalAddress).Port;
            var ccf = this.cb.ConnectAsync(loopback, port);
            this.clientChannel = ccf.GetAwaiter().GetResult();
        }

        protected TlsHandler CreateTlsHandler(bool isClient)
        {
            X509Certificate2 tlsCertificate = TestResourceHelper.GetTestCertificate();
            string targetHost = tlsCertificate.GetNameInfo(X509NameType.DnsName, false);
            TlsHandler tlsHandler = isClient ?
                new TlsHandler(new ClientTlsSettings(targetHost).AllowAnyServerCertificate()):
                new TlsHandler(new ServerTlsSettings(tlsCertificate).AllowAnyClientCertificate());
            return tlsHandler;
        }

        protected virtual int Port => 0;

        private void BootstrapEnv(int requestCountDown, int serverSettingsAckCount, int trailersCount)
        {
            var prefaceWrittenLatch = new CountdownEvent(1);
            var serverChannelLatch = new CountdownEvent(1);
            this.requestLatch = new CountdownEvent(requestCountDown);
            this.serverSettingsAckLatch = new CountdownEvent(serverSettingsAckCount);
            this.trailersLatch = trailersCount == 0 ? null : new CountdownEvent(trailersCount);

            this.sb = new ServerBootstrap();
            this.cb = new Bootstrap();

            this.SetupServerBootstrap(this.sb);
            this.sb.ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
            {
                this.SetInitialServerChannelPipeline(ch);
                serverChannelLatch.SafeSignal();
            }));

            this.SetupBootstrap(this.cb);
            this.cb.Handler(new ActionChannelInitializer<IChannel>(ch =>
            {
                this.SetInitialChannelPipeline(ch);
                ch.Pipeline.AddLast(new TestChannelHandlerAdapter(prefaceWrittenLatch));
            }));

            this.StartBootstrap();

            Assert.True(prefaceWrittenLatch.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(serverChannelLatch.Wait(TimeSpan.FromSeconds(WAIT_TIME_SECONDS)));
        }

        sealed class TestChannelHandlerAdapter : ChannelHandlerAdapter
        {
            readonly CountdownEvent prefaceWrittenLatch;

            public TestChannelHandlerAdapter(CountdownEvent countdown) => this.prefaceWrittenLatch = countdown;

            public override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
            {
                if (ReferenceEquals(evt, Http2ConnectionPrefaceAndSettingsFrameWrittenEvent.Instance))
                {
                    this.prefaceWrittenLatch.SafeSignal();
                    ctx.Pipeline.Remove(this);
                }
            }
        }

        private void VerifyHeadersOnly(IHttp2Headers expected, IPromise writePromise, Task writeFuture)
        {
            Assert.True(writePromise.Task.Wait(TimeSpan.FromSeconds(WAIT_TIME_SECONDS)));
            Assert.True(writePromise.IsSuccess);
            Assert.True(writeFuture.Wait(TimeSpan.FromSeconds(WAIT_TIME_SECONDS)));
            Assert.True(writeFuture.IsSuccess());
            this.AwaitRequests();
            this.serverListener.Verify(
                x => x.OnHeadersRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 5),
                    It.Is<IHttp2Headers>(v => expected.Equals(v)),
                    //It.Is<IHttp2Headers>(v => HeadersEquals(expected, v)),
                    It.Is<int>(v => v == 0),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true)));
            this.serverListener.Verify(
                x => x.OnDataRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>()), Times.Never());
        }

        private static bool HeadersEquals(IHttp2Headers expected, IHttp2Headers actual)
        {
            var result = expected.Equals(actual);
            return result;
        }

        private void AwaitRequests()
        {
            Assert.True(this.requestLatch.Wait(TimeSpan.FromSeconds(WAIT_TIME_SECONDS)));
            if (this.trailersLatch != null)
            {
                Assert.True(this.trailersLatch.Wait(TimeSpan.FromSeconds(WAIT_TIME_SECONDS)));
            }
            Assert.True(this.serverSettingsAckLatch.Wait(TimeSpan.FromSeconds(WAIT_TIME_SECONDS)));
        }

        private IChannelHandlerContext Ctx()
        {
            return this.clientChannel.Pipeline.FirstContext();
        }

        private IPromise NewPromise()
        {
            return this.Ctx().NewPromise();
        }
    }
}
