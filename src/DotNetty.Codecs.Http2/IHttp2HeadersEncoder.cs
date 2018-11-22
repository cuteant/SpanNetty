// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;

    /// <summary>
    /// Encodes <see cref="IHttp2Headers"/> into HPACK-encoded headers blocks.
    /// </summary>
    public interface IHttp2HeadersEncoder
    {
        /// <summary>
        /// Encodes the given headers and writes the output headers block to the given output buffer.
        /// </summary>
        /// <param name="streamId">the identifier of the stream for which the headers are encoded.</param>
        /// <param name="headers">the headers to be encoded.</param>
        /// <param name="buffer">the buffer to receive the encoded headers.</param>
        void EncodeHeaders(int streamId, IHttp2Headers headers, IByteBuffer buffer);

        /// <summary>
        /// Get the <see cref="IHttp2HeadersEncoderConfiguration"/> for this <see cref="IHttp2HeadersEncoder"/>.
        /// </summary>
        IHttp2HeadersEncoderConfiguration Configuration { get; }
    }
}
