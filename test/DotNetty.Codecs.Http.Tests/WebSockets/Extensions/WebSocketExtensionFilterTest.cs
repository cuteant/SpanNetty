namespace DotNetty.Codecs.Http.Tests.WebSockets.Extensions
{
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Codecs.Http.WebSockets.Extensions;
    using Xunit;

    public sealed class WebSocketExtensionFilterTest
    {
        [Fact]
        public void TestNeverSkip()
        {
            IWebSocketExtensionFilter neverSkip = NeverSkipWebSocketExtensionFilter.Instance;

            BinaryWebSocketFrame binaryFrame = new BinaryWebSocketFrame();
            Assert.False(neverSkip.MustSkip(binaryFrame));
            Assert.True(binaryFrame.Release());

            TextWebSocketFrame textFrame = new TextWebSocketFrame();
            Assert.False(neverSkip.MustSkip(textFrame));
            Assert.True(textFrame.Release());

            PingWebSocketFrame pingFrame = new PingWebSocketFrame();
            Assert.False(neverSkip.MustSkip(pingFrame));
            Assert.True(pingFrame.Release());

            PongWebSocketFrame pongFrame = new PongWebSocketFrame();
            Assert.False(neverSkip.MustSkip(pongFrame));
            Assert.True(pongFrame.Release());

            CloseWebSocketFrame closeFrame = new CloseWebSocketFrame();
            Assert.False(neverSkip.MustSkip(closeFrame));
            Assert.True(closeFrame.Release());

            ContinuationWebSocketFrame continuationFrame = new ContinuationWebSocketFrame();
            Assert.False(neverSkip.MustSkip(continuationFrame));
            Assert.True(continuationFrame.Release());
        }

        [Fact]
        public void TestAlwaysSkip()
        {
            IWebSocketExtensionFilter neverSkip = AlwaysSkipWebSocketExtensionFilter.Instance;

            BinaryWebSocketFrame binaryFrame = new BinaryWebSocketFrame();
            Assert.True(neverSkip.MustSkip(binaryFrame));
            Assert.True(binaryFrame.Release());

            TextWebSocketFrame textFrame = new TextWebSocketFrame();
            Assert.True(neverSkip.MustSkip(textFrame));
            Assert.True(textFrame.Release());

            PingWebSocketFrame pingFrame = new PingWebSocketFrame();
            Assert.True(neverSkip.MustSkip(pingFrame));
            Assert.True(pingFrame.Release());

            PongWebSocketFrame pongFrame = new PongWebSocketFrame();
            Assert.True(neverSkip.MustSkip(pongFrame));
            Assert.True(pongFrame.Release());

            CloseWebSocketFrame closeFrame = new CloseWebSocketFrame();
            Assert.True(neverSkip.MustSkip(closeFrame));
            Assert.True(closeFrame.Release());

            ContinuationWebSocketFrame continuationFrame = new ContinuationWebSocketFrame();
            Assert.True(neverSkip.MustSkip(continuationFrame));
            Assert.True(continuationFrame.Release());
        }
    }
}
