// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Runtime.CompilerServices;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using Microsoft.Extensions.Logging;
    using DotNettyLoggerFactory = DotNetty.Common.Internal.Logging.InternalLoggerFactory;

    /// <summary>
    /// Logs HTTP2 frames for debugging purposes.
    /// </summary>
    public class Http2FrameMsLogger : ChannelHandlerAdapter, IHttp2FrameLogger
    {
        private const int BufferLengthThreshold = 64;
        private readonly ILogger _logger;
        private readonly LogLevel _level;

        public Http2FrameMsLogger(LogLevel level)
            : this(level, DotNettyLoggerFactory.DefaultFactory.CreateLogger<Http2FrameMsLogger>())
        {
        }

        public Http2FrameMsLogger(LogLevel level, string name)
            : this(level, DotNettyLoggerFactory.DefaultFactory.CreateLogger(name))
        {
        }

        public Http2FrameMsLogger(LogLevel level, Type type)
            : this(level, DotNettyLoggerFactory.DefaultFactory.CreateLogger(type))
        {
        }

        private Http2FrameMsLogger(LogLevel level, ILogger logger)
        {
            if (logger is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.logger); }
            _level = level;
            _logger = logger;
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        public bool IsEnabled() => _logger.IsEnabled(_level);

        public void LogData(Direction direction, IChannelHandlerContext ctx, int streamId, IByteBuffer data,
            int padding, bool endStream)
        {
            if (IsEnabled())
            {
                _logger.Log(_level, "{0} {1} DATA: streamId={2} padding={3} endStream={4} length={5} bytes={6}",
                    ctx.Channel, direction, streamId, padding, endStream, data.ReadableBytes, ToString(data));
            }
        }

        public void LogHeaders(Direction direction, IChannelHandlerContext ctx, int streamId, IHttp2Headers headers,
            int padding, bool endStream)
        {
            if (IsEnabled())
            {
                _logger.Log(_level, "{0} {1} HEADERS: streamId={2} headers={3} padding={4} endStream={5}",
                    ctx.Channel, direction, streamId, headers, padding, endStream);
            }
        }

        public void LogHeaders(Direction direction, IChannelHandlerContext ctx, int streamId, IHttp2Headers headers,
            int streamDependency, short weight, bool exclusive, int padding, bool endStream)
        {
            if (IsEnabled())
            {
                _logger.Log(_level,
                    "{0} {1} HEADERS: streamId={2} headers={3} streamDependency={4} weight={5} exclusive={6} padding={7} endStream={8}",
                    ctx.Channel, direction, streamId, headers, streamDependency, weight, exclusive, padding, endStream);
            }
        }

        public void LogPriority(Direction direction, IChannelHandlerContext ctx, int streamId, int streamDependency,
            short weight, bool exclusive)
        {
            if (IsEnabled())
            {
                _logger.Log(_level, "{0} {1} PRIORITY: streamId={2} streamDependency={3} weight={4} exclusive={5}",
                    ctx.Channel, direction, streamId, streamDependency, weight, exclusive);
            }
        }

        public void LogRstStream(Direction direction, IChannelHandlerContext ctx, int streamId, Http2Error errorCode)
        {
            if (IsEnabled())
            {
                _logger.Log(_level, "{0} {1} RST_STREAM: streamId={2} errorCode={3}",
                    ctx.Channel, direction, streamId, errorCode);
            }
        }

        public void LogSettingsAck(Direction direction, IChannelHandlerContext ctx)
        {
            _logger.Log(_level, "{0} {1} SETTINGS: ack=true", ctx.Channel, direction);
        }

        public void LogSettings(Direction direction, IChannelHandlerContext ctx, Http2Settings settings)
        {
            if (IsEnabled())
            {
                _logger.Log(_level, "{0} {1} SETTINGS: ack=false settings={2}", ctx.Channel, direction, settings);
            }
        }

        public void LogPing(Direction direction, IChannelHandlerContext ctx, long data)
        {
            if (IsEnabled())
            {
                _logger.Log(_level, "{0} {1} PING: ack=false bytes={2}", ctx.Channel, direction, data);
            }
        }

        public void LogPingAck(Direction direction, IChannelHandlerContext ctx, long data)
        {
            if (IsEnabled())
            {
                _logger.Log(_level, "{0} {1} PING: ack=true bytes={2}", ctx.Channel, direction, data);
            }
        }

        public void LogPushPromise(Direction direction, IChannelHandlerContext ctx, int streamId, int promisedStreamId,
            IHttp2Headers headers, int padding)
        {
            if (IsEnabled())
            {
                _logger.Log(_level, "{0} {1} PUSH_PROMISE: streamId={2} promisedStreamId={3} headers={4} padding={5}",
                    ctx.Channel, direction, streamId, promisedStreamId, headers, padding);
            }
        }

        public void LogGoAway(Direction direction, IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData)
        {
            if (IsEnabled())
            {
                _logger.Log(_level, "{0} {1} GO_AWAY: lastStreamId={2} errorCode={3} length={4} bytes={5}",
                    ctx.Channel, direction, lastStreamId, errorCode, debugData.ReadableBytes, ToString(debugData));
            }
        }

        public void LogWindowsUpdate(Direction direction, IChannelHandlerContext ctx, int streamId, int windowSizeIncrement)
        {
            if (IsEnabled())
            {
                _logger.Log(_level, "{0} {1} WINDOW_UPDATE: streamId={2} windowSizeIncrement={3}",
                    ctx.Channel, direction, streamId, windowSizeIncrement);
            }
        }

        public void LogUnknownFrame(Direction direction, IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId,
            Http2Flags flags, IByteBuffer data)
        {
            if (IsEnabled())
            {
                _logger.Log(_level, "{0} {1} UNKNOWN: frameType={2} streamId={3} flags={4} length={5} bytes={6}",
                    ctx.Channel, direction, (byte)frameType & 0xFF, streamId, flags.Value, data.ReadableBytes, ToString(data));
            }
        }

        private string ToString(IByteBuffer buf)
        {
            if (_level == LogLevel.Trace || buf.ReadableBytes <= BufferLengthThreshold)
            {
                // Log the entire buffer.
                return ByteBufferUtil.HexDump(buf);
            }

            // Otherwise just log the first 64 bytes.
            int length = Math.Min(buf.ReadableBytes, BufferLengthThreshold);
            return ByteBufferUtil.HexDump(buf, buf.ReaderIndex, length) + "...";
        }

    }
}
