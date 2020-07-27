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
    /// Manager for the life cycle of the HTTP/2 connection. Handles graceful shutdown of the channel,
    /// closing only after all of the streams have closed.
    /// </summary>
    public interface IHttp2LifecycleManager
    {
        /// <summary>
        /// Closes the local side of the <paramref name="stream"/>. Depending on the <paramref name="stream"/> state this may result in
        /// <paramref name="stream"/> being closed. See <see cref="CloseStream(IHttp2Stream, Task)"/>.
        /// </summary>
        /// <param name="stream">the stream to be half closed.</param>
        /// <param name="future">See <see cref="CloseStream(IHttp2Stream, Task)"/>.</param>
        void CloseStreamLocal(IHttp2Stream stream, Task future);

        /// <summary>
        /// Closes the remote side of the <paramref name="stream"/>. Depending on the <paramref name="stream"/> state this may result in
        /// <paramref name="stream"/> being closed. See <see cref="CloseStream(IHttp2Stream, Task)"/>.
        /// </summary>
        /// <param name="stream">the stream to be half closed.</param>
        /// <param name="future">See <see cref="CloseStream(IHttp2Stream, Task)"/>.</param>
        void CloseStreamRemote(IHttp2Stream stream, Task future);

        /// <summary>
        /// Closes and deactivates the given <paramref name="stream"/>. A listener is also attached to <paramref name="future"/> and upon
        /// completion the underlying channel will be closed if <see cref="IHttp2Connection.NumActiveStreams"/> is 0.
        /// </summary>
        /// <param name="stream">the stream to be closed and deactivated.</param>
        /// <param name="future">when completed if <see cref="IHttp2Connection.NumActiveStreams"/> is 0 then the underlying channel
        /// will be closed.</param>
        void CloseStream(IHttp2Stream stream, Task future);

        /// <summary>
        /// Ensure the stream identified by <paramref name="streamId"/> is reset. If our local state does not indicate the stream has
        /// been reset yet then a <c>RST_STREAM</c> will be sent to the peer. If our local state indicates the stream
        /// has already been reset then the return status will indicate success without sending anything to the peer.
        /// </summary>
        /// <param name="ctx">The context used for communication and buffer allocation if necessary.</param>
        /// <param name="streamId">The identifier of the stream to reset.</param>
        /// <param name="errorCode">Justification as to why this stream is being reset. See <see cref="Http2Error"/>.</param>
        /// <param name="promise">Used to indicate the return status of this operation.</param>
        /// <returns>Will be considered successful when the connection and stream state has been updated, and a
        /// <c>RST_STREAM</c> frame has been sent to the peer. If the stream state has already been updated and a
        /// <c>RST_STREAM</c> frame has been sent then the return status may indicate success immediately.</returns>
        Task ResetStreamAsync(IChannelHandlerContext ctx, int streamId, Http2Error errorCode, IPromise promise);

        /// <summary>
        /// Prevents the peer from creating streams and close the connection if <paramref name="errorCode"/> is not
        /// <see cref="Http2Error.NoError"/>. After this call the peer is not allowed to create any new streams and the local
        /// endpoint will be limited to creating streams with <![CDATA[stream identifier <= lastStreamId]]>. This may result in
        /// sending a <c>GO_AWAY</c> frame (assuming we have not already sent one with
        /// <![CDATA[Last-Stream-ID <= lastStreamId]]>, or may just return success if a <c>GO_AWAY</c> has previously been sent.
        /// </summary>
        /// <param name="ctx">The context used for communication and buffer allocation if necessary.</param>
        /// <param name="lastStreamId">The last stream that the local endpoint is claiming it will accept.</param>
        /// <param name="errorCode">The rational as to why the connection is being closed. See <see cref="Http2Error"/>.</param>
        /// <param name="debugData">For diagnostic purposes (carries no semantic value).</param>
        /// <param name="promise">Used to indicate the return status of this operation.</param>
        /// <returns>Will be considered successful when the connection and stream state has been updated, and a
        /// <c>GO_AWAY</c> frame has been sent to the peer. If the stream state has already been updated and a
        /// <c>GO_AWAY</c> frame has been sent then the return status may indicate success immediately.</returns>
        Task GoAwayAsync(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData, IPromise promise);

        /// <summary>
        /// Processes the given error.
        /// </summary>
        /// <param name="ctx">The context used for communication and buffer allocation if necessary.</param>
        /// <param name="outbound"><c>true</c> if the error was caused by an outbound operation and so the corresponding
        /// <see cref="IPromise"/> was failed as well.</param>
        /// <param name="cause">the error.</param>
        void OnError(IChannelHandlerContext ctx, bool outbound, Exception cause);
    }
}
