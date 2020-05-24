using System;

namespace DotNetty.Handlers.Tls
{
    public class SniCompletionEvent : SslCompletionEvent
    {
        private readonly string _hostName;

        public SniCompletionEvent(string hostName)
        {
            _hostName = hostName;
        }

        public SniCompletionEvent(Exception cause)
            : this(null, cause)
        {
        }

        public SniCompletionEvent(string hostName, Exception cause)
            : base(cause)
        {
            _hostName = hostName;
        }

        /// <summary>
        /// Returns the SNI hostname send by the client if we were able to parse it, <code>null</code> otherwise.
        /// </summary>
        public string HostName => _hostName;

        public override string ToString()
        {
            return IsSuccess
                ? $"{nameof(SniCompletionEvent)}(SUCCESS'{_hostName}')"
                : $"{nameof(SniCompletionEvent)}({Cause.Message})";
        }
    }
}
