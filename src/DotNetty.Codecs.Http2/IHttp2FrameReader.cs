// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
