
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
        private Mock<VerifiableHttp2FrameWriter> frameWriter;
        private Http2FrameCodec frameCodec;
        private EmbeddedChannel channel;
        // For injecting inbound frames
        private IHttp2FrameListener frameListener;
        private IChannelHandlerContext http2HandlerCtx;
        private LastInboundHandler inboundHandler;

        private readonly IHttp2Headers request;
        private readonly IHttp2Headers response;

        public Http2FrameCodecTest()
        {
            this.request = new DefaultHttp2Headers();
            this.request.Method = HttpMethod.Get.AsciiName;
            this.request.Scheme = HttpScheme.Https.Name;
            this.request.Authority = new AsciiString("example.org");
            this.request.Path = new AsciiString("/foo");
            this.response = new DefaultHttp2Headers();
            this.response.Status = HttpResponseStatus.OK.CodeAsText;
            this.SetUp(Http2FrameCodecBuilder.ForServer(), new Http2Settings());
        }

        public void Dispose()
        {
            this.Dispose0();
        }

        private void Dispose0()
        {
            if (this.inboundHandler != null)
            {
                this.inboundHandler.FinishAndReleaseAll();
                this.inboundHandler = null;
            }
            var ch = this.channel;
            if (ch != null)
            {
                this.channel = null;
                ch.FinishAndReleaseAll();
                ch.CloseAsync();
            }
        }

        private void SetUp(Http2FrameCodecBuilder frameCodecBuilder, Http2Settings initialRemoteSettings)
        {
            // Some tests call this method twice. Once with JUnit's @Before and once directly to pass special settings.
            // This call ensures that in case of two consecutive calls to setUp(), the previous channel is shutdown and
            // ByteBufs are released correctly.
            this.Dispose0();

            this.frameWriter = new Mock<VerifiableHttp2FrameWriter>() { CallBase = true };
            var builder = frameCodecBuilder.FrameWriter(this.frameWriter.Object);
            builder.FrameLogger = new Http2FrameLogger(Common.Internal.Logging.InternalLogLevel.TRACE);
            builder.InitialSettings = initialRemoteSettings;
            this.frameCodec = frameCodecBuilder.Build();
            this.frameListener = ((DefaultHttp2ConnectionDecoder)this.frameCodec.Decoder).InternalFrameListener;
            this.inboundHandler = new LastInboundHandler();

            this.channel = new EmbeddedChannel();
            //channel.Connect(new InetSocketAddress(0));
            this.channel.Pipeline.AddLast(this.frameCodec);
            this.channel.Pipeline.AddLast(this.inboundHandler);
            this.channel.Pipeline.FireChannelActive();

            this.http2HandlerCtx = this.channel.Pipeline.Context(this.frameCodec);

            // Handshake
            this.frameWriter.Verify(
                x => x.WriteSettingsAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.http2HandlerCtx),
                    It.IsAny<Http2Settings>(),
                    It.IsAny<IPromise>()));
            this.frameWriter.VerifyNoOtherCalls();
            this.channel.WriteInbound(Http2CodecUtil.ConnectionPrefaceBuf());
            this.frameListener.OnSettingsRead(this.http2HandlerCtx, initialRemoteSettings);
            this.frameWriter.Verify(
                x => x.WriteSettingsAckAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.http2HandlerCtx),
                    It.IsAny<IPromise>()));
            this.frameListener.OnSettingsAckRead(this.http2HandlerCtx);

            var settingsFrame = this.inboundHandler.ReadInbound<IHttp2SettingsFrame>();
            Assert.NotNull(settingsFrame);
        }

        [Fact]
        public void StateChanges()
        {
            this.frameListener.OnHeadersRead(this.http2HandlerCtx, 1, this.request, 31, true);

            IHttp2Stream stream = this.frameCodec.Connection.Stream(1);
            Assert.NotNull(stream);
            Assert.Equal(Http2StreamState.HalfClosedRemote, stream.State);

            var evt = this.inboundHandler.ReadInboundMessageOrUserEvent<Http2FrameStreamEvent>();
            Assert.Equal(Http2StreamState.HalfClosedRemote, evt.Stream.State);

            var inboundFrame = this.inboundHandler.ReadInbound<IHttp2StreamFrame>();
            IHttp2FrameStream stream2 = inboundFrame.Stream;
            Assert.NotNull(stream2);
            Assert.Equal(1, stream2.Id);
            Assert.Equal(inboundFrame, new DefaultHttp2HeadersFrame(this.request, true, 31) { Stream = stream2 });
            Assert.Null(this.inboundHandler.ReadInbound<object>());

            this.inboundHandler.WriteOutbound(new DefaultHttp2HeadersFrame(this.response, true, 27) { Stream = stream2 });
            this.frameWriter.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.http2HandlerCtx),
                    It.Is<int>(v => v == 1),
                    It.Is<IHttp2Headers>(v => v.Equals(this.response)),
                    It.IsAny<int>(),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.Is<int>(v => v == 27),
                    It.Is<bool>(v => v == true),
                    It.IsAny<IPromise>()));
            this.frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()),
                Times.Never());

            Assert.Equal(Http2StreamState.Closed, stream.State);
            evt = this.inboundHandler.ReadInboundMessageOrUserEvent<Http2FrameStreamEvent>();
            Assert.Equal(Http2StreamState.Closed, evt.Stream.State);

            Assert.True(this.channel.Active);
        }

        [Fact]
        public void HeaderRequestHeaderResponse()
        {
            this.frameListener.OnHeadersRead(this.http2HandlerCtx, 1, this.request, 31, true);

            IHttp2Stream stream = this.frameCodec.Connection.Stream(1);
            Assert.NotNull(stream);
            Assert.Equal(Http2StreamState.HalfClosedRemote, stream.State);

            var inboundFrame = this.inboundHandler.ReadInbound<IHttp2StreamFrame>();
            IHttp2FrameStream stream2 = inboundFrame.Stream;
            Assert.NotNull(stream2);
            Assert.Equal(1, stream2.Id);
            Assert.Equal(inboundFrame, new DefaultHttp2HeadersFrame(this.request, true, 31) { Stream = stream2 });
            Assert.Null(this.inboundHandler.ReadInbound<object>());

            this.inboundHandler.WriteOutbound(new DefaultHttp2HeadersFrame(this.response, true, 27) { Stream = stream2 });
            this.frameWriter.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.http2HandlerCtx),
                    It.Is<int>(v => v == 1),
                    It.Is<IHttp2Headers>(v => v.Equals(this.response)),
                    It.IsAny<int>(),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.Is<int>(v => v == 27),
                    It.Is<bool>(v => v == true),
                    It.IsAny<IPromise>()));
            this.frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()),
                Times.Never());

            Assert.Equal(Http2StreamState.Closed, stream.State);
            Assert.True(this.channel.Active);
        }

        [Fact]
        public void FlowControlShouldBeResilientToMissingStreams()
        {
            IHttp2Connection conn = new DefaultHttp2Connection(true);
            IHttp2ConnectionEncoder enc = new DefaultHttp2ConnectionEncoder(conn, new DefaultHttp2FrameWriter());
            IHttp2ConnectionDecoder dec = new DefaultHttp2ConnectionDecoder(conn, enc, new DefaultHttp2FrameReader());
            Http2FrameCodec codec = new Http2FrameCodec(enc, dec, new Http2Settings());
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
            this.frameListener.OnHeadersRead(this.http2HandlerCtx, 1, this.request, 0, false);

            IHttp2Stream stream = this.frameCodec.Connection.Stream(1);
            Assert.NotNull(stream);
            Assert.Equal(Http2StreamState.Open, stream.State);

            IHttp2HeadersFrame inboundHeaders = this.inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            IHttp2FrameStream stream2 = inboundHeaders.Stream;
            Assert.NotNull(stream2);
            Assert.Equal(1, stream2.Id);
            Assert.Equal(new DefaultHttp2HeadersFrame(this.request, false) { Stream = stream2 }, inboundHeaders);
            Assert.Null(this.inboundHandler.ReadInbound<object>());

            IByteBuffer hello = BB("hello");
            this.frameListener.OnDataRead(this.http2HandlerCtx, 1, hello, 31, true);
            // Release hello to emulate ByteToMessageDecoder
            hello.Release();
            IHttp2DataFrame inboundData = this.inboundHandler.ReadInbound<IHttp2DataFrame>();
            IHttp2DataFrame expected = new DefaultHttp2DataFrame(BB("hello"), true, 31) { Stream = stream2 };
            Assert.Equal(expected, inboundData);

            Assert.Equal(1, inboundData.ReferenceCount);
            expected.Release();
            inboundData.Release();
            Assert.Null(this.inboundHandler.ReadInbound<object>());

            this.inboundHandler.WriteOutbound(new DefaultHttp2HeadersFrame(this.response, false) { Stream = stream2 });
            this.frameWriter.Verify(
                x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.http2HandlerCtx),
                    It.Is<int>(v => v == 1),
                    It.Is<IHttp2Headers>(v => v.Equals(this.response)),
                    It.IsAny<int>(),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.IsAny<IPromise>()));

            this.inboundHandler.WriteOutbound(new DefaultHttp2DataFrame(BB("world"), true, 27) { Stream = stream2 });
            var outboundData = new ArgumentCaptor<IByteBuffer>();
            this.frameWriter.Verify(
                x => x.WriteDataAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.http2HandlerCtx),
                    It.Is<int>(v => v == 1),
                    It.Is<IByteBuffer>(v => outboundData.Capture(v)),
                    It.Is<int>(v => v == 27),
                    It.Is<bool>(v => v == true),
                    It.IsAny<IPromise>()));

            IByteBuffer bb = BB("world");
            Assert.Equal(bb, outboundData.GetValue());
            Assert.Equal(1, outboundData.GetValue().ReferenceCount);
            bb.Release();
            this.frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()), Times.Never());
            Assert.True(this.channel.Active);
        }

        [Fact]
        public void SendRstStream()
        {
            this.frameListener.OnHeadersRead(this.http2HandlerCtx, 3, this.request, 31, true);

            IHttp2Stream stream = this.frameCodec.Connection.Stream(3);
            Assert.NotNull(stream);
            Assert.Equal(Http2StreamState.HalfClosedRemote, stream.State);

            IHttp2HeadersFrame inboundHeaders = this.inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(inboundHeaders);
            Assert.True(inboundHeaders.IsEndStream);

            IHttp2FrameStream stream2 = inboundHeaders.Stream;
            Assert.NotNull(stream2);
            Assert.Equal(3, stream2.Id);

            this.inboundHandler.WriteOutbound(new DefaultHttp2ResetFrame((Http2Error)314 /* non-standard error */) { Stream = stream2 });
            this.frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.http2HandlerCtx),
                    It.Is<int>(v => v == 3),
                    It.Is<Http2Error>(v => v == (Http2Error)314L),
                    It.IsAny<IPromise>()));
            Assert.Equal(Http2StreamState.Closed, stream.State);
            Assert.True(this.channel.Active);
        }

        [Fact]
        public void ReceiveRstStream()
        {
            this.frameListener.OnHeadersRead(this.http2HandlerCtx, 3, this.request, 31, false);

            IHttp2Stream stream = this.frameCodec.Connection.Stream(3);
            Assert.NotNull(stream);
            Assert.Equal(Http2StreamState.Open, stream.State);

            IHttp2HeadersFrame expectedHeaders = new DefaultHttp2HeadersFrame(this.request, false, 31);
            IHttp2HeadersFrame actualHeaders = this.inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            expectedHeaders.Stream = actualHeaders.Stream;
            Assert.Equal(expectedHeaders, actualHeaders);

            this.frameListener.OnRstStreamRead(this.http2HandlerCtx, 3, Http2Error.NoError);

            IHttp2ResetFrame expectedRst = new DefaultHttp2ResetFrame(Http2Error.NoError) { Stream = actualHeaders.Stream };
            IHttp2ResetFrame actualRst = this.inboundHandler.ReadInbound<IHttp2ResetFrame>();
            Assert.Equal(expectedRst, actualRst);

            Assert.Null(this.inboundHandler.ReadInbound<object>());
        }

        [Fact]
        public void SendGoAway()
        {
            this.frameListener.OnHeadersRead(this.http2HandlerCtx, 3, this.request, 31, false);

            IHttp2Stream stream = this.frameCodec.Connection.Stream(3);
            Assert.NotNull(stream);
            Assert.Equal(Http2StreamState.Open, stream.State);

            IByteBuffer debugData = BB("debug");
            IByteBuffer expected = debugData.Copy();

            IHttp2GoAwayFrame goAwayFrame = new DefaultHttp2GoAwayFrame(Http2Error.NoError, debugData.Slice());
            goAwayFrame.ExtraStreamIds = 2;

            this.inboundHandler.WriteOutbound(goAwayFrame);
            this.frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.http2HandlerCtx),
                    It.Is<int>(v => v == 7),
                    It.Is<Http2Error>(v => v == Http2Error.NoError),
                    It.Is<IByteBuffer>(v => v.Equals(expected)),
                    It.IsAny<IPromise>()));
            Assert.Equal(1, debugData.ReferenceCount);
            Assert.Equal(Http2StreamState.Open, stream.State);
            Assert.True(this.channel.Active);
            expected.Release();
        }

        [Fact]
        public void ReceiveGoaway()
        {
            IByteBuffer debugData = BB("foo");
            this.frameListener.OnGoAwayRead(this.http2HandlerCtx, 2, Http2Error.NoError, debugData);
            // Release debugData to emulate ByteToMessageDecoder
            debugData.Release();
            IHttp2GoAwayFrame expectedFrame = new DefaultHttp2GoAwayFrame(2, Http2Error.NoError, BB("foo"));
            IHttp2GoAwayFrame actualFrame = this.inboundHandler.ReadInbound<IHttp2GoAwayFrame>();

            Assert.Equal(expectedFrame, actualFrame);
            Assert.Null(this.inboundHandler.ReadInbound<object>());

            expectedFrame.Release();
            actualFrame.Release();
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

            var f = this.channel.WriteAsync(frame);
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
            this.frameListener.OnHeadersRead(this.http2HandlerCtx, 5, request, 31, false);

            IHttp2Stream stream = this.frameCodec.Connection.Stream(5);
            Assert.NotNull(stream);
            Assert.Equal(Http2StreamState.Open, stream.State);

            var debugData = BB("debug");
            IHttp2GoAwayFrame goAwayFrame = new DefaultHttp2GoAwayFrame(Http2Error.NoError, debugData.Slice());
            goAwayFrame.ExtraStreamIds = int.MaxValue;

            inboundHandler.WriteOutbound(goAwayFrame);
            // When the last stream id computation overflows, the last stream id should just be set to 2^31 - 1.
            this.frameWriter.Verify(
                x => x.WriteGoAwayAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.http2HandlerCtx),
                    It.Is<int>(v => v == int.MaxValue),
                    It.Is<Http2Error>(v => v == Http2Error.NoError),
                    It.Is<IByteBuffer>(v => v.Equals(debugData)),
                    It.IsAny<IPromise>()));
            Assert.Equal(1, debugData.ReferenceCount);
            Assert.Equal(Http2StreamState.Open, stream.State);
            Assert.True(channel.Active);
        }

        [Fact]
        public void StreamErrorShouldFireExceptionForInbound()
        {
            this.frameListener.OnHeadersRead(this.http2HandlerCtx, 3, request, 31, false);

            IHttp2Stream stream = this.frameCodec.Connection.Stream(3);
            Assert.NotNull(stream);

            StreamException streamEx = new StreamException(3, Http2Error.InternalError, "foo");
            frameCodec.OnError(http2HandlerCtx, false, streamEx);

            Http2FrameStreamEvent evt = inboundHandler.ReadInboundMessageOrUserEvent<Http2FrameStreamEvent>();
            Assert.Equal(Http2FrameStreamEvent.EventType.State, evt.Type);
            Assert.Equal(Http2StreamState.Open, evt.Stream.State);
            IHttp2HeadersFrame headersFrame = inboundHandler.ReadInboundMessageOrUserEvent<IHttp2HeadersFrame>();
            Assert.NotNull(headersFrame);

            try
            {
                inboundHandler.CheckException();
                Assert.False(true, "stream exception expected");
            }
            catch (Http2FrameStreamException e)
            {
                Assert.Equal(streamEx, e.InnerException);
            }

            Assert.Null(inboundHandler.ReadInboundMessageOrUserEvent<object>());
        }

        [Fact]
        public void StreamErrorShouldNotFireExceptionForOutbound()
        {
            this.frameListener.OnHeadersRead(this.http2HandlerCtx, 3, request, 31, false);

            IHttp2Stream stream = this.frameCodec.Connection.Stream(3);
            Assert.NotNull(stream);

            StreamException streamEx = new StreamException(3, Http2Error.InternalError, "foo");
            frameCodec.OnError(http2HandlerCtx, true, streamEx);

            Http2FrameStreamEvent evt = inboundHandler.ReadInboundMessageOrUserEvent<Http2FrameStreamEvent>();
            Assert.Equal(Http2FrameStreamEvent.EventType.State, evt.Type);
            Assert.Equal(Http2StreamState.Open, evt.Stream.State);
            IHttp2HeadersFrame headersFrame = inboundHandler.ReadInboundMessageOrUserEvent<IHttp2HeadersFrame>();
            Assert.NotNull(headersFrame);

            // No exception expected
            inboundHandler.CheckException();

            Assert.Null(inboundHandler.ReadInboundMessageOrUserEvent<object>());
        }

        [Fact]
        public void WindowUpdateFrameDecrementsConsumedBytes()
        {
            this.frameListener.OnHeadersRead(this.http2HandlerCtx, 3, request, 31, false);

            var connection = frameCodec.Connection;
            var stream = connection.Stream(3);
            Assert.NotNull(stream);

            var data = Unpooled.Buffer(100).WriteZero(100);
            frameListener.OnDataRead(http2HandlerCtx, 3, data, 0, true);

            IHttp2HeadersFrame inboundHeaders = inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(inboundHeaders);
            Assert.NotNull(inboundHeaders.Stream);

            IHttp2FrameStream stream2 = inboundHeaders.Stream;

            int before = connection.Local.FlowController.UnconsumedBytes(stream);
            var f = channel.WriteAsync(new DefaultHttp2WindowUpdateFrame(100) { Stream = stream2 });
            int after = connection.Local.FlowController.UnconsumedBytes(stream);
            Assert.Equal(100, before - after);
            Assert.True(f.IsSuccess());
            data.Release();
        }

        [Fact]
        public void WindowUpdateMayFail()
        {
            this.frameListener.OnHeadersRead(this.http2HandlerCtx, 3, request, 31, false);
            var connection = frameCodec.Connection;
            IHttp2Stream stream = connection.Stream(3);
            Assert.NotNull(stream);

            IHttp2HeadersFrame inboundHeaders = inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(inboundHeaders);

            IHttp2FrameStream stream2 = inboundHeaders.Stream;

            // Fails, cause trying to return too many bytes to the flow controller
            var f = channel.WriteAsync(new DefaultHttp2WindowUpdateFrame(100) { Stream = stream2 });
            Assert.True(f.IsCompleted);
            Assert.False(f.IsSuccess());
            Assert.IsType<Http2Exception>(f.Exception.InnerException);
        }

        [Fact]
        public void InboundWindowUpdateShouldBeForwarded()
        {
            this.frameListener.OnHeadersRead(this.http2HandlerCtx, 3, request, 31, false);
            frameListener.OnWindowUpdateRead(http2HandlerCtx, 3, 100);
            // Connection-level window update
            frameListener.OnWindowUpdateRead(http2HandlerCtx, 0, 100);

            IHttp2HeadersFrame headersFrame = inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(headersFrame);

            IHttp2WindowUpdateFrame windowUpdateFrame = inboundHandler.ReadInbound<IHttp2WindowUpdateFrame>();
            Assert.NotNull(windowUpdateFrame);
            Assert.Equal(3, windowUpdateFrame.Stream.Id);
            Assert.Equal(100, windowUpdateFrame.WindowSizeIncrement);

            // Window update for the connection should not be forwarded.
            Assert.Null(inboundHandler.ReadInbound<object>());
        }

        [Fact]
        public void StreamZeroWindowUpdateIncrementsConnectionWindow()
        {
            IHttp2Connection connection = frameCodec.Connection;
            IHttp2LocalFlowController localFlow = connection.Local.FlowController;
            int initialWindowSizeBefore = localFlow.InitialWindowSize;
            IHttp2Stream connectionStream = connection.ConnectionStream;
            int connectionWindowSizeBefore = localFlow.GetWindowSize(connectionStream);
            // We only replenish the flow control window after the amount consumed drops below the following threshold.
            // We make the threshold very "high" so that window updates will be sent when the delta is relatively small.
            ((DefaultHttp2LocalFlowController)localFlow).WindowUpdateRatio(connectionStream, .999f);

            int windowUpdate = 1024;

            channel.WriteAsync(new DefaultHttp2WindowUpdateFrame(windowUpdate));

            // The initial window size is only changed by Http2Settings, so it shouldn't change.
            Assert.Equal(initialWindowSizeBefore, localFlow.InitialWindowSize);
            // The connection window should be increased by the delta amount.
            Assert.Equal(connectionWindowSizeBefore + windowUpdate, localFlow.GetWindowSize(connectionStream));
        }

        [Fact]
        public void WindowUpdateDoesNotOverflowConnectionWindow()
        {
            IHttp2Connection connection = frameCodec.Connection;
            IHttp2LocalFlowController localFlow = connection.Local.FlowController;
            int initialWindowSizeBefore = localFlow.InitialWindowSize;

            channel.WriteAsync(new DefaultHttp2WindowUpdateFrame(int.MaxValue));

            // The initial window size is only changed by Http2Settings, so it shouldn't change.
            Assert.Equal(initialWindowSizeBefore, localFlow.InitialWindowSize);
            // The connection window should be increased by the delta amount.
            Assert.Equal(int.MaxValue, localFlow.GetWindowSize(connection.ConnectionStream));
        }

        [Fact]
        public void WriteUnknownFrame()
        {
            IHttp2FrameStream stream = frameCodec.NewStream();

            var buffer = Unpooled.Buffer().WriteByte(1);
            DefaultHttp2UnknownFrame unknownFrame = new DefaultHttp2UnknownFrame(
                    (Http2FrameTypes)20, new Http2Flags().Ack(true), buffer);
            unknownFrame.Stream = stream;
            channel.WriteAsync(unknownFrame);

            this.frameWriter.Verify(
                x => x.WriteFrameAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.http2HandlerCtx),
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
            channel.WriteAsync(new DefaultHttp2SettingsFrame(settings));

            this.frameWriter.Verify(
                x => x.WriteSettingsAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.http2HandlerCtx),
                    It.Is<Http2Settings>(v => v == settings),
                    It.IsAny<IPromise>()));
        }

        [Fact]//(timeout = 5000)
        public async Task NewOutboundStream()
        {
            IHttp2FrameStream stream = frameCodec.NewStream();

            Assert.NotNull(stream);
            Assert.False(Http2CodecUtil.IsStreamIdValid(stream.Id));

            var listenerExecuted = new TaskCompletionSource();

            await channel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers(), false) { Stream = stream });
            Assert.True(Http2CodecUtil.IsStreamIdValid(stream.Id));

            var data = Unpooled.Buffer().WriteZero(100);
            await channel.WriteAndFlushAsync(new DefaultHttp2DataFrame(data) { Stream = stream });
        }

        [Fact]
        public void NewOutboundStreamsShouldBeBuffered()
        {
            var builder = Http2FrameCodecBuilder.ForServer();
            builder.EncoderEnforceMaxConcurrentStreams = true;
            this.SetUp(builder, new Http2Settings().MaxConcurrentStreams(1));

            var stream1 = frameCodec.NewStream();
            var stream2 = frameCodec.NewStream();

            var promise1 = channel.NewPromise();
            var promise2 = channel.NewPromise();

            channel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()) { Stream = stream1 }, promise1);
            channel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()) { Stream = stream2 }, promise2);

            Assert.True(Http2CodecUtil.IsStreamIdValid(stream1.Id));
            channel.RunPendingTasks();
            Assert.True(Http2CodecUtil.IsStreamIdValid(stream2.Id));

            Assert.True(promise1.IsSuccess);
            Assert.False(promise2.IsCompleted);

            // Increase concurrent streams limit to 2
            frameListener.OnSettingsRead(http2HandlerCtx, new Http2Settings().MaxConcurrentStreams(2));

            channel.Flush();

            Assert.True(promise2.IsSuccess);
        }

        [Fact]
        public void StreamIdentifiersExhausted()
        {
            int maxServerStreamId = int.MaxValue - 1;

            Assert.NotNull(frameCodec.Connection.Local.CreateStream(maxServerStreamId, false));

            IHttp2FrameStream stream = frameCodec.NewStream();
            Assert.NotNull(stream);

            var writePromise = channel.NewPromise();
            channel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()) { Stream = stream }, writePromise);

            Assert.IsType<Http2NoMoreStreamIdsException>(writePromise.Task.Exception.InnerException);
        }

        [Fact]
        public void ReceivePing()
        {
            frameListener.OnPingRead(http2HandlerCtx, 12345L);

            IHttp2PingFrame pingFrame = inboundHandler.ReadInbound<IHttp2PingFrame>();
            Assert.NotNull(pingFrame);

            Assert.Equal(12345, pingFrame.Content);
            Assert.False(pingFrame.Ack);
        }

        [Fact]
        public void SendPing()
        {
            channel.WriteAndFlushAsync(new DefaultHttp2PingFrame(12345));

            this.frameWriter.Verify(
                x => x.WritePingAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.http2HandlerCtx),
                    It.Is<bool>(v => v == false),
                    It.Is<long>(v => v == 12345L),
                    It.IsAny<IPromise>()));
        }

        [Fact]
        public void ReceiveSettings()
        {
            Http2Settings settings = new Http2Settings().MaxConcurrentStreams(1);
            frameListener.OnSettingsRead(http2HandlerCtx, settings);

            IHttp2SettingsFrame settingsFrame = inboundHandler.ReadInbound<IHttp2SettingsFrame>();
            Assert.NotNull(settingsFrame);
            Assert.Equal(settings, settingsFrame.Settings);
        }

        [Fact]
        public void SendSettings()
        {
            Http2Settings settings = new Http2Settings().MaxConcurrentStreams(1);
            channel.WriteAndFlushAsync(new DefaultHttp2SettingsFrame(settings));

            this.frameWriter.Verify(
                x => x.WriteSettingsAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.http2HandlerCtx),
                    It.Is<Http2Settings>(v => v == settings),
                    It.IsAny<IPromise>()));
        }

        [Fact]
        public void IterateActiveStreams()
        {
            var builder = Http2FrameCodecBuilder.ForServer();
            builder.EncoderEnforceMaxConcurrentStreams = true;
            this.SetUp(builder, new Http2Settings().MaxConcurrentStreams(1));

            this.frameListener.OnHeadersRead(this.http2HandlerCtx, 3, request, 0, false);

            IHttp2HeadersFrame headersFrame = inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(headersFrame);

            IHttp2FrameStream activeInbond = headersFrame.Stream;

            IHttp2FrameStream activeOutbound = frameCodec.NewStream();
            channel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()) { Stream = activeOutbound });

            IHttp2FrameStream bufferedOutbound = frameCodec.NewStream();
            channel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()) { Stream = bufferedOutbound });

            //@SuppressWarnings("unused")
            IHttp2FrameStream idleStream = frameCodec.NewStream();

            ISet<IHttp2FrameStream> activeStreams = new HashSet<IHttp2FrameStream>();
            bool localVisit(IHttp2FrameStream stream)
            {
                activeStreams.Add(stream);
                return true;
            }
            frameCodec.ForEachActiveStream(localVisit);

            Assert.Equal(2, activeStreams.Count);

            ISet<IHttp2FrameStream> expectedStreams = new HashSet<IHttp2FrameStream>();
            expectedStreams.Add(activeInbond);
            expectedStreams.Add(activeOutbound);
            Assert.Equal(expectedStreams, activeStreams);
        }

        [Fact]
        public async Task StreamShouldBeOpenInListener()
        {
            IHttp2FrameStream stream2 = frameCodec.NewStream();
            Assert.Equal(Http2StreamState.Idle, stream2.State);

            await channel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()) { Stream = stream2 });
            Assert.Equal(Http2StreamState.Open, stream2.State);
        }

        [Fact]
        public void UpgradeEventNoRefCntError()
        {
            this.frameListener.OnHeadersRead(this.http2HandlerCtx, Http2CodecUtil.HttpUpgradeStreamId, request, 31, false);

            var constructors = typeof(HttpServerUpgradeHandler.UpgradeEvent).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var upgradeEvent = (HttpServerUpgradeHandler.UpgradeEvent)constructors[0].Invoke(new object[]
            {
                (AsciiString)"HTTP/2",
                new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/")
            });
            channel.Pipeline.FireUserEventTriggered(upgradeEvent);
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
            channel.Pipeline.AddAfter(http2HandlerCtx.Name, null, new TestUpgradeChannelHandler());

            this.frameListener.OnHeadersRead(this.http2HandlerCtx, Http2CodecUtil.HttpUpgradeStreamId, this.request, 31, false);


            string longString = "*".PadRight(70000, '*');
            DefaultFullHttpRequest request =
                new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/", BB(longString));

            var constructors = typeof(HttpServerUpgradeHandler.UpgradeEvent).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var upgradeEvent = (HttpServerUpgradeHandler.UpgradeEvent)constructors[0].Invoke(new object[]
            {
                (AsciiString)"HTTP/2",
                request
            });
            channel.Pipeline.FireUserEventTriggered(upgradeEvent);
        }

        private static IByteBuffer BB(string s)
        {
            return ByteBufferUtil.WriteUtf8(UnpooledByteBufferAllocator.Default, s);
        }
    }

    public class VerifiableHttp2FrameWriter : DefaultHttp2FrameWriter
    {
        public override Task WriteDataAsync(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream, IPromise promise)
        {
            // duplicate 'data' to prevent readerIndex from being changed, to ease verification
            return base.WriteDataAsync(ctx, streamId, data.Duplicate(), padding, endOfStream, promise);
        }
    }
}
