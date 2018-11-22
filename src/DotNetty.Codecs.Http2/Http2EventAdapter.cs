// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
