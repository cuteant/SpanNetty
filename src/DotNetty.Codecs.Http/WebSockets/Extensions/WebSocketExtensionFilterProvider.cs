namespace DotNetty.Codecs.Http.WebSockets.Extensions
{
    /// <summary>
    /// Extension filter provider that is responsible to provide filters for a certain <see cref="IWebSocketExtension"/> extension.
    /// </summary>
    public sealed class WebSocketExtensionFilterProvider : IWebSocketExtensionFilterProvider
    {
        public static readonly WebSocketExtensionFilterProvider Default = new WebSocketExtensionFilterProvider();

        private WebSocketExtensionFilterProvider() { }

        /// <summary>
        /// Returns the extension filter for <see cref="WebSocketExtensionEncoder"/> encoder.
        /// </summary>
        public IWebSocketExtensionFilter EncoderFilter => NeverSkipWebSocketExtensionFilter.Instance;

        /// <summary>
        /// Returns the extension filter for <see cref="WebSocketExtensionDecoder"/> decoder.
        /// </summary>
        public IWebSocketExtensionFilter DecoderFilter => NeverSkipWebSocketExtensionFilter.Instance;
    }
}
