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
    using System;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Reads HTTP/2 frames from an input <see cref="IByteBuffer"/> and notifies the specified
    /// <see cref="IHttp2FrameListener"/> when frames are complete.
    /// </summary>
    public interface IHttp2FrameReader : IDisposable
    {
        /// <summary>
        /// Attempts to read the next frame from the input buffer. If enough data is available to fully
        /// read the frame, notifies the listener of the read frame.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="input"></param>
        /// <param name="listener"></param>
        void ReadFrame(IChannelHandlerContext ctx, IByteBuffer input, IHttp2FrameListener listener);

        /// <summary>
        /// Get the configuration related elements for this <see cref="IHttp2FrameReader"/>.
        /// </summary>
        /// <returns></returns>
        IHttp2FrameReaderConfiguration Configuration { get; }

        /// <summary>
        /// Closes this reader and frees any allocated resources.
        /// </summary>
        void Close();
    }
}
