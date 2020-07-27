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
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// A writer responsible for marshaling HTTP/2 frames to the channel. All of the write methods in
    /// this interface write to the context, but DO NOT FLUSH. To perform a flush, you must separately
    /// call <see cref="IChannelHandlerContext.Flush()"/>.
    /// </summary>
    public interface IHttp2FrameWriter : IHttp2DataWriter, IDisposable
    {
        /// <summary>
        /// Writes a HEADERS frame to the remote endpoint.
        /// </summary>
        /// <param name="ctx">the context to use for writing.</param>
        /// <param name="streamId">the stream for which to send the frame.</param>
        /// <param name="headers">the headers to be sent.</param>
        /// <param name="padding">additional bytes that should be added to obscure the true content size. Must be between 0 and
        /// 256 (inclusive).</param>
        /// <param name="endOfStream">indicates if this is the last frame to be sent for the stream.</param>
        /// <param name="promise">the promise for the write.</param>
        /// <remarks><a href="https://tools.ietf.org/html/rfc7540#section-10.5.1">Section 10.5.1</a> states the following:
        /// <para>The header block MUST be processed to ensure a consistent connection state, unless the connection is closed.</para>
        /// If this call has modified the HPACK header state you <c>MUST</c> throw a connection error.
        /// If this call has <c>NOT</c> modified the HPACK header state you are free to throw a stream error.
        /// </remarks>
        /// <returns>the future for the write.</returns>
        Task WriteHeadersAsync(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers,
            int padding, bool endOfStream, IPromise promise);

        /// <summary>
        /// Writes a HEADERS frame with priority specified to the remote endpoint.
        /// </summary>
        /// <param name="ctx">the context to use for writing.</param>
        /// <param name="streamId">the stream for which to send the frame.</param>
        /// <param name="headers">the headers to be sent.</param>
        /// <param name="streamDependency">the stream on which this stream should depend, or 0 if it should depend on the connection.</param>
        /// <param name="weight">the weight for this stream.</param>
        /// <param name="exclusive">whether this stream should be the exclusive dependant of its parent.</param>
        /// <param name="padding">additional bytes that should be added to obscure the true content size. Must be between 0 and 256 (inclusive).</param>
        /// <param name="endOfStream">indicates if this is the last frame to be sent for the stream.</param>
        /// <param name="promise">the promise for the write.</param>
        /// <remarks><a href="https://tools.ietf.org/html/rfc7540#section-10.5.1">Section 10.5.1</a> states the following:
        /// <para>The header block MUST be processed to ensure a consistent connection state, unless the connection is closed.</para>
        /// If this call has modified the HPACK header state you <c>MUST</c> throw a connection error.
        /// If this call has <c>NOT</c> modified the HPACK header state you are free to throw a stream error.
        /// </remarks>
        /// <returns>the future for the write.</returns>
        Task WriteHeadersAsync(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers,
            int streamDependency, short weight, bool exclusive, int padding, bool endOfStream, IPromise promise);

        /// <summary>
        /// Writes a PRIORITY frame to the remote endpoint.
        /// </summary>
        /// <param name="ctx">the context to use for writing.</param>
        /// <param name="streamId">the stream for which to send the frame.</param>
        /// <param name="streamDependency">the stream on which this stream should depend, or 0 if it should depend on the connection.</param>
        /// <param name="weight">the weight for this stream.</param>
        /// <param name="exclusive">whether this stream should be the exclusive dependant of its parent.</param>
        /// <param name="promise">the promise for the write.</param>
        /// <returns>the future for the write.</returns>
        Task WritePriorityAsync(IChannelHandlerContext ctx, int streamId, int streamDependency,
            short weight, bool exclusive, IPromise promise);

        /// <summary>
        /// Writes a RST_STREAM frame to the remote endpoint.
        /// </summary>
        /// <param name="ctx">the context to use for writing.</param>
        /// <param name="streamId">the stream for which to send the frame.</param>
        /// <param name="errorCode">the error code indicating the nature of the failure.</param>
        /// <param name="promise">the promise for the write.</param>
        /// <returns>the future for the write.</returns>
        Task WriteRstStreamAsync(IChannelHandlerContext ctx, int streamId, Http2Error errorCode, IPromise promise);

        /// <summary>
        /// Writes a SETTINGS frame to the remote endpoint.
        /// </summary>
        /// <param name="ctx">the context to use for writing.</param>
        /// <param name="settings">the settings to be sent.</param>
        /// <param name="promise">the promise for the write.</param>
        /// <returns>the future for the write.</returns>
        Task WriteSettingsAsync(IChannelHandlerContext ctx, Http2Settings settings, IPromise promise);

        /// <summary>
        /// Writes a SETTINGS acknowledgment to the remote endpoint.
        /// </summary>
        /// <param name="ctx">the context to use for writing.</param>
        /// <param name="promise">the promise for the write.</param>
        /// <returns>the future for the write.</returns>
        Task WriteSettingsAckAsync(IChannelHandlerContext ctx, IPromise promise);

        /// <summary>
        /// Writes a PING frame to the remote endpoint.
        /// </summary>
        /// <param name="ctx">the context to use for writing.</param>
        /// <param name="ack">indicates whether this is an ack of a PING frame previously received from the remote endpoint.</param>
        /// <param name="data">the payload of the frame.</param>
        /// <param name="promise">the promise for the write.</param>
        /// <returns>the future for the write.</returns>
        Task WritePingAsync(IChannelHandlerContext ctx, bool ack, long data, IPromise promise);

        /// <summary>
        /// Writes a PUSH_PROMISE frame to the remote endpoint.
        /// </summary>
        /// <param name="ctx">the context to use for writing.</param>
        /// <param name="streamId">the stream for which to send the frame.</param>
        /// <param name="promisedStreamId">the ID of the promised stream.</param>
        /// <param name="headers">the headers to be sent.</param>
        /// <param name="padding">additional bytes that should be added to obscure the true content size. Must be between 0 and
        /// 256 (inclusive).</param>
        /// <param name="promise">the promise for the write.</param>
        /// <remarks><a href="https://tools.ietf.org/html/rfc7540#section-10.5.1">Section 10.5.1</a> states the following:
        /// <para>The header block MUST be processed to ensure a consistent connection state, unless the connection is closed.</para>
        /// If this call has modified the HPACK header state you <c>MUST</c> throw a connection error.
        /// If this call has <c>NOT</c> modified the HPACK header state you are free to throw a stream error.
        /// </remarks>
        /// <returns>the future for the write.</returns>
        Task WritePushPromiseAsync(IChannelHandlerContext ctx, int streamId, int promisedStreamId,
            IHttp2Headers headers, int padding, IPromise promise);

        /// <summary>
        /// Writes a GO_AWAY frame to the remote endpoint.
        /// </summary>
        /// <param name="ctx">the context to use for writing.</param>
        /// <param name="lastStreamId">the last known stream of this endpoint.</param>
        /// <param name="errorCode">the error code, if the connection was abnormally terminated.</param>
        /// <param name="debugData">application-defined debug data. This will be released by this method.</param>
        /// <param name="promise">the promise for the write.</param>
        /// <returns>the future for the write.</returns>
        Task WriteGoAwayAsync(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode,
            IByteBuffer debugData, IPromise promise);

        /// <summary>
        /// Writes a WINDOW_UPDATE frame to the remote endpoint.
        /// </summary>
        /// <param name="ctx">the context to use for writing.</param>
        /// <param name="streamId">the stream for which to send the frame.</param>
        /// <param name="windowSizeIncrement">the number of bytes by which the local inbound flow control window is increasing.</param>
        /// <param name="promise">the promise for the write.</param>
        /// <returns>the future for the write.</returns>
        Task WriteWindowUpdateAsync(IChannelHandlerContext ctx, int streamId,
            int windowSizeIncrement, IPromise promise);

        /// <summary>
        /// Generic write method for any HTTP/2 frame. This allows writing of non-standard frames.
        /// </summary>
        /// <param name="ctx">the context to use for writing.</param>
        /// <param name="frameType">the frame type identifier.</param>
        /// <param name="streamId">the stream for which to send the frame.</param>
        /// <param name="flags">the flags to write for this frame.</param>
        /// <param name="payload">the payload to write for this frame. This will be released by this method.</param>
        /// <param name="promise">the promise for the write.</param>
        /// <returns>the future for the write.</returns>
        Task WriteFrameAsync(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId,
            Http2Flags flags, IByteBuffer payload, IPromise promise);

        /// <summary>
        /// Get the configuration related elements for this <see cref="IHttp2FrameWriter"/>.
        /// </summary>
        /// <returns></returns>
        IHttp2FrameWriterConfiguration Configuration { get; }

        /// <summary>
        /// Closes this writer and frees any allocated resources.
        /// </summary>
        void Close();
    }
}
