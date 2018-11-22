// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;

    /// <summary>
    /// Decodes HPACK-encoded headers blocks into <see cref="IHttp2Headers"/>.
    /// </summary>
    public interface IHttp2HeadersDecoder
    {
        /// <summary>
        /// Decodes the given headers block and returns the headers.
        /// </summary>
        /// <param name="streamId"></param>
        /// <param name="headerBlock"></param>
        /// <returns></returns>
        IHttp2Headers DecodeHeaders(int streamId, IByteBuffer headerBlock);

        /// <summary>
        /// Get the <see cref="IHttp2HeadersDecoderConfiguration"/> for this <see cref="IHttp2HeadersDecoder"/>.
        /// </summary>
        IHttp2HeadersDecoderConfiguration Configuration { get; }
    }
}
