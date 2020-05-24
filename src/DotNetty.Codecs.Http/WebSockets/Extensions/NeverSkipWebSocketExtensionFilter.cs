namespace DotNetty.Codecs.Http.WebSockets.Extensions
{
    /// <summary>
    /// A <see cref="IWebSocketExtensionFilter"/> that never skip the evaluation of an
    /// any given extensions <see cref="IWebSocketExtension"/>.
    /// </summary>
    public sealed class NeverSkipWebSocketExtensionFilter : IWebSocketExtensionFilter
    {
        public static readonly IWebSocketExtensionFilter Instance = new NeverSkipWebSocketExtensionFilter();

        private NeverSkipWebSocketExtensionFilter() { }

        public bool MustSkip(WebSocketFrame frame) => false;
    }
}
