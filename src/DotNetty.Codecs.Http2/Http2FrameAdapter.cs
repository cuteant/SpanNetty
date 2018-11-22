// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Convenience class that provides no-op implementations for all methods of <see cref="IHttp2FrameListener"/>.
    /// </summary>
    public class Http2FrameAdapter : IHttp2FrameListener
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
    }
}
