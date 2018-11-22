// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// HTTP/2 HEADERS frame.
    /// </summary>
    public interface IHttp2HeadersFrame : IHttp2StreamFrame
    {
        /// <summary>
        /// A complete header list. CONTINUATION frames are automatically handled.
        /// </summary>
        /// <returns></returns>
        IHttp2Headers Headers { get; }

        /// <summary>
        /// Frame padding to use. Must be non-negative and less than 256.
        /// </summary>
        /// <returns></returns>
        int Padding { get; }

        /// <summary>
        /// Returns <c>true</c> if the END_STREAM flag ist set.
        /// </summary>
        /// <returns></returns>
        bool IsEndStream { get; }
    }
}
