// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// HTTP/2 WINDOW_UPDATE frame.
    /// </summary>
    public interface IHttp2WindowUpdateFrame: IHttp2StreamFrame
    {
        /// <summary>
        /// Number of bytes to increment the HTTP/2 stream's or connection's flow control window.
        /// </summary>
        int WindowSizeIncrement { get; }
    }
}
