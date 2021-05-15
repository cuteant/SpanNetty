
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Moq;
    using Xunit;

    /**
     * Unit tests for {@link Http2FrameCodec}.
     */
    public class Http2FrameCodecTest : IDisposable
    {
        // For verifying outbound frames
        private Mock<IHttp2FrameWriter> _frameWriter;
        private Http2FrameCodec _frameCodec;
        private EmbeddedChannel _channel;

        // For injecting inbound frames
        private Http2FrameInboundWriter _frameInboundWriter;

        private LastInboundHandler _inboundHandler;

        private readonly IHttp2Headers _request;
        private readonly IHttp2Headers _response;

        public Http2FrameCodecTest()
        {
            _request = new DefaultHttp2Headers();
            _request.Method = HttpMethod.Get.AsciiName;
            _request.Scheme = HttpScheme.Https.Name;
            _request.Authority = new AsciiString("example.org");
            _request.Path = new AsciiString("/foo");
            _response = new DefaultHttp2Headers();
            _response.Status = HttpResponseStatus.OK.CodeAsText;
            SetUp(Http2FrameCodecBuilder.ForServer(), new Http2Settings());
        }

        public void Dispose()
        {
            Dispose0();
        }

        private void Dispose0()
        {
            if (_inboundHandler != null)
            {
                _inboundHandler.FinishAndReleaseAll();
                _inboundHandler = null;
            }
            var ch = _channel;
            if (ch != null)
            {
                _channel = null;
                ch.FinishAndReleaseAll();
                ch.CloseAsync();
            }
        }

        private void SetUp(Http2FrameCodecBuilder frameCodecBuilder, Http2Settings initialRemoteSettings)
        {
            // Some tests call this method twice. Once with JUnit's @Before and once directly to pass special settings.
            // This call ensures that in case of two consecutive calls to setUp(), the previous channel is shutdown and
            // ByteBufs are released correctly.
            Dispose0();

            _frameWriter = Http2TestUtil.MockedFrameWriter();

            var builder = frameCodecBuilder.FrameWriter(_frameWriter.Object);
            builder.FrameLogger = new Http2FrameLogger(Common.Internal.Logging.InternalLogLevel.TRACE);
            builder.InitialSettings = initialRemoteSettings;
            _frameCodec = frameCodecBuilder.Build();
            _inboundHandler = new LastInboundHandler();

            _channel = new EmbeddedChannel();
            _frameInboundWriter = new Http2FrameInboundWriter(_channel);
            //channel.Connect(new InetSocketAddress(0));
            _channel.Pipeline.AddLast(_frameCodec);
            _channel.Pipeline.AddLast(_inboundHandler);
            _channel.Pipeline.FireChannelActive();

            // Handshake
            _frameWriter.Verify(
                x => x.WriteSettingsAsync(
                    It.Is<IChannelHandlerContext>(v => v == _frameCodec._ctx),
                    It.IsAny<Http2Settings>(),
                    It.IsAny<IPromise>()));
            _frameWriter.VerifyNoOtherCalls();
            _channel.WriteInbound(Http2CodecUtil.ConnectionPrefaceBuf());

            _frameInboundWriter.WriteInboundSettings(initialRemoteSettings);
            _frameWriter.Verify(
                x => x.WriteSettingsAckAsync(
                    It.Is<IChannelHandlerContext>(v => v == _frameCodec._ctx),
                    It.IsAny<IPromise>()));
            _frameInboundWriter.WriteInboundSettingsAck();

            var settingsFrame = _inboundHandler.ReadInbound<IHttp2SettingsFrame>();
            Assert.NotNull(settingsFrame);
            var settingsAckFrame = _inboundHandler.ReadInbound<IHttp2SettingsAckFrame>();
            Assert.NotNull(settingsAckFrame);
        }

        [Fact]
        public void StateChanges()
        {
            _frameInboundWriter.WriteInboundHeaders(1, _request, 31, true);

            IHttp2Stream stream = _frameCodec.Connection.Stream(1);
            Assert.NotNull(stream);
            Assert.Equal(Http2StreamState.HalfClosedRemote, stream.State);

            var evt = _inboundHandler.ReadInboundMessageOrUserEvent<Http2FrameStreamEvent>();
            Assert.Equal(Http2StreamState.HalfClosedRemote, evt.Stream.State);

            var inboundFrame = _inboundHandler.ReadInbound<IHttp2StreamFrame>();
            IHttp2FrameStream stream2 = inboundFrame.Stream;
            Assert.NotNull(stream2);
            Assert.Equal(1, stream2.Id);
            Assert.Equal(inboundFrame, new DefaultHttp2HeadersFrame(_request, true, 31) { Stream = stream2 });
            Assert.Null(_inboundHandler.ReadInbound());

            _channel.WriteOutbound(new DefaultHttp2HeadersFrame(_response, true, 27) { Stream = stream2 });
            _frameWriter.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _frameCodec._ctx),
                    It.Is<int>(v => v == 1),
                    It.Is<IHttp2Headers>(v => v.Equals(_response)),
                    It.Is<int>(v => v == 27),
                    It.Is<bool>(v => v == true),
                    It.IsAny<IPromise>()));
            _frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _frameCodec._ctx),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()),
                Times.Never());

            Assert.Equal(Http2StreamState.Closed, stream.State);
            evt = _inboundHandler.ReadInboundMessageOrUserEvent<Http2FrameStreamEvent>();
            Assert.Equal(Http2StreamState.Closed, evt.Stream.State);

            Assert.True(_channel.IsActive);
        }

        [Fact]
        public void HeaderRequestHeaderResponse()
        {
            _frameInboundWriter.WriteInboundHeaders(1, _request, 31, true);

            IHttp2Stream stream = _frameCodec.Connection.Stream(1);
            Assert.NotNull(stream);
            Assert.Equal(Http2StreamState.HalfClosedRemote, stream.State);

            var inboundFrame = _inboundHandler.ReadInbound<IHttp2StreamFrame>();
            IHttp2FrameStream stream2 = inboundFrame.Stream;
            Assert.NotNull(stream2);
            Assert.Equal(1, stream2.Id);
            Assert.Equal(inboundFrame, new DefaultHttp2HeadersFrame(_request, true, 31) { Stream = stream2 });
            Assert.Null(_inboundHandler.ReadInbound());

            _channel.WriteOutbound(new DefaultHttp2HeadersFrame(_response, true, 27) { Stream = stream2 });
            _frameWriter.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _frameCodec._ctx),
                    It.Is<int>(v => v == 1),
                    It.Is<IHttp2Headers>(v => v.Equals(_response)),
                    It.Is<int>(v => v == 27),
                    It.Is<bool>(v => v == true),
                    It.IsAny<IPromise>()));
            _frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _frameCodec._ctx),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()),
                Times.Never());

            Assert.Equal(Http2StreamState.Closed, stream.State);
            Assert.True(_channel.IsActive);
        }

        [Fact]
        public void FlowControlShouldBeResilientToMissingStreams()
        {
            IHttp2Connection conn = new DefaultHttp2Connection(true);
            IHttp2ConnectionEncoder enc = new DefaultHttp2ConnectionEncoder(conn, new DefaultHttp2FrameWriter());
            IHttp2ConnectionDecoder dec = new DefaultHttp2ConnectionDecoder(conn, enc, new DefaultHttp2FrameReader());
            Http2FrameCodec codec = new Http2FrameCodec(enc, dec, new Http2Settings(), false);
            EmbeddedChannel em = new EmbeddedChannel(codec);

            // We call #consumeBytes on a stream id which has not been seen yet to emulate the case
            // where a stream is deregistered which in reality can happen in response to a RST.
            Assert.False(codec.ConsumeBytes(1, 1));
            Assert.True(em.FinishAndReleaseAll());
            Assert.True(true);
        }

        [Fact]
        public void EntityRequestEntityResponse()
        {
            _frameInboundWriter.WriteInboundHeaders(1, _request, 0, false);

            IHttp2Stream stream = _frameCodec.Connection.Stream(1);
            Assert.NotNull(stream);
            Assert.Equal(Http2StreamState.Open, stream.State);

            IHttp2HeadersFrame inboundHeaders = _inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            IHttp2FrameStream stream2 = inboundHeaders.Stream;
            Assert.NotNull(stream2);
            Assert.Equal(1, stream2.Id);
            Assert.Equal(new DefaultHttp2HeadersFrame(_request, false) { Stream = stream2 }, inboundHeaders);
            Assert.Null(_inboundHandler.ReadInbound());

            IByteBuffer hello = Http2TestUtil.BB("hello");
            _frameInboundWriter.WriteInboundData(1, hello, 31, true);
            IHttp2DataFrame inboundData = _inboundHandler.ReadInbound<IHttp2DataFrame>();
            IHttp2DataFrame expected = new DefaultHttp2DataFrame(Http2TestUtil.BB("hello"), true, 31) { Stream = stream2 };
            Http2TestUtil.AssertEqualsAndRelease(expected, inboundData);

            Assert.Null(_inboundHandler.ReadInbound());

            _channel.WriteOutbound(new DefaultHttp2HeadersFrame(_response, false) { Stream = stream2 });
            _frameWriter.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _frameCodec._ctx),
                    It.Is<int>(v => v == 1),
                    It.Is<IHttp2Headers>(v => v.Equals(_response)),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.IsAny<IPromise>()));

            _channel.WriteOutbound(new DefaultHttp2DataFrame(Http2TestUtil.BB("world"), true, 27) { Stream = stream2 });
            var outboundData = new ArgumentCaptor<IByteBuffer>();
            _frameWriter.Verify(
                x => x.WriteDataAsync(
                    It.Is<IChannelHandlerContext>(v => v == _frameCodec._ctx),
                    It.Is<int>(v => v == 1),
                    It.Is<IByteBuffer>(v => outboundData.Capture(v)),
                    It.Is<int>(v => v == 27),
                    It.Is<bool>(v => v == true),
                    It.IsAny<IPromise>()));

            IByteBuffer bb = Http2TestUtil.BB("world");
            Assert.Equal(bb, outboundData.GetValue());
            Assert.Equal(1, outboundData.GetValue().ReferenceCount);
            bb.Release();
            outboundData.GetValue().Release();

            _frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _frameCodec._ctx),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()), Times.Never());
            Assert.True(_channel.IsActive);
        }

        [Fact]
        public void SendRstStream()
        {
            _frameInboundWriter.WriteInboundHeaders(3, _request, 31, true);

            IHttp2Stream stream = _frameCodec.Connection.Stream(3);
            Assert.NotNull(stream);
            Assert.Equal(Http2StreamState.HalfClosedRemote, stream.State);

            IHttp2HeadersFrame inboundHeaders = _inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(inboundHeaders);
            Assert.True(inboundHeaders.IsEndStream);

            IHttp2FrameStream stream2 = inboundHeaders.Stream;
            Assert.NotNull(stream2);
            Assert.Equal(3, stream2.Id);

            _channel.WriteOutbound(new DefaultHttp2ResetFrame((Http2Error)314 /* non-standard error */) { Stream = stream2 });
            _frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _frameCodec._ctx),
                    It.Is<int>(v => v == 3),
                    It.Is<Http2Error>(v => v == (Http2Error)314L),
                    It.IsAny<IPromise>()));
            Assert.Equal(Http2StreamState.Closed, stream.State);
            Assert.True(_channel.IsActive);
        }

        [Fact]
        public void ReceiveRstStream()
        {
            _frameInboundWriter.WriteInboundHeaders(3, _request, 31, false);

            IHttp2Stream stream = _frameCodec.Connection.Stream(3);
            Assert.NotNull(stream);
            Assert.Equal(Http2StreamState.Open, stream.State);

            IHttp2HeadersFrame expectedHeaders = new DefaultHttp2HeadersFrame(_request, false, 31);
            IHttp2HeadersFrame actualHeaders = _inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            expectedHeaders.Stream = actualHeaders.Stream;
            Assert.Equal(expectedHeaders, actualHeaders);

            _frameInboundWriter.WriteInboundRstStream(3, Http2Error.NoError);

            IHttp2ResetFrame expectedRst = new DefaultHttp2ResetFrame(Http2Error.NoError) { Stream = actualHeaders.Stream };
            IHttp2ResetFrame actualRst = _inboundHandler.ReadInbound<IHttp2ResetFrame>();
            Assert.Equal(expectedRst, actualRst);

            Assert.Null(_inboundHandler.ReadInbound());
        }

        [Fact]
        public void SendGoAway()
        {
            _frameInboundWriter.WriteInboundHeaders(3, _request, 31, false);

            IHttp2Stream stream = _frameCodec.Connection.Stream(3);
            Assert.NotNull(stream);
            Assert.Equal(Http2StreamState.Open, stream.State);

            IByteBuffer debugData = Http2TestUtil.BB("debug");
            IByteBuffer expected = debugData.Copy();

            IHttp2GoAwayFrame goAwayFrame = new DefaultHttp2GoAwayFrame(Http2Error.NoError, debugData.RetainedDuplicate());
            goAwayFrame.ExtraStreamIds = 2;

            _channel.WriteOutbound(goAwayFrame);
            _frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == _frameCodec._ctx),
                    It.Is<int>(v => v == 7),
                    It.Is<Http2Error>(v => v == Http2Error.NoError),
                    It.Is<IByteBuffer>(v => v.Equals(expected)),
                    It.IsAny<IPromise>()));
            Assert.Equal(Http2StreamState.Open, stream.State);
            Assert.True(_channel.IsActive);
            expected.Release();
            debugData.Release();
        }

        [Fact]
        public void ReceiveGoaway()
        {
            IByteBuffer debugData = Http2TestUtil.BB("foo");
            _frameInboundWriter.WriteInboundGoAway(2, Http2Error.NoError, debugData);
            IHttp2GoAwayFrame expectedFrame = new DefaultHttp2GoAwayFrame(2, Http2Error.NoError, Http2TestUtil.BB("foo"));
            IHttp2GoAwayFrame actualFrame = _inboundHandler.ReadInbound<IHttp2GoAwayFrame>();

            Http2TestUtil.AssertEqualsAndRelease(expectedFrame, actualFrame);
            Assert.Null(_inboundHandler.ReadInbound());
        }

        sealed class TestUnknownHttp2Frame : AbstractReferenceCounted, IHttp2Frame
        {
            public string Name => "UNKNOWN";

            public override IReferenceCounted Touch(object hint)
            {
                return this;
            }

            protected override void Deallocate() { }
        }

        [Fact]
        public async Task UnknownFrameTypeShouldThrowAndBeReleased()
        {

            var frame = new TestUnknownHttp2Frame();
            Assert.Equal(1, frame.ReferenceCount);

            var f = _channel.WriteAsync(frame);
            try
            {
                await f;
            }
            catch (Exception ex)
            {
                Assert.IsType<UnsupportedMessageTypeException>(ex);
            }
            Assert.True(f.IsCompleted);
            Assert.False(f.IsSuccess());

            Assert.Equal(0, frame.ReferenceCount);

        }

        [Fact]
        public void GoAwayLastStreamIdOverflowed()
        {
            _frameInboundWriter.WriteInboundHeaders(5, _request, 31, false);

            IHttp2Stream stream = _frameCodec.Connection.Stream(5);
            Assert.NotNull(stream);
            Assert.Equal(Http2StreamState.Open, stream.State);

            var debugData = Http2TestUtil.BB("debug");
            IHttp2GoAwayFrame goAwayFrame = new DefaultHttp2GoAwayFrame(Http2Error.NoError, debugData.RetainedDuplicate());
            goAwayFrame.ExtraStreamIds = int.MaxValue;

            _channel.WriteOutbound(goAwayFrame);
            // When the last stream id computation overflows, the last stream id should just be set to 2^31 - 1.
            _frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == _frameCodec._ctx),
                    It.Is<int>(v => v == int.MaxValue),
                    It.Is<Http2Error>(v => v == Http2Error.NoError),
                    It.Is<IByteBuffer>(v => v.Equals(debugData)),
                    It.IsAny<IPromise>()));
            debugData.Release();
            Assert.Equal(Http2StreamState.Open, stream.State);
            Assert.True(_channel.IsActive);
        }

        [Fact]
        public void StreamErrorShouldFireExceptionForInbound()
        {
            _frameInboundWriter.WriteInboundHeaders(3, _request, 31, false);

            IHttp2Stream stream = _frameCodec.Connection.Stream(3);
            Assert.NotNull(stream);

            StreamException streamEx = new StreamException(3, Http2Error.InternalError, "foo");
            _channel.Pipeline.FireExceptionCaught(streamEx);

            Http2FrameStreamEvent evt = _inboundHandler.ReadInboundMessageOrUserEvent<Http2FrameStreamEvent>();
            Assert.Equal(Http2FrameStreamEvent.EventType.State, evt.Type);
            Assert.Equal(Http2StreamState.Open, evt.Stream.State);
            IHttp2HeadersFrame headersFrame = _inboundHandler.ReadInboundMessageOrUserEvent<IHttp2HeadersFrame>();
            Assert.NotNull(headersFrame);

            try
            {
                _inboundHandler.CheckException();
                Assert.False(true, "stream exception expected");
            }
            catch (Http2FrameStreamException e)
            {
                Assert.Equal(streamEx, e.InnerException);
            }

            Assert.Null(_inboundHandler.ReadInboundMessageOrUserEvent<object>());
        }

        [Fact]
        public void StreamErrorShouldNotFireExceptionForOutbound()
        {
            _frameInboundWriter.WriteInboundHeaders(3, _request, 31, false);

            IHttp2Stream stream = _frameCodec.Connection.Stream(3);
            Assert.NotNull(stream);

            StreamException streamEx = new StreamException(3, Http2Error.InternalError, "foo");
            _frameCodec.OnError(_frameCodec._ctx, true, streamEx);

            Http2FrameStreamEvent evt = _inboundHandler.ReadInboundMessageOrUserEvent<Http2FrameStreamEvent>();
            Assert.Equal(Http2FrameStreamEvent.EventType.State, evt.Type);
            Assert.Equal(Http2StreamState.Open, evt.Stream.State);
            IHttp2HeadersFrame headersFrame = _inboundHandler.ReadInboundMessageOrUserEvent<IHttp2HeadersFrame>();
            Assert.NotNull(headersFrame);

            // No exception expected
            _inboundHandler.CheckException();

            Assert.Null(_inboundHandler.ReadInboundMessageOrUserEvent<object>());
        }

        [Fact]
        public void WindowUpdateFrameDecrementsConsumedBytes()
        {
            _frameInboundWriter.WriteInboundHeaders(3, _request, 31, false);

            var connection = _frameCodec.Connection;
            var stream = connection.Stream(3);
            Assert.NotNull(stream);

            var data = Unpooled.Buffer(100).WriteZero(100);
            _frameInboundWriter.WriteInboundData(3, data, 0, false);

            IHttp2HeadersFrame inboundHeaders = _inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(inboundHeaders);
            Assert.NotNull(inboundHeaders.Stream);

            IHttp2FrameStream stream2 = inboundHeaders.Stream;

            int before = connection.Local.FlowController.UnconsumedBytes(stream);
            var f = _channel.WriteAsync(new DefaultHttp2WindowUpdateFrame(100) { Stream = stream2 });
            int after = connection.Local.FlowController.UnconsumedBytes(stream);
            Assert.Equal(100, before - after);
            Assert.True(f.IsSuccess());
        }

        [Fact]
        public void WindowUpdateMayFail()
        {
            _frameInboundWriter.WriteInboundHeaders(3, _request, 31, false);
            var connection = _frameCodec.Connection;
            IHttp2Stream stream = connection.Stream(3);
            Assert.NotNull(stream);

            IHttp2HeadersFrame inboundHeaders = _inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(inboundHeaders);

            IHttp2FrameStream stream2 = inboundHeaders.Stream;

            // Fails, cause trying to return too many bytes to the flow controller
            var f = _channel.WriteAsync(new DefaultHttp2WindowUpdateFrame(100) { Stream = stream2 });
            Assert.True(f.IsCompleted);
            Assert.False(f.IsSuccess());
            Assert.IsType<Http2Exception>(f.Exception.InnerException);
        }

        [Fact]
        public void InboundWindowUpdateShouldBeForwarded()
        {
            _frameInboundWriter.WriteInboundHeaders(3, _request, 31, false);
            _frameInboundWriter.WriteInboundWindowUpdate(3, 100);
            // Connection-level window update
            _frameInboundWriter.WriteInboundWindowUpdate(0, 100);

            IHttp2HeadersFrame headersFrame = _inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(headersFrame);

            IHttp2WindowUpdateFrame windowUpdateFrame = _inboundHandler.ReadInbound<IHttp2WindowUpdateFrame>();
            Assert.NotNull(windowUpdateFrame);
            Assert.Equal(3, windowUpdateFrame.Stream.Id);
            Assert.Equal(100, windowUpdateFrame.WindowSizeIncrement);

            // Window update for the connection should not be forwarded.
            Assert.Null(_inboundHandler.ReadInbound());
        }

        [Fact]
        public void StreamZeroWindowUpdateIncrementsConnectionWindow()
        {
            IHttp2Connection connection = _frameCodec.Connection;
            IHttp2LocalFlowController localFlow = connection.Local.FlowController;
            int initialWindowSizeBefore = localFlow.InitialWindowSize;
            IHttp2Stream connectionStream = connection.ConnectionStream;
            int connectionWindowSizeBefore = localFlow.GetWindowSize(connectionStream);
            // We only replenish the flow control window after the amount consumed drops below the following threshold.
            // We make the threshold very "high" so that window updates will be sent when the delta is relatively small.
            ((DefaultHttp2LocalFlowController)localFlow).WindowUpdateRatio(connectionStream, .999f);

            int windowUpdate = 1024;

            _channel.WriteAsync(new DefaultHttp2WindowUpdateFrame(windowUpdate));

            // The initial window size is only changed by Http2Settings, so it shouldn't change.
            Assert.Equal(initialWindowSizeBefore, localFlow.InitialWindowSize);
            // The connection window should be increased by the delta amount.
            Assert.Equal(connectionWindowSizeBefore + windowUpdate, localFlow.GetWindowSize(connectionStream));
        }

        [Fact]
        public void WindowUpdateDoesNotOverflowConnectionWindow()
        {
            IHttp2Connection connection = _frameCodec.Connection;
            IHttp2LocalFlowController localFlow = connection.Local.FlowController;
            int initialWindowSizeBefore = localFlow.InitialWindowSize;

            _channel.WriteAsync(new DefaultHttp2WindowUpdateFrame(int.MaxValue));

            // The initial window size is only changed by Http2Settings, so it shouldn't change.
            Assert.Equal(initialWindowSizeBefore, localFlow.InitialWindowSize);
            // The connection window should be increased by the delta amount.
            Assert.Equal(int.MaxValue, localFlow.GetWindowSize(connection.ConnectionStream));
        }

        [Fact]
        public void WriteUnknownFrame()
        {
            IHttp2FrameStream stream = _frameCodec.NewStream();

            var buffer = Unpooled.Buffer().WriteByte(1);
            DefaultHttp2UnknownFrame unknownFrame = new DefaultHttp2UnknownFrame(
                    (Http2FrameTypes)20, new Http2Flags().Ack(true), buffer);
            unknownFrame.Stream = stream;
            _channel.WriteAsync(unknownFrame);

            _frameWriter.Verify(
                x => x.WriteFrameAsync(
                    It.Is<IChannelHandlerContext>(v => v == _frameCodec._ctx),
                    It.Is<Http2FrameTypes>(v => v == unknownFrame.FrameType),
                    It.Is<int>(v => v == unknownFrame.Stream.Id),
                    It.Is<Http2Flags>(v => v == unknownFrame.Flags),
                    It.Is<IByteBuffer>(v => v.Equals(buffer)),
                    It.IsAny<IPromise>()));
        }

        [Fact]
        public void SendSettingsFrame()
        {
            Http2Settings settings = new Http2Settings();
            _channel.WriteAsync(new DefaultHttp2SettingsFrame(settings));

            _frameWriter.Verify(
                x => x.WriteSettingsAsync(
                    It.Is<IChannelHandlerContext>(v => v == _frameCodec._ctx),
                    It.Is<Http2Settings>(v => v == settings),
                    It.IsAny<IPromise>()));
        }

        [Fact]//(timeout = 5000)
        public async Task NewOutboundStream()
        {
            IHttp2FrameStream stream = _frameCodec.NewStream();

            Assert.NotNull(stream);
            Assert.False(Http2CodecUtil.IsStreamIdValid(stream.Id));

            var listenerExecuted = new DefaultPromise();

            await _channel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers(), false) { Stream = stream });
            Assert.True(Http2CodecUtil.IsStreamIdValid(stream.Id));

            var data = Unpooled.Buffer().WriteZero(100);
            await _channel.WriteAndFlushAsync(new DefaultHttp2DataFrame(data) { Stream = stream });
        }

        [Fact]
        public void NewOutboundStreamsShouldBeBuffered()
        {
            var builder = Http2FrameCodecBuilder.ForServer();
            builder.EncoderEnforceMaxConcurrentStreams = true;
            SetUp(builder, new Http2Settings().MaxConcurrentStreams(1));

            var stream1 = _frameCodec.NewStream();
            var stream2 = _frameCodec.NewStream();

            var promise1 = _channel.NewPromise();
            var promise2 = _channel.NewPromise();

            _channel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()) { Stream = stream1 }, promise1);
            _channel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()) { Stream = stream2 }, promise2);

            Assert.True(Http2CodecUtil.IsStreamIdValid(stream1.Id));
            _channel.RunPendingTasks();
            Assert.True(Http2CodecUtil.IsStreamIdValid(stream2.Id));

            Assert.True(promise1.IsSuccess);
            Assert.False(promise2.IsCompleted);

            // Increase concurrent streams limit to 2
            _frameInboundWriter.WriteInboundSettings(new Http2Settings().MaxConcurrentStreams(2));

            _channel.Flush();

            Assert.True(promise2.IsSuccess);
        }

        [Fact]
        public void MultipleNewOutboundStreamsShouldBeBuffered()
        {
            var builder = Http2FrameCodecBuilder.ForServer();
            builder.EncoderEnforceMaxConcurrentStreams = true;
            // We use a limit of 1 and then increase it step by step.
            SetUp(builder, new Http2Settings().MaxConcurrentStreams(1));

            IHttp2FrameStream stream1 = _frameCodec.NewStream();
            IHttp2FrameStream stream2 = _frameCodec.NewStream();
            IHttp2FrameStream stream3 = _frameCodec.NewStream();

            IPromise promise1 = _channel.NewPromise();
            IPromise promise2 = _channel.NewPromise();
            IPromise promise3 = _channel.NewPromise();

            _channel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()) { Stream = stream1 }, promise1);
            _channel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()) { Stream = stream2 }, promise2);
            _channel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()) { Stream = stream3 }, promise3);

            Assert.True(Http2CodecUtil.IsStreamIdValid(stream1.Id));
            _channel.RunPendingTasks();
            Assert.True(Http2CodecUtil.IsStreamIdValid(stream2.Id));

            if (promise1.IsCompleted)
            {
                Assert.True(promise1.Task.IsSuccess());
            }
            else
            {
                try
                {
                    promise1.Task.GetAwaiter().GetResult();
                }
                catch (Exception)
                {
                    Assert.False(true);
                }
            }
            Assert.False(promise2.IsCompleted);
            Assert.False(promise3.IsCompleted);

            // Increase concurrent streams limit to 2
            _frameInboundWriter.WriteInboundSettings(new Http2Settings().MaxConcurrentStreams(2));
            _channel.Flush();

            // As we increased the limit to 2 we should have also succeed the second frame.
            if (promise2.IsCompleted)
            {
                Assert.True(promise2.Task.IsSuccess());
            }
            else
            {
                try
                {
                    promise2.Task.GetAwaiter().GetResult();
                }
                catch (Exception)
                {
                    Assert.False(true);
                }
            }
            Assert.False(promise3.IsCompleted);

            _frameInboundWriter.WriteInboundSettings(new Http2Settings().MaxConcurrentStreams(3));
            _channel.Flush();

            // With the max streams of 3 all streams should be succeed now.
            if (promise3.IsCompleted)
            {
                Assert.True(promise3.Task.IsSuccess());
            }
            else
            {
                try
                {
                    promise3.Task.GetAwaiter().GetResult();
                }
                catch (Exception)
                {
                    Assert.False(true);
                }
            }

            Assert.False(_channel.FinishAndReleaseAll());
        }

        [Fact]
        public void DoNotLeakOnFailedInitializationForChannels()
        {
            // We use a limit of 1 and then increase it step by step.
            SetUp(Http2FrameCodecBuilder.ForServer(), new Http2Settings().MaxConcurrentStreams(2));

            IHttp2FrameStream stream1 = _frameCodec.NewStream();
            IHttp2FrameStream stream2 = _frameCodec.NewStream();

            var stream1HeaderPromise = _channel.NewPromise();
            var stream2HeaderPromise = _channel.NewPromise();

            _channel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()) { Stream = stream1 },
                                  stream1HeaderPromise);
            _channel.RunPendingTasks();

            _frameInboundWriter.WriteInboundGoAway(stream1.Id, 0L, Unpooled.Empty);

            _channel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()) { Stream = stream2 },
                                  stream2HeaderPromise);
            _channel.RunPendingTasks();

            try
            {
                stream1HeaderPromise.Task.GetAwaiter().GetResult();
            }
            catch
            {
                Assert.False(true);
            }
            Assert.True(stream2HeaderPromise.IsCompleted);

            Assert.Equal(0, _frameCodec.NumInitializingStreams);
            Assert.False(_channel.FinishAndReleaseAll());
        }

        [Fact]
        public void StreamIdentifiersExhausted()
        {
            int maxServerStreamId = int.MaxValue - 1;

            Assert.NotNull(_frameCodec.Connection.Local.CreateStream(maxServerStreamId, false));

            IHttp2FrameStream stream = _frameCodec.NewStream();
            Assert.NotNull(stream);

            var writePromise = _channel.NewPromise();
            _channel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()) { Stream = stream }, writePromise);

            var goAwayFrame = _inboundHandler.ReadInbound<IHttp2GoAwayFrame>();
            Assert.NotNull(goAwayFrame);
            Assert.Equal(Http2Error.NoError, goAwayFrame.ErrorCode);
            Assert.Equal(int.MaxValue, goAwayFrame.LastStreamId);
            goAwayFrame.Release();

            Assert.IsType<Http2NoMoreStreamIdsException>(writePromise.Task.Exception.InnerException);
        }

        [Fact]
        public void ReceivePing()
        {
            _frameInboundWriter.WriteInboundPing(false, 12345L);

            IHttp2PingFrame pingFrame = _inboundHandler.ReadInbound<IHttp2PingFrame>();
            Assert.NotNull(pingFrame);

            Assert.Equal(12345, pingFrame.Content);
            Assert.False(pingFrame.Ack);
        }

        [Fact]
        public void SendPing()
        {
            _channel.WriteAndFlushAsync(new DefaultHttp2PingFrame(12345));

            _frameWriter.Verify(
                x => x.WritePingAsync(
                    It.Is<IChannelHandlerContext>(v => v == _frameCodec._ctx),
                    It.Is<bool>(v => v == false),
                    It.Is<long>(v => v == 12345L),
                    It.IsAny<IPromise>()));
        }

        [Fact]
        public void ReceiveSettings()
        {
            Http2Settings settings = new Http2Settings().MaxConcurrentStreams(1);
            _frameInboundWriter.WriteInboundSettings(settings);

            IHttp2SettingsFrame settingsFrame = _inboundHandler.ReadInbound<IHttp2SettingsFrame>();
            Assert.NotNull(settingsFrame);
            Assert.Equal(settings, settingsFrame.Settings);
        }

        [Fact]
        public void SendSettings()
        {
            Http2Settings settings = new Http2Settings().MaxConcurrentStreams(1);
            _channel.WriteAndFlushAsync(new DefaultHttp2SettingsFrame(settings));

            _frameWriter.Verify(
                x => x.WriteSettingsAsync(
                    It.Is<IChannelHandlerContext>(v => v == _frameCodec._ctx),
                    It.Is<Http2Settings>(v => v == settings),
                    It.IsAny<IPromise>()));
        }

        [Fact]
        public void IterateActiveStreams()
        {
            var builder = Http2FrameCodecBuilder.ForServer();
            builder.EncoderEnforceMaxConcurrentStreams = true;
            SetUp(builder, new Http2Settings().MaxConcurrentStreams(1));

            _frameInboundWriter.WriteInboundHeaders(3, _request, 0, false);

            IHttp2HeadersFrame headersFrame = _inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(headersFrame);

            IHttp2FrameStream activeInbond = headersFrame.Stream;

            IHttp2FrameStream activeOutbound = _frameCodec.NewStream();
            _channel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()) { Stream = activeOutbound });

            IHttp2FrameStream bufferedOutbound = _frameCodec.NewStream();
            _channel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()) { Stream = bufferedOutbound });

            //@SuppressWarnings("unused")
            IHttp2FrameStream idleStream = _frameCodec.NewStream();

            ISet<IHttp2FrameStream> activeStreams = new HashSet<IHttp2FrameStream>();
            bool localVisit(IHttp2FrameStream stream)
            {
                activeStreams.Add(stream);
                return true;
            }
            _frameCodec.ForEachActiveStream(localVisit);

            Assert.Equal(2, activeStreams.Count);

            ISet<IHttp2FrameStream> expectedStreams = new HashSet<IHttp2FrameStream>();
            expectedStreams.Add(activeInbond);
            expectedStreams.Add(activeOutbound);
            Assert.Equal(expectedStreams, activeStreams);
        }

        [Fact]
        public void AutoAckPingTrue()
        {
            var builder = Http2FrameCodecBuilder.ForServer();
            builder.AutoAckPingFrame = true;
            SetUp(builder, new Http2Settings());
            _frameInboundWriter.WriteInboundPing(false, 8);
            var frame = _inboundHandler.ReadInbound<IHttp2PingFrame>();
            Assert.False(frame.Ack);
            Assert.Equal(8, frame.Content);
            _frameWriter.Verify(
                x => x.WritePingAsync(
                    It.Is<IChannelHandlerContext>(v => v == _frameCodec._ctx),
                    It.Is<bool>(v => v == true),
                    It.Is<long>(v => v == 8L),
                    It.IsAny<IPromise>()));
        }

        [Fact]
        public void AutoAckPingFalse()
        {
            var builder = Http2FrameCodecBuilder.ForServer();
            builder.AutoAckPingFrame = false;
            SetUp(builder, new Http2Settings());
            _frameInboundWriter.WriteInboundPing(false, 8);
            _frameWriter.Verify(
                x => x.WritePingAsync(
                    It.Is<IChannelHandlerContext>(v => v == _frameCodec._ctx),
                    It.Is<bool>(v => v == true),
                    It.Is<long>(v => v == 8L),
                    It.IsAny<IPromise>()),
                Times.Never());
            var frame = _inboundHandler.ReadInbound<IHttp2PingFrame>();
            Assert.False(frame.Ack);
            Assert.Equal(8, frame.Content);

            // Now ack the frame manually.
            _channel.WriteAndFlushAsync(new DefaultHttp2PingFrame(8, true));
            _frameWriter.Verify(
                x => x.WritePingAsync(
                    It.Is<IChannelHandlerContext>(v => v == _frameCodec._ctx),
                    It.Is<bool>(v => v == true),
                    It.Is<long>(v => v == 8L),
                    It.IsAny<IPromise>()));
        }

        [Fact]
        public async Task StreamShouldBeOpenInListener()
        {
            IHttp2FrameStream stream2 = _frameCodec.NewStream();
            Assert.Equal(Http2StreamState.Idle, stream2.State);

            await _channel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()) { Stream = stream2 });
            Assert.Equal(Http2StreamState.Open, stream2.State);
        }

        [Fact]
        public void UpgradeEventNoRefCntError()
        {
            _frameInboundWriter.WriteInboundHeaders(Http2CodecUtil.HttpUpgradeStreamId, _request, 31, false);

            var constructors = typeof(HttpServerUpgradeHandler.UpgradeEvent).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var upgradeEvent = (HttpServerUpgradeHandler.UpgradeEvent)constructors[0].Invoke(new object[]
            {
                (AsciiString)"HTTP/2",
                new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/")
            });
            _channel.Pipeline.FireUserEventTriggered(upgradeEvent);
            Assert.Equal(1, upgradeEvent.ReferenceCount);
        }

        sealed class TestUpgradeChannelHandler : ChannelHandlerAdapter
        {
            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                if (msg is IHttp2DataFrame data)
                {
                    var f = ctx.WriteAndFlushAsync(new DefaultHttp2WindowUpdateFrame(data.InitialFlowControlledBytes) { Stream = data.Stream });
                    f.ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            ctx.FireExceptionCaught(t.Exception.InnerException);
                        }
                    }, TaskContinuationOptions.ExecuteSynchronously);
                }
                ReferenceCountUtil.Release(msg);
            }
        }
        [Fact]
        public void UpgradeWithoutFlowControlling()
        {
            _channel.Pipeline.AddAfter(_frameCodec._ctx.Name, null, new TestUpgradeChannelHandler());

            _frameInboundWriter.WriteInboundHeaders(Http2CodecUtil.HttpUpgradeStreamId, _request, 31, false);

            string longString = "*".PadRight(70000, '*');
            DefaultFullHttpRequest request =
                new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/", Http2TestUtil.BB(longString));

            var constructors = typeof(HttpServerUpgradeHandler.UpgradeEvent).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var upgradeEvent = (HttpServerUpgradeHandler.UpgradeEvent)constructors[0].Invoke(new object[]
            {
                (AsciiString)"HTTP/2",
                request
            });
            _channel.Pipeline.FireUserEventTriggered(upgradeEvent);
        }
    }
}
