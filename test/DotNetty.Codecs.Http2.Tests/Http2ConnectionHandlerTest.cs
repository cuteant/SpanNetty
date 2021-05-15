
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
    using DotNetty.Transport.Channels;
    using Moq;
    using Xunit;

    public class Http2ConnectionHandlerTest : IDisposable
    {
        private const int STREAM_ID = 1;
        private const int NON_EXISTANT_STREAM_ID = 13;

        private Http2ConnectionHandler _handler;
        private IPromise _promise;
        private IPromise _voidPromise;

        private Mock<IHttp2Connection> _connection;
        private Mock<IHttp2RemoteFlowController> _remoteFlow;
        private Mock<IHttp2LocalFlowController> _localFlow;
        private Mock<IHttp2ConnectionEndpoint<IHttp2RemoteFlowController>> _remote;
        private Mock<IHttp2RemoteFlowController> _remoteFlowController;
        private Mock<IHttp2ConnectionEndpoint<IHttp2LocalFlowController>> _local;
        private Mock<IHttp2LocalFlowController> _localFlowController;
        private Mock<IChannelHandlerContext> _ctx;
        private Mock<IEventExecutor> _executor;
        private Mock<IChannel> _channel;
        private Mock<IChannelPipeline> _pipeline;
        private Mock<IHttp2Stream> _stream;
        private Mock<IHttp2ConnectionDecoder> _decoder;
        private Mock<IHttp2ConnectionEncoder> _encoder;
        private Mock<IHttp2FrameWriter> _frameWriter;

        private Task _future;
        private string _goAwayDebugCap;

        public Http2ConnectionHandlerTest()
        {
            _connection = new Mock<IHttp2Connection>();
            _remoteFlow = new Mock<IHttp2RemoteFlowController>();
            _localFlow = new Mock<IHttp2LocalFlowController>();
            _remote = new Mock<IHttp2ConnectionEndpoint<IHttp2RemoteFlowController>>();
            _remoteFlowController = new Mock<IHttp2RemoteFlowController>();
            _local = new Mock<IHttp2ConnectionEndpoint<IHttp2LocalFlowController>>();
            _localFlowController = new Mock<IHttp2LocalFlowController>();
            _ctx = new Mock<IChannelHandlerContext>();
            _executor = new Mock<IEventExecutor>();
            _channel = new Mock<IChannel>();
            _pipeline = new Mock<IChannelPipeline>();
            _stream = new Mock<IHttp2Stream>();
            _decoder = new Mock<IHttp2ConnectionDecoder>();
            _encoder = new Mock<IHttp2ConnectionEncoder>();
            _frameWriter = new Mock<IHttp2FrameWriter>();

            _channel.Setup(x => x.Metadata).Returns(new ChannelMetadata(false));
            var config = new DefaultChannelConfiguration(_channel.Object);
            _channel.Setup(x => x.Configuration).Returns(config);

            _promise = new DefaultPromise();
            _voidPromise = new DefaultPromise();
            var fakeException = new Http2RuntimeException("Fake exception");
            _future = TaskUtil.FromException(fakeException);

            _encoder.Setup(x => x.Connection).Returns(_connection.Object);
            _decoder.Setup(x => x.Connection).Returns(_connection.Object);
            _encoder.Setup(x => x.FrameWriter).Returns(_frameWriter.Object);
            _encoder.Setup(x => x.FlowController).Returns(_remoteFlow.Object);
            _decoder.Setup(x => x.FlowController).Returns(_localFlow.Object);
            _frameWriter
                .Setup(x => x.WriteGoAwayAsync(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, Http2Error, IByteBuffer, IPromise>((ctx, id, err, buf, p) =>
                {
                    _goAwayDebugCap = buf.ToString(Encoding.UTF8);
                    buf.Release();
                    return _future;
                });
            _channel.Setup(x => x.IsActive).Returns(true);
            _channel.Setup(x => x.Pipeline).Returns(_pipeline.Object);
            _connection.Setup(x => x.Remote).Returns(_remote.Object);
            _remote.Setup(x => x.FlowController).Returns(_remoteFlowController.Object);
            _connection.Setup(x => x.Local).Returns(_local.Object);
            _local.Setup(x => x.FlowController).Returns(_localFlowController.Object);
            _connection
                .Setup(x => x.ForEachActiveStream(It.IsAny<IHttp2StreamVisitor>()))
                .Returns<IHttp2StreamVisitor>(visitor =>
                {
                    if (!visitor.Visit(_stream.Object))
                    {
                        return _stream.Object;
                    }
                    return null;
                });
            _connection
                .Setup(x => x.ForEachActiveStream(It.IsAny<Func<IHttp2Stream, bool>>()))
                .Returns<Func<IHttp2Stream, bool>>(visitor =>
                {
                    if (!visitor(_stream.Object))
                    {
                        return _stream.Object;
                    }
                    return null;
                });
            _connection.Setup(x => x.Stream(It.Is<int>(v => v == NON_EXISTANT_STREAM_ID))).Returns(default(IHttp2Stream));
            _connection.Setup(x => x.NumActiveStreams).Returns(1);
            _connection.Setup(x => x.Stream(It.Is<int>(v => v == STREAM_ID))).Returns(_stream.Object);
            _connection
                .Setup(x => x.GoAwaySent(
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>()))
                .Returns(true);
            _stream.Setup(x => x.Open(It.IsAny<bool>())).Returns(_stream.Object);
            _encoder
                .Setup(x => x.WriteSettingsAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.IsAny<Http2Settings>(),
                    It.Is<IPromise>(v => v == _promise)))
                .Returns(_future);
            _ctx.Setup(x => x.Allocator).Returns(UnpooledByteBufferAllocator.Default);
            _ctx.Setup(x => x.Channel).Returns(_channel.Object);
            _ctx.Setup(x => x.NewPromise()).Returns(_promise);
            _ctx.Setup(x => x.VoidPromise()).Returns(_voidPromise);
            _ctx.Setup(x => x.WriteAsync(It.IsAny<object>())).Returns(_future);
            _ctx.Setup(x => x.Executor).Returns(_executor.Object);
            _ctx
                .Setup(x => x.FireChannelRead(It.IsAny<object>()))
                .Returns<object>(msg =>
                {
                    ReferenceCountUtil.Release(msg);
                    return _ctx.Object;
                });

        }

        public void Dispose()
        {
            if (_handler != null)
            {
                _handler.HandlerRemoved(_ctx.Object);
            }
        }

        [Fact]
        public void OnHttpServerUpgradeWithoutHandlerAdded()
        {
            var b = new Http2ConnectionHandlerBuilder();
            b.FrameListener = new Http2FrameAdapter();
            b.IsServer = true;
            _handler = b.Build();
            var e = Assert.Throws<Http2Exception>(() => _handler.OnHttpServerUpgrade(new Http2Settings()));
            Assert.Equal(Http2Error.InternalError, e.Error);
        }

        [Fact]
        public void OnHttpClientUpgradeWithoutHandlerAdded()
        {
            var b = new Http2ConnectionHandlerBuilder();
            b.FrameListener = new Http2FrameAdapter();
            b.IsServer = false;
            _handler = b.Build();
            var e = Assert.Throws<Http2Exception>(() => _handler.OnHttpClientUpgrade());
            Assert.Equal(Http2Error.InternalError, e.Error);
        }

        [Fact]
        public void ClientShouldveSentPrefaceAndSettingsFrameWhenUserEventIsTriggered()
        {
            _connection.Setup(x => x.IsServer).Returns(false);
            _channel.Setup(x => x.IsActive).Returns(false);
            _handler = NewHandler();
            _channel.Setup(x => x.IsActive).Returns(true);

            var evt = Http2ConnectionPrefaceAndSettingsFrameWrittenEvent.Instance;

            AtomicBoolean verified = new AtomicBoolean(false);
            _ctx
                .Setup(x => x.FireUserEventTriggered(It.Is<object>(v => ReferenceEquals(v, evt))))
                .Returns<object>(msg =>
                {
                    Assert.Same(msg, evt);
                    _ctx.Verify(x => x.WriteAsync(It.Is<object>(d => Http2CodecUtil.ConnectionPrefaceBuf().Equals((IByteBuffer)d))));
                    _encoder.Verify(
                        x => x.WriteSettingsAsync(
                            It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                            It.IsAny<Http2Settings>(),
                            It.IsAny<IPromise>()));
                    verified.Value = true;
                    return _ctx.Object;
                });

            _handler.ChannelActive(_ctx.Object);
            Assert.True(verified.Value);
        }

        [Fact]
        public void ClientShouldSendClientPrefaceStringWhenActive()
        {
            _connection.Setup(x => x.IsServer).Returns(false);
            _channel.Setup(x => x.IsActive).Returns(false);
            _handler = NewHandler();
            _channel.Setup(x => x.IsActive).Returns(true);
            _handler.ChannelActive(_ctx.Object);
            _ctx.Verify(x => x.WriteAsync(It.Is<object>(d => Http2CodecUtil.ConnectionPrefaceBuf().Equals((IByteBuffer)d))));
        }

        [Fact]
        public void ServerShouldNotSendClientPrefaceStringWhenActive()
        {
            _connection.Setup(x => x.IsServer).Returns(true);
            _channel.Setup(x => x.IsActive).Returns(false);
            _handler = NewHandler();
            _channel.Setup(x => x.IsActive).Returns(true);
            _handler.ChannelActive(_ctx.Object);
            _ctx.Verify(x => x.WriteAsync(It.Is<object>(d => Http2CodecUtil.ConnectionPrefaceBuf().Equals((IByteBuffer)d))), Times.Never());
        }

        [Fact]
        public void ServerReceivingInvalidClientPrefaceStringShouldHandleException()
        {
            _connection.Setup(x => x.IsServer).Returns(true);
            _handler = NewHandler();
            _handler.ChannelRead(_ctx.Object, Unpooled.CopiedBuffer("BAD_PREFACE", Encoding.UTF8));
            var captor = new ArgumentCaptor<IByteBuffer>();
            _frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == 0),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IByteBuffer>(v => captor.Capture(v)),
                    It.Is<IPromise>(v => v == _promise)));
            Assert.Equal(0, captor.GetValue().ReferenceCount);
        }

        [Fact]
        public void ServerReceivingHttp1ClientPrefaceStringShouldIncludePreface()
        {
            _connection.Setup(x => x.IsServer).Returns(true);
            _handler = NewHandler();
            _handler.ChannelRead(_ctx.Object, Unpooled.CopiedBuffer("GET /path HTTP/1.1", Encoding.ASCII));
            var captor = new ArgumentCaptor<IByteBuffer>();
            _frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == 0),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IByteBuffer>(v => captor.Capture(v)),
                    It.Is<IPromise>(v => v == _promise)));
            Assert.Equal(0, captor.GetValue().ReferenceCount);
            Assert.Contains("/path", _goAwayDebugCap);
        }

        [Fact]
        public void ServerReceivingClientPrefaceStringFollowedByNonSettingsShouldHandleException()
        {
            _connection.Setup(x => x.IsServer).Returns(true);
            _handler = NewHandler();

            // Create a connection preface followed by a bunch of zeros (i.e. not a settings frame).
            IByteBuffer buf = Unpooled.Buffer().WriteBytes(Http2CodecUtil.ConnectionPrefaceBuf()).WriteZero(10);
            _handler.ChannelRead(_ctx.Object, buf);
            var captor = new ArgumentCaptor<IByteBuffer>();
            _frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == 0),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IByteBuffer>(v => captor.Capture(v)),
                    It.Is<IPromise>(v => v == _promise)),
                Times.AtLeastOnce);
            Assert.Equal(0, captor.GetValue().ReferenceCount);
        }

        [Fact]
        public void ServerReceivingValidClientPrefaceStringShouldContinueReadingFrames()
        {
            _connection.Setup(x => x.IsServer).Returns(true);
            _handler = NewHandler();
            IByteBuffer prefacePlusSome = AddSettingsHeader(Unpooled.Buffer().WriteBytes(Http2CodecUtil.ConnectionPrefaceBuf()));
            _handler.ChannelRead(_ctx.Object, prefacePlusSome);
            _decoder.Verify(
                x => x.DecodeFrame(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<List<object>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public void VerifyChannelHandlerCanBeReusedInPipeline()
        {
            _connection.Setup(x => x.IsServer).Returns(true);
            _handler = NewHandler();
            // Only read the connection preface...after preface is read internal state of Http2ConnectionHandler
            // is expected to change relative to the pipeline.
            IByteBuffer preface = Http2CodecUtil.ConnectionPrefaceBuf();
            _handler.ChannelRead(_ctx.Object, preface);
            _decoder.Verify(
                x => x.DecodeFrame(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<List<object>>()),
                Times.Never());

            // Now remove and add the this.handler...this is setting up the test condition.
            _handler.HandlerRemoved(_ctx.Object);
            _handler.HandlerAdded(_ctx.Object);

            // Now verify we can continue as normal, reading connection preface plus more.
            IByteBuffer prefacePlusSome = AddSettingsHeader(Unpooled.Buffer().WriteBytes(Http2CodecUtil.ConnectionPrefaceBuf()));
            _handler.ChannelRead(_ctx.Object, prefacePlusSome);
            _decoder.Verify(
                x => x.DecodeFrame(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<List<object>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public void ChannelInactiveShouldCloseStreams()
        {
            _handler = NewHandler();
            _handler.ChannelInactive(_ctx.Object);
            _connection.Verify(x => x.CloseAsync(It.IsAny<IPromise>()));
        }

        [Fact]
        public void ConnectionErrorShouldStartShutdown()
        {
            _handler = NewHandler();
            Http2Exception e = new Http2Exception(Http2Error.ProtocolError);
            _remote.Setup(x => x.LastStreamCreated).Returns(STREAM_ID);
            _handler.ExceptionCaught(_ctx.Object, e);
            var captor = new ArgumentCaptor<IByteBuffer>();
            _frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IByteBuffer>(v => captor.Capture(v)),
                    It.Is<IPromise>(v => v == _promise)));
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
            _handler = NewHandler();
            Http2Exception e = new HeaderListSizeException(STREAM_ID, Http2Error.ProtocolError,
                    "Header size exceeded max allowed size 8196", true);

            _stream.Setup(x => x.Id).Returns(STREAM_ID);
            _connection.Setup(x => x.IsServer).Returns(true);
            _stream.Setup(x => x.IsHeadersSent).Returns(false);
            _remote.Setup(x => x.LastStreamCreated).Returns(STREAM_ID);
            _frameWriter
                .Setup(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IPromise>(v => v == _promise)))
                .Returns(_future);

            _handler.ExceptionCaught(_ctx.Object, e);

            var captor = new ArgumentCaptor<IHttp2Headers>();
            _encoder.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<IHttp2Headers>(v => captor.Capture(v)),
                    It.Is<int>(v => v == padding),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == _promise)));
            IHttp2Headers headers = captor.GetValue();
            Assert.Equal(HttpResponseStatus.RequestHeaderFieldsTooLarge.CodeAsText, headers.Status);
            _frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IPromise>(v => v == _promise)));
        }

        [Fact]
        public void ServerShouldNeverSend431HeaderSizeErrorWhenEncoding()
        {
            int padding = 0;
            _handler = NewHandler();
            Http2Exception e = new HeaderListSizeException(STREAM_ID, Http2Error.ProtocolError,
                "Header size exceeded max allowed size 8196", false);

            _stream.Setup(x => x.Id).Returns(STREAM_ID);
            _connection.Setup(x => x.IsServer).Returns(true);
            _stream.Setup(x => x.IsHeadersSent).Returns(false);
            _remote.Setup(x => x.LastStreamCreated).Returns(STREAM_ID);
            _frameWriter
                .Setup(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IPromise>(v => v == _promise)))
                .Returns(_future);

            _handler.ExceptionCaught(_ctx.Object, e);

            _encoder.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<IHttp2Headers>(),
                    It.Is<int>(v => v == padding),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == _promise)), Times.Never());
            _frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IPromise>(v => v == _promise)));
        }

        [Fact]
        public void ClientShouldNeverSend431WhenHeadersAreTooLarge()
        {
            int padding = 0;
            _handler = NewHandler();
            Http2Exception e = new HeaderListSizeException(STREAM_ID, Http2Error.ProtocolError,
                    "Header size exceeded max allowed size 8196", true);

            _stream.Setup(x => x.Id).Returns(STREAM_ID);
            _connection.Setup(x => x.IsServer).Returns(false);
            _stream.Setup(x => x.IsHeadersSent).Returns(false);
            _remote.Setup(x => x.LastStreamCreated).Returns(STREAM_ID);
            _frameWriter
                .Setup(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IPromise>(v => v == _promise)))
                .Returns(_future);

            _handler.ExceptionCaught(_ctx.Object, e);

            _encoder.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<IHttp2Headers>(),
                    It.Is<int>(v => v == padding),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == _promise)), Times.Never());
            _frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IPromise>(v => v == _promise)));
        }

        sealed class PrefaceUserEventHttp2ConnectionHandler : Http2ConnectionHandler
        {
            readonly CountdownEvent _latch;

            public PrefaceUserEventHttp2ConnectionHandler(CountdownEvent latch, IHttp2ConnectionDecoder decoder, IHttp2ConnectionEncoder encoder, Http2Settings initialSettings)
                : base(decoder, encoder, initialSettings)
            {
                _latch = latch;
            }

            public override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
            {
                if (ReferenceEquals(evt, Http2ConnectionPrefaceAndSettingsFrameWrittenEvent.Instance))
                {
                    _latch.SafeSignal();
                }
            }
        }
        [Fact]
        public void PrefaceUserEventProcessed()
        {
            var latch = new CountdownEvent(1);
            _handler = new PrefaceUserEventHttp2ConnectionHandler(latch, _decoder.Object, _encoder.Object, new Http2Settings());
            _handler.HandlerAdded(_ctx.Object);
            Assert.True(latch.Wait(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void ServerShouldNeverSend431IfHeadersAlreadySent()
        {
            int padding = 0;
            _handler = NewHandler();
            Http2Exception e = new HeaderListSizeException(STREAM_ID, Http2Error.ProtocolError,
                "Header size exceeded max allowed size 8196", true);

            _stream.Setup(x => x.Id).Returns(STREAM_ID);
            _connection.Setup(x => x.IsServer).Returns(true);
            _stream.Setup(x => x.IsHeadersSent).Returns(true);
            _remote.Setup(x => x.LastStreamCreated).Returns(STREAM_ID);
            _frameWriter
                .Setup(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IPromise>(v => v == _promise)))
                .Returns(_future);
            _handler.ExceptionCaught(_ctx.Object, e);

            _encoder.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<IHttp2Headers>(),
                    It.Is<int>(v => v == padding),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == _promise)), Times.Never());

            _frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IPromise>(v => v == _promise)));
        }

        [Fact]
        public void ServerShouldCreateStreamIfNeededBeforeSending431()
        {
            int padding = 0;
            _handler = NewHandler();
            Http2Exception e = new HeaderListSizeException(STREAM_ID, Http2Error.ProtocolError,
                "Header size exceeded max allowed size 8196", true);

            _connection.Setup(x => x.Stream(It.Is<int>(v => v == STREAM_ID))).Returns(default(IHttp2Stream));
            _remote
                .Setup(x => x.CreateStream(
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<bool>(v => v == true)))
                .Returns(_stream.Object);
            _stream.Setup(x => x.Id).Returns(STREAM_ID);

            _connection.Setup(x => x.IsServer).Returns(true);
            _stream.Setup(x => x.IsHeadersSent).Returns(false);
            _remote.Setup(x => x.LastStreamCreated).Returns(STREAM_ID);
            _frameWriter
                .Setup(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IPromise>(v => v == _promise)))
                .Returns(_future);
            _handler.ExceptionCaught(_ctx.Object, e);

            _remote.Verify(
                x => x.CreateStream(
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<bool>(v => v == true)));
            _encoder.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<IHttp2Headers>(),
                    It.Is<int>(v => v == padding),
                    It.Is<bool>(v => v == true),
                    It.Is<IPromise>(v => v == _promise)));

            _frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError),
                    It.Is<IPromise>(v => v == _promise)));
        }

        [Fact]
        public void EncoderAndDecoderAreClosedOnChannelInactive()
        {
            _handler = NewHandler();
            _handler.ChannelActive(_ctx.Object);
            _channel.Setup(x => x.IsActive).Returns(false);
            _handler.ChannelInactive(_ctx.Object);
            _encoder.Verify(x => x.Close());
            _decoder.Verify(x => x.Close());
        }

        [Fact]
        public void WriteRstOnNonExistantStreamShouldSucceed()
        {
            _handler = NewHandler();
            _frameWriter
                .Setup(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == NON_EXISTANT_STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.StreamClosed),
                    It.Is<IPromise>(v => v == _promise)))
                .Returns(_future);
            _handler.ResetStreamAsync(_ctx.Object, NON_EXISTANT_STREAM_ID, Http2Error.StreamClosed, _promise);
            _frameWriter
                .Verify(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == NON_EXISTANT_STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.StreamClosed),
                    It.Is<IPromise>(v => v == _promise)));
        }

        [Fact]
        public void WriteRstOnClosedStreamShouldSucceed()
        {
            _handler = NewHandler();
            _stream.Setup(x => x.Id).Returns(STREAM_ID);
            _frameWriter
                .Setup(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()))
                .Returns(_future);
            _stream.Setup(x => x.State).Returns(Http2StreamState.Closed);
            _stream.Setup(x => x.IsHeadersSent).Returns(true);
            // The stream is "closed" but is still known about by the connection (connection().stream(..)
            // will return the stream). We should still write a RST_STREAM frame in this scenario.
            _handler.ResetStreamAsync(_ctx.Object, STREAM_ID, Http2Error.StreamClosed, _promise);
            _frameWriter
                .Verify(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()));
        }

        [Fact]
        public void WriteRstOnIdleStreamShouldNotWriteButStillSucceed()
        {
            _handler = NewHandler();
            _stream.Setup(x => x.State).Returns(Http2StreamState.Idle);
            _handler.ResetStreamAsync(_ctx.Object, STREAM_ID, Http2Error.StreamClosed, _promise);
            _frameWriter
                .Verify(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()),
                Times.Never());
            _stream.Verify(x => x.Close());
        }

        [Fact]
        public void CloseListenerShouldBeNotifiedOnlyOneTime()
        {
            _handler = NewHandler();
            _future = TaskUtil.Completed;
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
            _handler.Close(_ctx.Object, _promise);
            _connection
                .Setup(x => x.ForEachActiveStream(It.IsAny<IHttp2StreamVisitor>()))
                .Returns(default(IHttp2Stream));
            _connection
                .Setup(x => x.ForEachActiveStream(It.IsAny<Func<IHttp2Stream, bool>>()))
                .Returns(default(IHttp2Stream));
            if (_future.IsCompleted)
            {
                _connection.Setup(x => x.NumActiveStreams).Returns(0);
            }
            _handler.CloseStream(_stream.Object, _future);
            // Simulate another stream close call being made after the context should already be closed.
            _handler.CloseStream(_stream.Object, _future);
            _ctx.Verify(x => x.CloseAsync(It.IsAny<IPromise>()), Times.Once);
        }

        [Fact]
        public void CanSendGoAwayFrame()
        {
            IByteBuffer data = DummyData();
            var errorCode = Http2Error.InternalError;
            _future = TaskUtil.Completed;
            _handler = NewHandler();
            _handler.GoAwayAsync(_ctx.Object, STREAM_ID, errorCode, data, _promise);

            _connection.Verify(
                x => x.GoAwaySent(
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == errorCode),
                    It.Is<IByteBuffer>(v => v.Equals(data))));
            _frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == errorCode),
                    It.Is<IByteBuffer>(v => v.Equals(data)),
                    It.Is<IPromise>(v => v == _promise)));
            _ctx.Verify(x => x.CloseAsync());
            Assert.Equal(0, data.ReferenceCount);
        }

        [Fact]
        public void CanSendGoAwayFramesWithDecreasingLastStreamIds()
        {
            _handler = NewHandler();
            IByteBuffer data = DummyData();
            var errorCode = Http2Error.InternalError;

            _handler.GoAwayAsync(_ctx.Object, STREAM_ID + 2, errorCode, (IByteBuffer)data.Retain(), _promise);
            _frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID + 2),
                    It.Is<Http2Error>(v => v == errorCode),
                    It.Is<IByteBuffer>(v => v.Equals(data)),
                    It.Is<IPromise>(v => v == _promise)));
            _connection.Verify(
                x => x.GoAwaySent(
                    It.Is<int>(v => v == STREAM_ID + 2),
                    It.Is<Http2Error>(v => v == errorCode),
                    It.Is<IByteBuffer>(v => v.Equals(data))));
            _promise = new DefaultPromise();
            _handler.GoAwayAsync(_ctx.Object, STREAM_ID, errorCode, data, _promise);
            _frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == errorCode),
                    It.Is<IByteBuffer>(v => v.Equals(data)),
                    It.Is<IPromise>(v => v == _promise)));
            _connection.Verify(
                x => x.GoAwaySent(
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == errorCode),
                    It.Is<IByteBuffer>(v => v.Equals(data))));
            Assert.Equal(0, data.ReferenceCount);
        }

        [Fact]
        public void CannotSendGoAwayFrameWithIncreasingLastStreamIds()
        {
            _handler = NewHandler();
            IByteBuffer data = DummyData();
            var errorCode = Http2Error.InternalError;

            _handler.GoAwayAsync(_ctx.Object, STREAM_ID, errorCode, (IByteBuffer)data.Retain(), _promise);
            _connection.Verify(
                x => x.GoAwaySent(
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == errorCode),
                    It.Is<IByteBuffer>(v => v.Equals(data))));
            _frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == errorCode),
                    It.Is<IByteBuffer>(v => v.Equals(data)),
                    It.Is<IPromise>(v => v == _promise)));
            // The frameWriter is only mocked, so it should not have interacted with the promise.
            Assert.False(_promise.IsCompleted);

            _connection.Setup(x => x.GoAwaySent()).Returns(true);
            _remote.Setup(x => x.LastStreamKnownByPeer()).Returns(STREAM_ID);
            _connection
                .Setup(x => x.GoAwaySent(
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>()))
                .Throws(new InvalidOperationException());
            _handler.GoAwayAsync(_ctx.Object, STREAM_ID + 2, errorCode, data, _promise);
            Assert.True(_promise.IsCompleted);
            Assert.False(_promise.IsSuccess);
            Assert.Equal(0, data.ReferenceCount);
            _frameWriter.VerifyNoOtherCalls();
        }

        [Fact]
        public void CanSendGoAwayUsingVoidPromise()
        {
            _handler = NewHandler();
            IByteBuffer data = DummyData();
            var errorCode = Http2Error.InternalError;
            _handler = NewHandler();
            var cause = new Http2RuntimeException("fake exception");
            _frameWriter
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
            _handler.GoAwayAsync(_ctx.Object, STREAM_ID, errorCode, data, Http2TestUtil.NewVoidPromise(_channel.Object));
            _pipeline.Verify(x => x.FireExceptionCaught(It.Is<Exception>(v => ReferenceEquals(v, cause))));
        }

        [Fact]
        public void ChannelReadCompleteTriggersFlush()
        {
            _handler = NewHandler();
            _handler.ChannelReadComplete(_ctx.Object);
            _ctx.Verify(x => x.Flush(), Times.Once);
        }

        [Fact]
        public void ChannelReadCompleteCallsReadWhenAutoReadFalse()
        {
            _channel.Object.Configuration.IsAutoRead = false;
            _handler = NewHandler();
            _handler.ChannelReadComplete(_ctx.Object);
            _ctx.Verify(x => x.Read(), Times.Once());
        }

        [Fact]
        public void ChannelClosedDoesNotThrowPrefaceException()
        {
            _connection.Setup(x => x.IsServer).Returns(true);
            _handler = NewHandler();
            _channel.Setup(x => x.IsActive).Returns(false);
            _handler.ChannelInactive(_ctx.Object);
            _frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<IPromise>()),
                Times.Never());
            _frameWriter.Verify(
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
            WriteRstStreamUsingVoidPromise(NON_EXISTANT_STREAM_ID);
        }

        [Fact]
        public void WriteRstStreamForKnownStreamUsingVoidPromise()
        {
            WriteRstStreamUsingVoidPromise(STREAM_ID);
        }

        [Fact]
        public void GracefulShutdownTimeoutWhenConnectionErrorHardShutdownTest()
        {
            GracefulShutdownTimeoutWhenConnectionErrorTest0(ShutdownHint.HardShutdown);
        }

        [Fact]
        public void GracefulShutdownTimeoutWhenConnectionErrorGracefulShutdownTest()
        {
            GracefulShutdownTimeoutWhenConnectionErrorTest0(ShutdownHint.GracefulShutdown);
        }

        private void GracefulShutdownTimeoutWhenConnectionErrorTest0(ShutdownHint hint)
        {
            _handler = NewHandler();
            long expectedMillis = 1234;
            _handler.GracefulShutdownTimeout = TimeSpan.FromMilliseconds(expectedMillis);
            Http2Exception exception = new Http2Exception(Http2Error.ProtocolError, "Test error", hint);
            _handler.OnConnectionError(_ctx.Object, false, exception, exception);
            if (hint == ShutdownHint.GracefulShutdown)
            {
                _executor.Verify(
                    x => x.Schedule(
                        It.IsAny<Action<object>>(),
                        It.IsAny<object>(),
                        It.Is<TimeSpan>(v => v == TimeSpan.FromMilliseconds(expectedMillis))),
                    Times.AtLeastOnce());
            }
            else
            {
                _executor.Verify(
                    x => x.Schedule(
                        It.IsAny<Action<object, object>>(),
                        It.IsAny<object>(),
                        It.IsAny<object>(),
                        It.Is<TimeSpan>(v => v == TimeSpan.FromMilliseconds(expectedMillis))),
                    Times.AtLeastOnce());
            }
        }

        [Fact]
        public void GracefulShutdownTimeoutTest()
        {
            _handler = NewHandler();
            long expectedMillis = 1234;
            _handler.GracefulShutdownTimeout = TimeSpan.FromMilliseconds(expectedMillis);
            _handler.Close(_ctx.Object, _promise);
            _executor.Verify(
                x => x.Schedule(
                    It.IsAny<Action<object>>(),
                    It.IsAny<object>(),
                    It.Is<TimeSpan>(v => v == TimeSpan.FromMilliseconds(expectedMillis))),
                Times.AtLeastOnce());
        }

        [Fact]
        public void GracefulShutdownTimeoutNoActiveStreams()
        {
            _handler = NewHandler();
            _connection.Setup(x => x.NumActiveStreams).Returns(0);
            long expectedMillis = 1234;
            _handler.GracefulShutdownTimeout = TimeSpan.FromMilliseconds(expectedMillis);
            _handler.Close(_ctx.Object, _promise);
            _executor.Verify(
                x => x.Schedule(
                    It.IsAny<Action<object, object>>(),
                    It.IsAny<object>(),
                    It.IsAny<object>(),
                    It.Is<TimeSpan>(v => v == TimeSpan.FromMilliseconds(expectedMillis))),
                Times.AtLeastOnce());
        }

        [Fact]
        public void GracefulShutdownIndefiniteTimeoutTest()
        {
            _handler = NewHandler();
            _handler.GracefulShutdownTimeout = TimeSpan.FromMilliseconds(-1);
            _handler.Close(_ctx.Object, _promise);
            _executor.Verify(
                x => x.Schedule(
                    It.IsAny<Action<object, object>>(),
                    It.IsAny<object>(),
                    It.IsAny<object>(),
                    It.IsAny<TimeSpan>()),
                Times.Never());
        }

        [Fact]
        public void WriteMultipleRstFramesForSameStream()
        {
            _handler = NewHandler();
            _stream.SetupGet(x => x.Id).Returns(STREAM_ID);
            AtomicBoolean resetSent = new AtomicBoolean();
            _stream.Setup(x => x.ResetSent()).Returns(() =>
            {
                resetSent.Value = true;
                return _stream.Object;
            });
            _stream.SetupGet(x => x.IsResetSent).Returns(() => resetSent.Value);
            _frameWriter
                .Setup(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, Http2Error, IPromise>((ctx, id, err, p) =>
                {
                    p.TryComplete();
                    return p.Task;
                });

            var promise = new DefaultPromise();
            var promise2 = new DefaultPromise();
            promise.Task.ContinueWith(t =>
            {
                _handler.ResetStreamAsync(_ctx.Object, STREAM_ID, Http2Error.StreamClosed, promise2);
            }, TaskContinuationOptions.ExecuteSynchronously);

            _handler.ResetStreamAsync(_ctx.Object, STREAM_ID, Http2Error.Cancel, promise);
            _frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()));
            Assert.True(promise.IsSuccess);
            Assert.True(promise2.IsSuccess);
        }

        private Http2ConnectionHandler NewHandler()
        {
            Http2ConnectionHandler handler = new Http2ConnectionHandlerBuilder().Codec(_decoder.Object, _encoder.Object).Build();
            handler.HandlerAdded(_ctx.Object);
            return handler;
        }

        private void WriteRstStreamUsingVoidPromise(int streamId)
        {
            _handler = NewHandler();
            var cause = new Http2RuntimeException("fake exception");
            _stream.Setup(x => x.Id).Returns(STREAM_ID);
            _frameWriter
                .Setup(x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, Http2Error, IPromise>((ctx, id, err, p) =>
                {
                    Assert.False(p.IsVoid);
                    p.SetException(cause);
                    return p.Task;
                });
            _handler.ResetStreamAsync(_ctx.Object, streamId, Http2Error.StreamClosed, Http2TestUtil.NewVoidPromise(_channel.Object));
            _frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()));
            _pipeline.Verify(x => x.FireExceptionCaught(It.Is<Exception>(v => ReferenceEquals(v, cause))));
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
