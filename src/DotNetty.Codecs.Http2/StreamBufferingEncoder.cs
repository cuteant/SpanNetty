// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Implementation of a <see cref="IHttp2ConnectionEncoder"/> that dispatches all method call to another
    /// <see cref="IHttp2ConnectionEncoder"/>, until <c>SETTINGS_MAX_CONCURRENT_STREAMS</c> is reached.
    /// <para>When this limit is hit, instead of rejecting any new streams this implementation buffers newly
    /// created streams and their corresponding frames. Once an active stream gets closed or the maximum
    /// number of concurrent streams is increased, this encoder will automatically try to empty its
    /// buffer and create as many new streams as possible.</para>
    /// <para>If a <c>GOAWAY</c> frame is received from the remote endpoint, all buffered writes for streams
    /// with an ID less than the specified {@code lastStreamId} will immediately fail with a
    /// <see cref="Http2GoAwayException"/>.</para>
    /// <para>If the channel/encoder gets closed, all new and buffered writes will immediately fail with a
    /// <see cref="Http2ChannelClosedException"/>.</para>
    /// <para>This implementation makes the buffering mostly transparent and is expected to be used as a
    /// drop-in decorator of <see cref="DefaultHttp2ConnectionEncoder"/>.</para>
    /// </summary>
    public class StreamBufferingEncoder : DecoratingHttp2ConnectionEncoder
    {
        /// <summary>
        /// Buffer for any streams and corresponding frames that could not be created due to the maximum
        /// concurrent stream limit being hit.
        /// </summary>
        private readonly SortedDictionary<int, PendingStream> pendingStreams = new SortedDictionary<int, PendingStream>();
        private int maxConcurrentStreams;
        private bool closed;

        public StreamBufferingEncoder(IHttp2ConnectionEncoder encoder)
            : this(encoder, Http2CodecUtil.SmallestMaxConcurrentStreams)
        {
        }

        public StreamBufferingEncoder(IHttp2ConnectionEncoder encoder, int initialMaxConcurrentStreams)
            : base(encoder)
        {
            this.maxConcurrentStreams = initialMaxConcurrentStreams;
            this.Connection.AddListener(new DelegatingConnectionAdapter(this));
        }

        /// <summary>
        /// Indicates the number of streams that are currently buffered, awaiting creation.
        /// </summary>
        public int NumBufferedStreams() => this.pendingStreams.Count;

        public override Task WriteHeadersAsync(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream, IPromise promise)
        {
            return this.WriteHeadersAsync(ctx, streamId, headers, 0, Http2CodecUtil.DefaultPriorityWeight,
                false, padding, endOfStream, promise);
        }

        public override Task WriteHeadersAsync(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream, IPromise promise)
        {
            if (this.closed)
            {
                promise.SetException(new Http2ChannelClosedException());
                return promise.Task;
            }
            if (this.IsExistingStream(streamId) || this.Connection.GoAwayReceived())
            {
                return base.WriteHeadersAsync(ctx, streamId, headers, streamDependency, weight, exclusive, padding, endOfStream, promise);
            }
            if (this.CanCreateStream())
            {
                return base.WriteHeadersAsync(ctx, streamId, headers, streamDependency, weight, exclusive, padding, endOfStream, promise);
            }
            if (!this.pendingStreams.TryGetValue(streamId, out var pendingStream))
            {
                pendingStream = new PendingStream(ctx, streamId);
                this.pendingStreams.Add(streamId, pendingStream);
            }
            pendingStream.frames.Add(new HeadersFrame(this, headers, streamDependency, weight, exclusive,
                    padding, endOfStream, promise));
            return promise.Task;
        }

        public override Task WriteRstStreamAsync(IChannelHandlerContext ctx, int streamId, Http2Error errorCode, IPromise promise)
        {
            if (this.IsExistingStream(streamId))
            {
                return base.WriteRstStreamAsync(ctx, streamId, errorCode, promise);
            }
            // Since the delegate doesn't know about any buffered streams we have to handle cancellation
            // of the promises and releasing of the ByteBufs here.
            if (this.pendingStreams.TryGetValue(streamId, out var stream))
            {
                this.pendingStreams.Remove(streamId);
                // Sending a RST_STREAM to a buffered stream will succeed the promise of all frames
                // associated with the stream, as sending a RST_STREAM means that someone "doesn't care"
                // about the stream anymore and thus there is not point in failing the promises and invoking
                // error handling routines.
                stream.Close(null);
                promise.Complete();
            }
            else
            {
                promise.SetException(ThrowHelper.GetConnectionError_StreamDoesNotExist(streamId));
            }
            return promise.Task;
        }

        public override Task WriteDataAsync(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream, IPromise promise)
        {
            if (this.IsExistingStream(streamId))
            {
                return base.WriteDataAsync(ctx, streamId, data, padding, endOfStream, promise);
            }

            if (this.pendingStreams.TryGetValue(streamId, out var pendingStream))
            {
                pendingStream.frames.Add(new DataFrame(this, data, padding, endOfStream, promise));
            }
            else
            {
                ReferenceCountUtil.SafeRelease(data);
                promise.SetException(ThrowHelper.GetConnectionError_StreamDoesNotExist(streamId));
            }
            return promise.Task;
        }

        public override void RemoteSettings(Http2Settings settings)
        {
            // Need to let the delegate decoder handle the settings first, so that it sees the
            // new setting before we attempt to create any new streams.
            base.RemoteSettings(settings);

            // Get the updated value for SETTINGS_MAX_CONCURRENT_STREAMS.
            this.maxConcurrentStreams = this.Connection.Local.MaxActiveStreams;

            // Try to create new streams up to the new threshold.
            this.TryCreatePendingStreams();
        }

        public override void Close()
        {
            try
            {
                if (!this.closed)
                {
                    this.closed = true;

                    // Fail all buffered streams.
                    Http2ChannelClosedException e = new Http2ChannelClosedException();
                    if (this.pendingStreams.Count > 0)
                    {
                        foreach (var stream in this.pendingStreams.Values)
                        {
                            stream.Close(e);
                        }
                        this.pendingStreams.Clear();
                    }
                }
            }
            finally
            {
                base.Close();
            }
        }

        private void TryCreatePendingStreams()
        {
            if (this.pendingStreams.Count <= 0) { return; }

            var keyList = new List<int>(this.pendingStreams.Count);
            foreach (var item in this.pendingStreams)
            {
                if (!this.CanCreateStream()) { break; }

                keyList.Add(item.Key);
                var pendingStream = item.Value;
                try
                {
                    pendingStream.SendFrames();
                }
                catch (Exception t)
                {
                    pendingStream.Close(t);
                }
            }
            foreach (var item in keyList)
            {
                this.pendingStreams.Remove(item);
            }
        }

        private void CancelGoAwayStreams(int lastStreamId, Http2Error errorCode, IByteBuffer debugData)
        {
            if (this.pendingStreams.Count <= 0) { return; }

            var e = new Http2GoAwayException(lastStreamId, errorCode, ByteBufferUtil.GetBytes(debugData));

            var keyList = new List<int>(this.pendingStreams.Count);
            foreach (var stream in this.pendingStreams.Values)
            {
                if (stream.streamId > lastStreamId)
                {
                    keyList.Add(stream.streamId);
                    stream.Close(e);
                }
            }
            foreach (var item in keyList)
            {
                this.pendingStreams.Remove(item);
            }
        }

        /// <summary>
        /// Determines whether or not we're allowed to create a new stream right now.
        /// </summary>
        private bool CanCreateStream()
        {
            return this.Connection.Local.NumActiveStreams < this.maxConcurrentStreams;
        }

        private bool IsExistingStream(int streamId)
        {
            return streamId <= this.Connection.Local.LastStreamCreated;
        }

        sealed class DelegatingConnectionAdapter : Http2ConnectionAdapter
        {
            readonly StreamBufferingEncoder encoder;

            public DelegatingConnectionAdapter(StreamBufferingEncoder encoder) => this.encoder = encoder;

            public override void OnGoAwayReceived(int lastStreamId, Http2Error errorCode, IByteBuffer debugData)
            {
                this.encoder.CancelGoAwayStreams(lastStreamId, errorCode, debugData);
            }

            public override void OnStreamClosed(IHttp2Stream stream)
            {
                this.encoder.TryCreatePendingStreams();
            }
        }

        private sealed class PendingStream
        {
            readonly IChannelHandlerContext ctx;
            internal readonly int streamId;
            internal readonly List<Frame> frames = new List<Frame>(2);

            public PendingStream(IChannelHandlerContext ctx, int streamId)
            {
                this.ctx = ctx;
                this.streamId = streamId;
            }

            public void SendFrames()
            {
                foreach (Frame frame in frames)
                {
                    frame.Send(ctx, streamId);
                }
            }

            public void Close(Exception t)
            {
                foreach (Frame frame in frames)
                {
                    frame.Release(t);
                }
            }
        }

        private abstract class Frame
        {
            protected readonly IPromise promise;

            public Frame(IPromise promise) => this.promise = promise;

            /// <summary>
            /// Release any resources (features, buffers, ...) associated with the frame.
            /// </summary>
            /// <param name="ex"></param>
            public virtual void Release(Exception ex)
            {
                if (ex == null)
                {
                    promise.Complete();
                }
                else
                {
                    promise.SetException(ex);
                }
            }

            public abstract void Send(IChannelHandlerContext ctx, int streamId);
        }

        private sealed class HeadersFrame : Frame
        {
            readonly StreamBufferingEncoder encoder;
            readonly IHttp2Headers headers;
            readonly int streamDependency;
            readonly short weight;
            readonly bool exclusive;
            readonly int padding;
            readonly bool endOfStream;

            public HeadersFrame(StreamBufferingEncoder encoder, IHttp2Headers headers, int streamDependency, short weight, bool exclusive,
                int padding, bool endOfStream, IPromise promise)
                : base(promise)
            {
                this.encoder = encoder;
                this.headers = headers;
                this.streamDependency = streamDependency;
                this.weight = weight;
                this.exclusive = exclusive;
                this.padding = padding;
                this.endOfStream = endOfStream;
            }

            public override void Send(IChannelHandlerContext ctx, int streamId)
            {
                this.encoder.WriteHeadersAsync(ctx, streamId, this.headers, this.streamDependency,
                    this.weight, this.exclusive, this.padding, this.endOfStream, this.promise);
            }
        }

        private sealed class DataFrame : Frame
        {
            readonly StreamBufferingEncoder encoder;
            readonly IByteBuffer data;
            readonly int padding;
            readonly bool endOfStream;

            public DataFrame(StreamBufferingEncoder encoder, IByteBuffer data, int padding, bool endOfStream, IPromise promise)
                : base(promise)
            {
                this.encoder = encoder;
                this.data = data;
                this.padding = padding;
                this.endOfStream = endOfStream;
            }

            public override void Release(Exception ex)
            {
                base.Release(ex);
                ReferenceCountUtil.SafeRelease(this.data);
            }

            public override void Send(IChannelHandlerContext ctx, int streamId)
            {
                this.encoder.WriteDataAsync(ctx, streamId, this.data, this.padding, this.endOfStream, this.promise);
            }
        }
    }
}
