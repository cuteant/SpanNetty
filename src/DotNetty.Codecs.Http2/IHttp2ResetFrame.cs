// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// HTTP/2 RST_STREAM frame.
    /// </summary>
    public interface IHttp2ResetFrame: IHttp2StreamFrame
    {
        /// <summary>
        /// The reason for resetting the stream. Represented as an HTTP/2 error code.
        /// </summary>
        Http2Error ErrorCode { get; }
    }
}
