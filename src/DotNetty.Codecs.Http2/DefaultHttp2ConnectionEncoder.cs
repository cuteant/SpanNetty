// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Threading.Tasks;
    using CuteAnt.Collections;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Default implementation of <see cref="IHttp2ConnectionEncoder"/>.
    /// </summary>
    public class DefaultHttp2ConnectionEncoder : IHttp2ConnectionEncoder
    {
        private readonly IHttp2FrameWriter frameWriter;
        private readonly IHttp2Connection connection;
        private IHttp2LifecycleManager lifecycleManager;
        // We prefer ArrayDeque to LinkedList because later will produce more GC.
        // This initial capacity is plenty for SETTINGS traffic.
        private readonly Deque<Http2Settings> outstandingLocalSettingsQueue = new Deque<Http2Settings>(4);

        public DefaultHttp2ConnectionEncoder(IHttp2Connection connection, IHttp2FrameWriter frameWriter)
        {
            if (null == connection) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.connection); }
            if (null == frameWriter) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.frameWriter); }
            this.connection = connection;
            this.frameWriter = frameWriter;
            var connRemote = connection.Remote;
            if (connRemote.FlowController == null)
            {
                connRemote.FlowController = new DefaultHttp2RemoteFlowController(connection);
            }
        }

        public void LifecycleManager(IHttp2LifecycleManager lifecycleManager)
        {
            if (null == lifecycleManager) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.lifecycleManager); }
            this.lifecycleManager = lifecycleManager;
        }

        public IHttp2FrameWriter FrameWriter => this.frameWriter;

        public IHttp2Connection Connection => this.connection;

        public IHttp2RemoteFlowController FlowController => this.connection.Remote.FlowController;

        public Http2Settings PollSentSettings => this.outstandingLocalSettingsQueue.RemoveFromFront();

        public virtual void RemoteSettings(Http2Settings settings)
        {
            var pushEnabled = settings.PushEnabled();
            var config = this.Configuration;
            var outboundHeaderConfig = config.HeadersConfiguration;
            var outboundFrameSizePolicy = config.FrameSizePolicy;
            if (pushEnabled.HasValue)
            {
                if (!this.connection.IsServer && pushEnabled.Value)
                {
                    ThrowHelper.ThrowConnectionError_ClientReceivedAValueOfEnablePushSpecifiedToOtherThan0();
                }
                this.connection.Remote.AllowPushTo(pushEnabled.Value);
            }

            var maxConcurrentStreams = settings.MaxConcurrentStreams();
            if (maxConcurrentStreams.HasValue)
            {
                this.connection.Local.SetMaxActiveStreams((int)Math.Min(maxConcurrentStreams.Value, int.MaxValue));
            }

            var headerTableSize = settings.HeaderTableSize();
            if (headerTableSize.HasValue)
            {
                outboundHeaderConfig.SetMaxHeaderTableSize((int)Math.Min(headerTableSize.Value, int.MaxValue));
            }

            var maxHeaderListSize = settings.MaxHeaderListSize();
            if (maxHeaderListSize.HasValue)
            {
                outboundHeaderConfig.SetMaxHeaderListSize(maxHeaderListSize.Value);
            }

            var maxFrameSize = settings.MaxFrameSize();
            if (maxFrameSize.HasValue)
            {
                outboundFrameSizePolicy.SetMaxFrameSize(maxFrameSize.Value);
            }

            var initialWindowSize = settings.InitialWindowSize();
            if (initialWindowSize.HasValue)
            {
                this.FlowController.SetInitialWindowSize(initialWindowSize.Value);
            }
        }

        public virtual Task WriteDataAsync(IChannelHandlerContext ctx, int streamId,
            IByteBuffer data, int padding, bool endOfStream, IPromise promise)
        {
            IHttp2Stream stream;
            try
            {
                stream = this.RequireStream(streamId);

                // Verify that the stream is in the appropriate state for sending DATA frames.
                var steamState = stream.State;
                if (Http2StreamState.Open == steamState || Http2StreamState.HalfClosedRemote == steamState)
                {
                    // Allowed sending DATA frames in these states.
                }
                else
                {
                    ThrowHelper.ThrowInvalidOperationException_StreamInUnexpectedState(stream);
                }
            }
            catch (Exception e)
            {
                data.Release();
                promise.SetException(e);
                return promise.Task;
            }

            // Hand control of the frame to the flow controller.
            this.FlowController.AddFlowControlled(stream,
                    new FlowControlledData(this, stream, data, padding, endOfStream, promise, ctx.Channel));
            return promise.Task;
        }

        public Task WriteHeadersAsync(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers,
            int padding, bool endOfStream, IPromise promise)
        {
            return WriteHeadersAsync(ctx, streamId, headers, 0, Http2CodecUtil.DefaultPriorityWeight, false, padding, endOfStream, promise);
        }

        private static bool ValidateHeadersSentState(IHttp2Stream stream, IHttp2Headers headers, bool isServer, bool endOfStream)
        {
            var isInformational = isServer && HttpStatusClass.ValueOf(headers.Status) == HttpStatusClass.Informational;
            if ((isInformational || !endOfStream) && stream.IsHeadersSent || stream.IsTrailersSent)
            {
                ThrowHelper.ThrowInvalidOperationException_StreamSentTooManyHeadersEOS(stream.Id, endOfStream);
            }
            return isInformational;
        }

        public virtual Task WriteHeadersAsync(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers,
            int streamDependency, short weight, bool exclusive, int padding, bool endOfStream, IPromise promise)
        {
            try
            {
                var stream = this.connection.Stream(streamId);
                if (stream == null)
                {
                    try
                    {
                        stream = this.connection.Local.CreateStream(streamId, endOfStream);
                    }
                    catch (Http2Exception cause)
                    {
                        if (this.connection.Remote.MayHaveCreatedStream(streamId))
                        {
                            promise.TrySetException(ThrowHelper.GetInvalidOperationException_StreamNoLongerExists(streamId, cause));
                            return promise.Task;
                        }
                        throw cause;
                    }
                }
                else
                {
                    var streamState = stream.State;
                    if (Http2StreamState.ReservedLocal == streamState)
                    {
                        stream.Open(endOfStream);
                    }
                    else if (Http2StreamState.Open == streamState || Http2StreamState.HalfClosedRemote == streamState)
                    {
                        // Allowed sending headers in these states.
                    }
                    else
                    {
                        ThrowHelper.ThrowInvalidOperationException_StreamInUnexpectedState(stream);
                    }
                }

                // Trailing headers must go through flow control if there are other frames queued in flow control
                // for this stream.
                var flowController = this.FlowController;
                if (!endOfStream || !flowController.HasFlowControlled(stream))
                {
                    // The behavior here should mirror that in FlowControlledHeaders
                    promise = promise.Unvoid();

                    var isInformational = ValidateHeadersSentState(stream, headers, this.connection.IsServer, endOfStream);

                    var future = this.frameWriter.WriteHeadersAsync(ctx, streamId, headers, streamDependency,
                                                                    weight, exclusive, padding, endOfStream, promise);
                    // Writing headers may fail during the encode state if they violate HPACK limits.
                    var futureStatus = future.Status;
                    switch (futureStatus)
                    {
                        case TaskStatus.Canceled:
                        case TaskStatus.Faulted:
                            this.lifecycleManager.OnError(ctx, true, future.Exception.InnerException);
                            break;

                        default:
                            // Synchronously set the headersSent flag to ensure that we do not subsequently write
                            // other headers containing pseudo-header fields.
                            //
                            // This just sets internal stream state which is used elsewhere in the codec and doesn't
                            // necessarily mean the write will complete successfully.
                            stream.HeadersSent(isInformational);
                            if (futureStatus != TaskStatus.RanToCompletion)
                            {
                                // Either the future is not done or failed in the meantime.
                                NotifyLifecycleManagerOnError(future, this.lifecycleManager, ctx);
                            }
                            break;
                    }

                    if (endOfStream)
                    {
                        // Must handle calling onError before calling closeStreamLocal, otherwise the error handler will
                        // incorrectly think the stream no longer exists and so may not send RST_STREAM or perform similar
                        // appropriate action.
                        this.lifecycleManager.CloseStreamLocal(stream, future);
                    }

                    return future;
                }
                else
                {
                    // Pass headers to the flow-controller so it can maintain their sequence relative to DATA frames.
                    flowController.AddFlowControlled(stream,
                            new FlowControlledHeaders(this, stream, headers, streamDependency, weight, exclusive, padding,
                                                      true, promise));
                    return promise.Task;
                }
            }
            catch (Exception t)
            {
                this.lifecycleManager.OnError(ctx, true, t);
                promise.TrySetException(t);
                return promise.Task;
            }
        }

        public virtual Task WritePriorityAsync(IChannelHandlerContext ctx, int streamId, int streamDependency,
            short weight, bool exclusive, IPromise promise)
        {
            return this.frameWriter.WritePriorityAsync(ctx, streamId, streamDependency, weight, exclusive, promise);
        }

        public virtual Task WriteRstStreamAsync(IChannelHandlerContext ctx, int streamId, Http2Error errorCode, IPromise promise)
        {
            // Delegate to the lifecycle manager for proper updating of connection state.
            return this.lifecycleManager.ResetStreamAsync(ctx, streamId, errorCode, promise);
        }

        public virtual Task WriteSettingsAsync(IChannelHandlerContext ctx, Http2Settings settings, IPromise promise)
        {
            this.outstandingLocalSettingsQueue.AddToBack(settings);
            try
            {
                var pushEnabled = settings.PushEnabled();
                if (pushEnabled.HasValue && this.connection.IsServer)
                {
                    ThrowHelper.ThrowConnectionError_ServerSendingSettintsFrameWithEnablePushSpecified();
                }
            }
            catch (Exception e)
            {
                promise.SetException(e);
                return promise.Task;
            }

            return this.frameWriter.WriteSettingsAsync(ctx, settings, promise);
        }

        public virtual Task WriteSettingsAckAsync(IChannelHandlerContext ctx, IPromise promise)
        {
            return this.frameWriter.WriteSettingsAckAsync(ctx, promise);
        }

        public virtual Task WritePingAsync(IChannelHandlerContext ctx, bool ack, long data, IPromise promise)
        {
            return this.frameWriter.WritePingAsync(ctx, ack, data, promise);
        }

        public virtual Task WritePushPromiseAsync(IChannelHandlerContext ctx, int streamId, int promisedStreamId,
            IHttp2Headers headers, int padding, IPromise promise)
        {
            try
            {
                if (this.connection.GoAwayReceived())
                {
                    ThrowHelper.ThrowConnectionError_SendingPushPromiseAfterGoAwayReceived();
                }

                var stream = this.RequireStream(streamId);
                // Reserve the promised stream.
                this.connection.Local.ReservePushStream(promisedStreamId, stream);

                promise = promise.Unvoid();
                var future = this.frameWriter.WritePushPromiseAsync(ctx, streamId, promisedStreamId, headers, padding, promise);
                // Writing headers may fail during the encode state if they violate HPACK limits.
                var futureStatus = future.Status;
                switch (futureStatus)
                {
                    case TaskStatus.Canceled:
                    case TaskStatus.Faulted:
                        this.lifecycleManager.OnError(ctx, true, future.Exception.InnerException);
                        break;

                    default:
                        // This just sets internal stream state which is used elsewhere in the codec and doesn't
                        // necessarily mean the write will complete successfully.
                        stream.PushPromiseSent();
                        if (futureStatus != TaskStatus.RanToCompletion)
                        {
                            // Either the future is not done or failed in the meantime.
                            NotifyLifecycleManagerOnError(future, this.lifecycleManager, ctx);
                        }
                        break;
                }

                return future;
            }
            catch (Exception t)
            {
                this.lifecycleManager.OnError(ctx, true, t);
                promise.TrySetException(t);
                return promise.Task;
            }
        }

        public virtual Task WriteGoAwayAsync(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode,
            IByteBuffer debugData, IPromise promise)
        {
            return this.lifecycleManager.GoAwayAsync(ctx, lastStreamId, errorCode, debugData, promise);
        }

        public virtual Task WriteWindowUpdateAsync(IChannelHandlerContext ctx, int streamId,
            int windowSizeIncrement, IPromise promise)
        {
            promise.SetException(new NotSupportedException("Use the Http2[Inbound|Outbound]FlowController" +
                    " objects to control window sizes"));
            return promise.Task;
        }

        public virtual Task WriteFrameAsync(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId,
            Http2Flags flags, IByteBuffer payload, IPromise promise)
        {
            return this.frameWriter.WriteFrameAsync(ctx, frameType, streamId, flags, payload, promise);
        }

        public virtual void Close()
        {
            this.frameWriter.Close();
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose() => this.Close();

        public IHttp2FrameWriterConfiguration Configuration => this.frameWriter.Configuration;

        private IHttp2Stream RequireStream(int streamId)
        {
            var stream = this.connection.Stream(streamId);
            if (stream == null)
            {
                ThrowHelper.ThrowArgumentException_RequireStream(this.connection, streamId);
            }
            return stream;
        }

        /// <summary>
        /// Wrap a DATA frame so it can be written subject to flow-control. Note that this implementation assumes it
        /// only writes padding once for the entire payload as opposed to writing it once per-frame. This makes the
        /// <see cref="FlowControlledData.Size"/> calculation deterministic thereby greatly simplifying the implementation.
        /// <para>
        /// If frame-splitting is required to fit within max-frame-size and flow-control constraints we ensure that
        /// the passed promise is not completed until last frame write.
        /// </para>
        /// </summary>
        sealed class FlowControlledData : FlowControlledBase
        {
            private readonly CoalescingBufferQueue queue;
            private int dataSize;

            public FlowControlledData(DefaultHttp2ConnectionEncoder encoder,
                IHttp2Stream stream, IByteBuffer buf, int padding, bool endOfStream, IPromise promise, IChannel channel)
                : base(encoder, stream, padding, endOfStream, promise)
            {
                this.queue = new CoalescingBufferQueue(channel);
                this.queue.Add(buf, promise);
                this.dataSize = this.queue.ReadableBytes();
            }

            public override int Size => this.dataSize + this.padding;

            public override void Error(IChannelHandlerContext ctx, Exception cause)
            {
                this.queue.ReleaseAndFailAll(cause);
                // Don't update dataSize because we need to ensure the size() method returns a consistent size even after
                // error so we don't invalidate flow control when returning bytes to flow control.
                this.encoder.lifecycleManager.OnError(ctx, true, cause);
            }

            public override void Write(IChannelHandlerContext ctx, int allowedBytes)
            {
                int queuedData = this.queue.ReadableBytes();
                if (!endOfStream)
                {
                    if (queuedData == 0)
                    {
                        // There's no need to write any data frames because there are only empty data frames in the queue
                        // and it is not end of stream yet. Just complete their promises by getting the buffer corresponding
                        // to 0 bytes and writing it to the channel (to preserve notification order).
                        var writePromise0 = ctx.NewPromise();
                        this.AddListener(writePromise0);
                        ctx.WriteAsync(this.queue.Remove(0, writePromise0), writePromise0);
                        return;
                    }

                    if (allowedBytes == 0)
                    {
                        return;
                    }
                }

                // Determine how much data to write.
                int writableData = Math.Min(queuedData, allowedBytes);
                var writePromise = ctx.NewPromise();
                this.AddListener(writePromise);
                var toWrite = this.queue.Remove(writableData, writePromise);
                dataSize = this.queue.ReadableBytes();

                // Determine how much padding to write.
                int writablePadding = Math.Min(allowedBytes - writableData, padding);
                padding -= writablePadding;

                // Write the frame(s).
                this.encoder.frameWriter.WriteDataAsync(ctx, stream.Id, toWrite, writablePadding,
                        endOfStream && this.Size == 0, writePromise);
            }

            public override bool Merge(IChannelHandlerContext ctx, IHttp2RemoteFlowControlled next)
            {
                var nextData = next as FlowControlledData;
                if (null == nextData || (int.MaxValue - nextData.Size) < this.Size)
                {
                    return false;
                }
                nextData.queue.CopyTo(this.queue);
                dataSize = this.queue.ReadableBytes();
                // Given that we're merging data into a frame it doesn't really make sense to accumulate padding.
                padding = Math.Max(padding, nextData.padding);
                endOfStream = nextData.endOfStream;
                return true;
            }
        }

        private static void NotifyLifecycleManagerOnError(Task future, IHttp2LifecycleManager lm, IChannelHandlerContext ctx)
        {
#if NET40
            future.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    lm.OnError(ctx, true, t.Exception.InnerException);
                }
            }, TaskContinuationOptions.ExecuteSynchronously);
#else
            future.ContinueWith(NotifyLifecycleManagerOnErrorAction, Tuple.Create(lm, ctx), TaskContinuationOptions.ExecuteSynchronously);
#endif
        }

        private static readonly Action<Task, object> NotifyLifecycleManagerOnErrorAction = NotifyLifecycleManagerOnError0;
        private static void NotifyLifecycleManagerOnError0(Task t, object s)
        {
            var wrapped = (Tuple<IHttp2LifecycleManager, IChannelHandlerContext>)s;
            if (t.IsFaulted)
            {
                wrapped.Item1.OnError(wrapped.Item2, true, t.Exception.InnerException);
            }
        }

        /// <summary>
        /// Wrap headers so they can be written subject to flow-control. While headers do not have cost against the
        /// flow-control window their order with respect to other frames must be maintained, hence if a DATA frame is
        /// blocked on flow-control a HEADER frame must wait until this frame has been written.
        /// </summary>
        sealed class FlowControlledHeaders : FlowControlledBase
        {
            private readonly IHttp2Headers headers;
            private readonly int streamDependency;
            private readonly short weight;
            private readonly bool exclusive;

            public FlowControlledHeaders(DefaultHttp2ConnectionEncoder encoder,
                IHttp2Stream stream, IHttp2Headers headers, int streamDependency, short weight,
                bool exclusive, int padding, bool endOfStream, IPromise promise)
                : base(encoder, stream, padding, endOfStream, promise.Unvoid())
            {
                this.headers = headers;
                this.streamDependency = streamDependency;
                this.weight = weight;
                this.exclusive = exclusive;
            }

            public override int Size => 0;

            public override void Error(IChannelHandlerContext ctx, Exception cause)
            {
                if (ctx != null)
                {
                    this.encoder.lifecycleManager.OnError(ctx, true, cause);
                }
                promise.TrySetException(cause);
            }

            public override void Write(IChannelHandlerContext ctx, int allowedBytes)
            {
                var isInformational = ValidateHeadersSentState(stream, headers, this.encoder.connection.IsServer, endOfStream);
                // The code is currently requiring adding this listener before writing, in order to call onError() before
                // closeStreamLocal().
                this.AddListener(this.promise);

                var f = this.encoder.frameWriter.WriteHeadersAsync(ctx, stream.Id, headers, streamDependency, weight, exclusive,
                                                                   padding, endOfStream, promise);
                // Writing headers may fail during the encode state if they violate HPACK limits.
                switch (f.Status)
                {
                    case TaskStatus.Canceled:
                    case TaskStatus.Faulted:
                        break;
                    default:
                        // This just sets internal stream state which is used elsewhere in the codec and doesn't
                        // necessarily mean the write will complete successfully.
                        this.stream.HeadersSent(isInformational);
                        break;
                }
            }

            public override bool Merge(IChannelHandlerContext ctx, IHttp2RemoteFlowControlled next)
            {
                return false;
            }
        }

        /// <summary>
        /// Common base type for payloads to deliver via flow-control.
        /// </summary>
        public abstract class FlowControlledBase : IHttp2RemoteFlowControlled
        {
            protected readonly DefaultHttp2ConnectionEncoder encoder;
            protected readonly IHttp2Stream stream;
            protected IPromise promise;
            protected bool endOfStream;
            protected int padding;

            public FlowControlledBase(DefaultHttp2ConnectionEncoder encoder, IHttp2Stream stream, int padding, bool endOfStream, IPromise promise)
            {
                if (padding < 0)
                {
                    ThrowHelper.ThrowArgumentException_PositiveOrZero(ExceptionArgument.padding);
                }
                this.encoder = encoder;
                this.padding = padding;
                this.endOfStream = endOfStream;
                this.stream = stream;
                this.promise = promise;

            }

            private static readonly Action<Task, object> LinkOutcomeContinuationAction = LinkOutcomeContinuation;
            private static void LinkOutcomeContinuation(Task task, object state)
            {
                var self = (FlowControlledBase)state;
                if (task.IsFaulted)
                {
                    self.Error(self.encoder.FlowController.ChannelHandlerContext, task.Exception.InnerException);
                }
            }

            protected void AddListener(IPromise p)
            {
#if NET40
                void continuationAction(Task task)
                {
                    if (task.IsFaulted)
                    {
                        this.Error(this.encoder.FlowController.ChannelHandlerContext, task.Exception.InnerException);
                    }
                }
                p.Task.ContinueWith(continuationAction, TaskContinuationOptions.ExecuteSynchronously);
#else
                p.Task.ContinueWith(LinkOutcomeContinuationAction, this, TaskContinuationOptions.ExecuteSynchronously);
#endif
            }

            public abstract void Error(IChannelHandlerContext ctx, Exception cause);

            public abstract bool Merge(IChannelHandlerContext ctx, IHttp2RemoteFlowControlled next);

            public abstract int Size { get; }

            public abstract void Write(IChannelHandlerContext ctx, int allowedBytes);

            public void WriteComplete()
            {
                if (this.endOfStream)
                {
                    this.encoder.lifecycleManager.CloseStreamLocal(stream, promise.Task);
                }
            }
        }
    }
}