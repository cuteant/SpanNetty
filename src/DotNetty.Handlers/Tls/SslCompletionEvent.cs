using System;

namespace DotNetty.Handlers.Tls
{
    public abstract class SslCompletionEvent
    {
        private readonly Exception _cause;

        public SslCompletionEvent() { }

        public SslCompletionEvent(Exception cause)
        {
            if (cause is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.cause); }
            _cause = cause;
        }

        public bool IsSuccess => _cause is null;

        public Exception Cause => _cause;

        public override string ToString()
        {
            return IsSuccess
                ? $"{nameof(SslCompletionEvent)}(SUCCESS)"
                : $"{nameof(SslCompletionEvent)}({_cause.Message})";
        }
    }
}
