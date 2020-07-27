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

    /// <summary>
    /// Manager for the state of an HTTP/2 connection with the remote end-point.
    /// </summary>
    public interface IHttp2Connection
    {
        Task CloseCompletion { get; }

        /// <summary>
        /// Close this connection. No more new streams can be created after this point and
        /// all streams that exists (active or otherwise) will be closed and removed.
        /// 
        /// <para>Note if iterating active streams via <see cref="ForEachActiveStream(IHttp2StreamVisitor)"/> and an exception is
        /// thrown it is necessary to call this method again to ensure the close completes.</para>
        /// </summary>
        /// <param name="promise">Will be completed when all streams have been removed, and listeners have been notified.</param>
        /// <returns>A future that will be completed when all streams have been removed, and listeners have been notified.</returns>
        Task CloseAsync(IPromise promise);

        /// <summary>
        /// Creates a new key that is unique within this <see cref="IHttp2Connection"/>.
        /// </summary>
        IHttp2ConnectionPropertyKey NewKey();

        /// <summary>
        /// Adds a listener of stream life-cycle events.
        /// </summary>
        /// <param name="listener"></param>
        void AddListener(IHttp2ConnectionListener listener);

        /// <summary>
        /// Removes a listener of stream life-cycle events. If the same listener was added multiple times
        /// then only the first occurrence gets removed.
        /// </summary>
        /// <param name="listener"></param>
        void RemoveListener(IHttp2ConnectionListener listener);

        /// <summary>
        /// Gets the stream if it exists. If not, returns <c>null</c>.
        /// </summary>
        /// <param name="streamId"></param>
        /// <returns></returns>
        IHttp2Stream Stream(int streamId);

        /// <summary>
        /// Indicates whether or not the given stream may have existed within this connection. This is a short form
        /// for calling <see cref="IHttp2ConnectionEndpoint.MayHaveCreatedStream(int)"/> on both endpoints.
        /// </summary>
        /// <param name="streamId"></param>
        /// <returns></returns>
        bool StreamMayHaveExisted(int streamId);

        /// <summary>
        /// Gets the stream object representing the connection, itself (i.e. stream zero). This object
        /// always exists.
        /// </summary>
        IHttp2Stream ConnectionStream { get; }

        /// <summary>
        /// Gets the number of streams that are actively in use (i.e. <c>OPEN</c> or <c>HALF CLOSED</c>).
        /// </summary>
        int NumActiveStreams { get; }

        /// <summary>
        /// Provide a means of iterating over the collection of active streams.
        /// </summary>
        /// <param name="visitor">The visitor which will visit each active stream.</param>
        /// <returns>The stream before iteration stopped or <c>null</c> if iteration went past the end.</returns>
        IHttp2Stream ForEachActiveStream(IHttp2StreamVisitor visitor);
        /// <summary>
        /// Provide a means of iterating over the collection of active streams.
        /// </summary>
        /// <param name="visitor">The visitor which will visit each active stream.</param>
        /// <returns>The stream before iteration stopped or <c>null</c> if iteration went past the end.</returns>
        IHttp2Stream ForEachActiveStream(Func<IHttp2Stream, bool> visitor);

        /// <summary>
        /// Indicates whether or not the local endpoint for this connection is the server.
        /// </summary>
        bool IsServer { get; }

        /// <summary>
        /// Gets a view of this connection from the local <see cref="IHttp2ConnectionEndpoint"/>.
        /// </summary>
        IHttp2ConnectionEndpoint<IHttp2LocalFlowController> Local { get; }

        /// <summary>
        /// Gets a view of this connection from the remote <see cref="IHttp2ConnectionEndpoint"/>.
        /// </summary>
        IHttp2ConnectionEndpoint<IHttp2RemoteFlowController> Remote { get; }

        /// <summary>
        /// Indicates whether or not a <c>GOAWAY</c> was received from the remote endpoint.
        /// </summary>
        bool GoAwayReceived();

        /// <summary>
        /// Indicates that a <c>GOAWAY</c> was received from the remote endpoint and sets the last known stream.
        /// </summary>
        /// <param name="lastKnownStream">The Last-Stream-ID in the
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.8">GOAWAY</a> frame.</param>
        /// <param name="errorCode">the Error Code in the
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.8">GOAWAY</a> frame.</param>
        /// <param name="message">The Additional Debug Data in the
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.8">GOAWAY</a> frame. Note that reference count ownership
        /// belongs to the caller (ownership is not transferred to this method).</param>
        void GoAwayReceived(int lastKnownStream, Http2Error errorCode, IByteBuffer message);

        /// <summary>
        /// Indicates whether or not a <c>GOAWAY</c> was sent to the remote endpoint.
        /// </summary>
        bool GoAwaySent();

        /// <summary>
        /// Updates the local state of this <see cref="IHttp2Connection"/> as a result of a <c>GOAWAY</c> to send to the remote
        /// endpoint.
        /// </summary>
        /// <param name="lastKnownStream">The Last-Stream-ID in the
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.8">GOAWAY</a> frame.</param>
        /// <param name="errorCode">the Error Code in the
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.8">GOAWAY</a> frame.</param>
        /// <param name="message">The Additional Debug Data in the
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.8">GOAWAY</a> frame. Note that reference count ownership
        /// belongs to the caller (ownership is not transferred to this method).</param>
        /// <returns><c>true</c> if the corresponding <c>GOAWAY</c> frame should be sent to the remote endpoint.</returns>
        bool GoAwaySent(int lastKnownStream, Http2Error errorCode, IByteBuffer message);
    }
}
