// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// Configuration specific to <see cref="IHttp2FrameReader"/>
    /// </summary>
    public interface IHttp2FrameReaderConfiguration
    {
        /// <summary>
        /// Get the <see cref="IHttp2HeadersDecoderConfiguration"/> for this <see cref="IHttp2FrameReader"/>.
        /// </summary>
        /// <returns></returns>
        IHttp2HeadersDecoderConfiguration HeadersConfiguration { get; }

        /// <summary>
        /// Get the <see cref="IHttp2FrameSizePolicy"/> for this <see cref="IHttp2FrameReader"/>.
        /// </summary>
        /// <returns></returns>
        IHttp2FrameSizePolicy FrameSizePolicy { get; }
    }
}
