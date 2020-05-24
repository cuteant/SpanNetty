using System;
using System.Collections.Generic;
using System.Text;

namespace DotNetty.Codecs.Http.WebSockets.Extensions
{
    /// <summary>
    /// Extension filter provider that is responsible to provide filters for a certain <see cref="IWebSocketExtension"/> extension.
    /// </summary>
    public interface IWebSocketExtensionFilterProvider
    {
        /// <summary>
        /// Returns the extension filter for <see cref="WebSocketExtensionEncoder"/> encoder.
        /// </summary>
        IWebSocketExtensionFilter EncoderFilter { get; }

        /// <summary>
        /// Returns the extension filter for <see cref="WebSocketExtensionDecoder"/> decoder.
        /// </summary>
        IWebSocketExtensionFilter DecoderFilter { get; }
    }
}
