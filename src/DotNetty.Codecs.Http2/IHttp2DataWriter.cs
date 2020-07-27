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
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Interface that defines an object capable of producing HTTP/2 data frames.
    /// </summary>
    public interface IHttp2DataWriter
    {
        /// <summary>
        /// Writes a <c>DATA</c> frame to the remote endpoint. This will result in one or more
        /// frames being written to the context.
        /// </summary>
        /// <param name="ctx">the context to use for writing.</param>
        /// <param name="streamId">the stream for which to send the frame.</param>
        /// <param name="data">the payload of the frame. This will be released by this method.</param>
        /// <param name="padding">additional bytes that should be added to obscure the true content size. Must be between 0 and
        /// 256 (inclusive). A 1 byte padding is encoded as just the pad length field with value 0.
        /// A 256 byte padding is encoded as the pad length field with value 255 and 255 padding bytes
        /// appended to the end of the frame.</param>
        /// <param name="endOfStream">indicates if this is the last frame to be sent for the stream.</param>
        /// <param name="promise">the promise for the write.</param>
        /// <returns>the future for the write.</returns>
        Task WriteDataAsync(IChannelHandlerContext ctx, int streamId,
            IByteBuffer data, int padding, bool endOfStream, IPromise promise);
    }
}
