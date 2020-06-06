// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;

    /// <summary>
    /// A single stream within an HTTP/2 connection. To be used with the <see cref="Http2FrameCodec"/>.
    /// </summary>
    public interface IHttp2FrameStream : IEquatable<IHttp2FrameStream>
    {
        /// <summary>
        /// Returns the stream identifier.
        /// Use <see cref="Http2CodecUtil.IsStreamIdValid(int)"/> to check if the stream has already been assigned an identifier.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Returns the state of this stream.
        /// </summary>
        Http2StreamState State { get; }
    }
}
