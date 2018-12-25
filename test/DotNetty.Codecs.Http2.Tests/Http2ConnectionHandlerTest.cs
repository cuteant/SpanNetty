
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Tests.Common;
    using DotNetty.Transport.Channels;
    using Moq;
    using Xunit;

    public class Http2ConnectionHandlerTest : IDisposable
    {
        private const int STREAM_ID = 1;
        private const int NON_EXISTANT_STREAM_ID = 13;

        private Http2ConnectionHandler handler;
        private IPromise promise;
        private IPromise voidPromise;

        private Mock<IHttp2Connection> connection;
        private Mock<IHttp2RemoteFlowController> remoteFlow;
        private Mock<IHttp2LocalFlowController> localFlow;
        private Mock<IHttp2ConnectionEndpoint<IHttp2RemoteFlowController>> remote;
        private Mock<IHttp2RemoteFlowController> remoteFlowController;
        private Mock<IHttp2ConnectionEndpoint<IHttp2LocalFlowController>> local;
        private Mock<IHttp2LocalFlowController> localFlowController;
        private Mock<IChannelHandlerContext> ctx;
        private Mock<IEventExecutor> executor;
        private Mock<IChannel> channel;
        private Mock<IChannelPipeline> pipeline;
        private Mock<IHttp2Stream> stream;
        private Mock<IHttp2ConnectionDecoder> decoder;
        private Mock<IHttp2ConnectionEncoder> encoder;
        private Mock<IHttp2FrameWriter> frameWriter;

        private Task future;
        private string goAwayDebugCap;

        public Http2ConnectionHandlerTest()
        {
            this.connection = new Mock<IHttp2Connection>();
            this.remoteFlow = new Mock<IHttp2RemoteFlowController>();
            this.localFlow = new Mock<IHttp2LocalFlowController>();
            this.remote = new Mock<IHttp2ConnectionEndpoint<IHttp2RemoteFlowController>>();
            this.remoteFlowController = new Mock<IHttp2RemoteFlowController>();
            this.local = new Mock<IHttp2ConnectionEndpoint<IHttp2LocalFlowController>>();
            this.localFlowController = new Mock<IHttp2LocalFlowController>();
            this.ctx = new Mock<IChannelHandlerContext>();
            this.executor = new Mock<IEventExecutor>();
            this.channel = new Mock<IChannel>();
            this.pipeline = new Mock<IChannelPipeline>();
            this.stream = new Mock<IHttp2Stream>();
            this.decoder = new Mock<IHttp2ConnectionDecoder>();
            this.encoder = new Mock<IHttp2ConnectionEncoder>();
            this.frameWriter = new Mock<IHttp2FrameWriter>();

            this.channel.Setup(x => x.Metadata).Returns(new ChannelMetadata(false));
            var config = new DefaultChannelConfiguration(this.channel.Object);
            this.channel.Setup(x => x.Configuration).Returns(config);

            promise = new TaskCompletionSource();
            voidPromise = new TaskCompletionSource();
            var fakeException = new Http2RuntimeException("Fake exception");
            this.future = TaskUtil.FromException(fakeException);

            this.encoder.Setup(x => x.Connection).Returns(this.connection.Object);
            this.decoder.Setup(x => x.Connection).Returns(this.connection.Object);
            this.encoder.Setup(x => x.FrameWriter).Returns(this.frameWriter.Object);
            this.encoder.Setup(x => x.FlowController).Returns(this.remoteFlow.Object);
            this.decoder.Setup(x => x.FlowController).Returns(this.localFlow.Object);
            this.frameWriter
                .Setup(x => x.WriteGoAwayAsync(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, Http2Error, IByteBuffer, IPromise>((ctx, id, err, buf, p) =>
                {
                    this.goAwayDebugCap = buf.ToString(Encoding.UTF8);
                    buf.Release();
                    return this.future;
                });
            this.channel.Setup(x => x.Active).Returns(true);
            this.channel.Setup(x => x.Pipeline).Returns(this.pipeline.Object);
            this.connection.Setup(x => x.Remote).Returns(this.remote.Object);
            this.remote.Setup(x => x.FlowController).Returns(this.remoteFlowController.Object);
            this.connection.Setup(x => x.Local).Returns(this.local.Object);
            this.local.Setup(x => x.FlowController).Returns(this.localFlowController.Object);
            this.connection
                .Setup(x => x.ForEachActiveStream(It.IsAny<IHttp2StreamVisitor>()))
                .Returns<IHttp2StreamVisitor>(visitor =>
                {
                    if (!visitor.Visit(this.stream.Object))
                    {
                        return this.stream.Object;
                    }
                    return null;
                });
            this.connection
                .Setup(x => x.ForEachActiveStream(It.IsAny<Func<IHttp2Stream, bool>>()))
                .Returns<Func<IHttp2Stream, bool>>(visitor =>
                {
                    if (!visitor(this.stream.Object))
                    {
                        return this.stream.Object;
                    }
                    return null;
                });
            this.connection.Setup(x => x.Stream(It.Is<int>(v => v == NON_EXISTANT_STREAM_ID))).Returns(default(IHttp2Stream));
            this.connection.Setup(x => x.NumActiveStreams).Returns(1);
            this.connection.Setup(x => x.Stream(It.Is<int>(v => v == STREAM_ID))).Returns(this.stream.Object);
            this.connection
                .Setup(x => x.GoAwaySent(
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>()))
                .Returns(true);
            this.stream.Setup(x => x.Open(It.IsAny<bool>())).Returns(this.stream.Object);
            this.encoder
                .Setup(x => x.WriteSettingsAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<Http2Settings>(),
                    It.Is<IPromise>(v => v == this.promise)))
                .Returns(this.future);
            this.ctx.Setup(x => x.Allocator).Returns(UnpooledByteBufferAllocator.Default);
            this.ctx.Setup(x => x.Channel).Returns(this.channel.Object);
            this.ctx.Setup(x => x.NewPromise()).Returns(this.promise);
            this.ctx.Setup(x => x.VoidPromise()).Returns(this.voidPromise);
            this.ctx.Setup(x => x.WriteAsync(It.IsAny<object>())).Returns(this.future);
            this.ctx.Setup(x => x.Executor).Returns(this.executor.Object);
            this.ctx
                .Setup(x => x.FireChannelRead(It.IsAny<object>()))
                .Returns<object>(msg =>
                {
                    ReferenceCountUtil.Release(msg);
                    return this.ctx.Object;
                });

        }

        [Fact]
        public void OnHttpServerUpgradeWithoutHandlerAdded()
        {
            var b = new Http2ConnectionHandlerBuilder();
            b.FrameListener = new Http2FrameAdapter();
            b.IsServer = true;
            this.handler = b.Build();
            var e = Assert.Throws<Http2Exception>(() => this.handler.OnHttpServerUpgrade(new Http2Settings()));
            Assert.Equal(Http2Error.InternalError, e.Error);
        }

        [Fact]
        public void OnHttpClientUpgradeWithoutHandlerAdded()
        {
            var b = new Http2ConnectionHandlerBuilder();
            b.FrameListener = new Http2FrameAdapter();
            b.IsServer = false;
            this.handler = b.Build();
            var e = Assert.Throws<Http2Exception>(() => this.handler.OnHttpClientUpgrade());
            Assert.Equal(Http2Error.InternalError, e.Error);
        }

        [Fact]
        public void ClientShouldveSentPrefaceAndSettingsFrameWhenUserEventIsTriggered()
        {
            this.connection.Setup(x => x.IsServer).Returns(false);
            this.channel.Setup(x => x.Active).Returns(false);
            this.handler = this.NewHandler();
            this.channel.Setup(x => x.Active).Returns(true);

            var evt = Http2ConnectionPrefaceAndSettingsFrameWrittenEvent.Instance;

            AtomicBoolean verified = new AtomicBoolean(false);
            this.ctx
                .Setup(x => x.FireUserEventTriggered(It.Is<object>(v => ReferenceEquals(v, evt))))
                .Returns<object>(msg =>
                {
                    Assert.Same(msg, evt);
                    this.ctx.Verify(x => x.WriteAsync(It.Is<object>(d => Http2CodecUtil.ConnectionPrefaceBuf().Equals((IByteBuffer)d))));
                    this.encoder.Verify(
                        x => x.WriteSettingsAsync(
                            It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                            It.IsAny<Http2Settings>(),
                            It.IsAny<IPromise>()));
                    verified.Value = true;
                    return this.ctx.Object;
                });

            this.handler.ChannelActive(this.ctx.Object);
            Assert.True(verified.Value);
        }

        [Fact]
        public void ClientShouldSendClientPrefaceStringWhenActive()
        {
            this.connection.Setup(x => x.IsServer).Returns(false);
            this.channel.Setup(x => x.Active).Returns(false);
            this.handler = this.NewHandler();
            this.channel.Setup(x => x.Active).Returns(true);
            this.handler.ChannelActive(this.ctx.Object);
            this.ctx.Verify(x => x.WriteAsync(It.Is<object>(d => Http2CodecUtil.ConnectionPrefaceBuf().Equals((IByteBuffer)d))));
        }

        [Fact]
        public void ServerShouldNotSendClientPrefaceStringWhenActive()
        {
            this.connection.Setup(x => x.IsServer).Returns(true);
            this.channel.Setup(x => x.Active).Returns(false);
            this.handler = this.NewHandler();
            this.channel.Setup(x => x.Active).Returns(true);
            this.handler.ChannelActive(this.ctx.Object);
            this.ctx.Verify(x => x.WriteAsync(It.Is<object>(d => Http2CodecUtil.ConnectionPrefaceBuf().Equals((IByteBuffer)d))), Times.Never());
        }

        [Fact]
        public void ServerReceivingInvalidClientPrefaceStringShouldHandleException()
        {
            this.connection.Setup(x => x.IsServer).Returns(true);
            this.handler = this.NewHandler();
            this.handler.ChannelRead(this.ctx.Object, Unpooled.CopiedBuffer("BAD_PREFACE", Encoding.UTF8));
            var captor = new ArgumentCaptor<IByteBuffer>();
            this.frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == 0),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IByteBuffer>(v => captor.Capture(v)),
                    It.Is<IPromise>(v => v == this.promise)));
            Assert.Equal(0, captor.GetValue().ReferenceCount);
        }

        [Fact]
        public void ServerReceivingHttp1ClientPrefaceStringShouldIncludePreface()
        {
            this.connection.Setup(x => x.IsServer).Returns(true);
            this.handler = this.NewHandler();
            this.handler.ChannelRead(this.ctx.Object, Unpooled.CopiedBuffer("GET /path HTTP/1.1", Encoding.ASCII));
            var captor = new ArgumentCaptor<IByteBuffer>();
            this.frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == 0),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IByteBuffer>(v => captor.Capture(v)),
                    It.Is<IPromise>(v => v == this.promise)));
            Assert.Equal(0, captor.GetValue().ReferenceCount);
            Assert.Contains("/path", this.goAwayDebugCap);
        }

        [Fact]
        public void ServerReceivingClientPrefaceStringFollowedByNonSettingsShouldHandleException()
        {
            this.connection.Setup(x => x.IsServer).Returns(true);
            this.handler = this.NewHandler();

            // Create a connection preface followed by a bunch of zeros (i.e. not a settings frame).
            IByteBuffer buf = Unpooled.Buffer().WriteBytes(Http2CodecUtil.ConnectionPrefaceBuf()).WriteZero(10);
            this.handler.ChannelRead(this.ctx.Object, buf);
            var captor = new ArgumentCaptor<IByteBuffer>();
            this.frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == 0),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IByteBuffer>(v => captor.Capture(v)),
                    It.Is<IPromise>(v => v == this.promise)),
                Times.AtLeastOnce);
            Assert.Equal(0, captor.GetValue().ReferenceCount);
        }

        [Fact]
        public void ServerReceivingValidClientPrefaceStringShouldContinueReadingFrames()
        {
            this.connection.Setup(x => x.IsServer).Returns(true);
            this.handler = this.NewHandler();
            IByteBuffer prefacePlusSome = AddSettingsHeader(Unpooled.Buffer().WriteBytes(Http2CodecUtil.ConnectionPrefaceBuf()));
            this.handler.ChannelRead(this.ctx.Object, prefacePlusSome);
            this.decoder.Verify(
                x => x.DecodeFrame(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<List<object>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public void VerifyChannelHandlerCanBeReusedInPipeline()
        {
            this.connection.Setup(x => x.IsServer).Returns(true);
            this.handler = this.NewHandler();
            // Only read the connection preface...after preface is read internal state of Http2ConnectionHandler
            // is expected to change relative to the pipeline.
            IByteBuffer preface = Http2CodecUtil.ConnectionPrefaceBuf();
            this.handler.ChannelRead(this.ctx.Object, preface);
            this.decoder.Verify(
                x => x.DecodeFrame(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<List<object>>()),
                Times.Never());

            // Now remove and add the this.handler...this is setting up the test condition.
            this.handler.HandlerRemoved(this.ctx.Object);
            this.handler.HandlerAdded(this.ctx.Object);

            // Now verify we can continue as normal, reading connection preface plus more.
            IByteBuffer prefacePlusSome = AddSettingsHeader(Unpooled.Buffer().WriteBytes(Http2CodecUtil.ConnectionPrefaceBuf()));
            this.handler.ChannelRead(this.ctx.Object, prefacePlusSome);
            this.decoder.Verify(
                x => x.DecodeFrame(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<List<object>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public void ChannelInactiveShouldCloseStreams()
        {
            this.handler = this.NewHandler();
            this.handler.ChannelInactive(this.ctx.Object);
            this.connection.Verify(x => x.CloseAsync(It.IsAny<IPromise>()));
        }

        [Fact]
        public void ConnectionErrorShouldStartShutdown()
        {
            this.handler = this.NewHandler();
            Http2Exception e = new Http2Exception(Http2Error.ProtocolError);
            this.remote.Setup(x => x.LastStreamCreated).Returns(STREAM_ID);
            this.handler.ExceptionCaught(this.ctx.Object, e);
            var captor = new ArgumentCaptor<IByteBuffer>();
            this.frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IByteBuffer>(v => captor.Capture(v)),
                    It.Is<IPromise>(v => v == this.promise)));
            var buf = captor.GetValue();
            Assert.Equal(0, buf.ReferenceCount);
            // netty future.addListener 只配置了 ChannelFutureListener 的监听，而且isDone返回的值为false
            // 所以 processGoAwayWriteResult 没有执行
            //Assert.Equal(1, buf.ReferenceCount);
            //buf.Release();
        }

        [Fact]
        public void ServerShouldSend431OnHeaderSizeErrorWhenDecodingInitialHeaders()
        {
            int padding = 0;
            this.handler = this.NewHandler();
            Http2Exception e = new HeaderListSizeException(STREAM_ID, Http2Error.ProtocolError,
                    "Header size exceeded max allowed size 8196", true);

            this.stream.Setup(x => x.Id).Returns(STREAM_ID);
            this.connection.Setup(x => x.IsServer).Returns(true);
            this.stream.Setup(x => x.IsHeadersSent).Returns(false);
            this.remote.Setup(x => x.LastStreamCreated).Returns(STREAM_ID);
            this.frameWriter
                .Setup(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IPromise>(v => v == this.promise)))
                .Returns(this.future);

            this.handler.ExceptionCaught(this.ctx.Object, e);

            var captor = new ArgumentCaptor<IHttp2Headers>();
            this.encoder.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<IHttp2Headers>(v => captor.Capture(v)),
                    It.Is<int>(v => v == padding),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == this.promise)));
            IHttp2Headers headers = captor.GetValue();
            Assert.Equal(HttpResponseStatus.RequestHeaderFieldsTooLarge.CodeAsText, headers.Status);
            this.frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IPromise>(v => v == this.promise)));
        }

        [Fact]
        public void ServerShouldNeverSend431HeaderSizeErrorWhenEncoding()
        {
            int padding = 0;
            this.handler = this.NewHandler();
            Http2Exception e = new HeaderListSizeException(STREAM_ID, Http2Error.ProtocolError,
                "Header size exceeded max allowed size 8196", false);

            this.stream.Setup(x => x.Id).Returns(STREAM_ID);
            this.connection.Setup(x => x.IsServer).Returns(true);
            this.stream.Setup(x => x.IsHeadersSent).Returns(false);
            this.remote.Setup(x => x.LastStreamCreated).Returns(STREAM_ID);
            this.frameWriter
                .Setup(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IPromise>(v => v == this.promise)))
                .Returns(this.future);

            this.handler.ExceptionCaught(this.ctx.Object, e);

            this.encoder.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<IHttp2Headers>(),
                    It.Is<int>(v => v == padding),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == this.promise)), Times.Never());
            this.frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IPromise>(v => v == this.promise)));
        }

        [Fact]
        public void ClientShouldNeverSend431WhenHeadersAreTooLarge()
        {
            int padding = 0;
            this.handler = this.NewHandler();
            Http2Exception e = new HeaderListSizeException(STREAM_ID, Http2Error.ProtocolError,
                    "Header size exceeded max allowed size 8196", true);

            this.stream.Setup(x => x.Id).Returns(STREAM_ID);
            this.connection.Setup(x => x.IsServer).Returns(false);
            this.stream.Setup(x => x.IsHeadersSent).Returns(false);
            this.remote.Setup(x => x.LastStreamCreated).Returns(STREAM_ID);
            this.frameWriter
                .Setup(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IPromise>(v => v == this.promise)))
                .Returns(this.future);

            this.handler.ExceptionCaught(this.ctx.Object, e);

            this.encoder.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<IHttp2Headers>(),
                    It.Is<int>(v => v == padding),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == this.promise)), Times.Never());
            this.frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IPromise>(v => v == this.promise)));
        }

        sealed class PrefaceUserEventHttp2ConnectionHandler : Http2ConnectionHandler
        {
            readonly CountdownEvent latch;

            public PrefaceUserEventHttp2ConnectionHandler(CountdownEvent latch, IHttp2ConnectionDecoder decoder, IHttp2ConnectionEncoder encoder, Http2Settings initialSettings)
                : base(decoder, encoder, initialSettings)
            {
                this.latch = latch;
            }

            public override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
            {
                if (ReferenceEquals(evt, Http2ConnectionPrefaceAndSettingsFrameWrittenEvent.Instance))
                {
                    this.latch.SafeSignal();
                }
            }
        }
        [Fact]
        public void PrefaceUserEventProcessed()
        {
            var latch = new CountdownEvent(1);
            this.handler = new PrefaceUserEventHttp2ConnectionHandler(latch, this.decoder.Object, this.encoder.Object, new Http2Settings());
            this.handler.HandlerAdded(this.ctx.Object);
            Assert.True(latch.Wait(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void ServerShouldNeverSend431IfHeadersAlreadySent()
        {
            int padding = 0;
            this.handler = this.NewHandler();
            Http2Exception e = new HeaderListSizeException(STREAM_ID, Http2Error.ProtocolError,
                "Header size exceeded max allowed size 8196", true);

            this.stream.Setup(x => x.Id).Returns(STREAM_ID);
            this.connection.Setup(x => x.IsServer).Returns(true);
            this.stream.Setup(x => x.IsHeadersSent).Returns(true);
            this.remote.Setup(x => x.LastStreamCreated).Returns(STREAM_ID);
            this.frameWriter
                .Setup(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IPromise>(v => v == this.promise)))
                .Returns(this.future);
            this.handler.ExceptionCaught(this.ctx.Object, e);

            this.encoder.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<IHttp2Headers>(),
                    It.Is<int>(v => v == padding),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == this.promise)), Times.Never());

            this.frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IPromise>(v => v == this.promise)));
        }

        [Fact]
        public void ServerShouldCreateStreamIfNeededBeforeSending431()
        {
            int padding = 0;
            this.handler = this.NewHandler();
            Http2Exception e = new HeaderListSizeException(STREAM_ID, Http2Error.ProtocolError,
                "Header size exceeded max allowed size 8196", true);

            this.connection.Setup(x => x.Stream(It.Is<int>(v => v == STREAM_ID))).Returns(default(IHttp2Stream));
            this.remote
                .Setup(x => x.CreateStream(
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<bool>(v => v == true)))
                .Returns(this.stream.Object);
            this.stream.Setup(x => x.Id).Returns(STREAM_ID);

            this.connection.Setup(x => x.IsServer).Returns(true);
            this.stream.Setup(x => x.IsHeadersSent).Returns(false);
            this.remote.Setup(x => x.LastStreamCreated).Returns(STREAM_ID);
            this.frameWriter
                .Setup(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IPromise>(v => v == this.promise)))
                .Returns(this.future);
            this.handler.ExceptionCaught(this.ctx.Object, e);

            this.remote.Verify(
                x => x.CreateStream(
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<bool>(v => v == true)));
            this.encoder.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<IHttp2Headers>(),
                    It.Is<int>(v => v == padding),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == this.promise)));

            this.frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IPromise>(v => v == this.promise)));
        }

        [Fact]
        public void EncoderAndDecoderAreClosedOnChannelInactive()
        {
            this.handler = this.NewHandler();
            this.handler.ChannelActive(this.ctx.Object);
            this.channel.Setup(x => x.Active).Returns(false);
            this.handler.ChannelInactive(this.ctx.Object);
            this.encoder.Verify(x => x.Close());
            this.decoder.Verify(x => x.Close());
        }

        [Fact]
        public void WriteRstOnNonExistantStreamShouldSucceed()
        {
            this.handler = this.NewHandler();
            this.frameWriter
                .Setup(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == NON_EXISTANT_STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.StreamClosed),
                    It.Is<IPromise>(v => v == this.promise)))
                .Returns(this.future);
            this.handler.ResetStreamAsync(this.ctx.Object, NON_EXISTANT_STREAM_ID, Http2Error.StreamClosed, this.promise);
            this.frameWriter
                .Verify(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == NON_EXISTANT_STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.StreamClosed),
                    It.Is<IPromise>(v => v == this.promise)));
        }

        [Fact]
        public void WriteRstOnClosedStreamShouldSucceed()
        {
            this.handler = this.NewHandler();
            this.stream.Setup(x => x.Id).Returns(STREAM_ID);
            this.frameWriter
                .Setup(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()))
                .Returns(this.future);
            this.stream.Setup(x => x.State).Returns(Http2StreamState.Closed);
            this.stream.Setup(x => x.IsHeadersSent).Returns(true);
            // The stream is "closed" but is still known about by the connection (connection().stream(..)
            // will return the stream). We should still write a RST_STREAM frame in this scenario.
            this.handler.ResetStreamAsync(this.ctx.Object, STREAM_ID, Http2Error.StreamClosed, this.promise);
            this.frameWriter
                .Verify(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()));
        }

        [Fact]
        public void WriteRstOnIdleStreamShouldNotWriteButStillSucceed()
        {
            this.handler = this.NewHandler();
            this.stream.Setup(x => x.State).Returns(Http2StreamState.Idle);
            this.handler.ResetStreamAsync(this.ctx.Object, STREAM_ID, Http2Error.StreamClosed, this.promise);
            this.frameWriter
                .Verify(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()),
                Times.Never());
            this.stream.Verify(x => x.Close());
        }

        [Fact]
        public void CloseListenerShouldBeNotifiedOnlyOneTime()
        {
            this.handler = this.NewHandler();
            this.future = TaskUtil.Completed;
            //doAnswer(new Answer<ChannelFuture>() {
            //    @Override
            //    public ChannelFuture answer(InvocationOnMock invocation) throws Throwable {
            //        Object[] args = invocation.getArguments();
            //        GenericFutureListener<ChannelFuture> listener = (GenericFutureListener<ChannelFuture>) args[0];
            //        // Simulate that all streams have become inactive by the time the future completes.
            //        doAnswer(new Answer<Http2Stream>() {
            //            @Override
            //            public Http2Stream answer(InvocationOnMock in) throws Throwable {
            //                return null;
            //            }
            //        }).when(connection).forEachActiveStream(any(Http2StreamVisitor_class));
            //        when(connection.numActiveStreams()).thenReturn(0);
            //        // Simulate the future being completed.
            //        listener.operationComplete(future);
            //        return future;
            //    }
            //}).when(future).addListener(any(GenericFutureListener_class));
            this.handler.Close(this.ctx.Object, this.promise);
            this.connection
                .Setup(x => x.ForEachActiveStream(It.IsAny<IHttp2StreamVisitor>()))
                .Returns(default(IHttp2Stream));
            this.connection
                .Setup(x => x.ForEachActiveStream(It.IsAny<Func<IHttp2Stream, bool>>()))
                .Returns(default(IHttp2Stream));
            if (future.IsCompleted)
            {
                this.connection.Setup(x => x.NumActiveStreams).Returns(0);
            }
            this.handler.CloseStream(this.stream.Object, future);
            // Simulate another stream close call being made after the context should already be closed.
            this.handler.CloseStream(this.stream.Object, future);
            this.ctx.Verify(x => x.CloseAsync(It.IsAny<IPromise>()), Times.Once);
        }

        [Fact]
        public void CanSendGoAwayFrame()
        {
            IByteBuffer data = DummyData();
            var errorCode = Http2Error.InternalError;
            this.future = TaskUtil.Completed;
            this.handler = this.NewHandler();
            this.handler.GoAwayAsync(this.ctx.Object, STREAM_ID, errorCode, data, promise);

            this.connection.Verify(
                x => x.GoAwaySent(
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == errorCode),
                    It.Is<IByteBuffer>(v => v.Equals(data))));
            this.frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == errorCode),
                    It.Is<IByteBuffer>(v => v.Equals(data)),
                    It.Is<IPromise>(v => v == this.promise)));
            this.ctx.Verify(x => x.CloseAsync());
            Assert.Equal(0, data.ReferenceCount);
        }

        [Fact]
        public void CanSendGoAwayFramesWithDecreasingLastStreamIds()
        {
            this.handler = this.NewHandler();
            IByteBuffer data = DummyData();
            var errorCode = Http2Error.InternalError;

            this.handler.GoAwayAsync(this.ctx.Object, STREAM_ID + 2, errorCode, (IByteBuffer)data.Retain(), promise);
            this.frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID + 2),
                    It.Is<Http2Error>(v => v == errorCode),
                    It.Is<IByteBuffer>(v => v.Equals(data)),
                    It.Is<IPromise>(v => v == this.promise)));
            this.connection.Verify(
                x => x.GoAwaySent(
                    It.Is<int>(v => v == STREAM_ID + 2),
                    It.Is<Http2Error>(v => v == errorCode),
                    It.Is<IByteBuffer>(v => v.Equals(data))));
            this.promise = new TaskCompletionSource();
            this.handler.GoAwayAsync(this.ctx.Object, STREAM_ID, errorCode, data, promise);
            this.frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == errorCode),
                    It.Is<IByteBuffer>(v => v.Equals(data)),
                    It.Is<IPromise>(v => v == this.promise)));
            this.connection.Verify(
                x => x.GoAwaySent(
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == errorCode),
                    It.Is<IByteBuffer>(v => v.Equals(data))));
            Assert.Equal(0, data.ReferenceCount);
        }

        [Fact]
        public void CannotSendGoAwayFrameWithIncreasingLastStreamIds()
        {
            this.handler = this.NewHandler();
            IByteBuffer data = DummyData();
            var errorCode = Http2Error.InternalError;

            this.handler.GoAwayAsync(this.ctx.Object, STREAM_ID, errorCode, (IByteBuffer)data.Retain(), promise);
            this.connection.Verify(
                x => x.GoAwaySent(
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == errorCode),
                    It.Is<IByteBuffer>(v => v.Equals(data))));
            this.frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == errorCode),
                    It.Is<IByteBuffer>(v => v.Equals(data)),
                    It.Is<IPromise>(v => v == this.promise)));
            // The frameWriter is only mocked, so it should not have interacted with the promise.
            Assert.False(promise.IsCompleted);

            this.connection.Setup(x => x.GoAwaySent()).Returns(true);
            this.remote.Setup(x => x.LastStreamKnownByPeer()).Returns(STREAM_ID);
            this.connection
                .Setup(x => x.GoAwaySent(
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>()))
                .Throws(new InvalidOperationException());
            this.handler.GoAwayAsync(this.ctx.Object, STREAM_ID + 2, errorCode, data, promise);
            Assert.True(promise.IsCompleted);
            Assert.False(promise.IsSuccess);
            Assert.Equal(0, data.ReferenceCount);
            frameWriter.VerifyNoOtherCalls();
        }

        [Fact]
        public void CanSendGoAwayUsingVoidPromise()
        {
            this.handler = this.NewHandler();
            IByteBuffer data = DummyData();
            var errorCode = Http2Error.InternalError;
            this.handler = this.NewHandler();
            var cause = new Http2RuntimeException("fake exception");
            this.frameWriter
                .Setup(x => x.WriteGoAwayAsync(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<IPromise>()
                    ))
                .Returns<IChannelHandlerContext, int, Http2Error, IByteBuffer, IPromise>((c, id, err, buf, p) =>
                {
                    Assert.False(p.IsVoid);
                    // This is what DefaultHttp2FrameWriter does... I hate mocking :-(.
                    var aggregatedPromise = new SimplePromiseAggregator(p);
                    aggregatedPromise.NewPromise();
                    aggregatedPromise.DoneAllocatingPromises();
                    aggregatedPromise.SetException(cause);
                    return aggregatedPromise.Task;
                });
            this.handler.GoAwayAsync(this.ctx.Object, STREAM_ID, errorCode, data, Http2TestUtil.NewVoidPromise(this.channel.Object));
            this.pipeline.Verify(x => x.FireExceptionCaught(It.Is<Exception>(v => ReferenceEquals(v, cause))));
        }

        [Fact]
        public void ChannelReadCompleteTriggersFlush()
        {
            this.handler = this.NewHandler();
            this.handler.ChannelReadComplete(this.ctx.Object);
            this.ctx.Verify(x => x.Flush(), Times.Once);
        }

        [Fact]
        public void ChannelReadCompleteCallsReadWhenAutoReadFalse()
        {
            this.channel.Object.Configuration.AutoRead = false;
            this.handler = this.NewHandler();
            this.handler.ChannelReadComplete(this.ctx.Object);
            this.ctx.Verify(x => x.Read(), Times.Once());
        }

        [Fact]
        public void ChannelClosedDoesNotThrowPrefaceException()
        {
            this.connection.Setup(x => x.IsServer).Returns(true);
            this.handler = this.NewHandler();
            this.channel.Setup(x => x.Active).Returns(false);
            this.handler.ChannelInactive(this.ctx.Object);
            this.frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<IPromise>()),
                Times.Never());
            this.frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()),
                Times.Never());
        }

        [Fact]
        public void WriteRstStreamForUnknownStreamUsingVoidPromise()
        {
            this.WriteRstStreamUsingVoidPromise(NON_EXISTANT_STREAM_ID);
        }

        [Fact]
        public void WriteRstStreamForKnownStreamUsingVoidPromise()
        {
            this.WriteRstStreamUsingVoidPromise(STREAM_ID);
        }

        [Fact]
        public void GracefulShutdownTimeoutTest()
        {
            this.handler = this.NewHandler();
            long expectedMillis = 1234;
            this.handler.GracefulShutdownTimeout = TimeSpan.FromMilliseconds(expectedMillis);
            this.handler.Close(this.ctx.Object, this.promise);
            this.executor.Verify(
                x => x.Schedule(
                    It.IsAny<Action<object, object>>(),
                    It.IsAny<object>(),
                    It.IsAny<object>(),
                    It.Is<TimeSpan>(v => v == TimeSpan.FromMilliseconds(expectedMillis))));
        }

        [Fact]
        public void GracefulShutdownIndefiniteTimeoutTest()
        {
            this.handler = this.NewHandler();
            this.handler.GracefulShutdownTimeout = TimeSpan.FromMilliseconds(-1);
            this.handler.Close(this.ctx.Object, this.promise);
            this.executor.Verify(
                x => x.Schedule(
                    It.IsAny<Action<object, object>>(),
                    It.IsAny<object>(),
                    It.IsAny<object>(),
                    It.IsAny<TimeSpan>()),
                Times.Never());
        }

        public void Dispose()
        {
            if (this.handler != null)
            {
                this.handler.HandlerRemoved(this.ctx.Object);
            }
        }

        private Http2ConnectionHandler NewHandler()
        {
            Http2ConnectionHandler handler = new Http2ConnectionHandlerBuilder().Codec(this.decoder.Object, this.encoder.Object).Build();
            handler.HandlerAdded(this.ctx.Object);
            return handler;
        }

        private void WriteRstStreamUsingVoidPromise(int streamId)
        {
            this.handler = this.NewHandler();
            var cause = new Http2RuntimeException("fake exception");
            this.stream.Setup(x => x.Id).Returns(STREAM_ID);
            this.frameWriter
                .Setup(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, Http2Error, IPromise>((ctx, id, err, p) =>
                {
                    Assert.False(p.IsVoid);
                    p.SetException(cause);
                    return p.Task;
                });
            this.handler.ResetStreamAsync(this.ctx.Object, streamId, Http2Error.StreamClosed, Http2TestUtil.NewVoidPromise(this.channel.Object));
            this.frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()));
            this.pipeline.Verify(x => x.FireExceptionCaught(It.Is<Exception>(v => ReferenceEquals(v, cause))));
        }

        private static IByteBuffer DummyData()
        {
            return Unpooled.Buffer().WriteBytes(Encoding.UTF8.GetBytes("abcdefgh"));
        }

        private static IByteBuffer AddSettingsHeader(IByteBuffer buf)
        {
            buf.WriteMedium(Http2CodecUtil.SettingEntryLength);
            buf.WriteByte((int)Http2FrameTypes.Settings);
            buf.WriteByte(0);
            buf.WriteInt(0);
            return buf;
        }
    }
}
