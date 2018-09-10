namespace DotNetty.Transport.Libuv.Native
{
    public static class ErrorCodeExtensions
    {
        public static bool IsConnectionAbortError(this ErrorCode errCode)
        {
            switch (errCode)
            {
                case ErrorCode.ECANCELED:

                case ErrorCode.EPIPE:
                case ErrorCode.ENOTCONN:
                case ErrorCode.EINVAL:

                case ErrorCode.ENOTSOCK:
                case ErrorCode.EINTR:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsConnectionResetError(this ErrorCode errCode)
        {
            switch (errCode)
            {
                case ErrorCode.ECONNRESET:
                case ErrorCode.ECONNREFUSED:

                case ErrorCode.EPIPE:
                case ErrorCode.ENOTCONN:
                case ErrorCode.EINVAL:

                case ErrorCode.ENOTSOCK:
                case ErrorCode.EINTR:

                case ErrorCode.ETIMEDOUT:
                case ErrorCode.ESHUTDOWN:
                    return true;
                default:
                    return false;
            }
        }
    }
}
