namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Utilities;
    using Xunit;

    public abstract class WebSocketServerHandshakerTest
    {
        protected abstract WebSocketServerHandshaker NewHandshaker(string webSocketURL, string subprotocols, WebSocketDecoderConfig decoderConfig);

        protected abstract WebSocketVersion WebSocketVersion();

        [Fact]
        public void TestDuplicateHandshakeResponseHeaders()
        {
            WebSocketServerHandshaker serverHandshaker = NewHandshaker("ws://example.com/chat",
                                                                       "chat", WebSocketDecoderConfig.Default);
            IFullHttpRequest request = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/chat");
            request.Headers
                   .Set(HttpHeaderNames.Host, "example.com")
                   .Set(HttpHeaderNames.Origin, "example.com")
                   .Set(HttpHeaderNames.Upgrade, HttpHeaderValues.Websocket)
                   .Set(HttpHeaderNames.Connection, HttpHeaderValues.Upgrade)
                   .Set(HttpHeaderNames.SecWebsocketKey, "dGhlIHNhbXBsZSBub25jZQ==")
                   .Set(HttpHeaderNames.SecWebsocketOrigin, "http://example.com")
                   .Set(HttpHeaderNames.SecWebsocketProtocol, "chat, superchat")
                   .Set(HttpHeaderNames.SecWebsocketVersion, WebSocketVersion().ToHttpHeaderValue());
            HttpHeaders customResponseHeaders = new DefaultHttpHeaders();
            // set duplicate required headers and one custom
            customResponseHeaders
                    .Set(HttpHeaderNames.Connection, HttpHeaderValues.Upgrade)
                    .Set(HttpHeaderNames.Upgrade, HttpHeaderValues.Websocket)
                    .Set(AsciiString.Of("custom"), AsciiString.Of("header"));

            if (WebSocketVersion() != Http.WebSockets.WebSocketVersion.V00)
            {
                customResponseHeaders.Set(HttpHeaderNames.SecWebsocketAccept, "12345");
            }

            IFullHttpResponse response = null;
            try
            {
                response = serverHandshaker.NewHandshakeResponse(request, customResponseHeaders);
                HttpHeaders responseHeaders = response.Headers;

                Assert.Equal(1, responseHeaders.GetAll(HttpHeaderNames.Connection).Count);
                Assert.Equal(1, responseHeaders.GetAll(HttpHeaderNames.Upgrade).Count);
                Assert.True(responseHeaders.ContainsValue(AsciiString.Of("custom"), AsciiString.Of("header"), true));
                if (WebSocketVersion() != Http.WebSockets.WebSocketVersion.V00)
                {
                    Assert.False(responseHeaders.ContainsValue(HttpHeaderNames.SecWebsocketAccept, AsciiString.Of("12345"), false));
                }
            }
            finally
            {
                request.Release();
                if (response != null)
                {
                    response.Release();
                }
            }
        }
    }
}
