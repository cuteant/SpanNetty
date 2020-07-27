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
    using DotNetty.Transport.Channels;

    /// <summary>
    /// This class brings <see cref="IHttp2ConnectionListener"/> and <see cref="IHttp2FrameListener"/> together to provide
    /// NOOP implementation so inheriting classes can selectively choose which methods to override.
    /// </summary>
    public class Http2EventAdapter : IHttp2ConnectionListener, IHttp2FrameListener
    {
        public virtual int OnDataRead(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream)
        {
            return data.ReadableBytes + padding;
        }

        public virtual void OnGoAwayRead(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData)
        {
        }

        public virtual void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream)
        {
        }

        public virtual void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream)
        {
        }

        public virtual void OnPingAckRead(IChannelHandlerContext ctx, long data)
        {
        }

        public virtual void OnPingRead(IChannelHandlerContext ctx, long data)
        {
        }

        public virtual void OnPriorityRead(IChannelHandlerContext ctx, int streamId, int streamDependency, short weight, bool exclusive)
        {
        }

        public virtual void OnPushPromiseRead(IChannelHandlerContext ctx, int streamId, int promisedStreamId, IHttp2Headers headers, int padding)
        {
        }

        public virtual void OnRstStreamRead(IChannelHandlerContext ctx, int streamId, Http2Error errorCode)
        {
        }

        public virtual void OnSettingsAckRead(IChannelHandlerContext ctx)
        {
        }

        public virtual void OnSettingsRead(IChannelHandlerContext ctx, Http2Settings settings)
        {
        }

        public virtual void OnUnknownFrame(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId, Http2Flags flags, IByteBuffer payload)
        {
        }

        public virtual void OnWindowUpdateRead(IChannelHandlerContext ctx, int streamId, int windowSizeIncrement)
        {
        }


        public virtual void OnGoAwayReceived(int lastStreamId, Http2Error errorCode, IByteBuffer debugData)
        {
        }

        public virtual void OnGoAwaySent(int lastStreamId, Http2Error errorCode, IByteBuffer debugData)
        {
        }

        public virtual void OnStreamActive(IHttp2Stream stream)
        {
        }

        public virtual void OnStreamAdded(IHttp2Stream stream)
        {
        }

        public virtual void OnStreamClosed(IHttp2Stream stream)
        {
        }

        public virtual void OnStreamHalfClosed(IHttp2Stream stream)
        {
        }

        public virtual void OnStreamRemoved(IHttp2Stream stream)
        {
        }
    }
}
