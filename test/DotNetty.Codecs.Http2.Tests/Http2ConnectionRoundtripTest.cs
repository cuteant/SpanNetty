
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
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

    public sealed class LibuvHttp2ConnectionRoundtripTest : AbstractHttp2ConnectionRoundtripTest
    {
        static LibuvHttp2ConnectionRoundtripTest()
        {
            DotNetty.Common.ResourceLeakDetector.Level = Common.ResourceLeakDetector.DetectionLevel.Disabled;
        }

        public LibuvHttp2ConnectionRoundtripTest(ITestOutputHelper output) : base(output) { }

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

        [Fact(Skip = "not yet supported")]
        public override void FlowControlProperlyChunksLargeMessage()
        {
        }

        [Fact(Skip = "not yet supported")]
        public override void StressTest()
        {
        }
    }

    public sealed class SocketHttp2ConnectionRoundtripTest : AbstractHttp2ConnectionRoundtripTest
    {
        public SocketHttp2ConnectionRoundtripTest(ITestOutputHelper output) : base(output) { }

        protected override void SetupServerBootstrap(ServerBootstrap bootstrap)
        {
            bootstrap.Group(new MultithreadEventLoopGroup(1), new MultithreadEventLoopGroup())
                     .Channel<TcpServerSocketChannel>();
        }

        protected override void SetupBootstrap(Bootstrap bootstrap)
        {
            bootstrap.Group(new MultithreadEventLoopGroup()).Channel<TcpSocketChannel>();
        }

        [Fact(Skip = "slow")] // TODO https://github.com/cuteant/SpanNetty/issues/66
        public override void WriteOfEmptyReleasedBufferSingleBufferQueuedInFlowControllerShouldFail()
        {
            base.WriteOfEmptyReleasedBufferSingleBufferQueuedInFlowControllerShouldFail();
        }

        [Fact(Skip = "slow")]
        public override void StressTest()
        {
        }
    }

    public sealed class LocalHttp2ConnectionRoundtripTest : AbstractHttp2ConnectionRoundtripTest
    {
        public LocalHttp2ConnectionRoundtripTest(ITestOutputHelper output) : base(output) { }

        protected override void SetupServerBootstrap(ServerBootstrap bootstrap)
        {
            bootstrap.Group(new DefaultEventLoopGroup(1), new DefaultEventLoopGroup()).Channel<LocalServerChannel>();
        }

        protected override void SetupBootstrap(Bootstrap bootstrap)
        {
            bootstrap.Group(new DefaultEventLoopGroup()).Channel<LocalChannel>();
        }

        protected override void StartBootstrap()
        {
            _serverChannel = _sb.BindAsync(new LocalAddress("Http2ConnectionRoundtripTest")).GetAwaiter().GetResult();

            var ccf = _cb.ConnectAsync(_serverChannel.LocalAddress);
            _clientChannel = ccf.GetAwaiter().GetResult();
        }
    }

    [Collection("BootstrapEnv")]
    public abstract class AbstractHttp2ConnectionRoundtripTest : TestBase, IDisposable
    {
        private const long DEFAULT_AWAIT_TIMEOUT_SECONDS = 15;

        private Mock<IHttp2FrameListener> _clientListener;
        private Mock<IHttp2FrameListener> _serverListener;

        private Http2ConnectionHandler _http2Client;
        private Http2ConnectionHandler _http2Server;
        protected ServerBootstrap _sb;
        protected Bootstrap _cb;
        protected IChannel _serverChannel;
        private volatile IChannel _serverConnectedChannel;
        protected IChannel _clientChannel;
        private Http2TestUtil.FrameCountDown _serverFrameCountDown;
        private CountdownEvent _requestLatch;
        private CountdownEvent _serverSettingsAckLatch;
        private CountdownEvent _dataLatch;
        private CountdownEvent _trailersLatch;
        private CountdownEvent _goAwayLatch;

        public AbstractHttp2ConnectionRoundtripTest(ITestOutputHelper output)
            : base(output)
        {
            _clientListener = new Mock<IHttp2FrameListener>();
            _serverListener = new Mock<IHttp2FrameListener>();

            MockFlowControl(_clientListener);
            MockFlowControl(_serverListener);
        }

        public void Dispose()
        {
            if (_clientChannel != null)
            {
                _clientChannel.CloseAsync().GetAwaiter().GetResult();
                _clientChannel = null;
            }
            if (_serverChannel != null)
            {
                _serverChannel.CloseAsync().GetAwaiter().GetResult();
                _serverChannel = null;
            }
            var serverConnectedChannel = _serverConnectedChannel;
            if (serverConnectedChannel != null)
            {
                serverConnectedChannel.CloseAsync().GetAwaiter().GetResult();
                _serverConnectedChannel = null;
            }
            try
            {
                Task.WaitAll(
                    _sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    _sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    _cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
            catch
            {
                // Ignore RejectedExecutionException(on Azure DevOps)
            }
        }

        [Fact]
        public void InflightFrameAfterStreamResetShouldNotMakeConnectionUnusable()
        {
            BootstrapEnv(1, 1, 2, 1);
            var latch = new CountdownEvent(1);
            _serverListener
                .Setup(x => x.OnHeadersRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<IHttp2Headers>(),
                    It.IsAny<int>(),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>()))
                .Callback<IChannelHandlerContext, int, IHttp2Headers, int, short, bool, int, bool>((ctx, id, h, sd, w, e, pad, end) =>
                {
                    _http2Server.Encoder.WriteHeadersAsync(ctx, id, h, 0, false, ctx.NewPromise());
                    _http2Server.Flush(ctx);
                });
            _clientListener
                .Setup(x => x.OnHeadersRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 5),
                    It.IsAny<IHttp2Headers>(),
                    It.IsAny<int>(),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>()))
                .Callback<IChannelHandlerContext, int, IHttp2Headers, int, short, bool, int, bool>((ctx, id, h, sd, w, e, pad, end) =>
                {
                    latch.SafeSignal();
                });

            // Create a single stream by sending a HEADERS frame to the server.
            short weight = 16;
            IHttp2Headers headers = DummyHeaders();
            Http2TestUtil.RunInChannel(_clientChannel, () =>
            {
                _http2Client.Encoder.WriteHeadersAsync(Ctx(), 3, headers, 0, weight, false, 0, false, NewPromise());
                _http2Client.Flush(Ctx());
                _http2Client.Encoder.WriteRstStreamAsync(Ctx(), 3, Http2Error.InternalError, NewPromise());
                _http2Client.Flush(Ctx());
            });
            Http2TestUtil.RunInChannel(_clientChannel, () =>
            {
                _http2Client.Encoder.WriteHeadersAsync(Ctx(), 5, headers, 0, weight, false, 0, false, NewPromise());
                _http2Client.Flush(Ctx());
            });

            Assert.True(latch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
        }

        [Fact]
        public void HeadersWithEndStreamShouldNotSendError()
        {
            BootstrapEnv(1, 1, 2, 1);

            // Create a single stream by sending a HEADERS frame to the server.
            short weight = 16;
            IHttp2Headers headers = DummyHeaders();
            Http2TestUtil.RunInChannel(_clientChannel, () =>
            {
                _http2Client.Encoder.WriteHeadersAsync(Ctx(), 3, headers, 0, weight, false, 0, true, NewPromise());
                _http2Client.Flush(Ctx());
            });

            Assert.True(_requestLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            _serverListener.Verify(
                x => x.OnHeadersRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 3),
                    It.Is<IHttp2Headers>(v => v.Equals(headers)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == weight),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true)));
            // Wait for some time to see if a go_away or reset frame will be received.
            Thread.Sleep(1000);

            // Verify that no errors have been received.
            _serverListener.Verify(
                x => x.OnGoAwayRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>()), Times.Never());
            _serverListener.Verify(
                x => x.OnRstStreamRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>()), Times.Never());

            // The server will not respond, and so don't wait for graceful shutdown
            SetClientGracefulShutdownTime(0);
        }

        [Fact]
        public void EncodeViolatesMaxHeaderListSizeCanStillUseConnection()
        {
            BootstrapEnv(1, 2, 1, 0, 0);

            CountdownEvent serverSettingsAckLatch1 = new CountdownEvent(2);
            CountdownEvent serverSettingsAckLatch2 = new CountdownEvent(3);
            CountdownEvent clientSettingsLatch1 = new CountdownEvent(3);
            CountdownEvent serverRevHeadersLatch = new CountdownEvent(1);
            CountdownEvent clientHeadersLatch = new CountdownEvent(1);
            CountdownEvent clientDataWrite = new CountdownEvent(1);
            AtomicReference<Exception> clientHeadersWriteException = new AtomicReference<Exception>();
            AtomicReference<Exception> clientHeadersWriteException2 = new AtomicReference<Exception>();
            AtomicReference<Exception> clientDataWriteException = new AtomicReference<Exception>();

            IHttp2Headers headers = DummyHeaders();

            _serverListener
                .Setup(x => x.OnSettingsAckRead(It.IsAny<IChannelHandlerContext>()))
                .Callback<IChannelHandlerContext>(ctx =>
                {
                    serverSettingsAckLatch1.SafeSignal();
                    serverSettingsAckLatch2.SafeSignal();
                });
            _clientListener
                .Setup(x => x.OnSettingsRead(It.IsAny<IChannelHandlerContext>(), It.IsAny<Http2Settings>()))
                .Callback<IChannelHandlerContext, Http2Settings>((ctx, s) =>
                {
                    clientSettingsLatch1.SafeSignal();
                });

            // Manually add a listener for when we receive the expected headers on the server.
            _serverListener
                .Setup(x => x.OnHeadersRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 5),
                    It.Is<IHttp2Headers>(v => v.Equals(headers)),
                    It.IsAny<int>(),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true)))
                .Callback<IChannelHandlerContext, int, IHttp2Headers, int, short, bool, int, bool>((ctx, id, h, sd, w, ex, p, end) =>
                {
                    serverRevHeadersLatch.SafeSignal();
                });

            // Set the maxHeaderListSize to 100 so we may be able to write some headers, but not all. We want to verify
            // that we don't corrupt state if some can be written but not all.
            Http2TestUtil.RunInChannel(_serverConnectedChannel, () =>
            {
                _http2Server.Encoder.WriteSettingsAsync(
                    ServerCtx(),
                    new Http2Settings().CopyFrom(_http2Server.Decoder.LocalSettings).MaxHeaderListSize(100),
                    ServerNewPromise());
                _http2Server.Flush(ServerCtx());
            });

            Assert.True(serverSettingsAckLatch1.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));

            Http2TestUtil.RunInChannel(_clientChannel, () =>
            {
                _http2Client.Encoder
                    .WriteHeadersAsync(Ctx(), 3, headers, 0, false, NewPromise())
                    .ContinueWith(t => clientHeadersWriteException.Value = t.Exception.InnerException, TaskContinuationOptions.ExecuteSynchronously);

                // It is expected that this write should fail locally and the remote peer will never see this.
                _http2Client.Encoder
                    .WriteDataAsync(Ctx(), 3, Unpooled.Buffer(), 0, true, NewPromise())
                    .ContinueWith(t =>
                    {
                        clientDataWriteException.Value = t.Exception.InnerException;
                        clientDataWrite.SafeSignal();
                    }, TaskContinuationOptions.ExecuteSynchronously);
                _http2Client.Flush(Ctx());
            });

            Assert.True(clientDataWrite.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            Assert.NotNull(clientHeadersWriteException.Value); // Header encode should have exceeded maxHeaderListSize!
            Assert.NotNull(clientDataWriteException.Value); // Data on closed stream should fail!

            // Set the maxHeaderListSize to the max value so we can send the headers.
            Http2TestUtil.RunInChannel(_serverConnectedChannel, () =>
            {
                _http2Server.Encoder.WriteSettingsAsync(
                    ServerCtx(),
                    new Http2Settings().CopyFrom(_http2Server.Decoder.LocalSettings).MaxHeaderListSize(Http2CodecUtil.MaxHeaderListSize),
                    ServerNewPromise());
                _http2Server.Flush(ServerCtx());
            });

            Assert.True(clientSettingsLatch1.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            Assert.True(serverSettingsAckLatch2.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));

            Http2TestUtil.RunInChannel(_clientChannel, () =>
            {
                _http2Client.Encoder
                    .WriteHeadersAsync(Ctx(), 5, headers, 0, true, NewPromise())
                    .ContinueWith(t =>
                    {
                        clientHeadersWriteException2.Value = t.Exception?.InnerException;
                        clientHeadersLatch.SafeSignal();
                    }, TaskContinuationOptions.ExecuteSynchronously);
                _http2Client.Flush(Ctx());
            });

            Assert.True(clientHeadersLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            Assert.Null(clientHeadersWriteException2.Value); // "Client write of headers should succeed with increased header list size!"
            Assert.True(serverRevHeadersLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));

            _serverListener.Verify(
                x => x.OnDataRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>()), Times.Never());

            // Verify that no errors have been received.
            _serverListener.Verify(
                x => x.OnGoAwayRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>()), Times.Never());
            _serverListener.Verify(
                x => x.OnRstStreamRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>()), Times.Never());
            _clientListener.Verify(
                x => x.OnGoAwayRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>()), Times.Never());
            _clientListener.Verify(
                x => x.OnRstStreamRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>()), Times.Never());
        }

        [Fact]
        public void SettingsAckIsSentBeforeUsingFlowControl()
        {
            BootstrapEnv(1, 1, 1, 1);

            var serverSettingsAckLatch1 = new CountdownEvent(1);
            var serverSettingsAckLatch2 = new CountdownEvent(2);
            var serverDataLatch = new CountdownEvent(1);
            var clientWriteDataLatch = new CountdownEvent(1);
            byte[] data = new byte[] { 1, 2, 3, 4, 5 };
            var output = new ByteBufferStream(Unpooled.Buffer(data.Length));

            _serverListener
                .Setup(x => x.OnSettingsAckRead(It.IsAny<IChannelHandlerContext>()))
                .Callback<IChannelHandlerContext>(ctx =>
                {
                    serverSettingsAckLatch1.SafeSignal();
                    serverSettingsAckLatch2.SafeSignal();
                });
            _serverListener
                .Setup(x => x.OnDataRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 3),
                    It.IsAny<IByteBuffer>(),
                    It.Is<int>(v => v == 0),
                    It.IsAny<bool>()))
                .Returns<IChannelHandlerContext, int, IByteBuffer, int, bool>((ctx, id, buf, padding, e) =>
                {
                    int processedBytes = buf.ReadableBytes + padding;

                    buf.ReadBytes(output, buf.ReadableBytes);
                    serverDataLatch.SafeSignal();
                    return processedBytes;
                });

            IHttp2Headers headers = DummyHeaders();

            // The server initially reduces the connection flow control window to 0.
            Http2TestUtil.RunInChannel(_serverConnectedChannel, () =>
            {
                _http2Server.Encoder.WriteSettingsAsync(ServerCtx(),
                        new Http2Settings().CopyFrom(_http2Server.Decoder.LocalSettings)
                                .InitialWindowSize(0),
                        ServerNewPromise());
                _http2Server.Flush(ServerCtx());
            });

            Assert.True(serverSettingsAckLatch1.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));

            // The client should now attempt to send data, but the window size is 0 so it will be queued in the flow
            // controller.
            Http2TestUtil.RunInChannel(_clientChannel, () =>
            {
                _http2Client.Encoder.WriteHeadersAsync(Ctx(), 3, headers, 0, (short)16, false, 0, false, NewPromise());
                _http2Client.Encoder.WriteDataAsync(Ctx(), 3, Unpooled.WrappedBuffer(data), 0, true, NewPromise());
                _http2Client.Flush(Ctx());
                clientWriteDataLatch.SafeSignal();
            });

            Assert.True(clientWriteDataLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));

            // Now the server opens up the connection window to allow the client to send the pending data.
            Http2TestUtil.RunInChannel(_serverConnectedChannel, () =>
            {
                _http2Server.Encoder.WriteSettingsAsync(ServerCtx(),
                        new Http2Settings().CopyFrom(_http2Server.Decoder.LocalSettings)
                                .InitialWindowSize(data.Length),
                        ServerNewPromise());
                _http2Server.Flush(ServerCtx());
            });

            Assert.True(serverSettingsAckLatch2.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            Assert.True(serverDataLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            Assert.Equal(data, ByteBufferUtil.GetBytes(output.Buffer));

            // Verify that no errors have been received.
            _serverListener.Verify(
                x => x.OnGoAwayRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>()), Times.Never());
            _serverListener.Verify(
                x => x.OnRstStreamRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>()), Times.Never());
            _clientListener.Verify(
                x => x.OnGoAwayRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>()), Times.Never());
            _clientListener.Verify(
                x => x.OnRstStreamRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>()), Times.Never());
        }

        [Fact]
        public void PriorityUsingHigherValuedStreamIdDoesNotPreventUsingLowerStreamId()
        {
            BootstrapEnv(1, 1, 2, 0);

            IHttp2Headers headers = DummyHeaders();
            Http2TestUtil.RunInChannel(_clientChannel, () =>
            {
                _http2Client.Encoder.WritePriorityAsync(Ctx(), 5, 3, 14, false, NewPromise());
                _http2Client.Encoder.WriteHeadersAsync(Ctx(), 3, headers, 0, 16, false, 0, false, NewPromise());
                _http2Client.Flush(Ctx());
            });

            Assert.True(_serverSettingsAckLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            Assert.True(_requestLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));

            _serverListener.Verify(
                x => x.OnPriorityRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 5),
                    It.Is<int>(v => v == 3),
                    It.Is<short>(v => v == 14),
                    It.Is<bool>(v => v == false)));
            _serverListener.Verify(
                x => x.OnHeadersRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 3),
                    It.Is<IHttp2Headers>(v => v.Equals(headers)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == 16),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false)));

            // Verify that no errors have been received.
            _serverListener.Verify(
                x => x.OnGoAwayRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>()), Times.Never());
            _serverListener.Verify(
                x => x.OnRstStreamRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>()), Times.Never());
            _clientListener.Verify(
                x => x.OnGoAwayRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>()), Times.Never());
            _clientListener.Verify(
                x => x.OnRstStreamRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>()), Times.Never());
        }

        [Fact]
        public void HeadersUsingHigherValuedStreamIdPreventsUsingLowerStreamId()
        {
            BootstrapEnv(1, 1, 1, 0);

            IHttp2Headers headers = DummyHeaders();
            Http2TestUtil.RunInChannel(_clientChannel, () =>
            {
                _http2Client.Encoder.WriteHeadersAsync(Ctx(), 5, headers, 0, (short)16, false, 0, false,
                        NewPromise());
                _http2Client.Encoder.FrameWriter.WriteHeadersAsync(Ctx(), 3, headers, 0, (short)16, false, 0, false,
                        NewPromise());
                _http2Client.Flush(Ctx());
            });

            Assert.True(_serverSettingsAckLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            Assert.True(_requestLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));

            _serverListener.Verify(
                x => x.OnHeadersRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 5),
                    It.Is<IHttp2Headers>(v => v.Equals(headers)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == 16),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false)));
            _serverListener.Verify(
                x => x.OnHeadersRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 3),
                    It.IsAny<IHttp2Headers>(),
                    It.IsAny<int>(),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>()), Times.Never());

            // Client should receive a RST_STREAM for stream 3, but there is not Http2Stream object so the listener is never
            // notified.
            _serverListener.Verify(
                x => x.OnGoAwayRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>()), Times.Never());
            _serverListener.Verify(
                x => x.OnRstStreamRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>()), Times.Never());
            _clientListener.Verify(
                x => x.OnGoAwayRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>()), Times.Never());
            _clientListener.Verify(
                x => x.OnRstStreamRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>()), Times.Never());
        }

        [Fact]
        public void HeadersWriteForPeerStreamWhichWasResetShouldNotGoAway()
        {
            BootstrapEnv(1, 1, 1, 0);

            var serverGotRstLatch = new CountdownEvent(1);
            var serverWriteHeadersLatch = new CountdownEvent(1);
            var serverWriteHeadersCauseRef = new AtomicReference<Exception>();

            int streamId = 3;

            _serverListener
                .Setup(x => x.OnRstStreamRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == streamId),
                    It.IsAny<Http2Error>()))
                .Callback<IChannelHandlerContext, int, Http2Error>((ctx, id, err) =>
                {
                    if (streamId == id)
                    {
                        serverGotRstLatch.SafeSignal();
                    }
                });

            IHttp2Headers headers = DummyHeaders();
            Http2TestUtil.RunInChannel(_clientChannel, () =>
            {
                _http2Client.Encoder.WriteHeadersAsync(Ctx(), streamId, headers, Http2CodecUtil.ConnectionStreamId,
                        Http2CodecUtil.DefaultPriorityWeight, false, 0, false, NewPromise());
                _http2Client.Encoder.WriteRstStreamAsync(Ctx(), streamId, Http2Error.Cancel, NewPromise());
                _http2Client.Flush(Ctx());
            });

            Assert.True(_serverSettingsAckLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            Assert.True(serverGotRstLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));

            _serverListener.Verify(
                x => x.OnHeadersRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => v.Equals(headers)),
                    It.IsAny<int>(),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.Is<bool>(v => v == false)));

            // Now have the server attempt to send a headers frame simulating some asynchronous work.
            Http2TestUtil.RunInChannel(_serverConnectedChannel, () =>
            {
                _http2Server.Encoder
                        .WriteHeadersAsync(ServerCtx(), streamId, headers, 0, true, ServerNewPromise())
                        .ContinueWith(t =>
                        {
                            serverWriteHeadersCauseRef.Value = t.Exception.InnerException;
                            serverWriteHeadersLatch.SafeSignal();
                        }, TaskContinuationOptions.ExecuteSynchronously);

                _http2Server.Flush(ServerCtx());
            });

            Assert.True(serverWriteHeadersLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            var serverWriteHeadersCause = serverWriteHeadersCauseRef.Value;
            Assert.NotNull(serverWriteHeadersCause);
            Assert.Null(serverWriteHeadersCause as Http2Exception);

            // Server should receive a RST_STREAM for stream 3.
            _serverListener.Verify(
                x => x.OnGoAwayRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>()), Times.Never());
            _clientListener.Verify(
                x => x.OnGoAwayRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>()), Times.Never());
            _clientListener.Verify(
                x => x.OnRstStreamRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>()), Times.Never());
        }

        sealed class Http2ExceptionChannelHandlerAdapter : ChannelHandlerAdapter
        {
            public override void HandlerAdded(IChannelHandlerContext context)
            {
                throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "Fake Exception");
            }
        }
        [Fact]
        public void Http2ExceptionInPipelineShouldCloseConnection()
        {
            BootstrapEnv(1, 1, 2, 1);

            // Create a latch to track when the close occurs.
            var closeLatch = new CountdownEvent(1);
            _clientChannel.CloseCompletion.ContinueWith(t =>
            {
                closeLatch.SafeSignal();
            }, TaskContinuationOptions.ExecuteSynchronously);

            // Create a single stream by sending a HEADERS frame to the server.
            IHttp2Headers headers = DummyHeaders();
            Http2TestUtil.RunInChannel(_clientChannel, () =>
            {
                _http2Client.Encoder.WriteHeadersAsync(Ctx(), 3, headers, 0, (short)16, false, 0, false,
                        NewPromise());
                _http2Client.Flush(Ctx());
            });

            // Wait for the server to create the stream.
            Assert.True(_serverSettingsAckLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            Assert.True(_requestLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));

            // Add a handler that will immediately throw an exception.
            _clientChannel.Pipeline.AddFirst(new Http2ExceptionChannelHandlerAdapter());

            // Wait for the close to occur.
            Assert.True(closeLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            Assert.False(_clientChannel.IsOpen);
        }

        [Fact]
        public void ListenerExceptionShouldCloseConnection()
        {
            IHttp2Headers headers = DummyHeaders();
            _serverListener
                .Setup(x => x.OnHeadersRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 3),
                    It.Is<IHttp2Headers>(v => v.Equals(headers)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == 16),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false)))
                .Throws(new Http2RuntimeException("Fake Exception"));

            BootstrapEnv(1, 0, 1, 1);

            // Create a latch to track when the close occurs.
            var closeLatch = new CountdownEvent(1);
            _clientChannel.CloseCompletion.ContinueWith(t =>
            {
                closeLatch.SafeSignal();
            }, TaskContinuationOptions.ExecuteSynchronously);

            // Create a single stream by sending a HEADERS frame to the server.
            Http2TestUtil.RunInChannel(_clientChannel, () =>
            {
                _http2Client.Encoder.WriteHeadersAsync(Ctx(), 3, headers, 0, (short)16, false, 0, false,
                        NewPromise());
                _http2Client.Flush(Ctx());
            });

            // Wait for the server to create the stream.
            Assert.True(_serverSettingsAckLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            Assert.True(_requestLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));

            // Wait for the close to occur.
            Assert.True(closeLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            Assert.False(_clientChannel.IsOpen);
        }

        enum WriteEmptyBufferMode
        {
            SINGLE_END_OF_STREAM,
            SECOND_END_OF_STREAM,
            SINGLE_WITH_TRAILERS,
            SECOND_WITH_TRAILERS
        }

        [Fact]
        public virtual void WriteOfEmptyReleasedBufferSingleBufferQueuedInFlowControllerShouldFail()
        {
            WriteOfEmptyReleasedBufferQueuedInFlowControllerShouldFail(WriteEmptyBufferMode.SINGLE_END_OF_STREAM);
        }

        [Fact]
        public void WriteOfEmptyReleasedBufferSingleBufferTrailersQueuedInFlowControllerShouldFail()
        {
            WriteOfEmptyReleasedBufferQueuedInFlowControllerShouldFail(WriteEmptyBufferMode.SINGLE_WITH_TRAILERS);
        }

        [Fact]
        public void WriteOfEmptyReleasedBufferMultipleBuffersQueuedInFlowControllerShouldFail()
        {
            WriteOfEmptyReleasedBufferQueuedInFlowControllerShouldFail(WriteEmptyBufferMode.SECOND_END_OF_STREAM);
        }

        [Fact]
        public void WriteOfEmptyReleasedBufferMultipleBuffersTrailersQueuedInFlowControllerShouldFail()
        {
            WriteOfEmptyReleasedBufferQueuedInFlowControllerShouldFail(WriteEmptyBufferMode.SECOND_WITH_TRAILERS);
        }

        private async void WriteOfEmptyReleasedBufferQueuedInFlowControllerShouldFail(WriteEmptyBufferMode mode)
        {
            BootstrapEnv(1, 1, 2, 1);

            var emptyDataPromise = NewPromise();
            Http2TestUtil.RunInChannel(_clientChannel, () =>
            {
                _http2Client.Encoder.WriteHeadersAsync(Ctx(), 3, EmptyHttp2Headers.Instance, 0, (short)16, false, 0, false,
                        NewPromise());
                IByteBuffer emptyBuf = Unpooled.Buffer();
                emptyBuf.Release();
                switch (mode)
                {
                    case WriteEmptyBufferMode.SINGLE_END_OF_STREAM:
                        _http2Client.Encoder.WriteDataAsync(Ctx(), 3, emptyBuf, 0, true, emptyDataPromise);
                        break;
                    case WriteEmptyBufferMode.SECOND_END_OF_STREAM:
                        _http2Client.Encoder.WriteDataAsync(Ctx(), 3, emptyBuf, 0, false, emptyDataPromise);
                        _http2Client.Encoder.WriteDataAsync(Ctx(), 3, RandomBytes(8), 0, true, NewPromise());
                        break;
                    case WriteEmptyBufferMode.SINGLE_WITH_TRAILERS:
                        _http2Client.Encoder.WriteDataAsync(Ctx(), 3, emptyBuf, 0, false, emptyDataPromise);
                        _http2Client.Encoder.WriteHeadersAsync(Ctx(), 3, EmptyHttp2Headers.Instance, 0,
                                (short)16, false, 0, true, NewPromise());
                        break;
                    case WriteEmptyBufferMode.SECOND_WITH_TRAILERS:
                        _http2Client.Encoder.WriteDataAsync(Ctx(), 3, emptyBuf, 0, false, emptyDataPromise);
                        _http2Client.Encoder.WriteDataAsync(Ctx(), 3, RandomBytes(8), 0, false, NewPromise());
                        _http2Client.Encoder.WriteHeadersAsync(Ctx(), 3, EmptyHttp2Headers.Instance, 0,
                                (short)16, false, 0, true, NewPromise());
                        break;
                    default:
                        throw new Exception();
                }
                _http2Client.Flush(Ctx());
            });

            try
            {
                await emptyDataPromise.Task;
            }
            catch (Exception exc)
            {
                Assert.IsType<IllegalReferenceCountException>(exc);
            }
        }

        sealed class WriteFailureFlowControllerRemoveFrameHandlerAdapter : ChannelHandlerAdapter
        {
            private readonly Http2ConnectionHandler _http2Client;

            public WriteFailureFlowControllerRemoveFrameHandlerAdapter(Http2ConnectionHandler http2Client)
            {
                _http2Client = http2Client;
            }

            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                ReferenceCountUtil.Release(message);

                // Ensure we update the window size so we will try to write the rest of the frame while
                // processing the flush.
                _http2Client.Encoder.FlowController.SetInitialWindowSize(8);
                promise.SetException(new InvalidOperationException());
            }
        }
        [Fact]
        public async Task WriteFailureFlowControllerRemoveFrame()
        {
            BootstrapEnv(1, 1, 2, 1);

            var dataPromise = NewPromise();
            var assertPromise = NewPromise();
            Http2TestUtil.RunInChannel(_clientChannel, () =>
            {
                _http2Client.Encoder.WriteHeadersAsync(Ctx(), 3, EmptyHttp2Headers.Instance, 0, (short)16, false, 0, false, NewPromise());
                _clientChannel.Pipeline.AddFirst(new WriteFailureFlowControllerRemoveFrameHandlerAdapter(_http2Client));

                _http2Client.Encoder.FlowController.SetInitialWindowSize(4);
                _http2Client.Encoder.WriteDataAsync(Ctx(), 3, RandomBytes(8), 0, false, dataPromise);
                Assert.True(_http2Client.Encoder.FlowController
                        .HasFlowControlled(_http2Client.Connection.Stream(3)));

                _http2Client.Flush(Ctx());

                try
                {
                    // The Frame should have been removed after the write failed.
                    Assert.False(_http2Client.Encoder.FlowController
                            .HasFlowControlled(_http2Client.Connection.Stream(3)));
                    assertPromise.Complete();
                }
                catch (Exception error)
                {
                    assertPromise.SetException(error);
                }
            });

            try
            {
                await dataPromise.Task;
            }
            catch (Exception exc)
            {
                Assert.IsType<InvalidOperationException>(exc);
            }
        }

        sealed class Http2RuntimeExceptionChannelHandlerAdapter : ChannelHandlerAdapter
        {
            public override void HandlerAdded(IChannelHandlerContext context)
            {
                throw new Http2RuntimeException("Fake Exception");
            }
        }
        [Fact]
        public void NonHttp2ExceptionInPipelineShouldNotCloseConnection()
        {
            BootstrapEnv(1, 1, 2, 1);

            // Create a latch to track when the close occurs.
            var closeLatch = new CountdownEvent(1);
            _clientChannel.CloseCompletion.ContinueWith(t =>
            {
                closeLatch.SafeSignal();
            }, TaskContinuationOptions.ExecuteSynchronously);

            // Create a single stream by sending a HEADERS frame to the server.
            IHttp2Headers headers = DummyHeaders();
            Http2TestUtil.RunInChannel(_clientChannel, () =>
            {
                _http2Client.Encoder.WriteHeadersAsync(Ctx(), 3, headers, 0, (short)16, false, 0, false,
                        NewPromise());
                _http2Client.Flush(Ctx());
            });

            // Wait for the server to create the stream.
            Assert.True(_serverSettingsAckLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            Assert.True(_requestLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));

            // Add a handler that will immediately throw an exception.
            _clientChannel.Pipeline.AddFirst(new Http2RuntimeExceptionChannelHandlerAdapter());

            // The close should NOT occur.
            Assert.False(closeLatch.Wait(TimeSpan.FromSeconds(2)));
            Assert.True(_clientChannel.IsOpen);

            // Set the timeout very low because we know graceful shutdown won't complete
            SetClientGracefulShutdownTime(0);
        }

        [Fact]
        public void NoMoreStreamIdsShouldSendGoAway()
        {
            BootstrapEnv(1, 1, 3, 1, 1);

            // Don't wait for the server to close streams
            SetClientGracefulShutdownTime(0);

            // Create a single stream by sending a HEADERS frame to the server.
            IHttp2Headers headers = DummyHeaders();
            Http2TestUtil.RunInChannel(_clientChannel, () =>
            {
                _http2Client.Encoder.WriteHeadersAsync(Ctx(), 3, headers, 0, (short)16, false, 0,
                        true, NewPromise());
                _http2Client.Flush(Ctx());
            });

            Assert.True(_serverSettingsAckLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));

            Http2TestUtil.RunInChannel(_clientChannel, () =>
            {
                var errId = (uint)int.MaxValue + 1;
                _http2Client.Encoder.WriteHeadersAsync(Ctx(), (int)errId, headers, 0, (short)16, false, 0,
                        true, NewPromise());
                _http2Client.Flush(Ctx());
            });

            Assert.True(_goAwayLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            _serverListener.Verify(
                x => x.OnGoAwayRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 0),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.IsAny<IByteBuffer>()));
        }

        [Fact]
        public void CreateStreamAfterReceiveGoAwayShouldNotSendGoAway()
        {
            BootstrapEnv(1, 1, 2, 1, 1);

            // We want both sides to do graceful shutdown during the test.
            SetClientGracefulShutdownTime(10000);
            SetServerGracefulShutdownTime(10000);

            var clientGoAwayLatch = new CountdownEvent(1);
            _clientListener
                .Setup(x => x.OnGoAwayRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>()))
                .Callback<IChannelHandlerContext, int, Http2Error, IByteBuffer>((ctx, id, err, buf) =>
                {
                    clientGoAwayLatch.SafeSignal();
                });

            // Create a single stream by sending a HEADERS frame to the server.
            IHttp2Headers headers = DummyHeaders();
            Http2TestUtil.RunInChannel(_clientChannel, () =>
            {
                _http2Client.Encoder.WriteHeadersAsync(Ctx(), 3, headers, 0, (short)16, false, 0,
                        false, NewPromise());
                _http2Client.Flush(Ctx());
            });

            Assert.True(_serverSettingsAckLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));

            // Server has received the headers, so the stream is open
            Assert.True(_requestLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));

            Http2TestUtil.RunInChannel(_serverChannel, () =>
            {
                _http2Server.Encoder.WriteGoAwayAsync(ServerCtx(), 3, Http2Error.NoError, Unpooled.Empty, ServerNewPromise());
                _http2Server.Flush(ServerCtx());
            });

            // wait for the client to receive the GO_AWAY.
            Assert.True(clientGoAwayLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            _clientListener.Verify(
                x => x.OnGoAwayRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 3),
                    It.Is<Http2Error>(v => v == Http2Error.NoError),
                    It.IsAny<IByteBuffer>()));

            var clientWriteAfterGoAwayFutureRef = new AtomicReference<Task>();
            var clientWriteAfterGoAwayLatch = new CountdownEvent(1);
            Http2TestUtil.RunInChannel(_clientChannel, () =>
            {
                var f = _http2Client.Encoder.WriteHeadersAsync(Ctx(), 5, headers, 0, (short)16, false, 0,
                        true, NewPromise());
                clientWriteAfterGoAwayFutureRef.Value = f;
                _http2Client.Flush(Ctx());
                f.ContinueWith(t => clientWriteAfterGoAwayLatch.SafeSignal(), TaskContinuationOptions.ExecuteSynchronously);
            });

            // Wait for the client's write operation to complete.
            Assert.True(clientWriteAfterGoAwayLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));

            var clientWriteAfterGoAwayFuture = clientWriteAfterGoAwayFutureRef.Value;
            Assert.NotNull(clientWriteAfterGoAwayFuture);
            var clientCause = clientWriteAfterGoAwayFuture.Exception.InnerException;
            Assert.IsType<StreamException>(clientCause);
            Assert.Equal(Http2Error.RefusedStream, ((StreamException)clientCause).Error);

            // Wait for the server to receive a GO_AWAY, but this is expected to timeout!
            Assert.False(_goAwayLatch.Wait(TimeSpan.FromSeconds(1)));
            _serverListener.Verify(
                x => x.OnGoAwayRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>()), Times.Never());

            // Shutdown shouldn't wait for the server to close streams
            SetClientGracefulShutdownTime(0);
            SetServerGracefulShutdownTime(0);
        }

        [Fact]
        public void ListenerIsNotifiedOfGoawayBeforeStreamsAreRemovedFromTheConnection()
        {
            BootstrapEnv(1, 1, 2, 1, 1);

            // We want both sides to do graceful shutdown during the test.
            SetClientGracefulShutdownTime(10000);
            SetServerGracefulShutdownTime(10000);

            AtomicInteger clientStream3State = new AtomicInteger();
            CountdownEvent clientGoAwayLatch = new CountdownEvent(1);
            _clientListener
                .Setup(x => x.OnGoAwayRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>()))
                .Callback<IChannelHandlerContext, int, Http2Error, IByteBuffer>((ctx, id, err, buf) =>
                {
                    clientStream3State.Value = (int)_http2Client.Connection.Stream(3).State;
                    clientGoAwayLatch.SafeSignal();
                });

            // Create a single stream by sending a HEADERS frame to the server.
            IHttp2Headers headers = DummyHeaders();
            Http2TestUtil.RunInChannel(_clientChannel, () =>
            {
                _http2Client.Encoder.WriteHeadersAsync(Ctx(), 1, headers, 0, (short)16, false, 0,
                    false, NewPromise());
                _http2Client.Encoder.WriteHeadersAsync(Ctx(), 3, headers, 0, (short)16, false, 0,
                    false, NewPromise());
                _http2Client.Flush(Ctx());
            });

            Assert.True(_serverSettingsAckLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));

            // Server has received the headers, so the stream is open
            Assert.True(_requestLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));

            Http2TestUtil.RunInChannel(_serverChannel, () =>
            {
                _http2Server.Encoder.WriteGoAwayAsync(ServerCtx(), 1, Http2Error.NoError, Unpooled.Empty, ServerNewPromise());
                _http2Server.Flush(ServerCtx());
            });

            // wait for the client to receive the GO_AWAY.
            Assert.True(clientGoAwayLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            _clientListener.Verify(
                x => x.OnGoAwayRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 1),
                    It.Is<Http2Error>(v => v == Http2Error.NoError),
                    It.IsAny<IByteBuffer>()));
            Assert.Equal(Http2StreamState.Open, (Http2StreamState)clientStream3State.Value);

            // Make sure that stream 3 has been closed which is true if it's gone.
            CountdownEvent probeStreamCount = new CountdownEvent(1);
            AtomicBoolean stream3Exists = new AtomicBoolean();
            AtomicInteger streamCount = new AtomicInteger();
            Http2TestUtil.RunInChannel(_clientChannel, () =>
            {
                stream3Exists.Value = (_http2Client.Connection.Stream(3) != null);
                streamCount.Value = (_http2Client.Connection.NumActiveStreams);
                probeStreamCount.SafeSignal();
            });
            // The stream should be closed right after
            Assert.True(probeStreamCount.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            Assert.Equal(1, streamCount.Value);
            Assert.False(stream3Exists.Value);

            // Wait for the server to receive a GO_AWAY, but this is expected to timeout!
            Assert.False(_goAwayLatch.Wait(TimeSpan.FromSeconds(1)));
            _serverListener.Verify(
                x => x.OnGoAwayRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>()), Times.Never());

            // Shutdown shouldn't wait for the server to close streams
            SetClientGracefulShutdownTime(0);
            SetServerGracefulShutdownTime(0);
        }

        [Fact]
        public virtual void FlowControlProperlyChunksLargeMessage()
        {
            IHttp2Headers headers = DummyHeaders();

            // Create a large message to send.
            int length = 10485760; // 10MB

            // Create a buffer filled with random bytes.
            IByteBuffer data = RandomBytes(length);
            var output = new ByteBufferStream(Unpooled.Buffer(length));
            _serverListener
                .Setup(x => x.OnDataRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<int>(v => v == 3),
                    It.IsAny<IByteBuffer>(),
                    It.Is<int>(v => v == 0),
                    It.IsAny<bool>()))
                .Returns<IChannelHandlerContext, int, IByteBuffer, int, bool>((ctx, id, buf, padding, e) =>
                {
                    int processedBytes = buf.ReadableBytes + padding;

                    buf.ReadBytes(output, buf.ReadableBytes);
                    return processedBytes;
                });
            try
            {
                // Initialize the data latch based on the number of bytes expected.
                BootstrapEnv(length, 1, 2, 1);

                // Create the stream and send all of the data at once.
                Http2TestUtil.RunInChannel(_clientChannel, () =>
                {
                    _http2Client.Encoder.WriteHeadersAsync(Ctx(), 3, headers, 0, (short)16, false, 0,
                            false, NewPromise());
                    _http2Client.Encoder.WriteDataAsync(Ctx(), 3, data.RetainedDuplicate(), 0, false, NewPromise());

                    // Write trailers.
                    _http2Client.Encoder.WriteHeadersAsync(Ctx(), 3, headers, 0, (short)16, false, 0,
                            true, NewPromise());
                    _http2Client.Flush(Ctx());
                });

                // Wait for the trailers to be received.
                Assert.True(_serverSettingsAckLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
                Assert.True(_trailersLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));

                // Verify that headers and trailers were received.

                _serverListener.Verify(
                    x => x.OnHeadersRead(
                        It.IsAny<IChannelHandlerContext>(),
                        It.Is<int>(v => v == 3),
                        It.Is<IHttp2Headers>(v => v.Equals(headers)),
                        It.Is<int>(v => v == 0),
                        It.Is<short>(v => v == 16),
                        It.Is<bool>(v => v == false),
                        It.Is<int>(v => v == 0),
                        It.Is<bool>(v => v == false)));
                _serverListener.Verify(
                    x => x.OnHeadersRead(
                        It.IsAny<IChannelHandlerContext>(),
                        It.Is<int>(v => v == 3),
                        It.Is<IHttp2Headers>(v => v.Equals(headers)),
                        It.Is<int>(v => v == 0),
                        It.Is<short>(v => v == 16),
                        It.Is<bool>(v => v == false),
                        It.Is<int>(v => v == 0),
                        It.Is<bool>(v => v == true)));

                // Verify we received all the bytes.
                Assert.Equal(0, _dataLatch.CurrentCount);
                output.Flush();
                byte[] received = ByteBufferUtil.GetBytes(output.Buffer);
                Assert.Equal(data.Array, received);
            }
            finally
            {
                // Don't wait for server to close streams
                SetClientGracefulShutdownTime(0);
                data.Release();
                output.Close();
            }
        }

        [Fact]
        public virtual void StressTest()
        {
            IHttp2Headers headers = DummyHeaders();
            int length = 10;
            IByteBuffer data = RandomBytes(length);
            string dataAsHex = ByteBufferUtil.HexDump(data);
            long pingData = 8;
            int numStreams = 2000;

            // Collect all the ping buffers as we receive them at the server.
            long[] receivedPings = new long[numStreams];
            int nextIndex = 0;
            _serverListener
                .Setup(x => x.OnPingRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<long>()))
                .Callback<IChannelHandlerContext, long>((ctx, d) =>
                {
                    receivedPings[nextIndex++] = d;
                });

            // Collect all the data buffers as we receive them at the server.
            StringBuilder[] receivedData = new StringBuilder[numStreams];
            _serverListener
                .Setup(x => x.OnDataRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>()))
                .Returns<IChannelHandlerContext, int, IByteBuffer, int, bool>((ctx, streamId, buf, padding, e) =>
                {
                    int processedBytes = buf.ReadableBytes + padding;

                    int streamIndex = (streamId - 3) / 2;
                    StringBuilder builder = receivedData[streamIndex];
                    if (builder == null)
                    {
                        builder = new StringBuilder(dataAsHex.Length);
                        receivedData[streamIndex] = builder;
                    }
                    builder.Append(ByteBufferUtil.HexDump(buf));
                    return processedBytes;
                });
            try
            {
                BootstrapEnv(numStreams * length, 1, numStreams * 4, numStreams);
                Http2TestUtil.RunInChannel(_clientChannel, () =>
                {
                    int upperLimit = 3 + 2 * numStreams;
                    for (int streamId = 3; streamId < upperLimit; streamId += 2)
                    {
                        // Send a bunch of data on each stream.
                        _http2Client.Encoder.WriteHeadersAsync(Ctx(), streamId, headers, 0, (short)16,
                                false, 0, false, NewPromise());
                        _http2Client.Encoder.WritePingAsync(Ctx(), false, pingData,
                                NewPromise());
                        _http2Client.Encoder.WriteDataAsync(Ctx(), streamId, data.RetainedSlice(), 0,
                                                        false, NewPromise());
                        // Write trailers.
                        _http2Client.Encoder.WriteHeadersAsync(Ctx(), streamId, headers, 0, (short)16,
                                false, 0, true, NewPromise());
                        _http2Client.Flush(Ctx());
                    }
                });
                // Wait for all frames to be received.
                Assert.True(_serverSettingsAckLatch.Wait(TimeSpan.FromSeconds(60)));
                Assert.True(_trailersLatch.Wait(TimeSpan.FromSeconds(60)));
                _serverListener.Verify(
                    x => x.OnHeadersRead(
                        It.IsAny<IChannelHandlerContext>(),
                        It.IsAny<int>(),
                        It.Is<IHttp2Headers>(v => v.Equals(headers)),
                        It.Is<int>(v => v == 0),
                        It.Is<short>(v => v == 16),
                        It.Is<bool>(v => v == false),
                        It.Is<int>(v => v == 0),
                        It.Is<bool>(v => v == false)), Times.Exactly(numStreams));
                _serverListener.Verify(
                    x => x.OnHeadersRead(
                        It.IsAny<IChannelHandlerContext>(),
                        It.IsAny<int>(),
                        It.Is<IHttp2Headers>(v => v.Equals(headers)),
                        It.Is<int>(v => v == 0),
                        It.Is<short>(v => v == 16),
                        It.Is<bool>(v => v == false),
                        It.Is<int>(v => v == 0),
                        It.Is<bool>(v => v == true)), Times.Exactly(numStreams));
                _serverListener.Verify(
                    x => x.OnPingRead(
                        It.IsAny<IChannelHandlerContext>(),
                        It.IsAny<long>()), Times.Exactly(numStreams));
                _serverListener.Verify(
                    x => x.OnDataRead(
                        It.IsAny<IChannelHandlerContext>(),
                        It.IsAny<int>(),
                        It.IsAny<IByteBuffer>(),
                        It.Is<int>(v => v == 0),
                        It.Is<bool>(v => v == true)), Times.Never());
                foreach (StringBuilder builder in receivedData)
                {
                    Assert.Equal(dataAsHex, builder.ToString());
                }
                foreach (long receivedPing in receivedPings)
                {
                    Assert.Equal(pingData, receivedPing);
                }
            }
            finally
            {
                // Don't wait for server to close streams
                SetClientGracefulShutdownTime(0);
                data.Release();
            }
        }

        protected abstract void SetupServerBootstrap(ServerBootstrap bootstrap);

        protected abstract void SetupBootstrap(Bootstrap bootstrap);

        protected virtual void SetInitialServerChannelPipeline(IChannel ch, AtomicReference<Http2ConnectionHandler> serverHandlerRef)
        {
            _serverConnectedChannel = ch;
            var p = ch.Pipeline;
            _serverFrameCountDown =
                    new Http2TestUtil.FrameCountDown(_serverListener.Object, _serverSettingsAckLatch,
                            _requestLatch, _dataLatch, _trailersLatch, _goAwayLatch);
            serverHandlerRef.Value = (new Http2ConnectionHandlerBuilder()
            {
                IsServer = true,
                FrameListener = _serverFrameCountDown,
                IsValidateHeaders = false
            }).Build();
            p.AddLast(serverHandlerRef.Value);
        }

        protected virtual void SetInitialChannelPipeline(IChannel ch)
        {
            var p = ch.Pipeline;
            p.AddLast((new Http2ConnectionHandlerBuilder()
            {
                IsServer = false,
                FrameListener = _clientListener.Object,
                IsValidateHeaders = false,
                GracefulShutdownTimeout = TimeSpan.Zero
            }).Build());
        }

        protected virtual int Port => 0;

        protected virtual void StartBootstrap()
        {
            var loopback = IPAddress.IPv6Loopback;
            _serverChannel = _sb.BindAsync(loopback, Port).GetAwaiter().GetResult();

            var port = ((IPEndPoint)_serverChannel.LocalAddress).Port;
            var ccf = _cb.ConnectAsync(loopback, port);
            _clientChannel = ccf.GetAwaiter().GetResult();
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

        private void BootstrapEnv(int dataCountDown, int settingsAckCount,
            int requestCountDown, int trailersCountDown)
        {
            BootstrapEnv(dataCountDown, settingsAckCount, requestCountDown, trailersCountDown, -1);
        }

        private void BootstrapEnv(int dataCountDown, int settingsAckCount,
            int requestCountDown, int trailersCountDown, int goAwayCountDown)
        {
            var prefaceWrittenLatch = new CountdownEvent(1);
            _requestLatch = new CountdownEvent(requestCountDown);
            _serverSettingsAckLatch = new CountdownEvent(settingsAckCount);
            _dataLatch = new CountdownEvent(dataCountDown);
            _trailersLatch = new CountdownEvent(trailersCountDown);
            _goAwayLatch = goAwayCountDown > 0 ? new CountdownEvent(goAwayCountDown) : _requestLatch;
            _sb = new ServerBootstrap();
            _cb = new Bootstrap();

            AtomicReference<Http2ConnectionHandler> serverHandlerRef = new AtomicReference<Http2ConnectionHandler>();
            var serverInitLatch = new CountdownEvent(1);
            SetupServerBootstrap(_sb);
            _sb.ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
            {
                SetInitialServerChannelPipeline(ch, serverHandlerRef);
                serverInitLatch.SafeSignal();
            }));

            SetupBootstrap(_cb);
            _cb.Handler(new ActionChannelInitializer<IChannel>(ch =>
            {
                SetInitialChannelPipeline(ch);
                ch.Pipeline.AddLast(new TestChannelHandlerAdapter(prefaceWrittenLatch));
            }));

            StartBootstrap();

            Assert.True(prefaceWrittenLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            _http2Client = _clientChannel.Pipeline.Get<Http2ConnectionHandler>();
            Assert.True(serverInitLatch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
            _http2Server = serverHandlerRef.Value;
        }

        sealed class TestChannelHandlerAdapter : ChannelHandlerAdapter
        {
            readonly CountdownEvent _prefaceWrittenLatch;

            public TestChannelHandlerAdapter(CountdownEvent countdown) => _prefaceWrittenLatch = countdown;

            public override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
            {
                if (ReferenceEquals(evt, Http2ConnectionPrefaceAndSettingsFrameWrittenEvent.Instance))
                {
                    _prefaceWrittenLatch.SafeSignal();
                    ctx.Pipeline.Remove(this);
                }
            }
        }

        private IChannelHandlerContext Ctx()
        {
            return _clientChannel.Pipeline.FirstContext();
        }

        private IChannelHandlerContext ServerCtx()
        {
            return _serverConnectedChannel.Pipeline.FirstContext();
        }

        private IPromise NewPromise()
        {
            return Ctx().NewPromise();
        }

        private IPromise ServerNewPromise()
        {
            return ServerCtx().NewPromise();
        }

        private static IHttp2Headers DummyHeaders()
        {
            var headers = new DefaultHttp2Headers(false)
            {
                Method = new AsciiString("GET"),
                Scheme = new AsciiString("https"),
                Authority = new AsciiString("example.org"),
                Path = new AsciiString("/some/path/resource2")
            };
            headers.Add(Http2TestUtil.RandomString(), Http2TestUtil.RandomString());
            return headers;
        }

        private static void MockFlowControl(Mock<IHttp2FrameListener> listener)
        {
            listener
                .Setup(x => x.OnDataRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>()))
                .Returns<IChannelHandlerContext, int, IByteBuffer, int, bool>((ctx, id, buf, padding, end) =>
                {
                    int processedBytes = buf.ReadableBytes + padding;
                    return processedBytes;
                });
        }

        private void SetClientGracefulShutdownTime(long millis)
        {
            SetGracefulShutdownTime(_clientChannel, _http2Client, millis);
        }

        private void SetServerGracefulShutdownTime(long millis)
        {
            SetGracefulShutdownTime(_serverChannel, _http2Server, millis);
        }

        private static void SetGracefulShutdownTime(IChannel channel, Http2ConnectionHandler handler, long millis)
        {
            var latch = new CountdownEvent(1);
            Http2TestUtil.RunInChannel(channel, () =>
            {
                handler.GracefulShutdownTimeout = TimeSpan.FromMilliseconds(millis);
                latch.SafeSignal();
            });

            Assert.True(latch.Wait(TimeSpan.FromSeconds(DEFAULT_AWAIT_TIMEOUT_SECONDS)));
        }

        /**
         * Creates a {@link IByteBuffer} of the given length, filled with random bytes.
         */
        private static IByteBuffer RandomBytes(int length)
        {
            byte[] bytes = new byte[length];
            new Random().NextBytes(bytes);
            return Unpooled.WrappedBuffer(bytes);
        }
    }
}
