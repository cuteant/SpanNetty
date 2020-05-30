
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using Moq;
    using Xunit;

    public class Http2EmptyDataFrameListenerTest
    {
        private Mock<IHttp2FrameListener> _frameListener;
        private Mock<IChannelHandlerContext> _ctx;
        private Mock<IByteBuffer> _nonEmpty;

        private Http2EmptyDataFrameListener _listener;

        public Http2EmptyDataFrameListenerTest()
        {
            _frameListener = new Mock<IHttp2FrameListener>();
            _ctx = new Mock<IChannelHandlerContext>();
            _nonEmpty = new Mock<IByteBuffer>();
            _nonEmpty.Setup(x => x.IsReadable()).Returns(true);
            _listener = new Http2EmptyDataFrameListener(_frameListener.Object, 2);
        }

        [Fact]
        public void EmptyDataFrames()
        {
            var ctx = _ctx.Object;
            _listener.OnDataRead(ctx, 1, Unpooled.Empty, 0, false);
            _listener.OnDataRead(ctx, 1, Unpooled.Empty, 0, false);

            try
            {
                _listener.OnDataRead(ctx, 1, Unpooled.Empty, 0, false);
                Assert.False(true);
            }
            catch (Exception expected)
            {
                // expected
                Assert.IsType<Http2Exception>(expected);
            }
            _frameListener.Verify(
                x => x.OnDataRead(
                    It.Is<IChannelHandlerContext>(v => v == ctx),
                    It.Is<int>(v => v == 1),
                    It.IsAny<IByteBuffer>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false)),
                Times.Exactly(2));
        }

        [Fact]
        public void EmptyDataFramesWithNonEmptyInBetween()
        {
            var ctx = _ctx.Object;
            //Http2EmptyDataFrameListener listener = new Http2EmptyDataFrameListener(_frameListener.Object, 2);
            _listener.OnDataRead(ctx, 1, Unpooled.Empty, 0, false);
            _listener.OnDataRead(ctx, 1, _nonEmpty.Object, 0, false);

            _listener.OnDataRead(ctx, 1, Unpooled.Empty, 0, false);
            _listener.OnDataRead(ctx, 1, Unpooled.Empty, 0, false);

            try
            {
                _listener.OnDataRead(ctx, 1, Unpooled.Empty, 0, false);
                Assert.False(true);
            }
            catch (Exception expected)
            {
                // expected
                Assert.IsType<Http2Exception>(expected);
            }
            _frameListener.Verify(
                x => x.OnDataRead(
                    It.Is<IChannelHandlerContext>(v => v == ctx),
                    It.Is<int>(v => v == 1),
                    It.IsAny<IByteBuffer>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false)),
                Times.Exactly(4));
        }

        [Fact]
        public void EmptyDataFramesWithEndOfStreamInBetween()
        {
            var ctx = _ctx.Object;
            //Http2EmptyDataFrameListener listener = new Http2EmptyDataFrameListener(_frameListener.Object, 2);
            _listener.OnDataRead(ctx, 1, Unpooled.Empty, 0, false);
            _listener.OnDataRead(ctx, 1, Unpooled.Empty, 0, true);

            _listener.OnDataRead(ctx, 1, Unpooled.Empty, 0, false);
            _listener.OnDataRead(ctx, 1, Unpooled.Empty, 0, false);

            try
            {
                _listener.OnDataRead(ctx, 1, Unpooled.Empty, 0, false);
                Assert.False(true);
            }
            catch (Exception expected)
            {
                // expected
                Assert.IsType<Http2Exception>(expected);
            }
            _frameListener.Verify(
                x => x.OnDataRead(
                    It.Is<IChannelHandlerContext>(v => v == ctx),
                    It.Is<int>(v => v == 1),
                    It.IsAny<IByteBuffer>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true)),
                Times.Exactly(1));
            _frameListener.Verify(
                x => x.OnDataRead(
                    It.Is<IChannelHandlerContext>(v => v == ctx),
                    It.Is<int>(v => v == 1),
                    It.IsAny<IByteBuffer>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false)),
                Times.Exactly(3));
        }

        [Fact]
        public void EmptyDataFramesWithHeaderFrameInBetween()
        {
            var ctx = _ctx.Object;
            //Http2EmptyDataFrameListener listener = new Http2EmptyDataFrameListener(_frameListener.Object, 2);
            _listener.OnDataRead(ctx, 1, Unpooled.Empty, 0, false);
            _listener.OnHeadersRead(ctx, 1, EmptyHttp2Headers.Instance, 0, true);

            _listener.OnDataRead(ctx, 1, Unpooled.Empty, 0, false);
            _listener.OnDataRead(ctx, 1, Unpooled.Empty, 0, false);

            try
            {
                _listener.OnDataRead(ctx, 1, Unpooled.Empty, 0, false);
                Assert.False(true);
            }
            catch (Exception expected)
            {
                // expected
                Assert.IsType<Http2Exception>(expected);
            }

            _frameListener.Verify(
                x => x.OnHeadersRead(
                    It.Is<IChannelHandlerContext>(v => v == ctx),
                    It.Is<int>(v => v == 1),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true)),
                Times.Exactly(1));
            _frameListener.Verify(
                x => x.OnDataRead(
                    It.Is<IChannelHandlerContext>(v => v == ctx),
                    It.Is<int>(v => v == 1),
                    It.IsAny<IByteBuffer>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false)),
                Times.Exactly(3));
        }

        [Fact]
        public void EmptyDataFramesWithHeaderFrameInBetween2()
        {
            var ctx = _ctx.Object;
            //Http2EmptyDataFrameListener listener = new Http2EmptyDataFrameListener(_frameListener.Object, 2);
            _listener.OnDataRead(ctx, 1, Unpooled.Empty, 0, false);
            _listener.OnHeadersRead(ctx, 1, EmptyHttp2Headers.Instance, 0, (short)0, false, 0, true);

            _listener.OnDataRead(ctx, 1, Unpooled.Empty, 0, false);
            _listener.OnDataRead(ctx, 1, Unpooled.Empty, 0, false);

            try
            {
                _listener.OnDataRead(ctx, 1, Unpooled.Empty, 0, false);
                Assert.False(true);
            }
            catch (Exception expected)
            {
                // expected
                Assert.IsType<Http2Exception>(expected);
            }

            _frameListener.Verify(
                x => x.OnHeadersRead(
                    It.Is<IChannelHandlerContext>(v => v == ctx),
                    It.Is<int>(v => v == 1),
                    It.Is<IHttp2Headers>(v => ReferenceEquals(v, EmptyHttp2Headers.Instance)),
                    It.Is<int>(v => v == 0),
                    It.Is<short>(v => v == 0),
                    It.Is<bool>(v => v == false),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true)),
                Times.Exactly(1));
            _frameListener.Verify(
                x => x.OnDataRead(
                    It.Is<IChannelHandlerContext>(v => v == ctx),
                    It.Is<int>(v => v == 1),
                    It.IsAny<IByteBuffer>(),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false)),
                Times.Exactly(3));
        }
    }
}
