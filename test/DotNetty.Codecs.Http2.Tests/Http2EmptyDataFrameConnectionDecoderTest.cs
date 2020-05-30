
namespace DotNetty.Codecs.Http2.Tests
{
    using Moq;
    using Xunit;

    public class Http2EmptyDataFrameConnectionDecoderTest
    {
        [Fact]
        public void Decoration()
        {
            var decoder0 = new Mock<IHttp2ConnectionDecoder>();
            decoder0.Setup(x => x.FrameListener);
            IHttp2FrameListener listenerArgumentCaptor = null;
            decoder0.SetupSet(p => p.FrameListener = It.IsAny<IHttp2FrameListener>()).Callback<IHttp2FrameListener>(value => listenerArgumentCaptor = value);
            decoder0.SetupGet(p => p.FrameListener).Returns(() => listenerArgumentCaptor);

            var listener = new Mock<IHttp2FrameListener>();
            Http2EmptyDataFrameConnectionDecoder decoder = new Http2EmptyDataFrameConnectionDecoder(decoder0.Object, 2);
            decoder.FrameListener = listener.Object;
            // verify(delegate).frameListener(listenerArgumentCaptor.capture());
            Assert.IsType<Http2EmptyDataFrameListener>(decoder.FrameListener);
        }

        [Fact]
        public void DecorationWithNull()
        {
            var decoder0 = new Mock<IHttp2ConnectionDecoder>();
            Http2EmptyDataFrameConnectionDecoder decoder = new Http2EmptyDataFrameConnectionDecoder(decoder0.Object, 2);
            decoder.FrameListener = null;
            Assert.Null(decoder.FrameListener);
        }
    }
}
