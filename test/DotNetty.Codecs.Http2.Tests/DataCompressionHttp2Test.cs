
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.IO;
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
    using DotNetty.Transport.Channels.Sockets;
    using DotNetty.Transport.Libuv;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    //public sealed class TlsLibuvDataCompressionHttp2Test : LibuvDataCompressionHttp2Test
    //{
    //    public TlsLibuvDataCompressionHttp2Test(ITestOutputHelper output) : base(output) { }

    //    protected override void SetInitialServerChannelPipeline(IChannel ch)
    //    {
    //        ch.Pipeline.AddLast(this.CreateTlsHandler(false));
    //        base.SetInitialServerChannelPipeline(ch);
    //    }

    //    protected override void SetInitialChannelPipeline(IChannel ch)
    //    {
    //        ch.Pipeline.AddLast("tls", this.CreateTlsHandler(true));
    //        base.SetInitialChannelPipeline(ch);
    //    }
    //}

    public class LibuvDataCompressionHttp2Test : AbstractDataCompressionHttp2Test
    {
        static LibuvDataCompressionHttp2Test()
        {
            DotNetty.Common.ResourceLeakDetector.Level = Common.ResourceLeakDetector.DetectionLevel.Disabled;
        }

        public LibuvDataCompressionHttp2Test(ITestOutputHelper output) : base(output) { }

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

    //public sealed class TlsSocketDataCompressionHttp2Test : SocketDataCompressionHttp2Test
    //{
    //    public TlsSocketDataCompressionHttp2Test(ITestOutputHelper output) : base(output) { }

    //    protected override void SetInitialServerChannelPipeline(IChannel ch)
    //    {
    //        ch.Pipeline.AddLast(this.CreateTlsHandler(false));
    //        base.SetInitialServerChannelPipeline(ch);
    //    }

    //    protected override void SetInitialChannelPipeline(IChannel ch)
    //    {
    //        ch.Pipeline.AddLast("tls", this.CreateTlsHandler(true));
    //        base.SetInitialChannelPipeline(ch);
    //    }
    //}

    public class SocketDataCompressionHttp2Test : AbstractDataCompressionHttp2Test
    {
        public SocketDataCompressionHttp2Test(ITestOutputHelper output) : base(output) { }

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

    [Collection("BootstrapEnv")]
    public abstract class AbstractDataCompressionHttp2Test : TestBase, IDisposable
    {
        private static readonly AsciiString GET = new AsciiString("GET");
        private static readonly AsciiString POST = new AsciiString("POST");
        private static readonly AsciiString PATH = new AsciiString("/some/path");

        private Mock<IHttp2FrameListener> serverListener;
        private Mock<IHttp2FrameListener> clientListener;

        private IHttp2ConnectionEncoder clientEncoder;
        private ServerBootstrap sb;
        private Bootstrap cb;
        private IChannel serverChannel;
        private IChannel clientChannel;
        private volatile IChannel serverConnectedChannel;
        private CountdownEvent serverLatch;
        private IHttp2Connection serverConnection;
        private IHttp2Connection clientConnection;
        private Http2ConnectionHandler clientHandler;
        private MemoryStream serverOut;

        public AbstractDataCompressionHttp2Test(ITestOutputHelper output)
            : base(output)
        {
            this.serverListener = new Mock<IHttp2FrameListener>();
            this.clientListener = new Mock<IHttp2FrameListener>();

            this.serverListener
                .Setup(x => x.OnHeadersRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<IHttp2Headers>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>()))
                .Callback<IChannelHandlerContext, int, IHttp2Headers, int, bool>((ctx, id, h, p, endOfStream) =>
                {
                    if (endOfStream)
                    {
                        this.serverConnection.Stream(id).Close();
                    }
                });
            this.serverListener
                .Setup(x => x.OnHeadersRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<IHttp2Headers>(),
                    It.IsAny<int>(),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>()))
                .Callback<IChannelHandlerContext, int, IHttp2Headers, int, short, bool, int, bool>((ctx, id, h, sd, w, e, p, endOfStream) =>
                {
                    if (endOfStream)
                    {
                        this.serverConnection.Stream(id).Close();
                    }
                });
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
            if (serverConnectedChannel != null)
            {
                serverConnectedChannel.CloseAsync().GetAwaiter().GetResult();
                this.serverConnectedChannel = null;
            }
            try
            {
                Task.WaitAll(
                    this.sb.Group().ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero),
                    this.sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero),
                    this.cb.Group().ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero));
            }
            catch
            {
                // Ignore RejectedExecutionException(on Azure DevOps)
            }
            this.serverOut?.Close();
        }

        [Fact]
        public async Task JustHeadersNoData()
        {
            await this.BootstrapEnv(0);
            IHttp2Headers headers = new DefaultHttp2Headers() { Method = GET, Path = PATH };
            headers.Set(HttpHeaderNames.ContentEncoding, HttpHeaderValues.Gzip);

            Http2TestUtil.RunInChannel(this.clientChannel, () =>
            {
                this.clientEncoder.WriteHeadersAsync(this.CtxClient(), 3, headers, 0, true, this.NewPromiseClient());
                this.clientHandler.Flush(this.CtxClient());
            });
            this.AwaitServer();
            this.serverListener.Verify(
                x => x.OnHeadersRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 3),
                    It.Is<IHttp2Headers>(v => v.Equals(headers)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true)));
        }

        [Fact]
        public async Task GzipEncodingSingleEmptyMessage()
        {
            string text = "";
            var data = Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(text));
            await this.BootstrapEnv(data.ReadableBytes);
            try
            {
                IHttp2Headers headers = new DefaultHttp2Headers() { Method = POST, Path = PATH };
                headers.Set(HttpHeaderNames.ContentEncoding, HttpHeaderValues.Gzip);

                Http2TestUtil.RunInChannel(this.clientChannel, () =>
                {
                    this.clientEncoder.WriteHeadersAsync(this.CtxClient(), 3, headers, 0, false, this.NewPromiseClient());
                    this.clientEncoder.WriteDataAsync(this.CtxClient(), 3, (IByteBuffer)data.Retain(), 0, true, this.NewPromiseClient());
                    this.clientHandler.Flush(this.CtxClient());
                });
                this.AwaitServer();
                Assert.Equal(text, Encoding.UTF8.GetString(this.serverOut.ToArray()));
            }
            finally
            {
                data.Release();
            }
        }

        [Fact]
        public async Task GzipEncodingSingleMessage()
        {
            string text = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaabbbbbbbbbbbbbbbbbbbbbbbbbbbbbccccccccccccccccccccccc";
            var data = Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(text));
            await this.BootstrapEnv(data.ReadableBytes);
            try
            {
                IHttp2Headers headers = new DefaultHttp2Headers() { Method = POST, Path = PATH };
                headers.Set(HttpHeaderNames.ContentEncoding, HttpHeaderValues.Gzip);

                Http2TestUtil.RunInChannel(this.clientChannel, () =>
                {
                    this.clientEncoder.WriteHeadersAsync(this.CtxClient(), 3, headers, 0, false, this.NewPromiseClient());
                    this.clientEncoder.WriteDataAsync(this.CtxClient(), 3, (IByteBuffer)data.Retain(), 0, true, this.NewPromiseClient());
                    this.clientHandler.Flush(this.CtxClient());
                });
                this.AwaitServer();
                Assert.Equal(text, Encoding.UTF8.GetString(this.serverOut.ToArray()));
            }
            finally
            {
                data.Release();
            }
        }

        [Fact]
        public async Task GzipEncodingMultipleMessages()
        {
            string text1 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaabbbbbbbbbbbbbbbbbbbbbbbbbbbbbccccccccccccccccccccccc";
            string text2 = "dddddddddddddddddddeeeeeeeeeeeeeeeeeeeffffffffffffffffffff";
            var data1 = Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(text1));
            var data2 = Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(text2));
            await this.BootstrapEnv(data1.ReadableBytes + data2.ReadableBytes);
            try
            {
                IHttp2Headers headers = new DefaultHttp2Headers() { Method = POST, Path = PATH };
                headers.Set(HttpHeaderNames.ContentEncoding, HttpHeaderValues.Gzip);

                Http2TestUtil.RunInChannel(this.clientChannel, () =>
                {
                    this.clientEncoder.WriteHeadersAsync(this.CtxClient(), 3, headers, 0, false, this.NewPromiseClient());
                    this.clientEncoder.WriteDataAsync(this.CtxClient(), 3, (IByteBuffer)data1.Retain(), 0, false, this.NewPromiseClient());
                    this.clientEncoder.WriteDataAsync(this.CtxClient(), 3, (IByteBuffer)data2.Retain(), 0, true, this.NewPromiseClient());
                    this.clientHandler.Flush(this.CtxClient());
                });
                this.AwaitServer();
                Assert.Equal(text1 + text2, Encoding.UTF8.GetString(this.serverOut.ToArray()));
            }
            finally
            {
                data1.Release();
                data2.Release();
            }
        }

        [Fact]
        public async Task DeflateEncodingWriteLargeMessage()
        {
            int BUFFER_SIZE = 1 << 12;
            byte[] bytes = new byte[BUFFER_SIZE];
            new Random().NextBytes(bytes);
            await this.BootstrapEnv(BUFFER_SIZE);
            var data = Unpooled.WrappedBuffer(bytes);
            try
            {
                IHttp2Headers headers = new DefaultHttp2Headers() { Method = POST, Path = PATH };
                headers.Set(HttpHeaderNames.ContentEncoding, HttpHeaderValues.Gzip);

                Http2TestUtil.RunInChannel(this.clientChannel, () =>
                {
                    this.clientEncoder.WriteHeadersAsync(this.CtxClient(), 3, headers, 0, false, this.NewPromiseClient());
                    this.clientEncoder.WriteDataAsync(this.CtxClient(), 3, (IByteBuffer)data.Retain(), 0, true, this.NewPromiseClient());
                    this.clientHandler.Flush(this.CtxClient());
                });
                this.AwaitServer();
                Assert.Equal(data.ResetReaderIndex().ToString(Encoding.UTF8), Encoding.UTF8.GetString(this.serverOut.ToArray()));
            }
            finally
            {
                data.Release();
            }
        }

        protected abstract void SetupServerBootstrap(ServerBootstrap bootstrap);

        protected abstract void SetupBootstrap(Bootstrap bootstrap);

        protected virtual void SetInitialServerChannelPipeline(IChannel ch)
        {
            this.serverConnectedChannel = ch;
            var p = ch.Pipeline;
            IHttp2FrameWriter frameWriter = new DefaultHttp2FrameWriter();
            this.serverConnection.Remote.FlowController = new DefaultHttp2RemoteFlowController(this.serverConnection);
            this.serverConnection.Local.FlowController = new DefaultHttp2LocalFlowController(this.serverConnection).FrameWriter(frameWriter);
            IHttp2ConnectionEncoder encoder = new CompressorHttp2ConnectionEncoder(
                    new DefaultHttp2ConnectionEncoder(this.serverConnection, frameWriter));
            IHttp2ConnectionDecoder decoder =
                    new DefaultHttp2ConnectionDecoder(this.serverConnection, encoder, new DefaultHttp2FrameReader());
            Http2ConnectionHandler connectionHandler = new Http2ConnectionHandlerBuilder()
            {
                FrameListener = new DelegatingDecompressorFrameListener(this.serverConnection, this.serverListener.Object)
            }
                .Codec(decoder, encoder).Build();
            p.AddLast(connectionHandler);
        }

        protected virtual void SetInitialChannelPipeline(IChannel ch)
        {
            var p = ch.Pipeline;
            IHttp2FrameWriter frameWriter = new DefaultHttp2FrameWriter();
            this.clientConnection.Remote.FlowController = new DefaultHttp2RemoteFlowController(this.clientConnection);
            this.clientConnection.Local.FlowController = new DefaultHttp2LocalFlowController(this.clientConnection).FrameWriter(frameWriter);
            this.clientEncoder = new CompressorHttp2ConnectionEncoder(
                    new DefaultHttp2ConnectionEncoder(this.clientConnection, frameWriter));

            IHttp2ConnectionDecoder decoder =
                    new DefaultHttp2ConnectionDecoder(this.clientConnection, this.clientEncoder,
                            new DefaultHttp2FrameReader());
            this.clientHandler = new Http2ConnectionHandlerBuilder()
            {
                FrameListener = new DelegatingDecompressorFrameListener(this.clientConnection, this.clientListener.Object),
                // By default tests don't wait for server to gracefully shutdown streams
                GracefulShutdownTimeout = TimeSpan.Zero
            }
                .Codec(decoder, this.clientEncoder).Build();
            p.AddLast(this.clientHandler);
        }

        protected TlsHandler CreateTlsHandler(bool isClient)
        {
            X509Certificate2 tlsCertificate = TestResourceHelper.GetTestCertificate();
            string targetHost = tlsCertificate.GetNameInfo(X509NameType.DnsName, false);
            TlsHandler tlsHandler = isClient ?
                new TlsHandler(new ClientTlsSettings(targetHost).AllowAnyServerCertificate()) :
                new TlsHandler(new ServerTlsSettings(tlsCertificate).AllowAnyClientCertificate());
            return tlsHandler;
        }

        protected virtual int Port => 0;

        private async Task BootstrapEnv(int serverOutSize)
        {
            var prefaceWrittenLatch = new CountdownEvent(1);
            this.serverOut = new MemoryStream(serverOutSize);
            this.serverLatch = new CountdownEvent(1);
            this.sb = new ServerBootstrap();
            this.cb = new Bootstrap();

            // Streams are created before the normal flow for this test, so these connection must be initialized up front.
            this.serverConnection = new DefaultHttp2Connection(true);
            this.clientConnection = new DefaultHttp2Connection(false);

            this.serverConnection.AddListener(new TestHttp2ConnectionAdapter(this.serverLatch));

            this.serverListener
                .Setup(x => x.OnDataRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>()))
                .Returns<IChannelHandlerContext, int, IByteBuffer, int, bool>((ctx, id, buf, padding, end) =>
                {
                    int processedBytes = buf.ReadableBytes + padding;

                    buf.ReadBytes(this.serverOut, buf.ReadableBytes);
                    if (end)
                    {
                        this.serverConnection.Stream(id).Close();
                    }
                    return processedBytes;
                });
            var serverChannelLatch = new CountdownEvent(1);

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

            var loopback = IPAddress.IPv6Loopback;
            this.serverChannel = await this.sb.BindAsync(loopback, Port);

            var port = ((IPEndPoint)this.serverChannel.LocalAddress).Port;
            var ccf = this.cb.ConnectAsync(loopback, port);
            this.clientChannel = await ccf;
            Assert.True(prefaceWrittenLatch.Wait(TimeSpan.FromSeconds(10)));
            Assert.True(serverChannelLatch.Wait(TimeSpan.FromSeconds(10)));
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

        class TestHttp2ConnectionAdapter : Http2ConnectionAdapter
        {
            readonly CountdownEvent serverLatch;

            public TestHttp2ConnectionAdapter(CountdownEvent serverLatch)
            {
                this.serverLatch = serverLatch;
            }

            public override void OnStreamClosed(IHttp2Stream stream)
            {
                this.serverLatch.SafeSignal();
            }
        }

        private void AwaitServer()
        {
            Assert.True(this.serverLatch.Wait(TimeSpan.FromSeconds(5)));
            this.serverOut.Flush();
        }

        private IChannelHandlerContext CtxClient()
        {
            return this.clientChannel.Pipeline.FirstContext();
        }

        private IPromise NewPromiseClient()
        {
            return this.CtxClient().NewPromise();
        }
    }
}
