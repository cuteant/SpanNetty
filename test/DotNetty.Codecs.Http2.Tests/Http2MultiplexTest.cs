
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Moq;
    using Xunit;

    public abstract class Http2MultiplexTest<TCodec> : IDisposable
        where TCodec : Http2FrameCodec
    {
        private const int c_initialRemoteStreamWindow = 1024;

        private static IHttp2Headers s_request = new DefaultHttp2Headers()
        {
            Method = HttpMethod.Get.AsciiName,
            Scheme = HttpScheme.Https.Name,
            Authority = new AsciiString("example.org"),
            Path = new AsciiString("/foo")
        };

        private EmbeddedChannel _parentChannel;
        private Mock<IHttp2FrameWriter> _frameWriter;
        private Http2FrameInboundWriter _frameInboundWriter;
        private TestChannelInitializer _childChannelInitializer;
        private TCodec _codec;

        public Http2MultiplexTest()
        {
            _childChannelInitializer = new TestChannelInitializer();
            _parentChannel = new EmbeddedChannel();
            _frameInboundWriter = new Http2FrameInboundWriter(_parentChannel);
            //_parentChannel.Connect(new InetSocketAddress(0));
            _frameWriter = Http2TestUtil.MockedFrameWriter();
            _codec = NewCodec(_childChannelInitializer, _frameWriter.Object);
            _parentChannel.Pipeline.AddLast(_codec);
            IChannelHandler multiplexer = NewMultiplexer(_childChannelInitializer);
            if (multiplexer != null)
            {
                _parentChannel.Pipeline.AddLast(multiplexer);
            }

            _parentChannel.RunPendingTasks();
            _parentChannel.Pipeline.FireChannelActive();

            _parentChannel.WriteInbound(Http2CodecUtil.ConnectionPrefaceBuf());

            Http2Settings settings = new Http2Settings().InitialWindowSize(c_initialRemoteStreamWindow);
            _frameInboundWriter.WriteInboundSettings(settings);

            _frameWriter.Verify(
                x => x.WriteSettingsAckAsync(
                    It.Is<IChannelHandlerContext>(v => v == _codec._ctx),
                    It.IsAny<IPromise>()));

            _frameInboundWriter.WriteInboundSettingsAck();

            var settingsFrame = _parentChannel.ReadInbound<IHttp2SettingsFrame>();
            Assert.NotNull(settingsFrame);
            var settingsAckFrame = _parentChannel.ReadInbound<IHttp2SettingsAckFrame>();
            Assert.NotNull(settingsAckFrame);

            // Handshake
            _frameWriter.Verify(
                x => x.WriteSettingsAsync(
                    It.Is<IChannelHandlerContext>(v => v == _codec._ctx),
                    It.IsAny<Http2Settings>(),
                    It.IsAny<IPromise>()));
        }

        public void Dispose()
        {
            if (_childChannelInitializer.handler != null)
            {
                ((LastInboundHandler)_childChannelInitializer.handler).FinishAndReleaseAll();
            }
            _parentChannel.FinishAndReleaseAll();
            _codec = null;
        }

        protected abstract TCodec NewCodec(TestChannelInitializer childChannelInitializer, IHttp2FrameWriter frameWriter);

        protected abstract IChannelHandler NewMultiplexer(TestChannelInitializer childChannelInitializer);

        class ChannelHandlerForWriteUnknownFrame : ChannelHandlerAdapter
        {
            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                ctx.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()));
                ctx.WriteAndFlushAsync(new DefaultHttp2UnknownFrame((Http2FrameTypes)99, new Http2Flags()));
                ctx.FireChannelActive();
            }
        }

        [Fact]
        public void WriteUnknownFrame()
        {
            IHttp2StreamChannel childChannel = NewOutboundStream(new ChannelHandlerForWriteUnknownFrame());

            Assert.True(childChannel.Active);

            _parentChannel.RunPendingTasks();

            _frameWriter.Verify(
                x => x.WriteFrameAsync(
                    It.Is<IChannelHandlerContext>(v => v == _codec._ctx),
                    It.Is<Http2FrameTypes>(v => v == (Http2FrameTypes)99),
                    EqStreamId(childChannel),
                    It.IsAny<Http2Flags>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<IPromise>()));
        }

        class NewHttp2StreamChannelHandler : ChannelHandlerAdapter
        {
            private readonly AtomicReference<IHttp2StreamChannel> _streamChannelRef;
            private readonly IChannelHandler _childHandler;

            public NewHttp2StreamChannelHandler(AtomicReference<IHttp2StreamChannel> streamChannelRef, IChannelHandler childHandler)
            {
                _streamChannelRef = streamChannelRef;
                _childHandler = childHandler;
            }

            public override void ChannelRegistered(IChannelHandlerContext context)
            {
                Assert.Null(_streamChannelRef.Value);
                _streamChannelRef.Value = (IHttp2StreamChannel)context.Channel;
                context.Pipeline.AddLast(_childHandler);
                context.FireChannelRegistered();
            }
        }

        private IHttp2StreamChannel NewInboundStream(int streamId, bool endStream, IChannelHandler childHandler)
        {
            return NewInboundStream(streamId, endStream, null, childHandler);
        }

        private IHttp2StreamChannel NewInboundStream(int streamId, bool endStream, AtomicInteger maxReads, IChannelHandler childHandler)
        {
            var streamChannelRef = new AtomicReference<IHttp2StreamChannel>();
            _childChannelInitializer.maxReads = maxReads;
            _childChannelInitializer.handler = new NewHttp2StreamChannelHandler(streamChannelRef, childHandler);

            _frameInboundWriter.WriteInboundHeaders(streamId, s_request, 0, endStream);
            _parentChannel.RunPendingTasks();
            var channel = streamChannelRef.Value;
            Assert.Equal(streamId, channel.Stream.Id);
            return channel;
        }

        [Fact]
        public void ReadUnkownFrame()
        {
            LastInboundHandler handler = new LastInboundHandler();

            var channel = NewInboundStream(3, true, handler);
            _frameInboundWriter.WriteInboundFrame((Http2FrameTypes)99, channel.Stream.Id, new Http2Flags(), Unpooled.Empty);

            // header frame and unknown frame
            VerifyFramesMultiplexedToCorrectChannel(channel, handler, 2);

            var childChannel = NewOutboundStream(new ChannelHandlerAdapter());
            Assert.True(childChannel.Active);
        }

        [Fact]
        public void HeaderAndDataFramesShouldBeDelivered()
        {
            LastInboundHandler inboundHandler = new LastInboundHandler();
            _childChannelInitializer.handler = inboundHandler;

            var channel = NewInboundStream(3, false, inboundHandler);
            IHttp2HeadersFrame headersFrame = new DefaultHttp2HeadersFrame(s_request) { Stream = channel.Stream };
            IHttp2DataFrame dataFrame1 = new DefaultHttp2DataFrame(Http2TestUtil.BB("hello")) { Stream = channel.Stream };
            IHttp2DataFrame dataFrame2 = new DefaultHttp2DataFrame(Http2TestUtil.BB("world")) { Stream = channel.Stream };

            Assert.True(inboundHandler.IsChannelActive);
            _frameInboundWriter.WriteInboundData(channel.Stream.Id, Http2TestUtil.BB("hello"), 0, false);
            _frameInboundWriter.WriteInboundData(channel.Stream.Id, Http2TestUtil.BB("world"), 0, false);

            Assert.Equal(headersFrame, inboundHandler.ReadInbound<IHttp2HeadersFrame>());

            Http2TestUtil.AssertEqualsAndRelease(dataFrame1, inboundHandler.ReadInbound<IHttp2Frame>());
            Http2TestUtil.AssertEqualsAndRelease(dataFrame2, inboundHandler.ReadInbound<IHttp2Frame>());

            Assert.Null(inboundHandler.ReadInbound<object>());
        }

        [Fact]
        public void FramesShouldBeMultiplexed()
        {
            LastInboundHandler handler1 = new LastInboundHandler();
            IHttp2StreamChannel channel1 = NewInboundStream(3, false, handler1);
            LastInboundHandler handler2 = new LastInboundHandler();
            IHttp2StreamChannel channel2 = NewInboundStream(5, false, handler2);
            LastInboundHandler handler3 = new LastInboundHandler();
            IHttp2StreamChannel channel3 = NewInboundStream(11, false, handler3);

            VerifyFramesMultiplexedToCorrectChannel(channel1, handler1, 1);
            VerifyFramesMultiplexedToCorrectChannel(channel2, handler2, 1);
            VerifyFramesMultiplexedToCorrectChannel(channel3, handler3, 1);

            _frameInboundWriter.WriteInboundData(channel2.Stream.Id, Http2TestUtil.BB("hello"), 0, false);
            _frameInboundWriter.WriteInboundData(channel1.Stream.Id, Http2TestUtil.BB("foo"), 0, true);
            _frameInboundWriter.WriteInboundData(channel2.Stream.Id, Http2TestUtil.BB("world"), 0, true);
            _frameInboundWriter.WriteInboundData(channel3.Stream.Id, Http2TestUtil.BB("bar"), 0, true);

            VerifyFramesMultiplexedToCorrectChannel(channel1, handler1, 1);
            VerifyFramesMultiplexedToCorrectChannel(channel2, handler2, 2);
            VerifyFramesMultiplexedToCorrectChannel(channel3, handler3, 1);
        }

        [Fact]
        public void InboundDataFrameShouldUpdateLocalFlowController()
        {
            var flowController = new Mock<IHttp2LocalFlowController>();
            _codec.Connection.Local.FlowController = flowController.Object;

            LastInboundHandler handler = new LastInboundHandler();
            IHttp2StreamChannel channel = NewInboundStream(3, false, handler);

            var tenBytes = Http2TestUtil.BB("0123456789");

            _frameInboundWriter.WriteInboundData(channel.Stream.Id, tenBytes, 0, true);

            // Verify we marked the bytes as consumed
            flowController.Verify(
                x => x.ConsumeBytes(
                    It.Is<IHttp2Stream>(v => v.Id == channel.Stream.Id),
                    It.Is<int>(v => v == 10)));

            // headers and data frame
            VerifyFramesMultiplexedToCorrectChannel(channel, handler, 2);
        }

        [Fact]
        public void UnhandledHttp2FramesShouldBePropagated()
        {
            IHttp2PingFrame pingFrame = new DefaultHttp2PingFrame(0);
            _frameInboundWriter.WriteInboundPing(false, 0);
            Assert.Equal(_parentChannel.ReadInbound<object>(), pingFrame);

            DefaultHttp2GoAwayFrame goAwayFrame = new DefaultHttp2GoAwayFrame((Http2Error)1,
                    _parentChannel.Allocator.Buffer().WriteLong(8));
            _frameInboundWriter.WriteInboundGoAway(0, goAwayFrame.ErrorCode, goAwayFrame.Content.RetainedDuplicate());

            var frame = _parentChannel.ReadInbound<IHttp2GoAwayFrame>();
            Http2TestUtil.AssertEqualsAndRelease(frame, goAwayFrame);
        }

        [Fact]
        public void ChannelReadShouldRespectAutoRead()
        {
            LastInboundHandler inboundHandler = new LastInboundHandler();
            IHttp2StreamChannel childChannel = NewInboundStream(3, false, inboundHandler);
            Assert.True(childChannel.Configuration.AutoRead);
            var headersFrame = inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(headersFrame);

            childChannel.Configuration.AutoRead = false;

            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("hello world"), 0, false);
            var dataFrame0 = inboundHandler.ReadInbound<IHttp2DataFrame>();
            Assert.NotNull(dataFrame0);
            ReferenceCountUtil.Release(dataFrame0);

            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("foo"), 0, false);
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("bar"), 0, false);

            Assert.Null(inboundHandler.ReadInbound<object>());

            childChannel.Configuration.AutoRead = true;
            VerifyFramesMultiplexedToCorrectChannel(childChannel, inboundHandler, 2);
        }

        class ChannelReadShouldRespectAutoReadAndNotProduceNPEHandler : ChannelHandlerAdapter
        {
            private int _count;

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                context.FireChannelRead(message);
                // Close channel after 2 reads so there is still something in the inboundBuffer when the close happens.
                if (++_count == 2)
                {
                    context.CloseAsync();
                }
            }
        }

        [Fact]
        public void ChannelReadShouldRespectAutoReadAndNotProduceNPE()
        {
            LastInboundHandler inboundHandler = new LastInboundHandler();
            IHttp2StreamChannel childChannel = NewInboundStream(3, false, inboundHandler);
            Assert.True(childChannel.Configuration.AutoRead);
            var headersFrame = inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(headersFrame);

            childChannel.Configuration.AutoRead = false;
            childChannel.Pipeline.AddFirst(new ChannelReadShouldRespectAutoReadAndNotProduceNPEHandler());
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("hello world"), 0, false);
            var dataFrame0 = inboundHandler.ReadInbound<IHttp2DataFrame>();
            Assert.NotNull(dataFrame0);
            ReferenceCountUtil.Release(dataFrame0);

            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("foo"), 0, false);
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("bar"), 0, false);
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("bar"), 0, false);

            Assert.Null(inboundHandler.ReadInbound<object>());

            childChannel.Configuration.AutoRead = true;
            VerifyFramesMultiplexedToCorrectChannel(childChannel, inboundHandler, 3);
            inboundHandler.CheckException();
        }

        class ChannelHandlerForUseReadWithoutAutoRead : ChannelHandlerAdapter
        {
            readonly bool _readComplete;

            public ChannelHandlerForUseReadWithoutAutoRead(bool readComplete) => _readComplete = readComplete;

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                ctx.FireChannelRead(msg);
                if (!_readComplete)
                {
                    ctx.Read();
                }
            }

            public override void ChannelReadComplete(IChannelHandlerContext ctx)
            {
                ctx.FireChannelReadComplete();
                if (_readComplete)
                {
                    ctx.Read();
                }
            }
        }

        [Fact]
        public void ReadInChannelReadWithoutAutoRead()
        {
            UseReadWithoutAutoRead(false);
        }

        [Fact]
        public void ReadInChannelReadCompleteWithoutAutoRead()
        {
            UseReadWithoutAutoRead(true);
        }

        private void UseReadWithoutAutoRead(bool readComplete)
        {
            LastInboundHandler inboundHandler = new LastInboundHandler();
            IHttp2StreamChannel childChannel = NewInboundStream(3, false, inboundHandler);
            Assert.True(childChannel.Configuration.AutoRead);
            childChannel.Configuration.AutoRead = false;
            Assert.False(childChannel.Configuration.AutoRead);

            var headersFrame = inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(headersFrame);

            // Add a handler which will request reads.
            childChannel.Pipeline.AddFirst(new ChannelHandlerForUseReadWithoutAutoRead(readComplete));

            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("hello world"), 0, false);
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("foo"), 0, false);
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("bar"), 0, false);
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("hello world"), 0, false);
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("foo"), 0, false);
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("bar"), 0, true);

            VerifyFramesMultiplexedToCorrectChannel(childChannel, inboundHandler, 6);
        }

        private IHttp2StreamChannel NewOutboundStream(IChannelHandler handler)
        {
            return new Http2StreamChannelBootstrap(_parentChannel).Handler(handler)
                    .OpenAsync().GetAwaiter().GetResult();
        }

        /**
         * A child channel for an HTTP/2 stream in IDLE state (that is no headers sent or received),
         * should not emit a RST_STREAM frame on close, as this is a connection error of type protocol error.
         */
        [Fact]
        public void IdleOutboundStreamShouldNotWriteResetFrameOnClose()
        {
            LastInboundHandler handler = new LastInboundHandler();

            IChannel childChannel = NewOutboundStream(handler);
            Assert.True(childChannel.Active);

            childChannel.CloseAsync();
            _parentChannel.RunPendingTasks();

            Assert.False(childChannel.Open);
            Assert.False(childChannel.Active);
            Assert.Null(_parentChannel.ReadOutbound<object>());
        }

        class ChannelHandler_OutboundStreamShouldNotWriteResetFrameOnClose : ChannelHandlerAdapter
        {
            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                ctx.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()));
                ctx.FireChannelActive();
            }
        }
        [Fact]
        public void OutboundStreamShouldWriteResetFrameOnClose_HeadersSent()
        {
            IChannelHandler handler = new ChannelHandler_OutboundStreamShouldNotWriteResetFrameOnClose();

            IHttp2StreamChannel childChannel = NewOutboundStream(handler);
            Assert.True(childChannel.Active);

            childChannel.CloseAsync();
            _frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _codec._ctx),
                    EqStreamId(childChannel),
                    It.Is<Http2Error>(v => v == Http2Error.Cancel),
                    It.IsAny<IPromise>()));
        }

        [Fact]
        public void OutboundStreamShouldNotWriteResetFrameOnClose_IfStreamDidntExist()
        {
            bool headersWritten = false;
            _frameWriter
                .Setup(x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _codec._ctx),
                    It.IsAny<int>(),
                    It.IsAny<IHttp2Headers>(),
                    It.IsAny<int>(),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, IHttp2Headers, int, short, bool, int, bool, IPromise>((ctx, streamId, headers, streamDependency, weight, exclusive, padding, endOfStream, p) =>
                {
                    // We want to fail to write the first headers frame. This is what happens if the connection
                    // refuses to allocate a new stream due to having received a GOAWAY.
                    if (!headersWritten)
                    {
                        headersWritten = true;
                        p.SetException(new Exception("boom"));
                        return p.Task;
                    }
                    p.Complete();
                    return p.Task;
                });

            var childChannel = NewOutboundStream(new ChannelHandler_OutboundStreamShouldNotWriteResetFrameOnClose());

            Assert.False(childChannel.Active);

            childChannel.CloseAsync();
            _parentChannel.RunPendingTasks();
            // The channel was never active so we should not generate a RST frame.
            _frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _codec._ctx),
                    EqStreamId(childChannel),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()),
                Times.Never());

            Assert.Empty(_parentChannel.OutboundMessages);
        }

        [Fact]
        public void InboundRstStreamFireChannelInactive()
        {
            LastInboundHandler inboundHandler = new LastInboundHandler();
            IHttp2StreamChannel channel = NewInboundStream(3, false, inboundHandler);
            Assert.True(inboundHandler.IsChannelActive);
            _frameInboundWriter.WriteInboundRstStream(channel.Stream.Id, Http2Error.InternalError);

            Assert.False(inboundHandler.IsChannelActive);

            // A RST_STREAM frame should NOT be emitted, as we received a RST_STREAM.
            _frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _codec._ctx),
                    EqStreamId(channel),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()),
                Times.Never());
        }

        [Fact]
        public void StreamExceptionTriggersChildChannelExceptionAndClose()
        {
            LastInboundHandler inboundHandler = new LastInboundHandler();
            IHttp2StreamChannel channel = NewInboundStream(3, false, inboundHandler);
            Assert.True(channel.Active);
            StreamException cause = new StreamException(channel.Stream.Id, Http2Error.ProtocolError, "baaam!");
            _parentChannel.Pipeline.FireExceptionCaught(cause);

            Assert.False(channel.Active);

            Assert.Throws<StreamException>(() => inboundHandler.CheckException());
        }

        [Fact]
        public void StreamClosedErrorTranslatedToClosedChannelExceptionOnWrites()
        {
            LastInboundHandler inboundHandler = new LastInboundHandler();

            IHttp2StreamChannel childChannel = NewOutboundStream(inboundHandler);
            Assert.True(childChannel.Active);

            IHttp2Headers headers = new DefaultHttp2Headers();
            _frameWriter
                .Setup(x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _codec._ctx),
                    It.IsAny<int>(),
                    It.Is<IHttp2Headers>(v => v.Equals(headers)),
                    It.IsAny<int>(),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, IHttp2Headers, int, short, bool, int, bool, IPromise>((ctx, streamId, headers, streamDependency, weight, exclusive, padding, endOfStream, p) =>
                {
                    p.SetException(new StreamException(childChannel.Stream.Id, Http2Error.StreamClosed, "Stream Closed"));
                    return p.Task;
                });
            var future = childChannel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()));

            _parentChannel.Flush();

            Assert.False(childChannel.Active);
            Assert.False(childChannel.Open);

            inboundHandler.CheckException();

            Assert.ThrowsAsync<ClosedChannelException>(async () => await future);
        }

        [Fact]
        public void CreatingWritingReadingAndClosingOutboundStreamShouldWork()
        {
            LastInboundHandler inboundHandler = new LastInboundHandler();
            IHttp2StreamChannel childChannel = NewOutboundStream(inboundHandler);
            Assert.True(childChannel.Active);
            Assert.True(inboundHandler.IsChannelActive);

            // Write to the child channel
            IHttp2Headers headers = new DefaultHttp2Headers()
            {
                Scheme = HttpScheme.Https.Name,
                Method = HttpMethod.Get.AsciiName,
                Path = (AsciiString)"/foo.txt"
            };
            childChannel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(headers));

            // Read from the child channel
            _frameInboundWriter.WriteInboundHeaders(childChannel.Stream.Id, headers, 0, false);

            var headersFrame = inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(headersFrame);
            Assert.Equal(headers, headersFrame.Headers);

            // Close the child channel.
            childChannel.CloseAsync();

            _parentChannel.RunPendingTasks();
            // An active outbound stream should emit a RST_STREAM frame.
            _frameWriter.Verify(
                x => x.WriteRstStreamAsync(
                    It.Is<IChannelHandlerContext>(v => v == _codec._ctx),
                    EqStreamId(childChannel),
                    It.IsAny<Http2Error>(),
                    It.IsAny<IPromise>()));

            Assert.False(childChannel.Open);
            Assert.False(childChannel.Active);
            Assert.False(inboundHandler.IsChannelActive);
        }

        // Test failing the promise of the first headers frame of an outbound stream. In practice this error case would most
        // likely happen due to the max concurrent streams limit being hit or the channel running out of stream identifiers.
        //
        [Fact]
        public void FailedOutboundStreamCreationThrowsAndClosesChannel()
        {
            LastInboundHandler handler = new LastInboundHandler();
            IHttp2StreamChannel childChannel = NewOutboundStream(handler);
            Assert.True(childChannel.Active);

            IHttp2Headers headers = new DefaultHttp2Headers();
            _frameWriter
                .Setup(x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _codec._ctx),
                    It.IsAny<int>(),
                    It.Is<IHttp2Headers>(v => v == headers),
                    It.IsAny<int>(),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, IHttp2Headers, int, short, bool, int, bool, IPromise>((ctx, streamId, headers, streamDependency, weight, exclusive, padding, endOfStream, p) =>
                {
                    p.SetException(new Http2NoMoreStreamIdsException());
                    return p.Task;
                });

            var future = childChannel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(headers));
            _parentChannel.Flush();

            Assert.False(childChannel.Active);
            Assert.False(childChannel.Open);

            handler.CheckException();

            Assert.ThrowsAsync<Http2NoMoreStreamIdsException>(async () => await future);
        }

        [Fact]
        public void ChannelClosedWhenCloseListenerCompletes()
        {
            LastInboundHandler inboundHandler = new LastInboundHandler();
            IHttp2StreamChannel childChannel = NewInboundStream(3, false, inboundHandler);

            Assert.True(childChannel.Open);
            Assert.True(childChannel.Active);

            AtomicBoolean channelOpen = new AtomicBoolean(true);
            AtomicBoolean channelActive = new AtomicBoolean(true);

            // Create a promise before actually doing the close, because otherwise we would be adding a listener to a future
            // that is already completed because we are using EmbeddedChannel which executes code in the JUnit thread.
            var p = childChannel.NewPromise();
            p.Task.ContinueWith(future =>
            {
                channelOpen.Value = childChannel.Open;
                channelActive.Value = childChannel.Active;
            }, TaskContinuationOptions.ExecuteSynchronously);
            childChannel.CloseAsync(p).GetAwaiter().GetResult();

            Assert.False(channelOpen.Value);
            Assert.False(channelActive.Value);
            Assert.False(childChannel.Active);
        }

        [Fact]
        public void ChannelClosedWhenChannelClosePromiseCompletes()
        {
            LastInboundHandler inboundHandler = new LastInboundHandler();
            IHttp2StreamChannel childChannel = NewInboundStream(3, false, inboundHandler);

            Assert.True(childChannel.Open);
            Assert.True(childChannel.Active);

            AtomicBoolean channelOpen = new AtomicBoolean(true);
            AtomicBoolean channelActive = new AtomicBoolean(true);

            var closeFuture = childChannel.CloseAsync();
            closeFuture.ContinueWith(future =>
            {
                channelOpen.Value = childChannel.Open;
                channelActive.Value = childChannel.Active;
            }, TaskContinuationOptions.ExecuteSynchronously);
            closeFuture.GetAwaiter().GetResult();

            Assert.False(channelOpen.Value);
            Assert.False(channelActive.Value);
            Assert.False(childChannel.Active);
        }

        [Fact]
        public void ChannelClosedWhenWriteFutureFails()
        {
            var writePromises = new Deque<IPromise>();

            LastInboundHandler inboundHandler = new LastInboundHandler();
            IHttp2StreamChannel childChannel = NewInboundStream(3, false, inboundHandler);

            Assert.True(childChannel.Open);
            Assert.True(childChannel.Active);

            AtomicBoolean channelOpen = new AtomicBoolean(true);
            AtomicBoolean channelActive = new AtomicBoolean(true);

            IHttp2Headers headers = new DefaultHttp2Headers();
            _frameWriter
                .Setup(x => x.WriteHeadersAsync(
                    It.Is<IChannelHandlerContext>(v => v == _codec._ctx),
                    It.IsAny<int>(),
                    It.Is<IHttp2Headers>(v => v.Equals(headers)),
                    It.IsAny<int>(),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<IPromise>()))
                .Returns<IChannelHandlerContext, int, IHttp2Headers, int, short, bool, int, bool, IPromise>((ctx, streamId, headers, streamDependency, weight, exclusive, padding, endOfStream, p) =>
                {
                    writePromises.AddToBack(p);
                    return p.Task;
                });

            var f = childChannel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()));
            Assert.False(f.IsCompleted);
            f.ContinueWith(future =>
            {
                channelOpen.Value = childChannel.Open;
                channelActive.Value = childChannel.Active;
            }, TaskContinuationOptions.ExecuteSynchronously);

            var first = writePromises.RemoveFromFront();
            first.SetException(new ClosedChannelException());
            try
            {
                f.GetAwaiter().GetResult();
            }
            catch { }

            Assert.False(channelOpen.Value);
            Assert.False(channelActive.Value);
            Assert.False(childChannel.Active);
        }

        [Fact]
        public void ChannelClosedTwiceMarksPromiseAsSuccessful()
        {
            LastInboundHandler inboundHandler = new LastInboundHandler();
            IHttp2StreamChannel childChannel = NewInboundStream(3, false, inboundHandler);

            Assert.True(childChannel.Open);
            Assert.True(childChannel.Active);
            childChannel.CloseAsync().GetAwaiter().GetResult();
            childChannel.CloseAsync().GetAwaiter().GetResult();

            Assert.False(childChannel.Open);
            Assert.False(childChannel.Active);
        }

        [Fact]
        public void SettingChannelOptsAndAttrs()
        {
            AttributeKey<string> key = AttributeKey<string>.NewInstance(Guid.NewGuid().ToString());

            IChannel childChannel = NewOutboundStream(new ChannelHandlerAdapter());
            childChannel.Configuration.AutoRead = false;
            childChannel.Configuration.WriteSpinCount = 1000;
            childChannel.GetAttribute(key).Set("bar");
            Assert.False(childChannel.Configuration.AutoRead);
            Assert.Equal(1000, childChannel.Configuration.WriteSpinCount);
            Assert.Equal("bar", childChannel.GetAttribute(key).Get());
        }

        [Fact]
        public void OutboundFlowControlWritability()
        {
            IHttp2StreamChannel childChannel = NewOutboundStream(new ChannelHandlerAdapter());
            Assert.True(childChannel.Active);

            Assert.True(childChannel.IsWritable);
            childChannel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()));
            _parentChannel.Flush();

            // Test for initial window size
            Assert.True(c_initialRemoteStreamWindow < childChannel.Configuration.WriteBufferHighWaterMark);

            Assert.True(childChannel.IsWritable);
            childChannel.WriteAsync(new DefaultHttp2DataFrame(Unpooled.Buffer().WriteZero(16 * 1024 * 1024)));
            Assert.Equal(0, childChannel.BytesBeforeUnwritable);
            Assert.False(childChannel.IsWritable);
        }

        [Fact]
        public void WritabilityOfParentIsRespected()
        {
            IHttp2StreamChannel childChannel = NewOutboundStream(new ChannelHandlerAdapter());
            childChannel.Configuration.WriteBufferLowWaterMark = 2048;
            childChannel.Configuration.WriteBufferHighWaterMark = 4096;
            _parentChannel.Configuration.WriteBufferLowWaterMark = 256;
            _parentChannel.Configuration.WriteBufferHighWaterMark = 512;
            Assert.True(childChannel.IsWritable);
            Assert.True(_parentChannel.Active);

            childChannel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()));
            _parentChannel.Flush();

            Assert.True(childChannel.IsWritable);
            childChannel.WriteAsync(new DefaultHttp2DataFrame(Unpooled.Buffer().WriteZero(256)));
            Assert.True(childChannel.IsWritable);
            childChannel.WriteAndFlushAsync(new DefaultHttp2DataFrame(Unpooled.Buffer().WriteZero(512)));

            long bytesBeforeUnwritable = childChannel.BytesBeforeUnwritable;
            Assert.NotEqual(0, bytesBeforeUnwritable);
            // Add something to the ChannelOutboundBuffer of the parent to simulate queuing in the parents channel buffer
            // and verify that this only affect the writability of the parent channel while the child stays writable
            // until it used all of its credits.
            _parentChannel.Unsafe.OutboundBuffer.AddMessage(
                     Unpooled.Buffer().WriteZero(800), 800, _parentChannel.VoidPromise());
            Assert.False(_parentChannel.IsWritable);

            Assert.True(childChannel.IsWritable);
            Assert.Equal(4096, childChannel.BytesBeforeUnwritable);

            // Flush everything which simulate writing everything to the socket.
            _parentChannel.Flush();
            Assert.True(_parentChannel.IsWritable);
            Assert.True(childChannel.IsWritable);
            Assert.Equal(bytesBeforeUnwritable, childChannel.BytesBeforeUnwritable);

            var future = childChannel.WriteAndFlushAsync(new DefaultHttp2DataFrame(
                    Unpooled.Buffer().WriteZero((int)bytesBeforeUnwritable)));
            Assert.False(childChannel.IsWritable);
            Assert.True(_parentChannel.IsWritable);

            _parentChannel.Flush();
            Assert.False(future.IsCompleted);
            Assert.True(_parentChannel.IsWritable);
            Assert.False(childChannel.IsWritable);

            // Now write an window update frame for the stream which then should ensure we will flush the bytes that were
            // queued in the RemoteFlowController before for the stream.
            _frameInboundWriter.WriteInboundWindowUpdate(childChannel.Stream.Id, (int)bytesBeforeUnwritable);
            Assert.True(childChannel.IsWritable);
            Assert.True(future.IsCompleted);
        }

        class ChannelHandler_ChannelClosedWhenInactiveFired : ChannelHandlerAdapter
        {
            readonly AtomicBoolean _channelOpen;
            readonly AtomicBoolean _channelActive;

            public ChannelHandler_ChannelClosedWhenInactiveFired(AtomicBoolean channelOpen, AtomicBoolean channelActive)
            {
                _channelOpen = channelOpen;
                _channelActive = channelActive;
            }

            public override void ChannelInactive(IChannelHandlerContext ctx)
            {
                _channelOpen.Value = ctx.Channel.Open;
                _channelActive.Value = ctx.Channel.Active;

                base.ChannelInactive(ctx);
            }
        }

        [Fact]
        public void ChannelClosedWhenInactiveFired()
        {
            LastInboundHandler inboundHandler = new LastInboundHandler();
            IHttp2StreamChannel childChannel = NewInboundStream(3, false, inboundHandler);

            AtomicBoolean channelOpen = new AtomicBoolean(false);
            AtomicBoolean channelActive = new AtomicBoolean(false);
            Assert.True(childChannel.Open);
            Assert.True(childChannel.Active);

            childChannel.Pipeline.AddLast(new ChannelHandler_ChannelClosedWhenInactiveFired(channelOpen, channelActive));

            childChannel.CloseAsync().GetAwaiter().GetResult();
            Assert.False(channelOpen.Value);
            Assert.False(channelActive.Value);
        }

        class ChannelHandler_ChannelInactiveHappensAfterExceptionCaughtEvents0 : ChannelHandlerAdapter
        {
            public override void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
                context.CloseAsync();
                throw new Exception("exception");
            }
        }
        class ChannelHandler_ChannelInactiveHappensAfterExceptionCaughtEvents1 : ChannelHandlerAdapter
        {
            readonly AtomicInteger _count;
            readonly AtomicInteger _exceptionCaught;
            readonly AtomicInteger _channelInactive;
            readonly AtomicInteger _channelUnregistered;

            public ChannelHandler_ChannelInactiveHappensAfterExceptionCaughtEvents1(AtomicInteger count,
                AtomicInteger exceptionCaught, AtomicInteger channelInactive, AtomicInteger channelUnregistered)
            {
                _count = count;
                _exceptionCaught = exceptionCaught;
                _channelInactive = channelInactive;
                _channelUnregistered = channelUnregistered;
            }

            public override void ChannelInactive(IChannelHandlerContext context)
            {
                _channelInactive.Value = _count.GetAndIncrement();
                base.ChannelInactive(context);
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                _exceptionCaught.Value = _count.GetAndIncrement();
                base.ExceptionCaught(context, exception);
            }

            public override void ChannelUnregistered(IChannelHandlerContext context)
            {
                _channelUnregistered.Value = _count.GetAndIncrement();
                base.ChannelUnregistered(context);
            }
        }
        [Fact]
        public void ChannelInactiveHappensAfterExceptionCaughtEvents()
        {
            AtomicInteger count = new AtomicInteger(0);
            AtomicInteger exceptionCaught = new AtomicInteger(-1);
            AtomicInteger channelInactive = new AtomicInteger(-1);
            AtomicInteger channelUnregistered = new AtomicInteger(-1);
            IHttp2StreamChannel childChannel = NewOutboundStream(new ChannelHandler_ChannelInactiveHappensAfterExceptionCaughtEvents0());

            childChannel.Pipeline.AddLast(new ChannelHandler_ChannelInactiveHappensAfterExceptionCaughtEvents1(
                count, exceptionCaught, channelInactive, channelUnregistered));

            childChannel.Pipeline.FireUserEventTriggered(new object());
            _parentChannel.RunPendingTasks();

            // The events should have happened in this order because the inactive and deregistration events
            // get deferred as they do in the AbstractChannel.
            Assert.Equal(0, exceptionCaught.Value);
            Assert.Equal(1, channelInactive.Value);
            Assert.Equal(2, channelUnregistered.Value);
        }

        [Fact]
        public void CallUnsafeCloseMultipleTimes()
        {
            LastInboundHandler inboundHandler = new LastInboundHandler();
            IHttp2StreamChannel childChannel = NewInboundStream(3, false, inboundHandler);
            childChannel.Unsafe.Close(childChannel.VoidPromise());

            var promise = childChannel.NewPromise();
            childChannel.Unsafe.Close(promise);
            promise.Task.GetAwaiter().GetResult();
            childChannel.CloseCompletion.GetAwaiter().GetResult();
        }

        class ChannelHandlerContextConsumer : IConsumer<IChannelHandlerContext>
        {
            readonly AtomicBoolean _shouldDisableAutoRead;
            readonly AtomicInteger _channelReadCompleteCount;

            public ChannelHandlerContextConsumer(AtomicBoolean shouldDisableAutoRead, AtomicInteger channelReadCompleteCount = null)
            {
                _shouldDisableAutoRead = shouldDisableAutoRead;
                _channelReadCompleteCount = channelReadCompleteCount;
            }

            public void Accept(IChannelHandlerContext obj)
            {
                _channelReadCompleteCount?.Increment();
                if (_shouldDisableAutoRead.Value)
                {
                    obj.Channel.Configuration.AutoRead = false;
                }
            }
        }

        class ReadCompleteSupressHandler : ChannelHandlerAdapter
        {
            public override void ChannelReadComplete(IChannelHandlerContext context)
            {
                // We want to simulate the parent channel calling channelRead and delay calling channelReadComplete.
            }
        }

        [Fact]
        public void EndOfStreamDoesNotDiscardData()
        {
            AtomicInteger numReads = new AtomicInteger(1);
            AtomicBoolean shouldDisableAutoRead = new AtomicBoolean(false);
            LastInboundHandler inboundHandler = new LastInboundHandler(new ChannelHandlerContextConsumer(shouldDisableAutoRead));
            IHttp2StreamChannel childChannel = NewInboundStream(3, false, numReads, inboundHandler);
            childChannel.Configuration.AutoRead = false;

            IHttp2DataFrame dataFrame1 = new DefaultHttp2DataFrame(Http2TestUtil.BB("1")) { Stream = childChannel.Stream };
            IHttp2DataFrame dataFrame2 = new DefaultHttp2DataFrame(Http2TestUtil.BB("2")) { Stream = childChannel.Stream };
            IHttp2DataFrame dataFrame3 = new DefaultHttp2DataFrame(Http2TestUtil.BB("3")) { Stream = childChannel.Stream };
            IHttp2DataFrame dataFrame4 = new DefaultHttp2DataFrame(Http2TestUtil.BB("4")) { Stream = childChannel.Stream };

            Assert.Equal(new DefaultHttp2HeadersFrame(s_request) { Stream = childChannel.Stream }, inboundHandler.ReadInbound<IHttp2HeadersFrame>());

            var readCompleteSupressHandler = new ReadCompleteSupressHandler();
            _parentChannel.Pipeline.AddFirst(readCompleteSupressHandler);
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("1"), 0, false);

            Http2TestUtil.AssertEqualsAndRelease(dataFrame1, inboundHandler.ReadInbound<IHttp2DataFrame>());

            // Deliver frames, and then a stream closed while read is inactive.
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("2"), 0, false);
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("3"), 0, false);
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("4"), 0, false);

            shouldDisableAutoRead.Value = true;
            childChannel.Configuration.AutoRead = true;
            numReads.Value = 1;

            _frameInboundWriter.WriteInboundRstStream(childChannel.Stream.Id, Http2Error.NoError);

            // Detecting EOS should flush all pending data regardless of read calls.
            Http2TestUtil.AssertEqualsAndRelease(dataFrame2, inboundHandler.ReadInbound<IHttp2DataFrame>());
            Assert.Null(inboundHandler.ReadInbound<object>());

            // As we limited the number to 1 we also need to call read() again.
            childChannel.Read();

            Http2TestUtil.AssertEqualsAndRelease(dataFrame3, inboundHandler.ReadInbound<IHttp2DataFrame>());
            Http2TestUtil.AssertEqualsAndRelease(dataFrame4, inboundHandler.ReadInbound<IHttp2DataFrame>());

            var resetFrame = UseUserEventForResetFrame() ? inboundHandler.ReadUserEvent<IHttp2ResetFrame>() :
                    inboundHandler.ReadInbound<IHttp2ResetFrame>();

            Assert.Equal(childChannel.Stream, resetFrame.Stream);
            Assert.Equal(Http2Error.NoError, resetFrame.ErrorCode);

            Assert.Null(inboundHandler.ReadInbound<object>());

            // Now we want to call channelReadComplete and simulate the end of the read loop.
            _parentChannel.Pipeline.Remove(readCompleteSupressHandler);
            _parentChannel.FlushInbound();

            childChannel.CloseCompletion.GetAwaiter().GetResult();
        }

        protected abstract bool UseUserEventForResetFrame();

        protected abstract bool IgnoreWindowUpdateFrames();

        [Fact]
        public void ChildQueueIsDrainedAndNewDataIsDispatchedInParentReadLoopAutoRead()
        {
            AtomicInteger numReads = new AtomicInteger(1);
            AtomicInteger channelReadCompleteCount = new AtomicInteger(0);
            AtomicBoolean shouldDisableAutoRead = new AtomicBoolean(false);
            IConsumer<IChannelHandlerContext> ctxConsumer = new ChannelHandlerContextConsumer(shouldDisableAutoRead, channelReadCompleteCount);
            LastInboundHandler inboundHandler = new LastInboundHandler(ctxConsumer);
            IHttp2StreamChannel childChannel = NewInboundStream(3, false, numReads, inboundHandler);
            childChannel.Configuration.AutoRead = false;

            IHttp2DataFrame dataFrame1 = new DefaultHttp2DataFrame(Http2TestUtil.BB("1")) { Stream = childChannel.Stream };
            IHttp2DataFrame dataFrame2 = new DefaultHttp2DataFrame(Http2TestUtil.BB("2")) { Stream = childChannel.Stream };
            IHttp2DataFrame dataFrame3 = new DefaultHttp2DataFrame(Http2TestUtil.BB("3")) { Stream = childChannel.Stream };
            IHttp2DataFrame dataFrame4 = new DefaultHttp2DataFrame(Http2TestUtil.BB("4")) { Stream = childChannel.Stream };

            Assert.Equal(new DefaultHttp2HeadersFrame(s_request) { Stream = childChannel.Stream }, inboundHandler.ReadInbound<IHttp2HeadersFrame>());

            var readCompleteSupressHandler = new ReadCompleteSupressHandler();
            _parentChannel.Pipeline.AddFirst(readCompleteSupressHandler);

            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("1"), 0, false);

            Http2TestUtil.AssertEqualsAndRelease(dataFrame1, inboundHandler.ReadInbound<IHttp2DataFrame>());

            // We want one item to be in the queue, and allow the numReads to be larger than 1. This will ensure that
            // when beginRead() is called the child channel is added to the readPending queue of the parent channel.
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("2"), 0, false);

            numReads.Value = 10;
            shouldDisableAutoRead.Value = true;
            childChannel.Configuration.AutoRead = true;

            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("3"), 0, false);
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("4"), 0, false);

            // Detecting EOS should flush all pending data regardless of read calls.
            Http2TestUtil.AssertEqualsAndRelease(dataFrame2, inboundHandler.ReadInbound<IHttp2DataFrame>());
            Http2TestUtil.AssertEqualsAndRelease(dataFrame3, inboundHandler.ReadInbound<IHttp2DataFrame>());
            Http2TestUtil.AssertEqualsAndRelease(dataFrame4, inboundHandler.ReadInbound<IHttp2DataFrame>());

            Assert.Null(inboundHandler.ReadInbound<object>());

            // Now we want to call channelReadComplete and simulate the end of the read loop.
            _parentChannel.Pipeline.Remove(readCompleteSupressHandler);
            _parentChannel.FlushInbound();

            // 3 = 1 for initialization + 1 for read when auto read was off + 1 for when auto read was back on
            Assert.Equal(3, channelReadCompleteCount.Value);
        }

        [Fact]
        public void ChildQueueIsDrainedAndNewDataIsDispatchedInParentReadLoopNoAutoRead()
        {
            AtomicInteger numReads = new AtomicInteger(1);
            AtomicInteger channelReadCompleteCount = new AtomicInteger(0);
            AtomicBoolean shouldDisableAutoRead = new AtomicBoolean(false);
            IConsumer<IChannelHandlerContext> ctxConsumer = new ChannelHandlerContextConsumer(shouldDisableAutoRead, channelReadCompleteCount);
            LastInboundHandler inboundHandler = new LastInboundHandler(ctxConsumer);
            IHttp2StreamChannel childChannel = NewInboundStream(3, false, numReads, inboundHandler);
            childChannel.Configuration.AutoRead = false;

            IHttp2DataFrame dataFrame1 = new DefaultHttp2DataFrame(Http2TestUtil.BB("1")) { Stream = childChannel.Stream };
            IHttp2DataFrame dataFrame2 = new DefaultHttp2DataFrame(Http2TestUtil.BB("2")) { Stream = childChannel.Stream };
            IHttp2DataFrame dataFrame3 = new DefaultHttp2DataFrame(Http2TestUtil.BB("3")) { Stream = childChannel.Stream };
            IHttp2DataFrame dataFrame4 = new DefaultHttp2DataFrame(Http2TestUtil.BB("4")) { Stream = childChannel.Stream };

            Assert.Equal(new DefaultHttp2HeadersFrame(s_request) { Stream = childChannel.Stream }, inboundHandler.ReadInbound<IHttp2HeadersFrame>());

            var readCompleteSupressHandler = new ReadCompleteSupressHandler();
            _parentChannel.Pipeline.AddFirst(readCompleteSupressHandler);

            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("1"), 0, false);

            Http2TestUtil.AssertEqualsAndRelease(dataFrame1, inboundHandler.ReadInbound<IHttp2Frame>());

            // We want one item to be in the queue, and allow the numReads to be larger than 1. This will ensure that
            // when beginRead() is called the child channel is added to the readPending queue of the parent channel.
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("2"), 0, false);

            numReads.Value = 2;
            childChannel.Read();

            Http2TestUtil.AssertEqualsAndRelease(dataFrame2, inboundHandler.ReadInbound<IHttp2Frame>());

            Assert.Null(inboundHandler.ReadInbound<object>());

            // This is the second item that was read, this should be the last until we call read() again. This should also
            // notify of readComplete().
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("3"), 0, false);

            Http2TestUtil.AssertEqualsAndRelease(dataFrame3, inboundHandler.ReadInbound<IHttp2Frame>());

            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("4"), 0, false);
            Assert.Null(inboundHandler.ReadInbound<object>());

            childChannel.Read();

            Http2TestUtil.AssertEqualsAndRelease(dataFrame4, inboundHandler.ReadInbound<IHttp2Frame>());

            Assert.Null(inboundHandler.ReadInbound<object>());

            // Now we want to call channelReadComplete and simulate the end of the read loop.
            _parentChannel.Pipeline.Remove(readCompleteSupressHandler);
            _parentChannel.FlushInbound();

            // 3 = 1 for initialization + 1 for first read of 2 items + 1 for second read of 2 items +
            // 1 for parent channel readComplete
            Assert.Equal(4, channelReadCompleteCount.Value);
        }

        [Fact]
        public void UseReadWithoutAutoReadInRead()
        {
            UseReadWithoutAutoReadBuffered(false);
        }

        [Fact]
        public void UseReadWithoutAutoReadInReadComplete()
        {
            UseReadWithoutAutoReadBuffered(true);
        }

        private void UseReadWithoutAutoReadBuffered(bool triggerOnReadComplete)
        {
            LastInboundHandler inboundHandler = new LastInboundHandler();
            IHttp2StreamChannel childChannel = NewInboundStream(3, false, inboundHandler);
            Assert.True(childChannel.Configuration.AutoRead);
            childChannel.Configuration.AutoRead = false;
            Assert.False(childChannel.Configuration.AutoRead);

            var headersFrame = inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(headersFrame);

            // Write some bytes to get the channel into the idle state with buffered data and also verify we
            // do not dispatch it until we receive a read() call.
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("hello world"), 0, false);
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("foo"), 0, false);
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("bar"), 0, false);

            // Add a handler which will request reads.
            childChannel.Pipeline.AddFirst(new UseReadWithoutAutoReadBufferedHandler(triggerOnReadComplete));

            inboundHandler.Channel.Read();

            VerifyFramesMultiplexedToCorrectChannel(childChannel, inboundHandler, 3);

            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("hello world2"), 0, false);
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("foo2"), 0, false);
            _frameInboundWriter.WriteInboundData(childChannel.Stream.Id, Http2TestUtil.BB("bar2"), 0, true);

            VerifyFramesMultiplexedToCorrectChannel(childChannel, inboundHandler, 3);
        }

        class UseReadWithoutAutoReadBufferedHandler : ChannelHandlerAdapter
        {
            private readonly bool _triggerOnReadComplete;

            public UseReadWithoutAutoReadBufferedHandler(bool triggerOnReadComplete)
            {
                _triggerOnReadComplete = triggerOnReadComplete;
            }

            public override void ChannelReadComplete(IChannelHandlerContext context)
            {
                base.ChannelReadComplete(context);
                if (_triggerOnReadComplete)
                {
                    context.Read();
                    context.Read();
                }
            }

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                base.ChannelRead(context, message);
                if (!_triggerOnReadComplete)
                {
                    context.Read();
                    context.Read();
                }
            }
        }

        private static void VerifyFramesMultiplexedToCorrectChannel(IHttp2StreamChannel streamChannel,
                                                                    LastInboundHandler inboundHandler,
                                                                    int numFrames)
        {
            for (int i = 0; i < numFrames; i++)
            {
                IHttp2StreamFrame frame = inboundHandler.ReadInbound<IHttp2StreamFrame>();
                Assert.NotNull(frame);
                Assert.Equal(streamChannel.Stream, frame.Stream);
                ReferenceCountUtil.Release(frame);
            }
            Assert.Null(inboundHandler.ReadInbound<object>());
        }

        private static int EqStreamId(IHttp2StreamChannel channel)
        {
            return It.Is<int>(v => v == channel.Stream.Id);
        }
    }
}
