// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Provides a decorator around a <see cref="IHttp2FrameListener"/> and delegates all method calls
    /// </summary>
    public class Http2FrameListenerDecorator : IHttp2FrameListener
    {
        protected internal readonly IHttp2FrameListener _listener;

        public Http2FrameListenerDecorator(IHttp2FrameListener listener)
        {
            if (listener is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.listener); }
            _listener = listener;
        }

        public virtual int OnDataRead(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream)
        {
            return _listener.OnDataRead(ctx, streamId, data, padding, endOfStream);
        }

        public virtual void OnGoAwayRead(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData)
        {
            _listener.OnGoAwayRead(ctx, lastStreamId, errorCode, debugData);
        }

        public virtual void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream)
        {
            _listener.OnHeadersRead(ctx, streamId, headers, padding, endOfStream);
        }

        public virtual void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream)
        {
            _listener.OnHeadersRead(ctx, streamId, headers, streamDependency, weight, exclusive, padding, endOfStream);
        }

        public virtual void OnPingAckRead(IChannelHandlerContext ctx, long data)
        {
            _listener.OnPingAckRead(ctx, data);
        }

        public virtual void OnPingRead(IChannelHandlerContext ctx, long data)
        {
            _listener.OnPingRead(ctx, data);
        }

        public virtual void OnPriorityRead(IChannelHandlerContext ctx, int streamId, int streamDependency, short weight, bool exclusive)
        {
            _listener.OnPriorityRead(ctx, streamId, streamDependency, weight, exclusive);
        }

        public virtual void OnPushPromiseRead(IChannelHandlerContext ctx, int streamId, int promisedStreamId, IHttp2Headers headers, int padding)
        {
            _listener.OnPushPromiseRead(ctx, streamId, promisedStreamId, headers, padding);
        }

        public virtual void OnRstStreamRead(IChannelHandlerContext ctx, int streamId, Http2Error errorCode)
        {
            _listener.OnRstStreamRead(ctx, streamId, errorCode);
        }

        public virtual void OnSettingsAckRead(IChannelHandlerContext ctx)
        {
            _listener.OnSettingsAckRead(ctx);
        }

        public virtual void OnSettingsRead(IChannelHandlerContext ctx, Http2Settings settings)
        {
            _listener.OnSettingsRead(ctx, settings);
        }

        public virtual void OnUnknownFrame(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId, Http2Flags flags, IByteBuffer payload)
        {
            _listener.OnUnknownFrame(ctx, frameType, streamId, flags, payload);
        }

        public virtual void OnWindowUpdateRead(IChannelHandlerContext ctx, int streamId, int windowSizeIncrement)
        {
            _listener.OnWindowUpdateRead(ctx, streamId, windowSizeIncrement);
        }
    }
}
