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
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Decorator around a <see cref="IHttp2FrameWriter"/> that logs all outbound frames before calling the writer.
    /// </summary>
    public class Http2OutboundFrameLogger : IHttp2FrameWriter
    {
        private readonly IHttp2FrameWriter _writer;
        private readonly IHttp2FrameLogger _logger;

        public Http2OutboundFrameLogger(IHttp2FrameWriter writer, IHttp2FrameLogger logger)
        {
            if (writer is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.writer); }
            if (logger is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.logger); }
            _writer = writer;
            _logger = logger;
        }

        public void Close() => _writer.Close();

        public IHttp2FrameWriterConfiguration Configuration => _writer.Configuration;

        public void Dispose() => _writer.Dispose();

        public Task WriteDataAsync(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream, IPromise promise)
        {
            _logger.LogData(Direction.Outbound, ctx, streamId, data, padding, endOfStream);
            return _writer.WriteDataAsync(ctx, streamId, data, padding, endOfStream, promise);
        }

        public Task WriteFrameAsync(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId, Http2Flags flags, IByteBuffer payload, IPromise promise)
        {
            _logger.LogUnknownFrame(Direction.Outbound, ctx, frameType, streamId, flags, payload);
            return _writer.WriteFrameAsync(ctx, frameType, streamId, flags, payload, promise);
        }

        public Task WriteGoAwayAsync(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData, IPromise promise)
        {
            _logger.LogGoAway(Direction.Outbound, ctx, lastStreamId, errorCode, debugData);
            return _writer.WriteGoAwayAsync(ctx, lastStreamId, errorCode, debugData, promise);
        }

        public Task WriteHeadersAsync(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream, IPromise promise)
        {
            _logger.LogHeaders(Direction.Outbound, ctx, streamId, headers, padding, endOfStream);
            return _writer.WriteHeadersAsync(ctx, streamId, headers, padding, endOfStream, promise);
        }

        public Task WriteHeadersAsync(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream, IPromise promise)
        {
            _logger.LogHeaders(Direction.Outbound, ctx, streamId, headers, streamDependency, weight, exclusive,
                    padding, endOfStream);
            return _writer.WriteHeadersAsync(ctx, streamId, headers, streamDependency, weight,
                    exclusive, padding, endOfStream, promise);
        }

        public Task WritePingAsync(IChannelHandlerContext ctx, bool ack, long data, IPromise promise)
        {
            if (ack)
            {
                _logger.LogPingAck(Direction.Outbound, ctx, data);
            }
            else
            {
                _logger.LogPing(Direction.Outbound, ctx, data);
            }
            return _writer.WritePingAsync(ctx, ack, data, promise);
        }

        public Task WritePriorityAsync(IChannelHandlerContext ctx, int streamId, int streamDependency, short weight, bool exclusive, IPromise promise)
        {
            _logger.LogPriority(Direction.Outbound, ctx, streamId, streamDependency, weight, exclusive);
            return _writer.WritePriorityAsync(ctx, streamId, streamDependency, weight, exclusive, promise);
        }

        public Task WritePushPromiseAsync(IChannelHandlerContext ctx, int streamId, int promisedStreamId, IHttp2Headers headers, int padding, IPromise promise)
        {
            _logger.LogPushPromise(Direction.Outbound, ctx, streamId, promisedStreamId, headers, padding);
            return _writer.WritePushPromiseAsync(ctx, streamId, promisedStreamId, headers, padding, promise);
        }

        public Task WriteRstStreamAsync(IChannelHandlerContext ctx, int streamId, Http2Error errorCode, IPromise promise)
        {
            _logger.LogRstStream(Direction.Outbound, ctx, streamId, errorCode);
            return _writer.WriteRstStreamAsync(ctx, streamId, errorCode, promise);
        }

        public Task WriteSettingsAckAsync(IChannelHandlerContext ctx, IPromise promise)
        {
            _logger.LogSettingsAck(Direction.Outbound, ctx);
            return _writer.WriteSettingsAckAsync(ctx, promise);
        }

        public Task WriteSettingsAsync(IChannelHandlerContext ctx, Http2Settings settings, IPromise promise)
        {
            _logger.LogSettings(Direction.Outbound, ctx, settings);
            return _writer.WriteSettingsAsync(ctx, settings, promise);
        }

        public Task WriteWindowUpdateAsync(IChannelHandlerContext ctx, int streamId, int windowSizeIncrement, IPromise promise)
        {
            _logger.LogWindowsUpdate(Direction.Outbound, ctx, streamId, windowSizeIncrement);
            return _writer.WriteWindowUpdateAsync(ctx, streamId, windowSizeIncrement, promise);
        }
    }
}
