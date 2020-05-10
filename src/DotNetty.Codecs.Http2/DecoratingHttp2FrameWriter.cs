// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Decorator around another <see cref="IHttp2FrameWriter"/> instance.
    /// </summary>
    public class DecoratingHttp2FrameWriter : IHttp2FrameWriter
    {
        readonly IHttp2FrameWriter innerWriter;

        public DecoratingHttp2FrameWriter(IHttp2FrameWriter writer)
        {
            if (writer is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.writer); }

            this.innerWriter = writer;
        }

        public virtual void Close()
        {
            this.innerWriter.Close();
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose() => this.Close();

        public IHttp2FrameWriterConfiguration Configuration => this.innerWriter.Configuration;

        public virtual Task WriteDataAsync(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream, IPromise promise)
        {
            return this.innerWriter.WriteDataAsync(ctx, streamId, data, padding, endOfStream, promise);
        }

        public virtual Task WriteFrameAsync(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId, Http2Flags flags, IByteBuffer payload, IPromise promise)
        {
            return this.innerWriter.WriteFrameAsync(ctx, frameType, streamId, flags, payload, promise);
        }

        public virtual Task WriteGoAwayAsync(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData, IPromise promise)
        {
            return this.innerWriter.WriteGoAwayAsync(ctx, lastStreamId, errorCode, debugData, promise);
        }

        public virtual Task WriteHeadersAsync(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream, IPromise promise)
        {
            return this.innerWriter.WriteHeadersAsync(ctx, streamId, headers, padding, endOfStream, promise);
        }

        public virtual Task WriteHeadersAsync(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream, IPromise promise)
        {
            return this.innerWriter.WriteHeadersAsync(ctx, streamId, headers, streamDependency, weight, exclusive, padding, endOfStream, promise);
        }

        public virtual Task WritePingAsync(IChannelHandlerContext ctx, bool ack, long data, IPromise promise)
        {
            return this.innerWriter.WritePingAsync(ctx, ack, data, promise);
        }

        public virtual Task WritePriorityAsync(IChannelHandlerContext ctx, int streamId, int streamDependency, short weight, bool exclusive, IPromise promise)
        {
            return this.innerWriter.WritePriorityAsync(ctx, streamId, streamDependency, weight, exclusive, promise);
        }

        public virtual Task WritePushPromiseAsync(IChannelHandlerContext ctx, int streamId, int promisedStreamId, IHttp2Headers headers, int padding, IPromise promise)
        {
            return this.innerWriter.WritePushPromiseAsync(ctx, streamId, promisedStreamId, headers, padding, promise);
        }

        public virtual Task WriteRstStreamAsync(IChannelHandlerContext ctx, int streamId, Http2Error errorCode, IPromise promise)
        {
            return this.innerWriter.WriteRstStreamAsync(ctx, streamId, errorCode, promise);
        }

        public virtual Task WriteSettingsAckAsync(IChannelHandlerContext ctx, IPromise promise)
        {
            return this.innerWriter.WriteSettingsAckAsync(ctx, promise);
        }

        public virtual Task WriteSettingsAsync(IChannelHandlerContext ctx, Http2Settings settings, IPromise promise)
        {
            return this.innerWriter.WriteSettingsAsync(ctx, settings, promise);
        }

        public virtual Task WriteWindowUpdateAsync(IChannelHandlerContext ctx, int streamId, int windowSizeIncrement, IPromise promise)
        {
            return this.innerWriter.WriteWindowUpdateAsync(ctx, streamId, windowSizeIncrement, promise);
        }
    }
}
