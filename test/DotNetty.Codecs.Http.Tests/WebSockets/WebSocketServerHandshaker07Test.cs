namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using DotNetty.Codecs.Http.WebSockets;

    public class WebSocketServerHandshaker07Test: WebSocketServerHandshakerTest
    {
        protected override WebSocketServerHandshaker NewHandshaker(string webSocketURL, string subprotocols, WebSocketDecoderConfig decoderConfig)
        {
            return new WebSocketServerHandshaker07(webSocketURL, subprotocols, decoderConfig);
        }

        protected override WebSocketVersion WebSocketVersion()
        {
            return Http.WebSockets.WebSocketVersion.V07;
        }
    }
}
