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
        private readonly IHttp2FrameReader _reader;
        private readonly IHttp2FrameLogger _logger;

        public Http2InboundFrameLogger(IHttp2FrameReader reader, IHttp2FrameLogger logger)
        {
            if (reader is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.reader); }
            if (logger is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.logger); }
            _reader = reader;
            _logger = logger;
        }

        public void Close() => _reader.Close();

        public void Dispose() => _reader.Dispose();

        public IHttp2FrameReaderConfiguration Configuration => _reader.Configuration;

        public void ReadFrame(IChannelHandlerContext ctx, IByteBuffer input, IHttp2FrameListener listener)
        {
            _reader.ReadFrame(ctx, input, new DelegatingFrameListener(listener, _logger));
        }

        sealed class DelegatingFrameListener : IHttp2FrameListener
        {
            private readonly IHttp2FrameListener _listener;
            private readonly IHttp2FrameLogger _logger;

            public DelegatingFrameListener(IHttp2FrameListener listener, IHttp2FrameLogger logger)
            {
                _listener = listener;
                _logger = logger;
            }

            public int OnDataRead(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream)
            {
                _logger.LogData(Direction.Inbound, ctx, streamId, data, padding, endOfStream);
                return _listener.OnDataRead(ctx, streamId, data, padding, endOfStream);
            }

            public void OnGoAwayRead(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData)
            {
                _logger.LogGoAway(Direction.Inbound, ctx, lastStreamId, errorCode, debugData);
                _listener.OnGoAwayRead(ctx, lastStreamId, errorCode, debugData);
            }

            public void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream)
            {
                _logger.LogHeaders(Direction.Inbound, ctx, streamId, headers, padding, endOfStream);
                _listener.OnHeadersRead(ctx, streamId, headers, padding, endOfStream);
            }

            public void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream)
            {
                _logger.LogHeaders(Direction.Inbound, ctx, streamId, headers, streamDependency, weight, exclusive,
                        padding, endOfStream);
                _listener.OnHeadersRead(ctx, streamId, headers, streamDependency, weight, exclusive,
                        padding, endOfStream);
            }

            public void OnPingAckRead(IChannelHandlerContext ctx, long data)
            {
                _logger.LogPingAck(Direction.Inbound, ctx, data);
                _listener.OnPingAckRead(ctx, data);
            }

            public void OnPingRead(IChannelHandlerContext ctx, long data)
            {
                _logger.LogPing(Direction.Inbound, ctx, data);
                _listener.OnPingRead(ctx, data);
            }

            public void OnPriorityRead(IChannelHandlerContext ctx, int streamId, int streamDependency, short weight, bool exclusive)
            {
                _logger.LogPriority(Direction.Inbound, ctx, streamId, streamDependency, weight, exclusive);
                _listener.OnPriorityRead(ctx, streamId, streamDependency, weight, exclusive);
            }

            public void OnPushPromiseRead(IChannelHandlerContext ctx, int streamId, int promisedStreamId, IHttp2Headers headers, int padding)
            {
                _logger.LogPushPromise(Direction.Inbound, ctx, streamId, promisedStreamId, headers, padding);
                _listener.OnPushPromiseRead(ctx, streamId, promisedStreamId, headers, padding);
            }

            public void OnRstStreamRead(IChannelHandlerContext ctx, int streamId, Http2Error errorCode)
            {
                _logger.LogRstStream(Direction.Inbound, ctx, streamId, errorCode);
                _listener.OnRstStreamRead(ctx, streamId, errorCode);
            }

            public void OnSettingsAckRead(IChannelHandlerContext ctx)
            {
                _logger.LogSettingsAck(Direction.Inbound, ctx);
                _listener.OnSettingsAckRead(ctx);
            }

            public void OnSettingsRead(IChannelHandlerContext ctx, Http2Settings settings)
            {
                _logger.LogSettings(Direction.Inbound, ctx, settings);
                _listener.OnSettingsRead(ctx, settings);
            }

            public void OnUnknownFrame(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId, Http2Flags flags, IByteBuffer payload)
            {
                _logger.LogUnknownFrame(Direction.Inbound, ctx, frameType, streamId, flags, payload);
                _listener.OnUnknownFrame(ctx, frameType, streamId, flags, payload);
            }

            public void OnWindowUpdateRead(IChannelHandlerContext ctx, int streamId, int windowSizeIncrement)
            {
                _logger.LogWindowsUpdate(Direction.Inbound, ctx, streamId, windowSizeIncrement);
                _listener.OnWindowUpdateRead(ctx, streamId, windowSizeIncrement);
            }
        }
    }
}
