
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Tests.Common;
    using DotNetty.Transport.Channels;
    using Moq;
    using Xunit;

    public class DefaultHttp2ConnectionDecoderTest
    {
        private const int STREAM_ID = 3;
        private const int PUSH_STREAM_ID = 2;
        private const int STREAM_DEPENDENCY_ID = 5;
        private const int STATE_RECV_HEADERS = 1;
        private const int STATE_RECV_TRAILERS = 1 << 1;

        private IHttp2ConnectionDecoder decoder;
        private IPromise promise;

        private Mock<IHttp2Connection> connection;
        private Mock<IHttp2ConnectionEndpoint<IHttp2RemoteFlowController>> remote;
        private Mock<IHttp2ConnectionEndpoint<IHttp2LocalFlowController>> local;
        private Mock<IHttp2LocalFlowController> localFlow;
        private Mock<IHttp2RemoteFlowController> remoteFlow;
        private Mock<IChannelHandlerContext> ctx;
        private Mock<IChannel> channel;
        //private Mock<ChannelFuture> future;
        private Mock<IHttp2Stream> stream;
        private Mock<IHttp2Stream> pushStream;
        private Mock<IHttp2FrameListener> listener;
        private Mock<IHttp2FrameReader> reader;
        private Mock<IHttp2FrameWriter> writer;
        private Mock<IHttp2ConnectionEncoder> encoder;
        private Mock<IHttp2LifecycleManager> lifecycleManager;

        public DefaultHttp2ConnectionDecoderTest()
        {
            this.connection = new Mock<IHttp2Connection>();
            this.remote = new Mock<IHttp2ConnectionEndpoint<IHttp2RemoteFlowController>>();
            this.local = new Mock<IHttp2ConnectionEndpoint<IHttp2LocalFlowController>>();
            this.localFlow = new Mock<IHttp2LocalFlowController>();
            this.remoteFlow = new Mock<IHttp2RemoteFlowController>();
            this.ctx = new Mock<IChannelHandlerContext>();
            this.channel = new Mock<IChannel>();
            this.stream = new Mock<IHttp2Stream>();
            this.pushStream = new Mock<IHttp2Stream>();
            this.listener = new Mock<IHttp2FrameListener>();
            this.reader = new Mock<IHttp2FrameReader>();
            this.writer = new Mock<IHttp2FrameWriter>();
            this.encoder = new Mock<IHttp2ConnectionEncoder>();
            this.lifecycleManager = new Mock<IHttp2LifecycleManager>();

            this.promise = new TaskCompletionSource();
            var headersReceivedState = new AtomicInteger();

            this.channel.Setup(x => x.Active).Returns(true);
            this.stream.Setup(x => x.Id).Returns(STREAM_ID);
            this.stream.Setup(x => x.State).Returns(Http2StreamState.Open);
            this.stream.Setup(x => x.Open(It.IsAny<bool>())).Returns(this.stream.Object);
            this.pushStream.Setup(x => x.Id).Returns(PUSH_STREAM_ID);
            this.stream.Setup(x => x.IsHeadersReceived).Returns(() => (headersReceivedState.Value & STATE_RECV_HEADERS) != 0);
            this.stream.Setup(x => x.IsTrailersReceived).Returns(() => (headersReceivedState.Value & STATE_RECV_TRAILERS) != 0);
            this.stream.Setup(x => x.HeadersReceived(It.IsAny<bool>()))
                       .Returns<bool>(isInformational =>
                       {
                           if (isInformational)
                           {
                               return this.stream.Object;
                           }
                           for (; ; )
                           {
                               int current = headersReceivedState.Value;
                               int next = current;
                               if ((current & STATE_RECV_HEADERS) != 0)
                               {
                                   if ((current & STATE_RECV_TRAILERS) != 0)
                                   {
                                       throw new InvalidOperationException("already sent headers!");
                                   }
                                   next |= STATE_RECV_TRAILERS;
                               }
                               else
                               {
                                   next |= STATE_RECV_HEADERS;
                               }
                               if (headersReceivedState.CompareAndSet(current, next))
                               {
                                   break;
                               }
                           }
                           return this.stream.Object;
                       });
            this.connection.Setup(x => x.ForEachActiveStream(It.IsAny<IHttp2StreamVisitor>()))
                           .Returns<IHttp2StreamVisitor>(visitor =>
                           {
                               if (!visitor.Visit(this.stream.Object))
                               {
                                   return this.stream.Object;
                               }
                               return null;
                           });
            this.connection.Setup(x => x.ForEachActiveStream(It.IsAny<Func<IHttp2Stream, bool>>()))
                           .Returns<Func<IHttp2Stream, bool>>(visitor =>
                           {
                               if (!visitor(this.stream.Object))
                               {
                                   return this.stream.Object;
                               }
                               return null;
                           });
            this.connection.Setup(x => x.Stream(It.Is<int>(v => v == STREAM_ID))).Returns(this.stream.Object);
            this.connection.Setup(x => x.StreamMayHaveExisted(It.Is<int>(v => v == STREAM_ID))).Returns(true);
            this.connection.Setup(x => x.Local).Returns(this.local.Object);
            this.local.Setup(x => x.FlowController).Returns(this.localFlow.Object);
            this.encoder.Setup(x => x.FlowController).Returns(this.remoteFlow.Object);
            this.encoder.Setup(x => x.FrameWriter).Returns(this.writer.Object);
            this.connection.Setup(x => x.Remote).Returns(this.remote.Object);
            this.local
                .Setup(x => x.ReservePushStream(
                    It.Is<int>(v => v == PUSH_STREAM_ID),
                    It.Is<IHttp2Stream>(v => v == this.stream.Object)))
                .Returns(this.pushStream.Object);
            this.remote
                .Setup(x => x.ReservePushStream(
                    It.Is<int>(v => v == PUSH_STREAM_ID),
                    It.Is<IHttp2Stream>(v => v == this.stream.Object)))
                .Returns(this.pushStream.Object);
            this.ctx.Setup(x => x.Allocator).Returns(UnpooledByteBufferAllocator.Default);
            this.ctx.Setup(x => x.Channel).Returns(this.channel.Object);
            this.ctx.Setup(x => x.NewPromise()).Returns(this.promise);
            this.ctx.Setup(x => x.WriteAsync(It.IsAny<object>())).Returns(TaskUtil.Completed);

            this.decoder = new DefaultHttp2ConnectionDecoder(this.connection.Object, this.encoder.Object, this.reader.Object);
            this.decoder.LifecycleManager(this.lifecycleManager.Object);
            this.decoder.FrameListener = this.listener.Object;

            // Simulate receiving the initial settings from the remote endpoint.
            this.Decode().OnSettingsRead(this.ctx.Object, new Http2Settings());
            this.listener
                .Verify(x => x.OnSettingsRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<Http2Settings>(v => v.SequenceEqual(new Http2Settings()))));
            Assert.True(this.decoder.PrefaceReceived);
            this.encoder
                .Verify(x => x.WriteSettingsAckAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<IPromise>(v => v == this.promise)));

            // Simulate receiving the SETTINGS ACK for the initial settings.
            this.Decode().OnSettingsAckRead(this.ctx.Object);

            // Disallow any further flushes now that settings ACK has been sent
            this.ctx.Setup(x => x.Flush()).Throws(new Exception("forbidden"));
        }

        [Fact]
        public void DataReadAfterGoAwaySentShouldApplyFlowControl()
        {
            this.MockGoAwaySent();

            var data = DummyData();
            int padding = 10;
            int processedBytes = data.ReadableBytes + padding;
            this.MockFlowControl(processedBytes);
            try
            {
                this.Decode().OnDataRead(this.ctx.Object, STREAM_ID, data, padding, true);

                this.localFlow
                    .Verify(x => x.ReceiveFlowControlledFrame(
                        It.Is<IHttp2Stream>(v => v == this.stream.Object),
                        It.Is<IByteBuffer>(v => v.Equals(data)),
                        It.Is<int>(v => v == padding),
                        It.Is<bool>(v => v == true)));
                this.localFlow
                    .Verify(x => x.ConsumeBytes(
                        It.Is<IHttp2Stream>(v => v == this.stream.Object),
                        It.Is<int>(v => v == processedBytes)));

                // Verify that the event was absorbed and not propagated to the observer.
                this.listener
                    .Verify(x => x.OnDataRead(
                        It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                        It.IsAny<int>(),
                        It.IsAny<IByteBuffer>(),
                        It.IsAny<int>(),
                        It.IsAny<bool>()), Times.Never());
            }
            finally
            {
                data.Release();
            }
        }

        [Fact]
        public void DataReadAfterGoAwaySentShouldAllowFramesForStreamCreatedByLocalEndpoint()
        {
            this.MockGoAwaySentShouldAllowFramesForStreamCreatedByLocalEndpoint();

            var data = DummyData();
            int padding = 10;
            int processedBytes = data.ReadableBytes + padding;
            this.MockFlowControl(processedBytes);
            try
            {
                this.Decode().OnDataRead(this.ctx.Object, STREAM_ID, data, padding, true);
                this.localFlow
                    .Verify(x => x.ReceiveFlowControlledFrame(
                        It.Is<IHttp2Stream>(v => v == this.stream.Object),
                        It.Is<IByteBuffer>(v => v.Equals(data)),
                        It.Is<int>(v => v == padding),
                        It.Is<bool>(v => v == true)));
                this.localFlow
                    .Verify(x => x.ConsumeBytes(
                        It.Is<IHttp2Stream>(v => v == this.stream.Object),
                        It.Is<int>(v => v == processedBytes)));

                // Verify that the event was absorbed and not propagated to the observer.
                this.listener
                    .Verify(x => x.OnDataRead(
                        It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                        It.IsAny<int>(),
                        It.IsAny<IByteBuffer>(),
                        It.IsAny<int>(),
                        It.IsAny<bool>()));
            }
            finally
            {
                data.Release();
            }
        }

        [Fact]
        public void DataReadForUnknownStreamShouldApplyFlowControlAndFail()
        {
            this.connection.Setup(x => x.StreamMayHaveExisted(It.Is<int>(v => v == STREAM_ID))).Returns(true);
            this.connection.Setup(x => x.Stream(It.Is<int>(v => v == STREAM_ID))).Returns(default(IHttp2Stream));
            var data = DummyData();
            int padding = 10;
            int processedBytes = data.ReadableBytes + padding;
            try
            {
                this.Decode().OnDataRead(this.ctx.Object, STREAM_ID, data, padding, true);
            }
            catch (Exception exc)
            {
                Assert.True(exc is StreamException);
            }
            finally
            {
                try
                {
                    this.localFlow
                        .Verify(x => x.ReceiveFlowControlledFrame(
                            It.Is<IHttp2Stream>(v => v == null),
                            It.Is<IByteBuffer>(v => v.Equals(data)),
                            It.Is<int>(v => v == padding),
                            It.Is<bool>(v => v == true)));
                    this.localFlow
                        .Verify(x => x.ConsumeBytes(
                            It.Is<IHttp2Stream>(v => v == null),
                            It.Is<int>(v => v == processedBytes)));
                    this.localFlow.Verify(x => x.FrameWriter(It.IsAny<IHttp2FrameWriter>()));
                    this.localFlow.VerifyNoOtherCalls();
                    this.listener
                        .Verify(x => x.OnDataRead(
                            It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                            It.IsAny<int>(),
                            It.IsAny<IByteBuffer>(),
                            It.IsAny<int>(),
                            It.IsAny<bool>()), Times.Never());
                }
                finally
                {
                    data.Release();
                }
            }
        }

        [Fact]
        public void DataReadForUnknownStreamThatCouldntExistFail()
        {
            this.connection.Setup(x => x.StreamMayHaveExisted(It.Is<int>(v => v == STREAM_ID))).Returns(false);
            this.connection.Setup(x => x.Stream(It.Is<int>(v => v == STREAM_ID))).Returns(default(IHttp2Stream));
            var data = DummyData();
            int padding = 10;
            int processedBytes = data.ReadableBytes + padding;
            try
            {
                this.Decode().OnDataRead(this.ctx.Object, STREAM_ID, data, padding, true);
            }
            catch (Exception exc)
            {
                Assert.False(exc is StreamException);
                Assert.True(exc is Http2Exception);
            }
            finally
            {
                try
                {
                    this.localFlow
                        .Verify(x => x.ReceiveFlowControlledFrame(
                            It.Is<IHttp2Stream>(v => v == null),
                            It.Is<IByteBuffer>(v => v.Equals(data)),
                            It.Is<int>(v => v == padding),
                            It.Is<bool>(v => v == true)));
                    this.localFlow
                        .Verify(x => x.ConsumeBytes(
                            It.Is<IHttp2Stream>(v => v == null),
                            It.Is<int>(v => v == processedBytes)));
                    this.localFlow.Verify(x => x.FrameWriter(It.IsAny<IHttp2FrameWriter>()));
                    this.localFlow.VerifyNoOtherCalls();
                    this.listener
                        .Verify(x => x.OnDataRead(
                            It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                            It.IsAny<int>(),
                            It.IsAny<IByteBuffer>(),
                            It.IsAny<int>(),
                            It.IsAny<bool>()), Times.Never());
                }
                finally
                {
                    data.Release();
                }
            }
        }

        [Fact]
        public void DataReadForUnknownStreamShouldApplyFlowControl()
        {
            this.connection.Setup(x => x.Stream(It.Is<int>(v => v == STREAM_ID))).Returns(default(IHttp2Stream));
            var data = DummyData();
            int padding = 10;
            int processedBytes = data.ReadableBytes + padding;
            try
            {
                try
                {
                    this.Decode().OnDataRead(this.ctx.Object, STREAM_ID, data, padding, true);
                    Assert.False(true);
                }
                catch (Http2Exception)
                {
                    this.localFlow
                        .Verify(x => x.ReceiveFlowControlledFrame(
                            It.Is<IHttp2Stream>(v => v == null),
                            It.Is<IByteBuffer>(v => v.Equals(data)),
                            It.Is<int>(v => v == padding),
                            It.Is<bool>(v => v == true)));
                    this.localFlow
                        .Verify(x => x.ConsumeBytes(
                            It.Is<IHttp2Stream>(v => v == null),
                            It.Is<int>(v => v == processedBytes)));
                    this.localFlow.Verify(x => x.FrameWriter(It.IsAny<IHttp2FrameWriter>()));
                    this.localFlow.VerifyNoOtherCalls();

                    // Verify that the event was absorbed and not propagated to the observer.
                    this.listener
                        .Verify(x => x.OnDataRead(
                            It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                            It.IsAny<int>(),
                            It.IsAny<IByteBuffer>(),
                            It.IsAny<int>(),
                            It.IsAny<bool>()), Times.Never());
                }
            }
            finally
            {
                data.Release();
            }
        }

        [Fact]
        public void EmptyDataFrameShouldApplyFlowControl()
        {
            var data = Unpooled.Empty;
            int padding = 0;
            this.MockFlowControl(0);
            try
            {
                this.Decode().OnDataRead(this.ctx.Object, STREAM_ID, data, padding, true);
                this.localFlow
                    .Verify(x => x.ReceiveFlowControlledFrame(
                        It.Is<IHttp2Stream>(v => v == this.stream.Object),
                        It.Is<IByteBuffer>(v => v.Equals(data)),
                        It.Is<int>(v => v == padding),
                        It.Is<bool>(v => v == true)));

                // Now we ignore the empty bytes inside consumeBytes method, so it will be called once.
                this.localFlow
                    .Verify(x => x.ConsumeBytes(
                        It.Is<IHttp2Stream>(v => v == this.stream.Object),
                        It.Is<int>(v => v == 0)));

                // Verify that the empty data event was propagated to the observer.
                this.listener
                    .Verify(x => x.OnDataRead(
                        It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                        It.Is<int>(v => v == STREAM_ID),
                        It.Is<IByteBuffer>(v => v.Equals(data)),
                        It.Is<int>(v => v == padding),
                        It.Is<bool>(v => v == true)));
            }
            finally
            {
                data.Release();
            }
        }

        [Fact]
        public void DataReadForStreamInInvalidStateShouldThrow()
        {
            // Throw an exception when checking stream state.
            this.stream.Setup(x => x.State).Returns(Http2StreamState.Closed);
            IByteBuffer data = DummyData();
            try
            {
                this.Decode().OnDataRead(this.ctx.Object, STREAM_ID, data, 10, true);
            }
            catch (Exception exc)
            {
                Assert.IsType<StreamException>(exc);
            }
            finally
            {
                data.Release();
            }
        }

        [Fact]
        public void DataReadAfterGoAwaySentForStreamInInvalidStateShouldIgnore()
        {
            // Throw an exception when checking stream state.
            this.stream.Setup(x => x.State).Returns(Http2StreamState.Closed);
            this.MockGoAwaySent();
            IByteBuffer data = DummyData();
            try
            {
                this.Decode().OnDataRead(this.ctx.Object, STREAM_ID, data, 10, true);
                this.localFlow
                    .Verify(x => x.ReceiveFlowControlledFrame(
                        It.Is<IHttp2Stream>(v => v == this.stream.Object),
                        It.Is<IByteBuffer>(v => v.Equals(data)),
                        It.Is<int>(v => v == 10),
                        It.Is<bool>(v => v == true)));
                this.listener
                    .Verify(x => x.OnDataRead(
                        It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                        It.IsAny<int>(),
                        It.IsAny<IByteBuffer>(),
                        It.IsAny<int>(),
                        It.IsAny<bool>()), Times.Never());
            }
            finally
            {
                data.Release();
            }
        }

        [Fact]
        public void DataReadAfterGoAwaySentOnUnknownStreamShouldIgnore()
        {
            // Throw an exception when checking stream state.
            this.connection.Setup(x => x.Stream(It.Is<int>(v => v == STREAM_ID))).Returns(default(IHttp2Stream));
            this.MockGoAwaySent();
            IByteBuffer data = DummyData();
            try
            {
                this.Decode().OnDataRead(this.ctx.Object, STREAM_ID, data, 10, true);
                this.localFlow
                    .Verify(x => x.ReceiveFlowControlledFrame(
                        It.Is<IHttp2Stream>(v => v == null),
                        It.Is<IByteBuffer>(v => v.Equals(data)),
                        It.Is<int>(v => v == 10),
                        It.Is<bool>(v => v == true)));
                this.listener
                    .Verify(x => x.OnDataRead(
                        It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                        It.IsAny<int>(),
                        It.IsAny<IByteBuffer>(),
                        It.IsAny<int>(),
                        It.IsAny<bool>()), Times.Never());
            }
            finally
            {
                data.Release();
            }
        }

        [Fact]
        public void DataReadAfterRstStreamForStreamInInvalidStateShouldIgnore()
        {
            // Throw an exception when checking stream state.
            this.stream.Setup(x => x.State).Returns(Http2StreamState.Closed);
            this.stream.Setup(x => x.IsResetSent).Returns(true);
            IByteBuffer data = DummyData();
            try
            {
                this.Decode().OnDataRead(this.ctx.Object, STREAM_ID, data, 10, true);
                this.localFlow
                    .Verify(x => x.ReceiveFlowControlledFrame(
                        It.Is<IHttp2Stream>(v => v == this.stream.Object),
                        It.Is<IByteBuffer>(v => v.Equals(data)),
                        It.Is<int>(v => v == 10),
                        It.Is<bool>(v => v == true)));
                this.listener
                    .Verify(x => x.OnDataRead(
                        It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                        It.IsAny<int>(),
                        It.IsAny<IByteBuffer>(),
                        It.IsAny<int>(),
                        It.IsAny<bool>()), Times.Never());
            }
            finally
            {
                data.Release();
            }
        }

        [Fact]
        public void DataReadWithEndOfStreamShouldcloseStreamRemote()
        {
            IByteBuffer data = DummyData();
            try
            {
                this.Decode().OnDataRead(this.ctx.Object, STREAM_ID, data, 10, true);
                this.localFlow
                    .Verify(x => x.ReceiveFlowControlledFrame(
                        It.Is<IHttp2Stream>(v => v == this.stream.Object),
                        It.Is<IByteBuffer>(v => v.Equals(data)),
                        It.Is<int>(v => v == 10),
                        It.Is<bool>(v => v == true)));
                this.lifecycleManager
                    .Verify(x => x.CloseStreamRemote(
                        It.Is<IHttp2Stream>(v => v == this.stream.Object),
                        It.Is<Task>(v => v == TaskUtil.Completed)));
                this.listener
                    .Verify(x => x.OnDataRead(
                        It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                        It.Is<int>(v => v == STREAM_ID),
                        It.Is<IByteBuffer>(v => v.Equals(data)),
                        It.Is<int>(v => v == 10),
                        It.Is<bool>(v => v == true)));
            }
            finally
            {
                data.Release();
            }
        }

        [Fact]
        public void ErrorDuringDeliveryShouldReturnCorrectNumberOfBytes()
        {
            IByteBuffer data = DummyData();
            int padding = 10;
            AtomicInteger unprocessed = new AtomicInteger(data.ReadableBytes + padding);
            this.localFlow
                .Setup(x => x.UnconsumedBytes(It.Is<IHttp2Stream>(v => v == this.stream.Object)))
                .Returns<IHttp2Stream>(s => unprocessed.Value);
            this.localFlow
                .Setup(x => x.ConsumeBytes(It.Is<IHttp2Stream>(v => v == this.stream.Object), It.IsAny<int>()))
                .Returns<IHttp2Stream, int>((s, delta) =>
                {
                    var newValue = unprocessed.AddAndGet(-delta);
                    if (newValue < 0)
                    {
                        throw new Http2RuntimeException("Returned too many bytes");
                    }
                    return false;
                });
            // When the listener callback is called, process a few bytes and then throw.
            this.listener
                .Setup(x => x.OnDataRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<IByteBuffer>(),
                    It.Is<int>(v => v == 10),
                    It.Is<bool>(v => v == true)))
                .Returns(() =>
                    {
                        this.localFlow.Object.ConsumeBytes(this.stream.Object, 4);
                        throw new Http2RuntimeException("Fake Exception");
                    });
            try
            {
                this.Decode().OnDataRead(this.ctx.Object, STREAM_ID, data, padding, true);
                Assert.False(true, "Expected exception");
            }
            catch (Http2RuntimeException)
            {
                this.localFlow
                    .Verify(x => x.ReceiveFlowControlledFrame(
                        It.Is<IHttp2Stream>(v => v == this.stream.Object),
                        It.Is<IByteBuffer>(v => v.Equals(data)),
                        It.Is<int>(v => v == padding),
                        It.Is<bool>(v => v == true)));
                this.lifecycleManager
                    .Verify(x => x.CloseStreamRemote(
                        It.Is<IHttp2Stream>(v => v == this.stream.Object),
                        It.Is<Task>(v => v == TaskUtil.Completed)));
                this.listener
                    .Verify(x => x.OnDataRead(
                        It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                        It.Is<int>(v => v == STREAM_ID),
                        It.Is<IByteBuffer>(v => v.Equals(data)),
                        It.Is<int>(v => v == padding),
                        It.Is<bool>(v => v == true)));
                Assert.Equal(0, this.localFlow.Object.UnconsumedBytes(this.stream.Object));
            }
            finally
            {
                data.Release();
            }
        }

        [Fact]
        public void HeadersReadForUnknownStreamShouldThrow()
        {
            this.connection.Setup(x => x.Stream(It.Is<int>(v => v == STREAM_ID))).Returns(default(IHttp2Stream));
            Assert.Throws<StreamException>(() => this.Decode().OnHeadersRead(this.ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, false));
        }

        [Fact]
        public void HeadersReadForStreamThatAlreadySentResetShouldBeIgnored()
        {
            this.stream.Setup(x => x.IsResetSent).Returns(true);
            this.Decode().OnHeadersRead(this.ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, false);
            this.remote.Verify(
                x => x.CreateStream(
                    It.IsAny<int>(),
                    It.IsAny<bool>()),
                Times.Never());
            this.stream.Verify(x => x.Open(It.IsAny<bool>()), Times.Never());

            // Verify that the event was absorbed and not propagated to the observer.
            this.listener.Verify(
                x => x.OnHeadersRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<int>(),
                    It.IsAny<IHttp2Headers>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>()),
                Times.Never());
            this.remote.Verify(
                x => x.CreateStream(
                    It.IsAny<int>(),
                    It.IsAny<bool>()),
                Times.Never());
            this.stream.Verify(x => x.Open(It.IsAny<bool>()), Times.Never());
        }

        [Fact]
        public void HeadersReadForUnknownStreamAfterGoAwayShouldBeIgnored()
        {
            this.MockGoAwaySent();
            this.connection.Setup(x => x.Stream(It.Is<int>(v => v == STREAM_ID))).Returns(default(IHttp2Stream));
            this.Decode().OnHeadersRead(this.ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, false);
            this.remote.Verify(
                x => x.CreateStream(
                    It.IsAny<int>(),
                    It.IsAny<bool>()),
                Times.Never());
            this.stream.Verify(x => x.Open(It.IsAny<bool>()), Times.Never());

            // Verify that the event was absorbed and not propagated to the observer.
            this.listener.Verify(
                x => x.OnHeadersRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<int>(),
                    It.IsAny<IHttp2Headers>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>()),
                Times.Never());
            this.remote.Verify(
                x => x.CreateStream(
                    It.IsAny<int>(),
                    It.IsAny<bool>()),
                Times.Never());
            this.stream.Verify(x => x.Open(It.IsAny<bool>()), Times.Never());
        }

        [Fact]
        public void HeadersReadForUnknownStreamShouldCreateStream()
        {
            int streamId = 5;
            this.remote
                .Setup(x => x.CreateStream(
                    It.Is<int>(v => v == streamId),
                    It.IsAny<bool>()))
                .Returns(this.stream.Object);
            this.Decode().OnHeadersRead(this.ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, false);
            this.remote.Verify(
                x => x.CreateStream(
                    It.Is<int>(v => v == streamId),
                    It.Is<bool>(v => v == false)));
            this.listener.Verify(
                x => x.OnHeadersRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false)));
        }

        [Fact]
        public void HeadersReadForUnknownStreamShouldCreateHalfClosedStream()
        {
            int streamId = 5;
            this.remote
                .Setup(x => x.CreateStream(
                    It.Is<int>(v => v == streamId),
                    It.IsAny<bool>()))
                .Returns(this.stream.Object);
            this.Decode().OnHeadersRead(this.ctx.Object, streamId, EmptyHttp2Headers.Instance, 0, true);
            this.remote.Verify(
                x => x.CreateStream(
                    It.Is<int>(v => v == streamId),
                    It.Is<bool>(v => v == true)));
            this.listener.Verify(
                x => x.OnHeadersRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == streamId),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true)));
        }

        [Fact]
        public void HeadersReadForPromisedStreamShouldHalfOpenStream()
        {
            this.stream.Setup(x => x.State).Returns(Http2StreamState.ReservedRemote);
            this.Decode().OnHeadersRead(this.ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, false);
            this.stream.Verify(x => x.Open(It.Is<bool>(v => !v)));
            this.listener.Verify(
                x => x.OnHeadersRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false)));
        }

        [Fact]
        public void TrailersDoNotEndStreamThrows()
        {
            this.Decode().OnHeadersRead(this.ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, false);
            // Trailers must end the stream!
            Assert.Throws<StreamException>(() => this.Decode().OnHeadersRead(this.ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, false));
        }

        [Fact]
        public void TooManyHeadersEOSThrows()
        {
            Assert.Throws<StreamException>(() => this.TooManyHeaderThrows(true));
        }

        [Fact]
        public void TooManyHeadersNoEOSThrows()
        {
            Assert.Throws<StreamException>(() => this.TooManyHeaderThrows(false));
        }

        private void TooManyHeaderThrows(bool eos)
        {
            this.Decode().OnHeadersRead(this.ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, false);
            this.Decode().OnHeadersRead(this.ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, true);
            // We already received the trailers!
            this.Decode().OnHeadersRead(this.ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, eos);
        }

        private static IHttp2Headers InformationalHeaders()
        {
            return new DefaultHttp2Headers
            {
                Status = HttpResponseStatus.Continue.CodeAsText
            };
        }

        [Fact]
        public void InfoHeadersAndTrailersAllowed()
        {
            this.InfoHeadersAndTrailersAllowed0(true, 1);
        }

        [Fact]
        public void MultipleInfoHeadersAndTrailersAllowed()
        {
            this.InfoHeadersAndTrailersAllowed0(true, 10);
        }

        [Fact]
        public void InfoHeadersAndTrailersNoEOSThrows()
        {
            Assert.Throws<StreamException>(() => this.InfoHeadersAndTrailersAllowed0(false, 1));
        }

        [Fact]
        public void MultipleInfoHeadersAndTrailersNoEOSThrows()
        {
            Assert.Throws<StreamException>(() => this.InfoHeadersAndTrailersAllowed0(false, 10));
        }

        private void InfoHeadersAndTrailersAllowed0(bool eos, int infoHeaderCount)
        {
            for (int i = 0; i < infoHeaderCount; ++i)
            {
                this.Decode().OnHeadersRead(this.ctx.Object, STREAM_ID, InformationalHeaders(), 0, false);
            }
            this.Decode().OnHeadersRead(this.ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, false);
            this.Decode().OnHeadersRead(this.ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, eos);
        }

        [Fact]
        public void HeadersReadForPromisedStreamShouldCloseStream()
        {
            this.stream.Setup(x => x.State).Returns(Http2StreamState.ReservedRemote);
            this.Decode().OnHeadersRead(this.ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, 0, true);
            this.stream.Verify(x => x.Open(It.Is<bool>(v => v)));
            this.lifecycleManager
                .Verify(x => x.CloseStreamRemote(
                    It.Is<IHttp2Stream>(v => v == this.stream.Object),
                    It.Is<Task>(v => v == TaskUtil.Completed)));
            this.listener.Verify(
                x => x.OnHeadersRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == Http2CodecUtil.DefaultPriorityWeight),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true)));
        }

        [Fact]
        public void HeadersDependencyNotCreatedShouldCreateAndSucceed()
        {
            short weight = 1;
            this.Decode().OnHeadersRead(this.ctx.Object, STREAM_ID, EmptyHttp2Headers.Instance, STREAM_DEPENDENCY_ID,
                    weight, true, 0, true);
            this.listener.Verify(
                x => x.OnHeadersRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == STREAM_DEPENDENCY_ID),
                    It.Is<short>(v => v == weight),
                    It.Is<bool>(v => v == true),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true)));

            this.remoteFlow.Verify(
                x => x.UpdateDependencyTree(
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<int>(v => v == STREAM_DEPENDENCY_ID),
                    It.Is<short>(v => v == weight),
                    It.Is<bool>(v => v == true)));
            this.lifecycleManager
                .Verify(x => x.CloseStreamRemote(
                    It.Is<IHttp2Stream>(v => v == this.stream.Object),
                    It.IsAny<Task>()));
        }

        [Fact]
        public void PushPromiseReadAfterGoAwaySentShouldBeIgnored()
        {
            this.MockGoAwaySent();
            this.Decode().OnPushPromiseRead(this.ctx.Object, STREAM_ID, PUSH_STREAM_ID, EmptyHttp2Headers.Instance, 0);
            this.remote.Verify(
                x => x.ReservePushStream(
                    It.IsAny<int>(),
                    It.IsAny<IHttp2Stream>()), Times.Never());
            this.listener.Verify(
                x => x.OnPushPromiseRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<IHttp2Headers>(),
                    It.IsAny<int>()), Times.Never());
        }

        [Fact]
        public void PushPromiseReadAfterGoAwayShouldAllowFramesForStreamCreatedByLocalEndpoint()
        {
            this.MockGoAwaySentShouldAllowFramesForStreamCreatedByLocalEndpoint();
            this.Decode().OnPushPromiseRead(this.ctx.Object, STREAM_ID, PUSH_STREAM_ID, EmptyHttp2Headers.Instance, 0);
            this.remote.Verify(
                x => x.ReservePushStream(
                    It.IsAny<int>(),
                    It.IsAny<IHttp2Stream>()));
            this.listener.Verify(
                x => x.OnPushPromiseRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<IHttp2Headers>(),
                    It.IsAny<int>()));
        }

        [Fact]
        public void PushPromiseReadForUnknownStreamShouldThrow()
        {
            this.connection.Setup(x => x.Stream(It.Is<int>(v => v == STREAM_ID))).Returns(default(IHttp2Stream));
            Assert.Throws<StreamException>(() => this.Decode().OnPushPromiseRead(this.ctx.Object, STREAM_ID, PUSH_STREAM_ID, EmptyHttp2Headers.Instance, 0));
        }

        [Fact]
        public void PushPromiseReadShouldSucceed()
        {
            this.Decode().OnPushPromiseRead(this.ctx.Object, STREAM_ID, PUSH_STREAM_ID, EmptyHttp2Headers.Instance, 0);
            this.remote.Verify(
                x => x.ReservePushStream(
                    It.Is<int>(v => v == PUSH_STREAM_ID),
                    It.Is<IHttp2Stream>(v => v == this.stream.Object)));
            this.listener.Verify(
                x => x.OnPushPromiseRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<int>(v => v == PUSH_STREAM_ID),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0)));
        }

        [Fact]
        public void PriorityReadAfterGoAwaySentShouldAllowFramesForStreamCreatedByLocalEndpoint()
        {
            this.MockGoAwaySentShouldAllowFramesForStreamCreatedByLocalEndpoint();
            this.Decode().OnPriorityRead(this.ctx.Object, STREAM_ID, 0, (short)255, true);
            this.remoteFlow.Verify(
                x => x.UpdateDependencyTree(
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == 255),
                    It.Is<bool>(v => v == true)));
            this.listener.Verify(
                x => x.OnPriorityRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<short>(),
                    It.IsAny<bool>()));
        }

        [Fact]
        public void PriorityReadForUnknownStreamShouldNotBeIgnored()
        {
            this.connection.Setup(x => x.Stream(It.Is<int>(v => v == STREAM_ID))).Returns(default(IHttp2Stream));
            this.Decode().OnPriorityRead(this.ctx.Object, STREAM_ID, 0, (short)255, true);
            this.remoteFlow.Verify(
                x => x.UpdateDependencyTree(
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == 255),
                    It.Is<bool>(v => v == true)));
            this.listener.Verify(
                x => x.OnPriorityRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == 255),
                    It.Is<bool>(v => v == true)));
        }

        [Fact]
        public void PriorityReadShouldNotCreateNewStream()
        {
            this.connection.Setup(x => x.StreamMayHaveExisted(It.Is<int>(v => v == STREAM_ID))).Returns(false);
            this.connection.Setup(x => x.Stream(It.Is<int>(v => v == STREAM_ID))).Returns(default(IHttp2Stream));
            this.Decode().OnPriorityRead(this.ctx.Object, STREAM_ID, STREAM_DEPENDENCY_ID, (short)255, true);
            this.remoteFlow.Verify(
                x => x.UpdateDependencyTree(
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<int>(v => v == STREAM_DEPENDENCY_ID),
                    It.Is<short>(v => v == 255),
                    It.Is<bool>(v => v == true)));
            this.listener.Verify(
                x => x.OnPriorityRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<int>(v => v == STREAM_DEPENDENCY_ID),
                    It.Is<short>(v => v == 255),
                    It.Is<bool>(v => v == true)));
            this.remote.Verify(
                x => x.CreateStream(
                    It.Is<int>(v => v == STREAM_ID),
                    It.IsAny<bool>()), Times.Never());
            this.stream.Verify(x => x.Open(It.IsAny<bool>()), Times.Never());
        }

        [Fact]
        public void WindowUpdateReadAfterGoAwaySentShouldBeIgnored()
        {
            this.MockGoAwaySent();
            this.Decode().OnWindowUpdateRead(this.ctx.Object, STREAM_ID, 10);
            this.remoteFlow.Verify(
                x => x.IncrementWindowSize(
                    It.IsAny<IHttp2Stream>(),
                    It.IsAny<int>()), Times.Never());
            this.listener.Verify(
                x => x.OnWindowUpdateRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<int>(),
                    It.IsAny<int>()), Times.Never());
        }

        [Fact]
        public void WindowUpdateReadAfterGoAwaySentShouldAllowFramesForStreamCreatedByLocalEndpoint()
        {
            this.MockGoAwaySentShouldAllowFramesForStreamCreatedByLocalEndpoint();
            this.Decode().OnWindowUpdateRead(this.ctx.Object, STREAM_ID, 10);
            this.remoteFlow.Verify(
                x => x.IncrementWindowSize(
                    It.IsAny<IHttp2Stream>(),
                    It.IsAny<int>()));
            this.listener.Verify(
                x => x.OnWindowUpdateRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<int>(),
                    It.IsAny<int>()));
        }

        [Fact]
        public void WindowUpdateReadForUnknownStreamShouldThrow()
        {
            this.connection.Setup(x => x.StreamMayHaveExisted(It.Is<int>(v => v == STREAM_ID))).Returns(false);
            this.connection.Setup(x => x.Stream(It.Is<int>(v => v == STREAM_ID))).Returns(default(IHttp2Stream));
            Assert.Throws<Http2Exception>(() => this.Decode().OnWindowUpdateRead(this.ctx.Object, STREAM_ID, 10));
        }

        [Fact]
        public void WindowUpdateReadForUnknownStreamShouldBeIgnored()
        {
            this.connection.Setup(x => x.Stream(It.Is<int>(v => v == STREAM_ID))).Returns(default(IHttp2Stream));
            this.Decode().OnWindowUpdateRead(this.ctx.Object, STREAM_ID, 10);
            this.remoteFlow.Verify(
                x => x.IncrementWindowSize(
                    It.IsAny<IHttp2Stream>(),
                    It.IsAny<int>()), Times.Never());
            this.listener.Verify(
                x => x.OnWindowUpdateRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<int>(),
                    It.IsAny<int>()), Times.Never());
        }

        [Fact]
        public void WindowUpdateReadShouldSucceed()
        {
            this.Decode().OnWindowUpdateRead(this.ctx.Object, STREAM_ID, 10);
            this.remoteFlow.Verify(
                x => x.IncrementWindowSize(
                    It.Is<IHttp2Stream>(v => v == this.stream.Object),
                    It.Is<int>(v => v == 10)));
            this.listener.Verify(
                x => x.OnWindowUpdateRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<int>(v => v == 10)));
        }

        [Fact]
        public void RstStreamReadAfterGoAwayShouldSucceed()
        {
            this.connection.Setup(x => x.GoAwaySent()).Returns(true);
            this.Decode().OnRstStreamRead(this.ctx.Object, STREAM_ID, Http2Error.ProtocolError);
            this.lifecycleManager
                .Verify(x => x.CloseStream(
                    It.Is<IHttp2Stream>(v => v == this.stream.Object),
                    It.Is<Task>(v => v == TaskUtil.Completed)));
            this.listener.Verify(
                x => x.OnRstStreamRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>()));
        }

        [Fact]
        public void RstStreamReadForUnknownStreamShouldThrow()
        {
            this.connection.Setup(x => x.StreamMayHaveExisted(It.Is<int>(v => v == STREAM_ID))).Returns(false);
            this.connection.Setup(x => x.Stream(It.Is<int>(v => v == STREAM_ID))).Returns(default(IHttp2Stream));
            Assert.Throws<Http2Exception>(() => this.Decode().OnRstStreamRead(this.ctx.Object, STREAM_ID, Http2Error.ProtocolError));
        }

        [Fact]
        public void RstStreamReadForUnknownStreamShouldBeIgnored()
        {
            this.connection.Setup(x => x.Stream(It.Is<int>(v => v == STREAM_ID))).Returns(default(IHttp2Stream));
            this.Decode().OnRstStreamRead(this.ctx.Object, STREAM_ID, Http2Error.ProtocolError);
            this.lifecycleManager
                .Verify(x => x.CloseStream(
                    It.Is<IHttp2Stream>(v => v == this.stream.Object),
                    It.Is<Task>(v => v == TaskUtil.Completed)), Times.Never());
            this.listener.Verify(
                x => x.OnRstStreamRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<int>(),
                    It.IsAny<Http2Error>()), Times.Never());
        }

        [Fact]
        public void RstStreamReadShouldCloseStream()
        {
            this.Decode().OnRstStreamRead(this.ctx.Object, STREAM_ID, Http2Error.ProtocolError);
            this.lifecycleManager
                .Verify(x => x.CloseStream(
                    It.Is<IHttp2Stream>(v => v == this.stream.Object),
                    It.Is<Task>(v => v == TaskUtil.Completed)));
            this.listener.Verify(
                x => x.OnRstStreamRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == STREAM_ID),
                    It.Is<Http2Error>(v => v == Http2Error.ProtocolError)));
        }

        [Fact]
        public void RstStreamOnIdleStreamShouldThrow()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                this.stream.Setup(x => x.State).Returns(Http2StreamState.Idle);
                this.Decode().OnRstStreamRead(this.ctx.Object, STREAM_ID, Http2Error.ProtocolError);
                this.lifecycleManager
                    .Verify(x => x.CloseStream(
                        It.Is<IHttp2Stream>(v => v == this.stream.Object),
                        It.Is<Task>(v => v == TaskUtil.Completed)));
                this.listener.Verify(
                    x => x.OnRstStreamRead(
                        It.IsAny<IChannelHandlerContext>(),
                        It.IsAny<int>(),
                        It.IsAny<Http2Error>()), Times.Never());
            });
        }

        [Fact]
        public void PingReadWithAckShouldNotifyListener()
        {
            this.Decode().OnPingAckRead(this.ctx.Object, 0L);
            this.listener.Verify(
                x => x.OnPingAckRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<long>(v => v == 0L)));
        }

        [Fact]
        public void PingReadShouldReplyWithAck()
        {
            this.Decode().OnPingRead(this.ctx.Object, 0L);
            this.encoder.Verify(
                x => x.WritePingAsync(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<bool>(v => v == true),
                    It.Is<long>(v => v == 0L),
                    It.Is<IPromise>(v => v == this.promise)));
            this.listener.Verify(
                x => x.OnPingAckRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.IsAny<long>()), Times.Never());
        }

        [Fact]
        public void SettingsReadWithAckShouldNotifyListener()
        {
            this.Decode().OnSettingsAckRead(this.ctx.Object);
            // Take into account the time this was called during setup().
            this.listener.Verify(
                x => x.OnSettingsAckRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object)), Times.Exactly(2));
        }

        [Fact]
        public void SettingsReadShouldSetValues()
        {
            Http2Settings settings = new Http2Settings();
            settings.PushEnabled(true);
            settings.InitialWindowSize(123);
            settings.MaxConcurrentStreams(456);
            settings.HeaderTableSize(789);
            this.Decode().OnSettingsRead(this.ctx.Object, settings);
            this.encoder.Verify(
                x => x.RemoteSettings(
                    It.Is<Http2Settings>(v => v.SequenceEqual(settings))));
            this.listener.Verify(
                x => x.OnSettingsRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<Http2Settings>(v => v.SequenceEqual(settings))));
        }

        [Fact]
        public void GoAwayShouldReadShouldUpdateConnectionState()
        {
            this.Decode().OnGoAwayRead(this.ctx.Object, 1, (Http2Error)2L, Unpooled.Empty);
            this.connection.Verify(
                x => x.GoAwayReceived(
                    It.Is<int>(v => v == 1),
                    It.Is<Http2Error>(v => v == (Http2Error)2L),
                    It.Is<IByteBuffer>(v => v.Equals(Unpooled.Empty))));
            this.listener.Verify(
                x => x.OnGoAwayRead(
                    It.Is<IChannelHandlerContext>(v => v == this.ctx.Object),
                    It.Is<int>(v => v == 1),
                    It.Is<Http2Error>(v => v == (Http2Error)2L),
                    It.Is<IByteBuffer>(v => v.Equals(Unpooled.Empty))));
        }

        private static IByteBuffer DummyData()
        {
            // The buffer is purposely 8 bytes so it will even work for a ping frame.
            return Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes("abcdefgh"));
        }

        /**
         * Calls the decode method on the handler and gets back the captured internal listener
         */
        private IHttp2FrameListener Decode()
        {
            var internalListener = new ArgumentCaptor<IHttp2FrameListener>();
            this.reader.Setup(
                x => x.ReadFrame(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<IByteBuffer>(),
                    It.Is<IHttp2FrameListener>(v => internalListener.Capture(v)))).Callback(() => { });
            this.decoder.DecodeFrame(this.ctx.Object, Unpooled.Empty, new List<object>());
            return internalListener.GetValue();
        }

        private void MockFlowControl(int processedBytes)
        {
            this.listener
                .Setup(x => x.OnDataRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>()))
                .Returns<IChannelHandlerContext, int, IByteBuffer, int, bool>((c, id, d, p, e) => processedBytes);
        }

        private void MockGoAwaySent()
        {
            this.connection.Setup(x => x.GoAwaySent()).Returns(true);
            this.remote.Setup(x => x.IsValidStreamId(It.Is<int>(v => v == STREAM_ID))).Returns(true);
            this.remote.Setup(x => x.LastStreamKnownByPeer()).Returns(0);
        }

        private void MockGoAwaySentShouldAllowFramesForStreamCreatedByLocalEndpoint()
        {
            this.connection.Setup(x => x.GoAwaySent()).Returns(true);
            this.remote.Setup(x => x.IsValidStreamId(It.Is<int>(v => v == STREAM_ID))).Returns(false);
            this.remote.Setup(x => x.LastStreamKnownByPeer()).Returns(0);
        }
    }
}
