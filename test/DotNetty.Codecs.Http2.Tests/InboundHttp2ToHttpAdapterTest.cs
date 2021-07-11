
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

    public sealed class LibuvInboundHttp2ToHttpAdapterTest : AbstractInboundHttp2ToHttpAdapterTest
    {
        static LibuvInboundHttp2ToHttpAdapterTest()
        {
            DotNetty.Common.ResourceLeakDetector.Level = Common.ResourceLeakDetector.DetectionLevel.Disabled;
        }

        public LibuvInboundHttp2ToHttpAdapterTest(ITestOutputHelper output) : base(output) { }

        protected override void SetupServerBootstrap(ServerBootstrap bootstrap)
        {
            var dispatcher = new DispatcherEventLoopGroup();
            var bossGroup = dispatcher;
            var workGroup = new WorkerEventLoopGroup(dispatcher);
            bootstrap.Group(bossGroup, workGroup)
                     .Channel<TcpServerChannel>();
        }

        protected override void SetupBootstrap(Bootstrap bootstrap)
        {
            bootstrap.Group(new EventLoopGroup()).Channel<TcpChannel>();
        }
    }

    //public sealed class TlsSocketInboundHttp2ToHttpAdapterTest : SocketInboundHttp2ToHttpAdapterTest
    //{
    //    public TlsSocketInboundHttp2ToHttpAdapterTest(ITestOutputHelper output) : base(output) { }

    //    protected override void SetInitialServerChannelPipeline(IChannel ch)
    //    {
    //        ch.Pipeline.AddLast(this.CreateTlsHandler(false));
    //        base.SetInitialServerChannelPipeline(ch);
    //    }

    //    protected override void SetInitialChannelPipeline(IChannel ch)
    //    {
    //        ch.Pipeline.AddLast(this.CreateTlsHandler(true));
    //        base.SetInitialChannelPipeline(ch);
    //    }
    //}

    public class SocketInboundHttp2ToHttpAdapterTest : AbstractInboundHttp2ToHttpAdapterTest
    {
        public SocketInboundHttp2ToHttpAdapterTest(ITestOutputHelper output) : base(output) { }

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

    public sealed class LocalInboundHttp2ToHttpAdapterTest : AbstractInboundHttp2ToHttpAdapterTest
    {
        public LocalInboundHttp2ToHttpAdapterTest(ITestOutputHelper output) : base(output) { }

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
            this.serverChannel = this.sb.BindAsync(new LocalAddress("InboundHttp2ToHttpAdapterTest")).GetAwaiter().GetResult();

            var ccf = this.cb.ConnectAsync(this.serverChannel.LocalAddress);
            this.clientChannel = ccf.GetAwaiter().GetResult();
        }
    }

    /**
     * Testing the {@link InboundHttp2ToHttpAdapter} and base class {@link InboundHttp2ToHttpAdapter} for HTTP/2
     * frames into {@link HttpObject}s
     */
    [Collection("BootstrapEnv")]
    public abstract class AbstractInboundHttp2ToHttpAdapterTest : TestBase, IDisposable
    {
        private IList<IFullHttpMessage> capturedRequests;
        private IList<IFullHttpMessage> capturedResponses;

        private Mock<IHttpResponseListener> serverListener;
        private Mock<IHttpResponseListener> clientListener;
        private Mock<IHttpSettingsListener> settingsListener;

        private Http2ConnectionHandler serverHandler;
        private Http2ConnectionHandler clientHandler;
        protected ServerBootstrap sb;
        protected Bootstrap cb;
        protected IChannel serverChannel;
        private volatile IChannel serverConnectedChannel;
        protected IChannel clientChannel;
        private CountdownEvent serverLatch;
        private CountdownEvent clientLatch;
        private CountdownEvent serverLatch2;
        private CountdownEvent clientLatch2;
        private CountdownEvent settingsLatch;
        private int maxContentLength;
        private HttpResponseDelegator serverDelegator;
        private HttpResponseDelegator clientDelegator;
        private HttpSettingsDelegator settingsDelegator;
        private Http2Exception clientException;

        public AbstractInboundHttp2ToHttpAdapterTest(ITestOutputHelper output)
            : base(output)
        {
            this.serverListener = new Mock<IHttpResponseListener>();
            this.clientListener = new Mock<IHttpResponseListener>();
            this.settingsListener = new Mock<IHttpSettingsListener>();
        }

        public void Dispose()
        {
            this.CleanupCapturedRequests();
            this.CleanupCapturedResponses();
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
            if (serverConnectedChannel != null)
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
        public void ClientRequestSingleHeaderNoDataFrames()
        {
            this.BootstrapEnv(1, 1, 1);
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get,
                   "/some/path/resource2", true);
            try
            {
                HttpHeaders httpHeaders = request.Headers;
                httpHeaders.Set(HttpConversionUtil.ExtensionHeaderNames.Scheme, "https");
                httpHeaders.Set(HttpHeaderNames.Host, "example.org");
                httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 3);
                httpHeaders.SetInt(HttpHeaderNames.ContentLength, 0);
                httpHeaders.SetShort(HttpConversionUtil.ExtensionHeaderNames.StreamWeight, 16);
                var http2Headers = new DefaultHttp2Headers()
                {
                    Method = new AsciiString("GET"),
                    Scheme = new AsciiString("https"),
                    Authority = new AsciiString("example.org"),
                    Path = new AsciiString("/some/path/resource2"),
                };
                Http2TestUtil.RunInChannel(this.clientChannel, () =>
                {
                    this.clientHandler.Encoder.WriteHeadersAsync(this.CtxClient(), 3, http2Headers, 0, true, this.NewPromiseClient());
                    this.clientChannel.Flush();
                });
                this.AwaitRequests();
                var requestCaptor = new ArgumentCaptor<IFullHttpMessage>();
                this.serverListener.Verify(x => x.MessageReceived(It.Is<IHttpObject>(v => requestCaptor.Capture((IFullHttpMessage)v))));
                this.capturedRequests = requestCaptor.GetAllValues();
                Assert.Equal(request, (IFullHttpRequest)this.capturedRequests[0]);
            }
            finally
            {
                request.Release();
            }
        }

        [Fact]
        public void ClientRequestSingleHeaderCookieSplitIntoMultipleEntries()
        {
            this.BootstrapEnv(1, 1, 1);
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get,
                   "/some/path/resource2", true);
            try
            {
                HttpHeaders httpHeaders = request.Headers;
                httpHeaders.Set(HttpConversionUtil.ExtensionHeaderNames.Scheme, "https");
                httpHeaders.Set(HttpHeaderNames.Host, "example.org");
                httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 3);
                httpHeaders.SetInt(HttpHeaderNames.ContentLength, 0);
                httpHeaders.Set(HttpHeaderNames.Cookie, "a=b; c=d; e=f");
                httpHeaders.SetShort(HttpConversionUtil.ExtensionHeaderNames.StreamWeight, 16);
                var http2Headers = new DefaultHttp2Headers()
                {
                    Method = new AsciiString("GET"),
                    Scheme = new AsciiString("https"),
                    Authority = new AsciiString("example.org"),
                    Path = new AsciiString("/some/path/resource2"),
                };
                http2Headers.Add(HttpHeaderNames.Cookie, (AsciiString)"a=b");
                http2Headers.Add(HttpHeaderNames.Cookie, (AsciiString)"c=d");
                http2Headers.Add(HttpHeaderNames.Cookie, (AsciiString)"e=f");
                Http2TestUtil.RunInChannel(this.clientChannel, () =>
                {
                    this.clientHandler.Encoder.WriteHeadersAsync(this.CtxClient(), 3, http2Headers, 0, true, this.NewPromiseClient());
                    this.clientChannel.Flush();
                });
                this.AwaitRequests();
                var requestCaptor = new ArgumentCaptor<IFullHttpMessage>();
                this.serverListener.Verify(x => x.MessageReceived(It.Is<IHttpObject>(v => requestCaptor.Capture((IFullHttpMessage)v))));
                this.capturedRequests = requestCaptor.GetAllValues();
                Assert.Equal(request, (IFullHttpRequest)this.capturedRequests[0]);
            }
            finally
            {
                request.Release();
            }
        }

        [Fact]
        public void ClientRequestSingleHeaderCookieSplitIntoMultipleEntries2()
        {
            this.BootstrapEnv(1, 1, 1);
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get,
                   "/some/path/resource2", true);
            try
            {
                HttpHeaders httpHeaders = request.Headers;
                httpHeaders.Set(HttpConversionUtil.ExtensionHeaderNames.Scheme, "https");
                httpHeaders.Set(HttpHeaderNames.Host, "example.org");
                httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 3);
                httpHeaders.SetInt(HttpHeaderNames.ContentLength, 0);
                httpHeaders.Set(HttpHeaderNames.Cookie, "a=b; c=d; e=f");
                httpHeaders.SetShort(HttpConversionUtil.ExtensionHeaderNames.StreamWeight, 16);
                var http2Headers = new DefaultHttp2Headers()
                {
                    Method = new AsciiString("GET"),
                    Scheme = new AsciiString("https"),
                    Authority = new AsciiString("example.org"),
                    Path = new AsciiString("/some/path/resource2"),
                };
                http2Headers.Add(HttpHeaderNames.Cookie, (AsciiString)"a=b; c=d");
                http2Headers.Add(HttpHeaderNames.Cookie, (AsciiString)"e=f");
                Http2TestUtil.RunInChannel(this.clientChannel, () =>
                {
                    this.clientHandler.Encoder.WriteHeadersAsync(this.CtxClient(), 3, http2Headers, 0, true, this.NewPromiseClient());
                    this.clientChannel.Flush();
                });
                this.AwaitRequests();
                var requestCaptor = new ArgumentCaptor<IFullHttpMessage>();
                this.serverListener.Verify(x => x.MessageReceived(It.Is<IHttpObject>(v => requestCaptor.Capture((IFullHttpMessage)v))));
                this.capturedRequests = requestCaptor.GetAllValues();
                Assert.Equal(request, (IFullHttpRequest)this.capturedRequests[0]);
            }
            finally
            {
                request.Release();
            }
        }

        [Fact]
        public void ClientRequestSingleHeaderNonAsciiShouldThrow()
        {
            this.BootstrapEnv(1, 1, 1);
            var http2Headers = new DefaultHttp2Headers()
            {
                Method = new AsciiString("GET"),
                Scheme = new AsciiString("https"),
                Authority = new AsciiString("example.org"),
                Path = new AsciiString("/some/path/resource2"),
            };
            http2Headers.Add(new AsciiString(Encoding.UTF8.GetBytes("çã"), true),
                             new AsciiString(Encoding.UTF8.GetBytes("Ãã"), true));
            Http2TestUtil.RunInChannel(this.clientChannel, () =>
            {
                this.clientHandler.Encoder.WriteHeadersAsync(this.CtxClient(), 3, http2Headers, 0, true, this.NewPromiseClient());
                this.clientChannel.Flush();
            });
            this.AwaitResponses();
            Assert.True(Http2Exception.IsStreamError(this.clientException));
        }

        [Fact]
        public void ClientRequestOneDataFrame()
        {
            this.BootstrapEnv(1, 1, 1);
            string text = "hello world";
            IByteBuffer content = Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(text));
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get,
                   "/some/path/resource2", content, true);
            try
            {
                HttpHeaders httpHeaders = request.Headers;
                httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 3);
                httpHeaders.SetInt(HttpHeaderNames.ContentLength, text.Length);
                httpHeaders.SetShort(HttpConversionUtil.ExtensionHeaderNames.StreamWeight, 16);
                var http2Headers = new DefaultHttp2Headers()
                {
                    Method = new AsciiString("GET"),
                    Path = new AsciiString("/some/path/resource2"),
                };
                Http2TestUtil.RunInChannel(this.clientChannel, () =>
                {
                    this.clientHandler.Encoder.WriteHeadersAsync(this.CtxClient(), 3, http2Headers, 0, false, this.NewPromiseClient());
                    this.clientHandler.Encoder.WriteDataAsync(this.CtxClient(), 3, content.RetainedDuplicate(), 0, true, this.NewPromiseClient());
                    this.clientChannel.Flush();
                });
                this.AwaitRequests();
                var requestCaptor = new ArgumentCaptor<IFullHttpMessage>();
                this.serverListener.Verify(x => x.MessageReceived(It.Is<IHttpObject>(v => requestCaptor.Capture((IFullHttpMessage)v))));
                this.capturedRequests = requestCaptor.GetAllValues();
                Assert.Equal(request, (IFullHttpRequest)this.capturedRequests[0]);
            }
            finally
            {
                request.Release();
            }
        }

        [Fact]
        public void ClientRequestMultipleDataFrames()
        {
            this.BootstrapEnv(1, 1, 1);
            string text = "hello world big time data!";
            IByteBuffer content = Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(text));
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get,
                   "/some/path/resource2", content, true);
            try
            {
                HttpHeaders httpHeaders = request.Headers;
                httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 3);
                httpHeaders.SetInt(HttpHeaderNames.ContentLength, text.Length);
                httpHeaders.SetShort(HttpConversionUtil.ExtensionHeaderNames.StreamWeight, 16);
                var http2Headers = new DefaultHttp2Headers()
                {
                    Method = new AsciiString("GET"),
                    Path = new AsciiString("/some/path/resource2"),
                };
                int midPoint = text.Length / 2;
                Http2TestUtil.RunInChannel(this.clientChannel, () =>
                {
                    this.clientHandler.Encoder.WriteHeadersAsync(this.CtxClient(), 3, http2Headers, 0, false, this.NewPromiseClient());
                    this.clientHandler.Encoder.WriteDataAsync(
                            this.CtxClient(), 3, content.RetainedSlice(0, midPoint), 0, false, this.NewPromiseClient());
                    this.clientHandler.Encoder.WriteDataAsync(
                            this.CtxClient(), 3, content.RetainedSlice(midPoint, text.Length - midPoint),
                            0, true, this.NewPromiseClient());
                    this.clientChannel.Flush();
                });
                this.AwaitRequests();
                var requestCaptor = new ArgumentCaptor<IFullHttpMessage>();
                this.serverListener.Verify(x => x.MessageReceived(It.Is<IHttpObject>(v => requestCaptor.Capture((IFullHttpMessage)v))));
                this.capturedRequests = requestCaptor.GetAllValues();
                Assert.Equal(request, (IFullHttpRequest)this.capturedRequests[0]);
            }
            finally
            {
                request.Release();
            }
        }

        [Fact]
        public void ClientRequestMultipleEmptyDataFrames()
        {
            this.BootstrapEnv(1, 1, 1);
            string text = "";
            IByteBuffer content = Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(text));
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get,
                   "/some/path/resource2", content, true);
            try
            {
                HttpHeaders httpHeaders = request.Headers;
                httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 3);
                httpHeaders.SetInt(HttpHeaderNames.ContentLength, text.Length);
                httpHeaders.SetShort(HttpConversionUtil.ExtensionHeaderNames.StreamWeight, 16);
                var http2Headers = new DefaultHttp2Headers()
                {
                    Method = new AsciiString("GET"),
                    Path = new AsciiString("/some/path/resource2"),
                };
                Http2TestUtil.RunInChannel(this.clientChannel, () =>
                {
                    this.clientHandler.Encoder.WriteHeadersAsync(this.CtxClient(), 3, http2Headers, 0, false, this.NewPromiseClient());
                    this.clientHandler.Encoder.WriteDataAsync(this.CtxClient(), 3, (IByteBuffer)content.Retain(), 0, false, this.NewPromiseClient());
                    this.clientHandler.Encoder.WriteDataAsync(this.CtxClient(), 3, (IByteBuffer)content.Retain(), 0, false, this.NewPromiseClient());
                    this.clientHandler.Encoder.WriteDataAsync(this.CtxClient(), 3, (IByteBuffer)content.Retain(), 0, true, this.NewPromiseClient());
                    this.clientChannel.Flush();
                });
                this.AwaitRequests();
                var requestCaptor = new ArgumentCaptor<IFullHttpMessage>();
                this.serverListener.Verify(x => x.MessageReceived(It.Is<IHttpObject>(v => requestCaptor.Capture((IFullHttpMessage)v))));
                this.capturedRequests = requestCaptor.GetAllValues();
                Assert.Equal(request, (IFullHttpRequest)this.capturedRequests[0]);
            }
            finally
            {
                request.Release();
            }
        }

        [Fact]
        public void ClientRequestTrailingHeaders()
        {
            this.BootstrapEnv(1, 1, 1);
            string text = "some data";
            IByteBuffer content = Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(text));
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get,
                   "/some/path/resource2", content, true);
            try
            {
                HttpHeaders httpHeaders = request.Headers;
                httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 3);
                httpHeaders.SetInt(HttpHeaderNames.ContentLength, text.Length);
                httpHeaders.SetShort(HttpConversionUtil.ExtensionHeaderNames.StreamWeight, 16);
                HttpHeaders trailingHeaders = request.TrailingHeaders;
                trailingHeaders.Set(AsciiString.Of("Foo"), AsciiString.Of("goo"));
                trailingHeaders.Set(AsciiString.Of("fOo2"), AsciiString.Of("goo2"));
                trailingHeaders.Add(AsciiString.Of("foO2"), AsciiString.Of("goo3"));
                var http2Headers = new DefaultHttp2Headers()
                {
                    Method = new AsciiString("GET"),
                    Path = new AsciiString("/some/path/resource2"),
                };
                IHttp2Headers http2Headers2 = new DefaultHttp2Headers();
                http2Headers2.Set(new AsciiString("foo"), new AsciiString("goo"));
                http2Headers2.Set(new AsciiString("foo2"), new AsciiString("goo2"));
                http2Headers2.Add(new AsciiString("foo2"), new AsciiString("goo3"));
                Http2TestUtil.RunInChannel(this.clientChannel, () =>
                {
                    this.clientHandler.Encoder.WriteHeadersAsync(this.CtxClient(), 3, http2Headers, 0, false, this.NewPromiseClient());
                    this.clientHandler.Encoder.WriteDataAsync(this.CtxClient(), 3, content.RetainedDuplicate(), 0, false, this.NewPromiseClient());
                    this.clientHandler.Encoder.WriteHeadersAsync(this.CtxClient(), 3, http2Headers2, 0, true, this.NewPromiseClient());
                    this.clientChannel.Flush();
                });
                this.AwaitRequests();
                var requestCaptor = new ArgumentCaptor<IFullHttpMessage>();
                this.serverListener.Verify(x => x.MessageReceived(It.Is<IHttpObject>(v => requestCaptor.Capture((IFullHttpMessage)v))));
                this.capturedRequests = requestCaptor.GetAllValues();
                Assert.Equal(request, (IFullHttpRequest)this.capturedRequests[0]);
            }
            finally
            {
                request.Release();
            }
        }

        [Fact]
        public void ClientRequestStreamDependencyInHttpMessageFlow()
        {
            this.BootstrapEnv(1, 2, 1);
            string text = "hello world big time data!";
            IByteBuffer content = Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(text));
            string text2 = "hello world big time data...number 2!!";
            IByteBuffer content2 = Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(text2));
            IFullHttpRequest request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Put,
                   "/some/path/resource", content, true);
            IFullHttpMessage request2 = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Put,
                   "/some/path/resource2", content2, true);
            try
            {
                HttpHeaders httpHeaders = request.Headers;
                httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 3);
                httpHeaders.SetInt(HttpHeaderNames.ContentLength, text.Length);
                httpHeaders.SetShort(HttpConversionUtil.ExtensionHeaderNames.StreamWeight, 16);
                HttpHeaders httpHeaders2 = request2.Headers;
                httpHeaders2.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 5);
                httpHeaders2.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamDependencyId, 3);
                httpHeaders2.SetShort(HttpConversionUtil.ExtensionHeaderNames.StreamWeight, 123);
                httpHeaders2.SetInt(HttpHeaderNames.ContentLength, text2.Length);
                var http2Headers = new DefaultHttp2Headers()
                {
                    Method = new AsciiString("PUT"),
                    Path = new AsciiString("/some/path/resource"),
                };
                var http2Headers2 = new DefaultHttp2Headers()
                {
                    Method = new AsciiString("PUT"),
                    Path = new AsciiString("/some/path/resource2"),
                };
                Http2TestUtil.RunInChannel(this.clientChannel, () =>
                {
                    this.clientHandler.Encoder.WriteHeadersAsync(this.CtxClient(), 3, http2Headers, 0, false, this.NewPromiseClient());
                    this.clientHandler.Encoder.WriteHeadersAsync(this.CtxClient(), 5, http2Headers2, 3, 123, true, 0, false, this.NewPromiseClient());
                    this.clientChannel.Flush(); // Headers are queued in the flow controller and so flush them.
                    this.clientHandler.Encoder.WriteDataAsync(this.CtxClient(), 3, content.RetainedDuplicate(), 0, true, this.NewPromiseClient());
                    this.clientHandler.Encoder.WriteDataAsync(this.CtxClient(), 5, content2.RetainedDuplicate(), 0, true, this.NewPromiseClient());
                    this.clientChannel.Flush();
                });
                this.AwaitRequests();
                var httpObjectCaptor = new ArgumentCaptor<IFullHttpMessage>();
                this.serverListener.Verify(x => x.MessageReceived(It.Is<IHttpObject>(v => httpObjectCaptor.Capture((IFullHttpMessage)v))), Times.Exactly(2));
                this.capturedRequests = httpObjectCaptor.GetAllValues();
                Assert.Equal(request, (IFullHttpRequest)this.capturedRequests[0]);
                Assert.Equal(request2, this.capturedRequests[1]);
            }
            finally
            {
                request.Release();
                request2.Release();
            }
        }

        [Fact]
        public void ServerRequestPushPromise()
        {
            this.BootstrapEnv(1, 1, 1);
            string text = "hello world big time data!";
            IByteBuffer content = Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(text));
            string text2 = "hello world smaller data?";
            IByteBuffer content2 = Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(text2));
            IFullHttpMessage response = new DefaultFullHttpResponse(DotNetty.Codecs.Http.HttpVersion.Http11, HttpResponseStatus.OK,
                   content, true);
            IFullHttpMessage response2 = new DefaultFullHttpResponse(DotNetty.Codecs.Http.HttpVersion.Http11, HttpResponseStatus.Created,
                   content2, true);
            IFullHttpMessage request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get, "/push/test",
                   true);
            try
            {
                HttpHeaders httpHeaders = response.Headers;
                httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 3);
                httpHeaders.SetInt(HttpHeaderNames.ContentLength, text.Length);
                httpHeaders.SetShort(HttpConversionUtil.ExtensionHeaderNames.StreamWeight, 16);
                HttpHeaders httpHeaders2 = response2.Headers;
                httpHeaders2.Set(HttpConversionUtil.ExtensionHeaderNames.Scheme, "https");
                httpHeaders2.Set(HttpHeaderNames.Host, "example.org");
                httpHeaders2.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 5);
                httpHeaders2.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamDependencyId, 3);
                httpHeaders2.SetInt(HttpHeaderNames.ContentLength, text2.Length);

                httpHeaders = request.Headers;
                httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 3);
                httpHeaders.SetInt(HttpHeaderNames.ContentLength, 0);
                httpHeaders.SetShort(HttpConversionUtil.ExtensionHeaderNames.StreamWeight, 16);
                var http2Headers3 = new DefaultHttp2Headers()
                {
                    Method = new AsciiString("GET"),
                    Path = new AsciiString("/push/test"),
                };
                Http2TestUtil.RunInChannel(this.clientChannel, () =>
                {
                    this.clientHandler.Encoder.WriteHeadersAsync(this.CtxClient(), 3, http2Headers3, 0, true, this.NewPromiseClient());
                    this.clientChannel.Flush();
                });
                this.AwaitRequests();
                var requestCaptor = new ArgumentCaptor<IFullHttpMessage>();
                this.serverListener.Verify(x => x.MessageReceived(It.Is<IHttpObject>(v => requestCaptor.Capture((IFullHttpMessage)v))));
                this.capturedRequests = requestCaptor.GetAllValues();
                Assert.Equal(request, (IFullHttpRequest)this.capturedRequests[0]);

                IHttp2Headers http2Headers = new DefaultHttp2Headers() { Status = new AsciiString("200") };
                // The PUSH_PROMISE frame includes a header block that contains a
                // complete set of request header fields that the server attributes to
                // the request.
                // https://tools.ietf.org/html/rfc7540#section-8.2.1
                // Therefore, we should consider the case where there is no Http response status.
                IHttp2Headers http2Headers2 = new DefaultHttp2Headers()
                {
                    Scheme = new AsciiString("https"),
                    Authority = new AsciiString("example.org")
                };
                Http2TestUtil.RunInChannel(this.serverConnectedChannel, () =>
                {
                    this.serverHandler.Encoder.WriteHeadersAsync(this.CtxServer(), 3, http2Headers, 0, false, this.NewPromiseServer());
                    this.serverHandler.Encoder.WritePushPromiseAsync(this.CtxServer(), 3, 2, http2Headers2, 0, this.NewPromiseServer());
                    this.serverHandler.Encoder.WriteDataAsync(this.CtxServer(), 3, content.RetainedDuplicate(), 0, true, this.NewPromiseServer());
                    this.serverHandler.Encoder.WriteDataAsync(this.CtxServer(), 5, content2.RetainedDuplicate(), 0, true, this.NewPromiseServer());
                    this.serverConnectedChannel.Flush();
                });
                this.AwaitResponses();
                var responseCaptor = new ArgumentCaptor<IFullHttpMessage>();
                this.clientListener.Verify(x => x.MessageReceived(It.Is<IHttpObject>(v => responseCaptor.Capture((IFullHttpMessage)v))));
                this.capturedResponses = responseCaptor.GetAllValues();
                Assert.Equal(response, this.capturedResponses[0]);
            }
            finally
            {
                request.Release();
                response.Release();
                response2.Release();
            }
        }

        [Fact]
        public void ServerResponseHeaderInformational()
        {
            this.BootstrapEnv(1, 2, 1, 2, 1);
            IFullHttpMessage request = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Put, "/info/test", true);
            HttpHeaders httpHeaders = request.Headers;
            httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 3);
            httpHeaders.Set(HttpHeaderNames.Expect, HttpHeaderValues.Continue);
            httpHeaders.SetInt(HttpHeaderNames.ContentLength, 0);
            httpHeaders.SetShort(HttpConversionUtil.ExtensionHeaderNames.StreamWeight, 16);

            IHttp2Headers http2Headers = new DefaultHttp2Headers()
            {
                Method = new AsciiString("PUT"),
                Path = new AsciiString("/info/test")
            };
            http2Headers.Set(new AsciiString(HttpHeaderNames.Expect.ToString()), new AsciiString(HttpHeaderValues.Continue.ToString()));
            IFullHttpMessage response = new DefaultFullHttpResponse(DotNetty.Codecs.Http.HttpVersion.Http11, HttpResponseStatus.Continue);
            string text = "a big payload";
            IByteBuffer payload = Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(text));
            IFullHttpMessage request2 = (IFullHttpMessage)request.Replace(payload);
            IFullHttpMessage response2 = new DefaultFullHttpResponse(DotNetty.Codecs.Http.HttpVersion.Http11, HttpResponseStatus.OK);

            try
            {
                Http2TestUtil.RunInChannel(this.clientChannel, () =>
                {
                    this.clientHandler.Encoder.WriteHeadersAsync(this.CtxClient(), 3, http2Headers, 0, false, this.NewPromiseClient());
                    this.clientChannel.Flush();
                });

                this.AwaitRequests();
                httpHeaders = response.Headers;
                httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 3);
                httpHeaders.SetInt(HttpHeaderNames.ContentLength, 0);
                IHttp2Headers http2HeadersResponse = new DefaultHttp2Headers() { Status = new AsciiString("100") };
                Http2TestUtil.RunInChannel(this.serverConnectedChannel, () =>
                {
                    this.serverHandler.Encoder.WriteHeadersAsync(this.CtxServer(), 3, http2HeadersResponse, 0, false, this.NewPromiseServer());
                    this.serverConnectedChannel.Flush();
                });

                this.AwaitResponses();
                httpHeaders = request2.Headers;
                httpHeaders.SetInt(HttpHeaderNames.ContentLength, text.Length);
                httpHeaders.Remove(HttpHeaderNames.Expect);
                Http2TestUtil.RunInChannel(this.clientChannel, () =>
                {
                    this.clientHandler.Encoder.WriteDataAsync(this.CtxClient(), 3, payload.RetainedDuplicate(), 0, true, this.NewPromiseClient());
                    this.clientChannel.Flush();
                });

                this.AwaitRequests2();
                httpHeaders = response2.Headers;
                httpHeaders.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, 3);
                httpHeaders.SetInt(HttpHeaderNames.ContentLength, 0);
                httpHeaders.SetShort(HttpConversionUtil.ExtensionHeaderNames.StreamWeight, 16);

                IHttp2Headers http2HeadersResponse2 = new DefaultHttp2Headers() { Status = new AsciiString("200") };
                Http2TestUtil.RunInChannel(this.serverConnectedChannel, () =>
                {
                    this.serverHandler.Encoder.WriteHeadersAsync(this.CtxServer(), 3, http2HeadersResponse2, 0, true, this.NewPromiseServer());
                    this.serverConnectedChannel.Flush();
                });

                this.AwaitResponses2();
                var requestCaptor = new ArgumentCaptor<IFullHttpMessage>();
                this.serverListener.Verify(x => x.MessageReceived(It.Is<IHttpObject>(v => requestCaptor.Capture((IFullHttpMessage)v))), Times.Exactly(2));
                this.capturedRequests = requestCaptor.GetAllValues();
                Assert.Equal(2, this.capturedRequests.Count);
                // We do not expect to have this header in the captured request so remove it now.
                Assert.NotNull(request.Headers.Remove((AsciiString)"x-http2-stream-weight"));

                Assert.Equal(request, (IFullHttpRequest)this.capturedRequests[0]);
                Assert.Equal(request2, this.capturedRequests[1]);

                var responseCaptor = new ArgumentCaptor<IFullHttpMessage>();
                this.clientListener.Verify(x => x.MessageReceived(It.Is<IHttpObject>(v => responseCaptor.Capture((IFullHttpMessage)v))), Times.Exactly(2));
                this.capturedResponses = responseCaptor.GetAllValues();
                Assert.Equal(2, this.capturedResponses.Count);
                Assert.Equal(response, this.capturedResponses[0]);
                Assert.Equal(response2, this.capturedResponses[1]);
            }
            finally
            {
                request.Release();
                request2.Release();
                response.Release();
                response2.Release();
            }
        }

        [Fact]
        public void PropagateSettings()
        {
            this.BootstrapEnv(1, 1, 2);
            Http2Settings settings = new Http2Settings().PushEnabled(true);
            Http2TestUtil.RunInChannel(this.clientChannel, () =>
            {
                this.clientHandler.Encoder.WriteSettingsAsync(this.CtxClient(), settings, this.NewPromiseClient());
                this.clientChannel.Flush();
            });
            Assert.True(this.settingsLatch.Wait(TimeSpan.FromSeconds(5)));
            var settingsCaptor = new ArgumentCaptor<Http2Settings>();
            this.settingsListener.Verify(x => x.MessageReceived(It.Is<Http2Settings>(v => settingsCaptor.Capture(v))), Times.Exactly(2));
            Assert.Equal(settings, settingsCaptor.GetValue());
        }

        protected abstract void SetupServerBootstrap(ServerBootstrap bootstrap);

        protected abstract void SetupBootstrap(Bootstrap bootstrap);

        protected virtual void SetInitialServerChannelPipeline(IChannel ch)
        {
            this.serverConnectedChannel = ch;
            var p = ch.Pipeline;
            var connection = new DefaultHttp2Connection(true);

            this.serverHandler = new Http2ConnectionHandlerBuilder()
            {
                FrameListener = new InboundHttp2ToHttpAdapterBuilder(connection)
                {
                    MaxContentLength = this.maxContentLength,
                    IsValidateHttpHeaders = true,
                    IsPropagateSettings = true,
                }.Build(),
                Connection = connection,
                GracefulShutdownTimeout = TimeSpan.Zero
            }.Build();
            p.AddLast(this.serverHandler);

            this.serverDelegator = new HttpResponseDelegator(this.serverListener.Object, this.serverLatch, this.serverLatch2);
            p.AddLast(this.serverDelegator);
            this.settingsDelegator = new HttpSettingsDelegator(this.settingsListener.Object, this.settingsLatch);
            p.AddLast(this.settingsDelegator);
        }

        protected virtual void SetInitialChannelPipeline(IChannel ch)
        {
            var p = ch.Pipeline;
            var connection = new DefaultHttp2Connection(false);

            this.clientHandler = new Http2ConnectionHandlerBuilder()
            {
                FrameListener = new InboundHttp2ToHttpAdapterBuilder(connection)
                {
                    MaxContentLength = this.maxContentLength,
                }.Build(),
                Connection = connection,
                GracefulShutdownTimeout = TimeSpan.Zero
            }.Build();
            p.AddLast(this.clientHandler);

            this.clientDelegator = new HttpResponseDelegator(this.clientListener.Object, this.clientLatch, this.clientLatch2);
            p.AddLast(this.clientDelegator);
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
                new TlsHandler(stream => new SslStream(stream, true, (sender, certificate, chain, errors) => true), new ClientTlsSettings(targetHost)) :
                new TlsHandler(new ServerTlsSettings(tlsCertificate));
            return tlsHandler;
        }

        protected virtual int Port => 0;

        private void BootstrapEnv(int clientLatchCount, int serverLatchCount, int settingsLatchCount)
        {
            this.BootstrapEnv(clientLatchCount, clientLatchCount, serverLatchCount, serverLatchCount, settingsLatchCount);
        }

        private void BootstrapEnv(int clientLatchCount, int clientLatchCount2, int serverLatchCount,
            int serverLatchCount2, int settingsLatchCount)
        {
            var prefaceWrittenLatch = new CountdownEvent(1);
            this.clientDelegator = null;
            this.serverDelegator = null;
            this.serverConnectedChannel = null;
            this.maxContentLength = 1024;
            var serverChannelLatch = new CountdownEvent(1);
            this.serverLatch = new CountdownEvent(serverLatchCount);
            this.clientLatch = new CountdownEvent(clientLatchCount);
            this.serverLatch2 = new CountdownEvent(serverLatchCount2);
            this.clientLatch2 = new CountdownEvent(clientLatchCount2);
            this.settingsLatch = new CountdownEvent(settingsLatchCount);

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

                ch.Pipeline.AddLast(new TestChannelHandlerAdapterEC(this));

                ch.Pipeline.AddLast(new TestChannelHandlerAdapter(prefaceWrittenLatch));
            }));

            this.StartBootstrap();

            Assert.True(prefaceWrittenLatch.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(serverChannelLatch.Wait(TimeSpan.FromSeconds(5)));
        }

        sealed class TestChannelHandlerAdapterEC : ChannelHandlerAdapter
        {
            readonly AbstractInboundHttp2ToHttpAdapterTest self;

            public TestChannelHandlerAdapterEC(AbstractInboundHttp2ToHttpAdapterTest self)
            {
                this.self = self;
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception cause)
            {
                Http2Exception e = Http2CodecUtil.GetEmbeddedHttp2Exception(cause);
                if (e != null)
                {
                    self.clientException = e;
                    self.clientLatch.SafeSignal();
                }
                else
                {
                    base.ExceptionCaught(context, cause);
                }
            }
        }

        sealed class TestChannelHandlerAdapter : ChannelHandlerAdapter
        {
            readonly CountdownEvent prefaceWrittenLatch;

            public TestChannelHandlerAdapter(CountdownEvent countdown)
            {
                this.prefaceWrittenLatch = countdown;
            }

            public override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
            {
                if (ReferenceEquals(evt, Http2ConnectionPrefaceAndSettingsFrameWrittenEvent.Instance))
                {
                    this.prefaceWrittenLatch.SafeSignal();
                    ctx.Pipeline.Remove(this);
                }
            }
        }

        private void CleanupCapturedRequests()
        {
            if (this.capturedRequests != null)
            {
                foreach (IFullHttpMessage capturedRequest in this.capturedRequests)
                {
                    capturedRequest.Release();
                }
                this.capturedRequests = null;
            }
        }

        private void CleanupCapturedResponses()
        {
            if (this.capturedResponses != null)
            {
                foreach (IFullHttpMessage capturedResponse in this.capturedResponses)
                {
                    capturedResponse.Release();
                }
                this.capturedResponses = null;
            }
        }

        private void AwaitRequests()
        {
            Assert.True(this.serverLatch.Wait(TimeSpan.FromSeconds(5)));
        }

        private void AwaitResponses()
        {
            Assert.True(this.clientLatch.Wait(TimeSpan.FromSeconds(5)));
        }

        private void AwaitRequests2()
        {
            Assert.True(this.serverLatch2.Wait(TimeSpan.FromSeconds(5)));
        }

        private void AwaitResponses2()
        {
            Assert.True(this.clientLatch2.Wait(TimeSpan.FromSeconds(5)));
        }

        private IChannelHandlerContext CtxClient()
        {
            return this.clientChannel.Pipeline.FirstContext();
        }

        private IPromise NewPromiseClient()
        {
            return this.CtxClient().NewPromise();
        }

        private IChannelHandlerContext CtxServer()
        {
            return this.serverConnectedChannel.Pipeline.FirstContext();
        }

        private IPromise NewPromiseServer()
        {
            return this.CtxServer().NewPromise();
        }

        sealed class HttpResponseDelegator : SimpleChannelInboundHandler2<IHttpObject>
        {
            private readonly IHttpResponseListener listener;
            private readonly CountdownEvent latch;
            private readonly CountdownEvent latch2;

            public HttpResponseDelegator(IHttpResponseListener listener, CountdownEvent latch, CountdownEvent latch2)
                : base(false)
            {
                this.listener = listener;
                this.latch = latch;
                this.latch2 = latch2;
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, IHttpObject msg)
            {
                this.listener.MessageReceived(msg);
                this.latch.SafeSignal();
                this.latch2.SafeSignal();
            }
        }

        sealed class HttpSettingsDelegator : SimpleChannelInboundHandler2<Http2Settings>
        {
            private readonly IHttpSettingsListener listener;
            private readonly CountdownEvent latch;

            public HttpSettingsDelegator(IHttpSettingsListener listener, CountdownEvent latch)
                : base(false)
            {
                this.listener = listener;
                this.latch = latch;
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, Http2Settings settings)
            {
                this.listener.MessageReceived(settings);
                this.latch.SafeSignal();
            }
        }
    }

    public interface IHttpResponseListener
    {
        void MessageReceived(IHttpObject obj);
    }

    public interface IHttpSettingsListener
    {
        void MessageReceived(Http2Settings settings);
    }
}
