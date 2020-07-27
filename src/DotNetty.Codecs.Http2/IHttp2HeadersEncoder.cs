/*
 * Copyright 2012 The Netty Project
 *
 * The Netty Project licenses this file to you under the Apache License,
 * version 2.0 (the "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at:
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

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
