/*
 * Copyright (c) Johnny Z. All rights reserved.
 *
 *   https://github.com/StormHub/NetUV
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com)
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.NetUV.Native
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
