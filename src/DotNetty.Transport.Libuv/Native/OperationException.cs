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

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using DotNetty.Common.Internal;

    public sealed class OperationException : Exception
    {
        private static readonly CachedReadConcurrentDictionary<string, ErrorCode> s_errorCodeCache;
        private static readonly Func<string, ErrorCode> s_convertErrorCodeFunc;

        static OperationException()
        {
            s_errorCodeCache = new CachedReadConcurrentDictionary<string, ErrorCode>(StringComparer.Ordinal);
            s_convertErrorCodeFunc = e => ConvertErrorCode(e);
        }

        public OperationException(int errorCode, string errorName, string description)
            : base($"{errorName} : {description}")
        {
            Code = errorCode;
            Name = errorName;
            Description = description;

            ErrorCode = s_errorCodeCache.GetOrAdd(errorName, s_convertErrorCodeFunc);
        }

        public int Code { get; }

        public string Name { get; }

        public string Description { get; }

        public ErrorCode ErrorCode { get; }

        public override string Message => $"{Name} ({ErrorCode}) : {base.Message}";

        static ErrorCode ConvertErrorCode(string errorName)
        {
            if (!Enum.TryParse(errorName, true, out ErrorCode value))
            {
                value = ErrorCode.UNKNOWN;
            }
            return value;
        }
    }
}
