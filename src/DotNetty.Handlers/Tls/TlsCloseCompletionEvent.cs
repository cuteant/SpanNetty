using System;

namespace DotNetty.Handlers.Tls
{
    /// <summary>
    /// Event that is fired once the close_notify was received or if an failure happens before it was received.
    /// </summary>
    public sealed class TlsCloseCompletionEvent : TlsCompletionEvent
    {
        public static readonly TlsCloseCompletionEvent Success = new TlsCloseCompletionEvent();

        /// <summary>
        /// Creates a new event that indicates a successful receiving of close_notify.
        /// </summary>
        private TlsCloseCompletionEvent() { }

        /// <summary>
        /// Creates a new event that indicates an close_notify was not received because of an previous error.
        /// Use <see cref="Success"/> to indicate a success.
        /// </summary>
        /// <param name="cause"></param>
        public TlsCloseCompletionEvent(Exception cause) : base(cause) { }
    }
}
