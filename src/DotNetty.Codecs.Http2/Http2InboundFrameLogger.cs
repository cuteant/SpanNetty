// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Decorator around a <see cref="IHttp2FrameReader"/> that logs all inbound frames before calling back the listener.
    /// </summary>
    public class Http2InboundFrameLogger : IHttp2FrameReader
    {
        private readonly IHttp2FrameReader reader;
        private readonly IHttp2FrameLogger logger;

        public Http2InboundFrameLogger(IHttp2FrameReader reader, IHttp2FrameLogger logger)
        {
            if (reader is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.reader); }
            if (logger is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.logger); }
            this.reader = reader;
            this.logger = logger;
        }

        public void Close() => this.reader.Close();

        public void Dispose() => this.reader.Dispose();

        public IHttp2FrameReaderConfiguration Configuration => this.reader.Configuration;

        public void ReadFrame(IChannelHandlerContext ctx, IByteBuffer input, IHttp2FrameListener listener)
        {
            this.reader.ReadFrame(ctx, input, new DelegatingFrameListener(listener, logger));
        }

        sealed class DelegatingFrameListener : IHttp2FrameListener
        {
            private readonly IHttp2FrameListener listener;
            private readonly IHttp2FrameLogger logger;

            public DelegatingFrameListener(IHttp2FrameListener listener, IHttp2FrameLogger logger)
            {
                this.listener = listener;
                this.logger = logger;
            }

            public int OnDataRead(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream)
            {
                this.logger.LogData(Direction.Inbound, ctx, streamId, data, padding, endOfStream);
                return this.listener.OnDataRead(ctx, streamId, data, padding, endOfStream);
            }

            public void OnGoAwayRead(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData)
            {
                this.logger.LogGoAway(Direction.Inbound, ctx, lastStreamId, errorCode, debugData);
                this.listener.OnGoAwayRead(ctx, lastStreamId, errorCode, debugData);
            }

            public void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream)
            {
                this.logger.LogHeaders(Direction.Inbound, ctx, streamId, headers, padding, endOfStream);
                this.listener.OnHeadersRead(ctx, streamId, headers, padding, endOfStream);
            }

            public void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream)
            {
                this.logger.LogHeaders(Direction.Inbound, ctx, streamId, headers, streamDependency, weight, exclusive,
                        padding, endOfStream);
                this.listener.OnHeadersRead(ctx, streamId, headers, streamDependency, weight, exclusive,
                        padding, endOfStream);
            }

            public void OnPingAckRead(IChannelHandlerContext ctx, long data)
            {
                this.logger.LogPingAck(Direction.Inbound, ctx, data);
                this.listener.OnPingAckRead(ctx, data);
            }

            public void OnPingRead(IChannelHandlerContext ctx, long data)
            {
                this.logger.LogPing(Direction.Inbound, ctx, data);
                this.listener.OnPingRead(ctx, data);
            }

            public void OnPriorityRead(IChannelHandlerContext ctx, int streamId, int streamDependency, short weight, bool exclusive)
            {
                this.logger.LogPriority(Direction.Inbound, ctx, streamId, streamDependency, weight, exclusive);
                this.listener.OnPriorityRead(ctx, streamId, streamDependency, weight, exclusive);
            }

            public void OnPushPromiseRead(IChannelHandlerContext ctx, int streamId, int promisedStreamId, IHttp2Headers headers, int padding)
            {
                this.logger.LogPushPromise(Direction.Inbound, ctx, streamId, promisedStreamId, headers, padding);
                this.listener.OnPushPromiseRead(ctx, streamId, promisedStreamId, headers, padding);
            }

            public void OnRstStreamRead(IChannelHandlerContext ctx, int streamId, Http2Error errorCode)
            {
                this.logger.LogRstStream(Direction.Inbound, ctx, streamId, errorCode);
                this.listener.OnRstStreamRead(ctx, streamId, errorCode);
            }

            public void OnSettingsAckRead(IChannelHandlerContext ctx)
            {
                this.logger.LogSettingsAck(Direction.Inbound, ctx);
                this.listener.OnSettingsAckRead(ctx);
            }

            public void OnSettingsRead(IChannelHandlerContext ctx, Http2Settings settings)
            {
                this.logger.LogSettings(Direction.Inbound, ctx, settings);
                this.listener.OnSettingsRead(ctx, settings);
            }

            public void OnUnknownFrame(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId, Http2Flags flags, IByteBuffer payload)
            {
                this.logger.LogUnknownFrame(Direction.Inbound, ctx, frameType, streamId, flags, payload);
                this.listener.OnUnknownFrame(ctx, frameType, streamId, flags, payload);
            }

            public void OnWindowUpdateRead(IChannelHandlerContext ctx, int streamId, int windowSizeIncrement)
            {
                this.logger.LogWindowsUpdate(Direction.Inbound, ctx, streamId, windowSizeIncrement);
                this.listener.OnWindowUpdateRead(ctx, streamId, windowSizeIncrement);
            }
        }
    }
}
