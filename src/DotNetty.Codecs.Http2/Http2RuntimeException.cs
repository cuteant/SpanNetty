// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;

    public class Http2RuntimeException : Exception
    {
        public Http2RuntimeException() : base() { }

        public Http2RuntimeException(string message) : base(message) { }

        public Http2RuntimeException(string message, Exception innerException) : base(message, innerException) { }
    }
}
