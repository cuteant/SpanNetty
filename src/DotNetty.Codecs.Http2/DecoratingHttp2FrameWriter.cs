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
        readonly IHttp2FrameWriter _innerWriter;

        public DecoratingHttp2FrameWriter(IHttp2FrameWriter writer)
        {
            if (writer is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.writer); }

            _innerWriter = writer;
        }

        public virtual void Close()
        {
            _innerWriter.Close();
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose() => Close();

        public IHttp2FrameWriterConfiguration Configuration => _innerWriter.Configuration;

        public virtual Task WriteDataAsync(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream, IPromise promise)
        {
            return _innerWriter.WriteDataAsync(ctx, streamId, data, padding, endOfStream, promise);
        }

        public virtual Task WriteFrameAsync(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId, Http2Flags flags, IByteBuffer payload, IPromise promise)
        {
            return _innerWriter.WriteFrameAsync(ctx, frameType, streamId, flags, payload, promise);
        }

        public virtual Task WriteGoAwayAsync(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData, IPromise promise)
        {
            return _innerWriter.WriteGoAwayAsync(ctx, lastStreamId, errorCode, debugData, promise);
        }

        public virtual Task WriteHeadersAsync(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream, IPromise promise)
        {
            return _innerWriter.WriteHeadersAsync(ctx, streamId, headers, padding, endOfStream, promise);
        }

        public virtual Task WriteHeadersAsync(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream, IPromise promise)
        {
            return _innerWriter.WriteHeadersAsync(ctx, streamId, headers, streamDependency, weight, exclusive, padding, endOfStream, promise);
        }

        public virtual Task WritePingAsync(IChannelHandlerContext ctx, bool ack, long data, IPromise promise)
        {
            return _innerWriter.WritePingAsync(ctx, ack, data, promise);
        }

        public virtual Task WritePriorityAsync(IChannelHandlerContext ctx, int streamId, int streamDependency, short weight, bool exclusive, IPromise promise)
        {
            return _innerWriter.WritePriorityAsync(ctx, streamId, streamDependency, weight, exclusive, promise);
        }

        public virtual Task WritePushPromiseAsync(IChannelHandlerContext ctx, int streamId, int promisedStreamId, IHttp2Headers headers, int padding, IPromise promise)
        {
            return _innerWriter.WritePushPromiseAsync(ctx, streamId, promisedStreamId, headers, padding, promise);
        }

        public virtual Task WriteRstStreamAsync(IChannelHandlerContext ctx, int streamId, Http2Error errorCode, IPromise promise)
        {
            return _innerWriter.WriteRstStreamAsync(ctx, streamId, errorCode, promise);
        }

        public virtual Task WriteSettingsAckAsync(IChannelHandlerContext ctx, IPromise promise)
        {
            return _innerWriter.WriteSettingsAckAsync(ctx, promise);
        }

        public virtual Task WriteSettingsAsync(IChannelHandlerContext ctx, Http2Settings settings, IPromise promise)
        {
            return _innerWriter.WriteSettingsAsync(ctx, settings, promise);
        }

        public virtual Task WriteWindowUpdateAsync(IChannelHandlerContext ctx, int streamId, int windowSizeIncrement, IPromise promise)
        {
            return _innerWriter.WriteWindowUpdateAsync(ctx, streamId, windowSizeIncrement, promise);
        }
    }
}
