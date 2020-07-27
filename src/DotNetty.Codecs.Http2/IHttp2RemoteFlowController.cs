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
    using DotNetty.Transport.Channels;

    /// <summary>
    /// A <see cref="IHttp2FlowController"/> for controlling the flow of outbound <c>DATA</c> frames to the remote
    /// endpoint.
    /// </summary>
    public interface IHttp2RemoteFlowController : IHttp2FlowController
    {
        /// <summary>
        /// Get the <see cref="IChannelHandlerContext"/> for which to apply flow control on.
        /// <para>This is intended for us by <see cref="IHttp2RemoteFlowControlled"/> implementations only. Use with caution.</para>
        /// </summary>
        /// <returns>The <see cref="IChannelHandlerContext"/> for which to apply flow control on.</returns>
        IChannelHandlerContext ChannelHandlerContext { get; }

        /// <summary>
        /// Queues a payload for transmission to the remote endpoint. There is no guarantee as to when the data
        /// will be written or how it will be assigned to frames.
        /// before sending.
        /// <para>Writes do not actually occur until <see cref="WritePendingBytes()"/> is called.</para>
        /// </summary>
        /// <param name="stream">the subject stream. Must not be the connection stream object.</param>
        /// <param name="payload">payload to write subject to flow-control accounting and ordering rules.</param>
        void AddFlowControlled(IHttp2Stream stream, IHttp2RemoteFlowControlled payload);

        /// <summary>
        /// Determine if <paramref name="stream"/> has any <see cref="IHttp2RemoteFlowControlled"/> frames currently queued.
        /// </summary>
        /// <param name="stream">the stream to check if it has flow controlled frames.</param>
        /// <returns><c>true</c> if <paramref name="stream"/> has any <see cref="IHttp2RemoteFlowControlled"/> frames currently queued.</returns>
        bool HasFlowControlled(IHttp2Stream stream);

        /// <summary>
        /// Write all data pending in the flow controller up to the flow-control limits.
        /// </summary>
        /// <exception cref="Http2Exception">throws if a protocol-related error occurred.</exception>
        void WritePendingBytes();

        /// <summary>
        /// Set the active listener on the flow-controller.
        /// </summary>
        /// <param name="listener">listener to notify when the a write occurs, can be <c>null</c>.</param>
        void Listener(IHttp2RemoteFlowControllerListener listener);

        /// <summary>
        /// Determine if the <paramref name="stream"/> has bytes remaining for use in the flow control window.
        /// <para>Note that this method respects channel writability. The channel must be writable for this method to
        /// return <c>true</c>.</para>
        /// </summary>
        /// <param name="stream">The stream to test.</param>
        /// <returns><c>true</c> if the <paramref name="stream"/> has bytes remaining for use in the flow control window and the
        /// channel is writable, <c>false</c> otherwise.</returns>
        bool IsWritable(IHttp2Stream stream);

        /// <summary>
        /// Notification that the writability of <see cref="ChannelHandlerContext"/> has changed.
        /// </summary>
        /// <exception cref="Http2Exception">If any writes occur as a result of this call and encounter errors.</exception>
        void ChannelWritabilityChanged();

        /// <summary>
        /// Explicitly update the dependency tree. This method is called independently of stream state changes.
        /// </summary>
        /// <param name="childStreamId">The stream identifier associated with the child stream.</param>
        /// <param name="parentStreamId">The stream identifier associated with the parent stream. May be <c>0</c>,
        /// to make <paramref name="childStreamId"/> and immediate child of the connection.</param>
        /// <param name="weight">The weight which is used relative to other child streams for <paramref name="parentStreamId"/>. This value
        /// must be between 1 and 256 (inclusive).</param>
        /// <param name="exclusive">If <paramref name="childStreamId"/> should be the exclusive dependency of <paramref name="parentStreamId"/>.</param>
        void UpdateDependencyTree(int childStreamId, int parentStreamId, short weight, bool exclusive);
    }
}
