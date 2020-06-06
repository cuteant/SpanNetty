using System;

namespace DotNetty.Handlers.Tls
{
    public abstract class TlsCompletionEvent
    {
        private readonly Exception _cause;

        public TlsCompletionEvent() { }

        public TlsCompletionEvent(Exception cause)
        {
            if (cause is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.cause); }
            _cause = cause;
        }

        public bool IsSuccess => _cause is null;

        public Exception Cause => _cause;

        public override string ToString()
        {
            return IsSuccess
                ? $"{nameof(TlsCompletionEvent)}(SUCCESS)"
                : $"{nameof(TlsCompletionEvent)}({_cause.Message})";
        }
    }
}
