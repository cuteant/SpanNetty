// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        private readonly IHttp2FrameWriter writer;
        private readonly IHttp2FrameLogger logger;

        public Http2OutboundFrameLogger(IHttp2FrameWriter writer, IHttp2FrameLogger logger)
        {
            if (null == writer) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.writer); }
            if (null == logger) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.logger); }
            this.writer = writer;
            this.logger = logger;
        }

        public void Close() => this.writer.Close();

        public IHttp2FrameWriterConfiguration Configuration => this.writer.Configuration;

        public void Dispose() => this.writer.Dispose();

        public Task WriteDataAsync(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream, IPromise promise)
        {
            this.logger.LogData(Direction.Outbound, ctx, streamId, data, padding, endOfStream);
            return writer.WriteDataAsync(ctx, streamId, data, padding, endOfStream, promise);
        }

        public Task WriteFrameAsync(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId, Http2Flags flags, IByteBuffer payload, IPromise promise)
        {
            this.logger.LogUnknownFrame(Direction.Outbound, ctx, frameType, streamId, flags, payload);
            return writer.WriteFrameAsync(ctx, frameType, streamId, flags, payload, promise);
        }

        public Task WriteGoAwayAsync(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData, IPromise promise)
        {
            this.logger.LogGoAway(Direction.Outbound, ctx, lastStreamId, errorCode, debugData);
            return writer.WriteGoAwayAsync(ctx, lastStreamId, errorCode, debugData, promise);
        }

        public Task WriteHeadersAsync(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream, IPromise promise)
        {
            this.logger.LogHeaders(Direction.Outbound, ctx, streamId, headers, padding, endOfStream);
            return writer.WriteHeadersAsync(ctx, streamId, headers, padding, endOfStream, promise);
        }

        public Task WriteHeadersAsync(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream, IPromise promise)
        {
            this.logger.LogHeaders(Direction.Outbound, ctx, streamId, headers, streamDependency, weight, exclusive,
                    padding, endOfStream);
            return writer.WriteHeadersAsync(ctx, streamId, headers, streamDependency, weight,
                    exclusive, padding, endOfStream, promise);
        }

        public Task WritePingAsync(IChannelHandlerContext ctx, bool ack, long data, IPromise promise)
        {
            if (ack)
            {
                this.logger.LogPingAck(Direction.Outbound, ctx, data);
            }
            else
            {
                this.logger.LogPing(Direction.Outbound, ctx, data);
            }
            return writer.WritePingAsync(ctx, ack, data, promise);
        }

        public Task WritePriorityAsync(IChannelHandlerContext ctx, int streamId, int streamDependency, short weight, bool exclusive, IPromise promise)
        {
            this.logger.LogPriority(Direction.Outbound, ctx, streamId, streamDependency, weight, exclusive);
            return writer.WritePriorityAsync(ctx, streamId, streamDependency, weight, exclusive, promise);
        }

        public Task WritePushPromiseAsync(IChannelHandlerContext ctx, int streamId, int promisedStreamId, IHttp2Headers headers, int padding, IPromise promise)
        {
            this.logger.LogPushPromise(Direction.Outbound, ctx, streamId, promisedStreamId, headers, padding);
            return writer.WritePushPromiseAsync(ctx, streamId, promisedStreamId, headers, padding, promise);
        }

        public Task WriteRstStreamAsync(IChannelHandlerContext ctx, int streamId, Http2Error errorCode, IPromise promise)
        {
            this.logger.LogRstStream(Direction.Outbound, ctx, streamId, errorCode);
            return writer.WriteRstStreamAsync(ctx, streamId, errorCode, promise);
        }

        public Task WriteSettingsAckAsync(IChannelHandlerContext ctx, IPromise promise)
        {
            this.logger.LogSettingsAck(Direction.Outbound, ctx);
            return writer.WriteSettingsAckAsync(ctx, promise);
        }

        public Task WriteSettingsAsync(IChannelHandlerContext ctx, Http2Settings settings, IPromise promise)
        {
            this.logger.LogSettings(Direction.Outbound, ctx, settings);
            return writer.WriteSettingsAsync(ctx, settings, promise);
        }

        public Task WriteWindowUpdateAsync(IChannelHandlerContext ctx, int streamId, int windowSizeIncrement, IPromise promise)
        {
            this.logger.LogWindowsUpdate(Direction.Outbound, ctx, streamId, windowSizeIncrement);
            return writer.WriteWindowUpdateAsync(ctx, streamId, windowSizeIncrement, promise);
        }
    }
}
