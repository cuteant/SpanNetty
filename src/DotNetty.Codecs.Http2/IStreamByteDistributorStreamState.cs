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
    /// <summary>
    /// State information for the stream, indicating the number of bytes that are currently
    /// streamable. This is provided to the <see cref="IStreamByteDistributor.UpdateStreamableBytes(IStreamByteDistributorStreamState)"/> method.
    /// </summary>
    public interface IStreamByteDistributorStreamState
    {
        /// <summary>
        /// Gets the stream this state is associated with.
        /// </summary>
        IHttp2Stream Stream { get; }

        /// <summary>
        /// Get the amount of bytes this stream has pending to send. The actual amount written must not exceed
        /// <see cref="WindowSize"/>!
        /// </summary>
        /// <returns>The amount of bytes this stream has pending to send.
        /// see <see cref="Http2CodecUtil.StreamableBytes(IStreamByteDistributorStreamState)"/>.</returns>
        long PendingBytes { get; }

        /// <summary>
        /// Indicates whether or not there are frames pending for this stream.
        /// </summary>
        bool HasFrame { get; }

        /// <summary>
        /// The size (in bytes) of the stream's flow control window. The amount written must not exceed this amount!
        /// <para>A <see cref="IStreamByteDistributor"/> needs to know the stream's window size in order to avoid allocating bytes
        /// if the window size is negative. The window size being <c>0</c> may also be significant to determine when if
        /// an stream has been given a chance to write an empty frame, and also enables optimizations like not writing
        /// empty frames in some situations (don't write headers until data can also be written).</para>
        /// </summary>
        /// <returns>the size of the stream's flow control window.
        /// see <see cref="Http2CodecUtil.StreamableBytes(IStreamByteDistributorStreamState)"/>.</returns>
        int WindowSize { get; }
    }
}