// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// Thrown if buffered streams are terminated due to this encoder being closed.
    /// </summary>
    public sealed class Http2ChannelClosedException : Http2Exception
    {
        public Http2ChannelClosedException()
            : base(Http2Error.RefusedStream, "Connection closed")
        {
        }
    }
}
