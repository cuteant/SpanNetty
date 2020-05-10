// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;

    /// <summary>
    /// A HTTP/2 exception for a specific <see cref="IHttp2FrameStream"/>.
    /// </summary>
    public sealed class Http2FrameStreamException : Exception
    {
        public Http2FrameStreamException(IHttp2FrameStream stream, Http2Error error, Exception cause)
            : base(cause.Message, cause)
        {
            if (stream is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.stream); }

            this.Stream = stream;
            this.Error = error;
        }

        public Http2Error Error { get; }

        public IHttp2FrameStream Stream { get; }
    }
}
