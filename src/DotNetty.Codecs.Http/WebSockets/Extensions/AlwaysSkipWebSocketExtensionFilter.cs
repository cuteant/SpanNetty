namespace DotNetty.Codecs.Http.WebSockets.Extensions
{
    /// <summary>
    /// A <see cref="IWebSocketExtensionFilter"/> that always skip the evaluation of an
    /// any given extensions <see cref="IWebSocketExtension"/>.
    /// </summary>
    public sealed class AlwaysSkipWebSocketExtensionFilter : IWebSocketExtensionFilter
    {
        public static readonly IWebSocketExtensionFilter Instance = new AlwaysSkipWebSocketExtensionFilter();

        private AlwaysSkipWebSocketExtensionFilter() { }

        public bool MustSkip(WebSocketFrame frame) => true;
    }
}
