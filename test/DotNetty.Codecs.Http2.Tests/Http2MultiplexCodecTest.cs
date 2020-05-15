
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
    using Xunit;

    public class Http2MultiplexCodecTest : IDisposable
    {
        private EmbeddedChannel parentChannel;
        private Action<object, IPromise> writer;

        private TestChannelInitializer childChannelInitializer;

        private static IHttp2Headers request = new DefaultHttp2Headers()
        {
            Method = HttpMethod.Get.AsciiName,
            Scheme = HttpScheme.Https.Name,
            Authority = new AsciiString("example.org"),
            Path = new AsciiString("/foo")
        };

        private TestableHttp2MultiplexCodec codec;
        private TestableHttp2MultiplexCodec.Stream inboundStream;
        private TestableHttp2MultiplexCodec.Stream outboundStream;

        private const int initialRemoteStreamWindow = 1024;

        public Http2MultiplexCodecTest()
        {
            this.childChannelInitializer = new TestChannelInitializer();
            this.parentChannel = new EmbeddedChannel();

            this.writer = (msg, promise) =>
            {
                this.parentChannel.OutboundMessages.Enqueue(msg);
                promise.Complete();
            };

            //parentChannel.connect(new InetSocketAddress(0));
            this.codec = (TestableHttp2MultiplexCodec)new TestableHttp2MultiplexCodecBuilder(true, childChannelInitializer, this).Build();
            this.parentChannel.Pipeline.AddLast(this.codec);
            this.parentChannel.RunPendingTasks();

            Http2Settings settings = new Http2Settings().InitialWindowSize(initialRemoteStreamWindow);
            this.codec.OnHttp2Frame(new DefaultHttp2SettingsFrame(settings));

            this.inboundStream = (TestableHttp2MultiplexCodec.Stream)this.codec.NewStream();
            this.inboundStream.id = 3;
            this.outboundStream = (TestableHttp2MultiplexCodec.Stream)this.codec.NewStream();
            this.outboundStream.id = 2;
        }

        public void Dispose()
        {
            if (this.childChannelInitializer.handler != null)
            {
                ((LastInboundHandler)this.childChannelInitializer.handler).FinishAndReleaseAll();
            }
            this.parentChannel.FinishAndReleaseAll();
            this.codec = null;
        }

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
            this.childChannelInitializer.handler = new ChannelHandlerForWriteUnknownFrame();

            IChannel childChannel = this.NewOutboundStream();
            Assert.True(childChannel.Active);

            IHttp2FrameStream stream = this.ReadOutboundHeadersAndAssignId();
            this.parentChannel.RunPendingTasks();

            IHttp2UnknownFrame frame = this.parentChannel.ReadOutbound<IHttp2UnknownFrame>();
            Assert.Equal(stream, frame.Stream);
            Assert.Equal((Http2FrameTypes)99, frame.FrameType);
            Assert.Equal(new Http2Flags(), frame.Flags);
            frame.Release();
        }

        [Fact]
        public void HeaderAndDataFramesShouldBeDelivered()
        {
            LastInboundHandler inboundHandler = new LastInboundHandler();
            this.childChannelInitializer.handler = inboundHandler;

            IHttp2HeadersFrame headersFrame = new DefaultHttp2HeadersFrame(request) { Stream = this.inboundStream };
            IHttp2DataFrame dataFrame1 = new DefaultHttp2DataFrame(BB("hello")) { Stream = this.inboundStream };
            IHttp2DataFrame dataFrame2 = new DefaultHttp2DataFrame(BB("world")) { Stream = this.inboundStream };

            Assert.False(inboundHandler.IsChannelActive);
            this.inboundStream.state = Http2StreamState.Open;
            this.codec.OnHttp2StreamStateChanged(this.inboundStream);
            this.codec.OnHttp2Frame(headersFrame);
            Assert.True(inboundHandler.IsChannelActive);
            this.codec.OnHttp2Frame(dataFrame1);
            this.codec.OnHttp2Frame(dataFrame2);

            Assert.Equal(headersFrame, inboundHandler.ReadInbound<IHttp2HeadersFrame>());
            Assert.Equal(dataFrame1, inboundHandler.ReadInbound<IHttp2DataFrame>());
            Assert.Equal(dataFrame2, inboundHandler.ReadInbound<IHttp2DataFrame>());
            Assert.Null(inboundHandler.ReadInbound<object>());

            dataFrame1.Release();
            dataFrame2.Release();
        }

        [Fact]
        public void FramesShouldBeMultiplexed()
        {
            TestableHttp2MultiplexCodec.Stream stream3 = (TestableHttp2MultiplexCodec.Stream)this.codec.NewStream();
            stream3.id = 3;
            TestableHttp2MultiplexCodec.Stream stream5 = (TestableHttp2MultiplexCodec.Stream)this.codec.NewStream();
            stream5.id = 5;

            TestableHttp2MultiplexCodec.Stream stream11 = (TestableHttp2MultiplexCodec.Stream)this.codec.NewStream();
            stream11.id = 11;

            LastInboundHandler inboundHandler3 = this.StreamActiveAndWriteHeaders(stream3);
            LastInboundHandler inboundHandler5 = this.StreamActiveAndWriteHeaders(stream5);
            LastInboundHandler inboundHandler11 = this.StreamActiveAndWriteHeaders(stream11);

            VerifyFramesMultiplexedToCorrectChannel(stream3, inboundHandler3, 1);
            VerifyFramesMultiplexedToCorrectChannel(stream5, inboundHandler5, 1);
            VerifyFramesMultiplexedToCorrectChannel(stream11, inboundHandler11, 1);

            this.codec.OnHttp2Frame(new DefaultHttp2DataFrame(BB("hello"), false) { Stream = stream5 });
            this.codec.OnHttp2Frame(new DefaultHttp2DataFrame(BB("foo"), true) { Stream = stream3 });
            this.codec.OnHttp2Frame(new DefaultHttp2DataFrame(BB("world"), true) { Stream = stream5 });
            this.codec.OnHttp2Frame(new DefaultHttp2DataFrame(BB("bar"), true) { Stream = stream11 });
            VerifyFramesMultiplexedToCorrectChannel(stream5, inboundHandler5, 2);
            VerifyFramesMultiplexedToCorrectChannel(stream3, inboundHandler3, 1);
            VerifyFramesMultiplexedToCorrectChannel(stream11, inboundHandler11, 1);
        }

        [Fact]
        public void InboundDataFrameShouldEmitWindowUpdateFrame()
        {
            LastInboundHandler inboundHandler = this.StreamActiveAndWriteHeaders(this.inboundStream);
            IByteBuffer tenBytes = BB("0123456789");
            this.codec.OnHttp2Frame(new DefaultHttp2DataFrame(tenBytes, true) { Stream = this.inboundStream });
            this.codec.OnChannelReadComplete();

            var windowUpdate = this.parentChannel.ReadOutbound<IHttp2WindowUpdateFrame>();
            Assert.NotNull(windowUpdate);

            Assert.Equal(this.inboundStream, windowUpdate.Stream);
            Assert.Equal(10, windowUpdate.WindowSizeIncrement);

            // headers and data frame
            VerifyFramesMultiplexedToCorrectChannel(this.inboundStream, inboundHandler, 2);
        }

        [Fact]
        public void UnhandledHttp2FramesShouldBePropagated()
        {
            Assert.NotNull(this.parentChannel.ReadInbound<object>() as IHttp2SettingsFrame);

            IHttp2PingFrame pingFrame = new DefaultHttp2PingFrame(0);
            this.codec.OnHttp2Frame(pingFrame);
            Assert.Same(this.parentChannel.ReadInbound<object>(), pingFrame);

            DefaultHttp2GoAwayFrame goAwayFrame =
                    new DefaultHttp2GoAwayFrame((Http2Error)1, this.parentChannel.Allocator.Buffer().WriteLong(8));
            this.codec.OnHttp2Frame(goAwayFrame);

            var frame = this.parentChannel.ReadInbound<IHttp2GoAwayFrame>();
            Assert.Same(frame, goAwayFrame);
            Assert.True(frame.Release());
        }

        [Fact]
        public void ChannelReadShouldRespectAutoRead()
        {
            LastInboundHandler inboundHandler = this.StreamActiveAndWriteHeaders(this.inboundStream);
            IChannel childChannel = inboundHandler.Channel;
            Assert.True(childChannel.Configuration.AutoRead);
            var headersFrame = inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(headersFrame);

            childChannel.Configuration.AutoRead = false;
            this.codec.OnHttp2Frame(
                    new DefaultHttp2DataFrame(BB("hello world"), false) { Stream = this.inboundStream });
            this.codec.OnChannelReadComplete();
            var dataFrame0 = inboundHandler.ReadInbound<IHttp2DataFrame>();
            Assert.NotNull(dataFrame0);
            ReferenceCountUtil.Release(dataFrame0);

            this.codec.OnHttp2Frame(new DefaultHttp2DataFrame(BB("foo"), false) { Stream = this.inboundStream });
            this.codec.OnHttp2Frame(new DefaultHttp2DataFrame(BB("bar"), true) { Stream = this.inboundStream });
            this.codec.OnChannelReadComplete();

            dataFrame0 = inboundHandler.ReadInbound<IHttp2DataFrame>();
            Assert.Null(dataFrame0);

            childChannel.Configuration.AutoRead = true;
            VerifyFramesMultiplexedToCorrectChannel(this.inboundStream, inboundHandler, 2);
        }

        class ChannelHandlerForUseReadWithoutAutoRead : ChannelHandlerAdapter
        {
            readonly bool readComplete;

            public ChannelHandlerForUseReadWithoutAutoRead(bool readComplete) => this.readComplete = readComplete;

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                ctx.FireChannelRead(msg);
                if (!this.readComplete)
                {
                    ctx.Read();
                }
            }

            public override void ChannelReadComplete(IChannelHandlerContext ctx)
            {
                ctx.FireChannelReadComplete();
                if (this.readComplete)
                {
                    ctx.Read();
                }
            }
        }

        [Fact]
        public void ReadInChannelReadWithoutAutoRead()
        {
            this.UseReadWithoutAutoRead(false);
        }

        [Fact]
        public void ReadInChannelReadCompleteWithoutAutoRead()
        {
            this.UseReadWithoutAutoRead(true);
        }

        private void UseReadWithoutAutoRead(bool readComplete)
        {
            LastInboundHandler inboundHandler = this.StreamActiveAndWriteHeaders(inboundStream);
            var childChannel = inboundHandler.Channel;
            Assert.True(childChannel.Configuration.AutoRead);
            childChannel.Configuration.AutoRead = false;
            Assert.False(childChannel.Configuration.AutoRead);

            var headersFrame = inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(headersFrame);

            // Add a handler which will request reads.
            childChannel.Pipeline.AddFirst(new ChannelHandlerForUseReadWithoutAutoRead(readComplete));

            codec.OnHttp2Frame(
                    new DefaultHttp2DataFrame(BB("hello world"), false) { Stream = this.inboundStream });
            codec.OnHttp2Frame(new DefaultHttp2DataFrame(BB("foo"), false) { Stream = this.inboundStream });
            codec.OnHttp2Frame(new DefaultHttp2DataFrame(BB("bar"), true) { Stream = this.inboundStream });
            codec.OnChannelReadComplete();

            codec.OnHttp2Frame(
                    new DefaultHttp2DataFrame(BB("hello world"), false) { Stream = this.inboundStream });
            codec.OnHttp2Frame(new DefaultHttp2DataFrame(BB("foo"), false) { Stream = this.inboundStream });
            codec.OnHttp2Frame(new DefaultHttp2DataFrame(BB("bar"), true) { Stream = this.inboundStream });
            codec.OnChannelReadComplete();

            VerifyFramesMultiplexedToCorrectChannel(inboundStream, inboundHandler, 6);
        }

        /**
         * A child channel for a HTTP/2 stream in IDLE state (that is no headers sent or received),
         * should not emit a RST_STREAM frame on close, as this is a connection error of type protocol error.
         */

        [Fact]
        public void IdleOutboundStreamShouldNotWriteResetFrameOnClose()
        {
            childChannelInitializer.handler = new LastInboundHandler();

            IChannel childChannel = this.NewOutboundStream();
            Assert.True(childChannel.Active);

            childChannel.CloseAsync();
            this.parentChannel.RunPendingTasks();

            Assert.False(childChannel.Open);
            Assert.False(childChannel.Active);
            Assert.Null(this.parentChannel.ReadOutbound<object>());
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
            this.childChannelInitializer.handler = new ChannelHandler_OutboundStreamShouldNotWriteResetFrameOnClose();

            IChannel childChannel = this.NewOutboundStream();
            Assert.True(childChannel.Active);

            IHttp2FrameStream stream2 = this.ReadOutboundHeadersAndAssignId();

            childChannel.CloseAsync();
            this.parentChannel.RunPendingTasks();

            var reset = this.parentChannel.ReadOutbound<IHttp2ResetFrame>();
            Assert.Equal(stream2, reset.Stream);
            Assert.Equal(Http2Error.Cancel, reset.ErrorCode);
        }

        [Fact]
        public void OutboundStreamShouldNotWriteResetFrameOnClose_IfStreamDidntExist()
        {
            var headersWritten = false;
            var prevWriter = this.writer;
            this.writer = (msg, promise) =>
            {
                // We want to fail to write the first headers frame. This is what happens if the connection
                // refuses to allocate a new stream due to having received a GOAWAY.
                if (!headersWritten && msg is IHttp2HeadersFrame)
                {
                    headersWritten = true;
                    IHttp2HeadersFrame headersFrame = (IHttp2HeadersFrame)msg;
                    TestableHttp2MultiplexCodec.Stream stream =
                            (TestableHttp2MultiplexCodec.Stream)headersFrame.Stream;
                    stream.id = 1;
                    promise.SetException(new Exception("boom"));
                }
                else
                {
                    prevWriter(msg, promise);
                }
            };

            this.childChannelInitializer.handler = new ChannelHandler_OutboundStreamShouldNotWriteResetFrameOnClose();

            IChannel childChannel = this.NewOutboundStream();
            Assert.False(childChannel.Active);

            childChannel.CloseAsync();
            this.parentChannel.RunPendingTasks();
            Assert.True(this.parentChannel.OutboundMessages.IsEmpty);
        }

        [Fact]
        public void InboundRstStreamFireChannelInactive()
        {
            LastInboundHandler inboundHandler = this.StreamActiveAndWriteHeaders(this.inboundStream);
            Assert.True(inboundHandler.IsChannelActive);
            this.codec.OnHttp2Frame(new DefaultHttp2ResetFrame(Http2Error.InternalError) { Stream = this.inboundStream });
            this.codec.OnChannelReadComplete();

            // This will be called by the frame codec.
            this.inboundStream.state = Http2StreamState.Closed;
            this.codec.OnHttp2StreamStateChanged(this.inboundStream);
            this.parentChannel.RunPendingTasks();

            Assert.False(inboundHandler.IsChannelActive);
            // A RST_STREAM frame should NOT be emitted, as we received a RST_STREAM.
            Assert.Null(this.parentChannel.ReadOutbound<object>());
        }

        [Fact]
        public void StreamExceptionTriggersChildChannelExceptionAndClose()
        {
            LastInboundHandler inboundHandler = this.StreamActiveAndWriteHeaders(this.inboundStream);

            StreamException cause = new StreamException(this.inboundStream.Id, Http2Error.ProtocolError, "baaam!");
            Http2FrameStreamException http2Ex = new Http2FrameStreamException(
                    this.inboundStream, Http2Error.ProtocolError, cause);
            this.codec.OnHttp2FrameStreamException(http2Ex);

            Assert.Throws<StreamException>(() => inboundHandler.CheckException());
        }

        [Fact]
        public void StreamExceptionClosesChildChannel()
        {
            LastInboundHandler inboundHandler = this.StreamActiveAndWriteHeaders(this.inboundStream);

            Assert.True(inboundHandler.IsChannelActive);
            StreamException cause = new StreamException(this.inboundStream.id, Http2Error.ProtocolError, "baaam!");
            Http2FrameStreamException http2Ex = new Http2FrameStreamException(
                    this.inboundStream, Http2Error.ProtocolError, cause);
            this.codec.OnHttp2FrameStreamException(http2Ex);
            this.parentChannel.RunPendingTasks();

            Assert.False(inboundHandler.IsChannelActive);
            Assert.Throws<StreamException>(() => inboundHandler.CheckException());
        }

        [Fact]
        public void StreamClosedErrorTranslatedToClosedChannelExceptionOnWrites()
        {
            this.writer = (msg, promise) =>
            {
                promise.TrySetException(new StreamException(this.inboundStream.Id, Http2Error.StreamClosed, "Stream Closed"));
            };
            LastInboundHandler inboundHandler = new LastInboundHandler();
            childChannelInitializer.handler = inboundHandler;

            IChannel childChannel = this.NewOutboundStream();
            Assert.True(childChannel.Active);

            var future = childChannel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()));
            this.parentChannel.Flush();

            Assert.False(childChannel.Active);
            Assert.False(childChannel.Open);

            inboundHandler.CheckException();

            Assert.ThrowsAsync<ClosedChannelException>(async () => await future);
        }

        [Fact]
        public void CreatingWritingReadingAndClosingOutboundStreamShouldWork()
        {
            LastInboundHandler inboundHandler = new LastInboundHandler();
            childChannelInitializer.handler = inboundHandler;

            IHttp2StreamChannel childChannel = this.NewOutboundStream();
            Assert.True(childChannel.Active);
            Assert.True(inboundHandler.IsChannelActive);

            // Write to the child channel
            IHttp2Headers headers = new DefaultHttp2Headers()
            {
                Scheme = AsciiString.Of("https"),
                Method = AsciiString.Of("GET"),
                Path = AsciiString.Of("/foo.txt")
            };
            childChannel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(headers));

            this.ReadOutboundHeadersAndAssignId();

            // Read from the child channel
            headers = new DefaultHttp2Headers() { Scheme = AsciiString.Of("https"), Status = AsciiString.Of("200") };
            this.codec.OnHttp2Frame(new DefaultHttp2HeadersFrame(headers) { Stream = childChannel.Stream });
            this.codec.OnChannelReadComplete();

            var headersFrame = inboundHandler.ReadInbound<IHttp2HeadersFrame>();
            Assert.NotNull(headersFrame);
            Assert.Same(headers, headersFrame.Headers);

            // Close the child channel.
            childChannel.CloseAsync();

            this.parentChannel.RunPendingTasks();
            // An active outbound stream should emit a RST_STREAM frame.
            var rstFrame = this.parentChannel.ReadOutbound<IHttp2ResetFrame>();
            Assert.NotNull(rstFrame);
            Assert.Equal(childChannel.Stream, rstFrame.Stream);
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
            this.writer = (msg, promise) =>
            {
                promise.TrySetException(new Http2NoMoreStreamIdsException());
            };
            LastInboundHandler inboundHandler = new LastInboundHandler();
            childChannelInitializer.handler = inboundHandler;

            IChannel childChannel = this.NewOutboundStream();
            Assert.True(childChannel.Active);

            var future = childChannel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()));
            this.parentChannel.Flush();

            Assert.False(childChannel.Active);
            Assert.False(childChannel.Open);

            inboundHandler.CheckException();

            Assert.ThrowsAsync<Http2NoMoreStreamIdsException>(async () => await future);
        }

        [Fact]
        public void ChannelClosedWhenCloseListenerCompletes()
        {
            LastInboundHandler inboundHandler = this.StreamActiveAndWriteHeaders(this.inboundStream);
            IHttp2StreamChannel childChannel = (IHttp2StreamChannel)inboundHandler.Channel;

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
            LastInboundHandler inboundHandler = this.StreamActiveAndWriteHeaders(this.inboundStream);
            IHttp2StreamChannel childChannel = (IHttp2StreamChannel)inboundHandler.Channel;

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
            this.writer = (msg, promise) =>
            {
                ReferenceCountUtil.Release(msg);
                writePromises.AddToBack(promise);
            };

            LastInboundHandler inboundHandler = this.StreamActiveAndWriteHeaders(this.inboundStream);
            IHttp2StreamChannel childChannel = (IHttp2StreamChannel)inboundHandler.Channel;

            Assert.True(childChannel.Open);
            Assert.True(childChannel.Active);

            AtomicBoolean channelOpen = new AtomicBoolean(true);
            AtomicBoolean channelActive = new AtomicBoolean(true);

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
            LastInboundHandler inboundHandler = this.StreamActiveAndWriteHeaders(this.inboundStream);
            IHttp2StreamChannel childChannel = (IHttp2StreamChannel)inboundHandler.Channel;

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
            AttributeKey<string> key = AttributeKey<string>.NewInstance("foo");

            IChannel childChannel = this.NewOutboundStream();
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
            IHttp2StreamChannel childChannel = this.NewOutboundStream();
            Assert.True(childChannel.Active);

            Assert.True(childChannel.IsWritable);
            childChannel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()));
            this.parentChannel.Flush();

            IHttp2FrameStream stream = this.ReadOutboundHeadersAndAssignId();

            // Test for initial window size
            Assert.Equal(initialRemoteStreamWindow, childChannel.Configuration.WriteBufferHighWaterMark);

            this.codec.OnHttp2StreamWritabilityChanged(stream, true);
            Assert.True(childChannel.IsWritable);
            this.codec.OnHttp2StreamWritabilityChanged(stream, false);
            Assert.False(childChannel.IsWritable);
        }

        [Fact]
        public void WritabilityAndFlowControl()
        {
            LastInboundHandler inboundHandler = this.StreamActiveAndWriteHeaders(this.inboundStream);
            IHttp2StreamChannel childChannel = (IHttp2StreamChannel)inboundHandler.Channel;
            Assert.Equal("", inboundHandler.WritabilityStates);

            // HEADERS frames are not flow controlled, so they should not affect the flow control window.
            childChannel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers()));
            this.codec.OnHttp2StreamWritabilityChanged(childChannel.Stream, true);

            Assert.Equal("true", inboundHandler.WritabilityStates, StringComparer.OrdinalIgnoreCase);

            this.codec.OnHttp2StreamWritabilityChanged(childChannel.Stream, true);
            Assert.Equal("true", inboundHandler.WritabilityStates, StringComparer.OrdinalIgnoreCase);

            this.codec.OnHttp2StreamWritabilityChanged(childChannel.Stream, false);
            Assert.Equal("true,false", inboundHandler.WritabilityStates, StringComparer.OrdinalIgnoreCase);

            this.codec.OnHttp2StreamWritabilityChanged(childChannel.Stream, false);
            Assert.Equal("true,false", inboundHandler.WritabilityStates, StringComparer.OrdinalIgnoreCase);
        }

        class ChannelHandler_ChannelClosedWhenInactiveFired : ChannelHandlerAdapter
        {
            readonly AtomicBoolean channelOpen;
            readonly AtomicBoolean channelActive;

            public ChannelHandler_ChannelClosedWhenInactiveFired(AtomicBoolean channelOpen, AtomicBoolean channelActive)
            {
                this.channelOpen = channelOpen;
                this.channelActive = channelActive;
            }

            public override void ChannelInactive(IChannelHandlerContext ctx)
            {
                this.channelOpen.Value = ctx.Channel.Open;
                this.channelActive.Value = ctx.Channel.Active;

                base.ChannelInactive(ctx);
            }
        }
        [Fact]
        public void ChannelClosedWhenInactiveFired()
        {
            LastInboundHandler inboundHandler = this.StreamActiveAndWriteHeaders(this.inboundStream);
            IHttp2StreamChannel childChannel = (IHttp2StreamChannel)inboundHandler.Channel;

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
            readonly AtomicInteger count;
            readonly AtomicInteger exceptionCaught;
            readonly AtomicInteger channelInactive;
            readonly AtomicInteger channelUnregistered;

            public ChannelHandler_ChannelInactiveHappensAfterExceptionCaughtEvents1(AtomicInteger count,
                AtomicInteger exceptionCaught, AtomicInteger channelInactive, AtomicInteger channelUnregistered)
            {
                this.count = count;
                this.exceptionCaught = exceptionCaught;
                this.channelInactive = channelInactive;
                this.channelUnregistered = channelUnregistered;
            }

            public override void ChannelInactive(IChannelHandlerContext context)
            {
                this.channelInactive.Value = this.count.GetAndIncrement();
                base.ChannelInactive(context);
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                this.exceptionCaught.Value = this.count.GetAndIncrement();
                base.ExceptionCaught(context, exception);
            }

            public override void ChannelUnregistered(IChannelHandlerContext context)
            {
                this.channelUnregistered.Value = this.count.GetAndIncrement();
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
            IHttp2StreamChannel childChannel = this.NewOutboundStream();

            childChannel.Pipeline.AddLast(new ChannelHandler_ChannelInactiveHappensAfterExceptionCaughtEvents0());

            childChannel.Pipeline.AddLast(new ChannelHandler_ChannelInactiveHappensAfterExceptionCaughtEvents1(
                count, exceptionCaught, channelInactive, channelUnregistered));

            childChannel.Pipeline.FireUserEventTriggered(new object());
            this.parentChannel.RunPendingTasks();

            // The events should have happened in this order because the inactive and deregistration events
            // get deferred as they do in the AbstractChannel.
            Assert.Equal(0, exceptionCaught.Value);
            Assert.Equal(1, channelInactive.Value);
            Assert.Equal(2, channelUnregistered.Value);
        }

        [Fact(Skip = "not supported anymore atm")]
        public void CancellingWritesBeforeFlush()
        {
            LastInboundHandler inboundHandler = this.StreamActiveAndWriteHeaders(this.inboundStream);
            IChannel childChannel = inboundHandler.Channel;

            IHttp2HeadersFrame headers1 = new DefaultHttp2HeadersFrame(new DefaultHttp2Headers());
            IHttp2HeadersFrame headers2 = new DefaultHttp2HeadersFrame(new DefaultHttp2Headers());
            IPromise writePromise = childChannel.NewPromise();
            childChannel.WriteAsync(headers1, writePromise);
            childChannel.WriteAsync(headers2);
            Assert.True(writePromise.TrySetCanceled());
            childChannel.Flush();

            IHttp2HeadersFrame headers = this.parentChannel.ReadOutbound<IHttp2HeadersFrame>();
            Assert.Same(headers, headers2);
        }

        [Fact]
        public void CallUnsafeCloseMultipleTimes()
        {
            LastInboundHandler inboundHandler = this.StreamActiveAndWriteHeaders(this.inboundStream);
            IHttp2StreamChannel childChannel = (IHttp2StreamChannel)inboundHandler.Channel;
            childChannel.Unsafe.Close(childChannel.VoidPromise());

            var promise = childChannel.NewPromise();
            childChannel.Unsafe.Close(promise);
            promise.Task.GetAwaiter().GetResult();
            childChannel.CloseCompletion.GetAwaiter().GetResult();
        }

        class ChannelHandlerContextConsumer : IConsumer<IChannelHandlerContext>
        {
            readonly AtomicBoolean shouldDisableAutoRead;
            readonly AtomicInteger channelReadCompleteCount;

            public ChannelHandlerContextConsumer(AtomicBoolean shouldDisableAutoRead, AtomicInteger channelReadCompleteCount = null)
            {
                this.shouldDisableAutoRead = shouldDisableAutoRead;
                this.channelReadCompleteCount = channelReadCompleteCount;
            }

            public void Accept(IChannelHandlerContext obj)
            {
                this.channelReadCompleteCount?.Increment();
                if (this.shouldDisableAutoRead.Value)
                {
                    obj.Channel.Configuration.AutoRead = false;
                }
            }
        }
        [Fact]
        public void EndOfStreamDoesNotDiscardData()
        {
            AtomicInteger numReads = new AtomicInteger(1);
            AtomicBoolean shouldDisableAutoRead = new AtomicBoolean(false);
            IConsumer<IChannelHandlerContext> ctxConsumer = new ChannelHandlerContextConsumer(shouldDisableAutoRead);
            LastInboundHandler inboundHandler = this.StreamActiveAndWriteHeaders(this.inboundStream, numReads, ctxConsumer);
            IHttp2StreamChannel childChannel = (IHttp2StreamChannel)inboundHandler.Channel;
            childChannel.Configuration.AutoRead = false;

            IHttp2DataFrame dataFrame1 = new DefaultHttp2DataFrame(BB("1")) { Stream = this.inboundStream };
            IHttp2DataFrame dataFrame2 = new DefaultHttp2DataFrame(BB("2")) { Stream = this.inboundStream };
            IHttp2DataFrame dataFrame3 = new DefaultHttp2DataFrame(BB("3")) { Stream = this.inboundStream };
            IHttp2DataFrame dataFrame4 = new DefaultHttp2DataFrame(BB("4")) { Stream = this.inboundStream };

            Assert.Equal(new DefaultHttp2HeadersFrame(request) { Stream = this.inboundStream }, inboundHandler.ReadInbound<IHttp2HeadersFrame>());

            // We want to simulate the parent channel calling channelRead and delay calling channelReadComplete.
            this.parentChannel.WriteOneInbound(new object());
            this.codec.OnHttp2Frame(dataFrame1);
            Assert.Equal(dataFrame1, inboundHandler.ReadInbound<IHttp2DataFrame>());

            // Deliver frames, and then a stream closed while read is inactive.
            this.codec.OnHttp2Frame(dataFrame2);
            this.codec.OnHttp2Frame(dataFrame3);
            this.codec.OnHttp2Frame(dataFrame4);

            shouldDisableAutoRead.Value = true;
            childChannel.Configuration.AutoRead = true;
            numReads.Value = 1;

            this.inboundStream.state = Http2StreamState.Closed;
            this.codec.OnHttp2StreamStateChanged(this.inboundStream);

            // Detecting EOS should flush all pending data regardless of read calls.
            Assert.Equal(dataFrame2, inboundHandler.ReadInbound<IHttp2DataFrame>());
            Assert.Equal(dataFrame3, inboundHandler.ReadInbound<IHttp2DataFrame>());
            Assert.Equal(dataFrame4, inboundHandler.ReadInbound<IHttp2DataFrame>());
            Assert.Null(inboundHandler.ReadInbound<object>());

            // Now we want to call channelReadComplete and simulate the end of the read loop.
            this.parentChannel.FlushInbound();

            childChannel.CloseCompletion.GetAwaiter().GetResult();

            dataFrame1.Release();
            dataFrame2.Release();
            dataFrame3.Release();
            dataFrame4.Release();
        }

        [Fact]
        public void ChildQueueIsDrainedAndNewDataIsDispatchedInParentReadLoopAutoRead()
        {
            AtomicInteger numReads = new AtomicInteger(1);
            AtomicInteger channelReadCompleteCount = new AtomicInteger(0);
            AtomicBoolean shouldDisableAutoRead = new AtomicBoolean(false);
            IConsumer<IChannelHandlerContext> ctxConsumer = new ChannelHandlerContextConsumer(shouldDisableAutoRead, channelReadCompleteCount);
            LastInboundHandler inboundHandler = this.StreamActiveAndWriteHeaders(this.inboundStream, numReads, ctxConsumer);
            IHttp2StreamChannel childChannel = (IHttp2StreamChannel)inboundHandler.Channel;
            childChannel.Configuration.AutoRead = false;

            IHttp2DataFrame dataFrame1 = new DefaultHttp2DataFrame(BB("1")) { Stream = this.inboundStream };
            IHttp2DataFrame dataFrame2 = new DefaultHttp2DataFrame(BB("2")) { Stream = this.inboundStream };
            IHttp2DataFrame dataFrame3 = new DefaultHttp2DataFrame(BB("3")) { Stream = this.inboundStream };
            IHttp2DataFrame dataFrame4 = new DefaultHttp2DataFrame(BB("4")) { Stream = this.inboundStream };

            Assert.Equal(new DefaultHttp2HeadersFrame(request) { Stream = this.inboundStream }, inboundHandler.ReadInbound<IHttp2HeadersFrame>());

            // We want to simulate the parent channel calling channelRead and delay calling channelReadComplete.
            this.parentChannel.WriteOneInbound(new object());
            this.codec.OnHttp2Frame(dataFrame1);
            Assert.Equal(dataFrame1, inboundHandler.ReadInbound<IHttp2DataFrame>());

            // We want one item to be in the queue, and allow the numReads to be larger than 1. This will ensure that
            // when beginRead() is called the child channel is added to the readPending queue of the parent channel.
            this.codec.OnHttp2Frame(dataFrame2);

            numReads.Value = 10;
            shouldDisableAutoRead.Value = true;
            childChannel.Configuration.AutoRead = true;

            this.codec.OnHttp2Frame(dataFrame3);
            this.codec.OnHttp2Frame(dataFrame4);

            // Detecting EOS should flush all pending data regardless of read calls.
            Assert.Equal(dataFrame2, inboundHandler.ReadInbound<IHttp2DataFrame>());
            Assert.Equal(dataFrame3, inboundHandler.ReadInbound<IHttp2DataFrame>());
            Assert.Equal(dataFrame4, inboundHandler.ReadInbound<IHttp2DataFrame>());
            Assert.Null(inboundHandler.ReadInbound<object>());

            // Now we want to call channelReadComplete and simulate the end of the read loop.
            this.parentChannel.FlushInbound();

            // 3 = 1 for initialization + 1 for read when auto read was off + 1 for when auto read was back on
            Assert.Equal(3, channelReadCompleteCount.Value);

            dataFrame1.Release();
            dataFrame2.Release();
            dataFrame3.Release();
            dataFrame4.Release();
        }

        [Fact]
        public void ChildQueueIsDrainedAndNewDataIsDispatchedInParentReadLoopNoAutoRead()
        {
            AtomicInteger numReads = new AtomicInteger(1);
            AtomicInteger channelReadCompleteCount = new AtomicInteger(0);
            AtomicBoolean shouldDisableAutoRead = new AtomicBoolean(false);
            IConsumer<IChannelHandlerContext> ctxConsumer = new ChannelHandlerContextConsumer(shouldDisableAutoRead, channelReadCompleteCount);
            LastInboundHandler inboundHandler = this.StreamActiveAndWriteHeaders(this.inboundStream, numReads, ctxConsumer);
            IHttp2StreamChannel childChannel = (IHttp2StreamChannel)inboundHandler.Channel;
            childChannel.Configuration.AutoRead = false;

            IHttp2DataFrame dataFrame1 = new DefaultHttp2DataFrame(BB("1")) { Stream = this.inboundStream };
            IHttp2DataFrame dataFrame2 = new DefaultHttp2DataFrame(BB("2")) { Stream = this.inboundStream };
            IHttp2DataFrame dataFrame3 = new DefaultHttp2DataFrame(BB("3")) { Stream = this.inboundStream };
            IHttp2DataFrame dataFrame4 = new DefaultHttp2DataFrame(BB("4")) { Stream = this.inboundStream };

            Assert.Equal(new DefaultHttp2HeadersFrame(request) { Stream = this.inboundStream }, inboundHandler.ReadInbound<IHttp2HeadersFrame>());

            // We want to simulate the parent channel calling channelRead and delay calling channelReadComplete.
            this.parentChannel.WriteOneInbound(new object());
            this.codec.OnHttp2Frame(dataFrame1);
            Assert.Equal(dataFrame1, inboundHandler.ReadInbound<IHttp2DataFrame>());

            // We want one item to be in the queue, and allow the numReads to be larger than 1. This will ensure that
            // when beginRead() is called the child channel is added to the readPending queue of the parent channel.
            this.codec.OnHttp2Frame(dataFrame2);

            numReads.Value = 2;
            childChannel.Read();
            Assert.Equal(dataFrame2, inboundHandler.ReadInbound<IHttp2DataFrame>());
            Assert.Null(inboundHandler.ReadInbound<object>());

            // This is the second item that was read, this should be the last until we call read() again. This should also
            // notify of readComplete().
            this.codec.OnHttp2Frame(dataFrame3);
            Assert.Equal(dataFrame3, inboundHandler.ReadInbound<IHttp2DataFrame>());

            this.codec.OnHttp2Frame(dataFrame4);
            Assert.Null(inboundHandler.ReadInbound<object>());

            childChannel.Read();
            Assert.Equal(dataFrame4, inboundHandler.ReadInbound<IHttp2DataFrame>());
            Assert.Null(inboundHandler.ReadInbound<object>());

            // Now we want to call channelReadComplete and simulate the end of the read loop.
            this.parentChannel.FlushInbound();

            // 3 = 1 for initialization + 1 for first read of 2 items + 1 for second read of 2 items +
            // 1 for parent channel readComplete
            Assert.Equal(4, channelReadCompleteCount.Value);

            dataFrame1.Release();
            dataFrame2.Release();
            dataFrame3.Release();
            dataFrame4.Release();
        }

        private IHttp2StreamChannel NewOutboundStream()
        {
            return new Http2StreamChannelBootstrap(this.parentChannel).Handler(this.childChannelInitializer)
                    .OpenAsync().GetAwaiter().GetResult();
        }


        private LastInboundHandler StreamActiveAndWriteHeaders(IHttp2FrameStream stream)
        {
            return StreamActiveAndWriteHeaders(stream, null, NoopConsumer<IChannelHandlerContext>.Instance);
        }

        private LastInboundHandler StreamActiveAndWriteHeaders(IHttp2FrameStream stream,
                                                               AtomicInteger maxReads,
                                                               IConsumer<IChannelHandlerContext> contextConsumer)
        {
            LastInboundHandler inboundHandler = new LastInboundHandler(contextConsumer);
            this.childChannelInitializer.handler = inboundHandler;
            this.childChannelInitializer.maxReads = maxReads;
            Assert.False(inboundHandler.IsChannelActive);
            ((TestableHttp2MultiplexCodec.Stream)stream).state = Http2StreamState.Open;
            this.codec.OnHttp2StreamStateChanged(stream);
            this.codec.OnHttp2Frame(new DefaultHttp2HeadersFrame(request) { Stream = stream });
            this.codec.OnChannelReadComplete();
            Assert.True(inboundHandler.IsChannelActive);

            return inboundHandler;
        }

        private static void VerifyFramesMultiplexedToCorrectChannel(IHttp2FrameStream stream,
                                                                    LastInboundHandler inboundHandler,
                                                                    int numFrames)
        {
            for (int i = 0; i < numFrames; i++)
            {
                IHttp2StreamFrame frame = inboundHandler.ReadInbound<IHttp2StreamFrame>();
                Assert.NotNull(frame);
                Assert.Equal(stream, frame.Stream);
                ReferenceCountUtil.Release(frame);
            }
            Assert.Null(inboundHandler.ReadInbound<object>());
        }

        private static IByteBuffer BB(string s)
        {
            return ByteBufferUtil.WriteUtf8(UnpooledByteBufferAllocator.Default, s);
        }

        /**
         * Simulates the frame codec, in first assigning an identifier and the completing the write promise.
         */
        private IHttp2FrameStream ReadOutboundHeadersAndAssignId()
        {
            // Only peek at the frame, so to not complete the promise of the write. We need to first
            // assign a stream identifier, as the frame codec would do.
            IHttp2HeadersFrame headersFrame = (IHttp2HeadersFrame)this.parentChannel.OutboundMessages.Peek();
            Assert.NotNull(headersFrame);
            Assert.NotNull(headersFrame.Stream);
            Assert.False(Http2CodecUtil.IsStreamIdValid(headersFrame.Stream.Id));
            TestableHttp2MultiplexCodec.Stream frameStream = (TestableHttp2MultiplexCodec.Stream)headersFrame.Stream;
            frameStream.Id = this.outboundStream.Id;
            // Create the stream in the Http2Connection.
            try
            {
                IHttp2Stream stream = this.codec.Connection.Local.CreateStream(
                        headersFrame.Stream.Id, headersFrame.IsEndStream);
                frameStream.InternalStream = stream;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to create a stream", ex);
            }

            // Now read it and complete the write promise.
            Assert.Same(headersFrame, this.parentChannel.ReadOutbound<IHttp2HeadersFrame>());

            return headersFrame.Stream;
        }

        /**
         * This class removes the bits that would transform the frames to bytes and so make it easier to test the actual
         * special handling of the codec.
         */
        class TestableHttp2MultiplexCodec : Http2MultiplexCodec
        {
            readonly Http2MultiplexCodecTest self;

            public TestableHttp2MultiplexCodec(IHttp2ConnectionEncoder encoder, IHttp2ConnectionDecoder decoder,
                Http2Settings initialSettings, IChannelHandler inboundStreamHandler, Http2MultiplexCodecTest self)
                : base(encoder, decoder, initialSettings, inboundStreamHandler, null)
            {
                this.self = self;
            }

            public void OnHttp2Frame(IHttp2Frame frame)
            {
                this.OnHttp2Frame(this.InternalContext, frame);
            }

            public void OnChannelReadComplete()
            {
                this.OnChannelReadComplete(this.InternalContext);
            }

            public void OnHttp2StreamStateChanged(IHttp2FrameStream stream)
            {
                this.OnHttp2StreamStateChanged(this.InternalContext, stream);
            }

            public void OnHttp2FrameStreamException(Http2FrameStreamException cause)
            {
                this.OnHttp2FrameStreamException(this.InternalContext, cause);
            }

            public void OnHttp2StreamWritabilityChanged(IHttp2FrameStream stream, bool writable)
            {
                this.OnHttp2StreamWritabilityChanged(this.InternalContext, stream, writable);
            }

            internal override bool OnBytesConsumed(IChannelHandlerContext ctx, IHttp2FrameStream stream, int bytes)
            {
                this.self.writer(new DefaultHttp2WindowUpdateFrame(bytes) { Stream = stream }, ctx.NewPromise());
                return true;
            }

            public override void Write(IChannelHandlerContext ctx, object msg, IPromise promise)
            {
                this.self.writer(msg, promise);
            }

            internal override void Flush0(IChannelHandlerContext ctx)
            {
                // Do nothing
            }

            internal override DefaultHttp2FrameStream NewStream()
            {
                return new Stream();
            }

            public class Stream : Http2MultiplexCodecStream
            {
                public Http2StreamState state = Http2StreamState.Idle;
                public int id = -1;

                public override int Id
                {
                    get => this.id;
                    set => this.id = value;
                }

                public override Http2StreamState State => this.state;
            }
        }

        class TestableHttp2MultiplexCodecBuilder : Http2MultiplexCodecBuilder
        {
            readonly Http2MultiplexCodecTest self;

            public TestableHttp2MultiplexCodecBuilder(bool server, IChannelHandler childHandler, Http2MultiplexCodecTest self)
                : base(server, childHandler)
            {
                this.self = self;
            }

            protected override Http2MultiplexCodec Build(IHttp2ConnectionDecoder decoder, IHttp2ConnectionEncoder encoder, Http2Settings initialSettings)
            {
                return new TestableHttp2MultiplexCodec(encoder, decoder, initialSettings, this.childHandler, this.self);
            }
        }
    }
}
