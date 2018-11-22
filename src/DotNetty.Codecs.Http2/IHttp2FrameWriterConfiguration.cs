// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// Configuration specific to <see cref="IHttp2FrameWriter"/>
    /// </summary>
    public interface IHttp2FrameWriterConfiguration
    {
        /// <summary>
        /// Get the <see cref="IHttp2HeadersEncoderConfiguration"/> for this <see cref="IHttp2FrameWriter"/>.
        /// </summary>
        IHttp2HeadersEncoderConfiguration HeadersConfiguration { get; }

        /// <summary>
        /// Get the <see cref="IHttp2FrameSizePolicy"/> for this <see cref="IHttp2FrameWriter"/>.
        /// </summary>
        IHttp2FrameSizePolicy FrameSizePolicy { get; }
    }
}
