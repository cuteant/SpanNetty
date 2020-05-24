using System;

namespace DotNetty.Codecs.Http.WebSockets
{
    /// <summary>
    /// An <see cref="DecoderException"/> which is thrown when the received <see cref="WebSocketFrame"/> data could not be decoded by
    /// an inbound handler.
    /// </summary>
    public class CorruptedWebSocketFrameException : CorruptedFrameException
    {
        public CorruptedWebSocketFrameException()
            : this(WebSocketCloseStatus.ProtocolError, null, null)
        {
        }

        public CorruptedWebSocketFrameException(WebSocketCloseStatus status, string message)
            : this(status, message, null)
        {
        }

        public CorruptedWebSocketFrameException(WebSocketCloseStatus status, Exception cause)
            : this(status, null, cause)
        {
        }

        public CorruptedWebSocketFrameException(WebSocketCloseStatus status, string message, Exception cause)
            : base(message is null ? status.ReasonText.ToString() : message, cause)
        {
            CloseStatus = status;
        }

        public WebSocketCloseStatus CloseStatus { get; }
    }
}
