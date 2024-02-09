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
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Default implementation of <see cref="IHttp2ConnectionEncoder"/>.
    /// </summary>
    public class DefaultHttp2ConnectionEncoder : IHttp2ConnectionEncoder, IHttp2SettingsReceivedConsumer
    {
        private readonly IHttp2FrameWriter _frameWriter;
        private readonly IHttp2Connection _connection;
        private IHttp2LifecycleManager _lifecycleManager;
        // We prefer ArrayDeque to LinkedList because later will produce more GC.
        // This initial capacity is plenty for SETTINGS traffic.
        private readonly Deque<Http2Settings> _outstandingLocalSettingsQueue = new Deque<Http2Settings>(4);
        private Deque<Http2Settings> _outstandingRemoteSettingsQueue;

        public DefaultHttp2ConnectionEncoder(IHttp2Connection connection, IHttp2FrameWriter frameWriter)
        {
            if (connection is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.connection); }
            if (frameWriter is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.frameWriter); }
            _connection = connection;
            _frameWriter = frameWriter;
            var connRemote = connection.Remote;
            if (connRemote.FlowController is null)
            {
                connRemote.FlowController = new DefaultHttp2RemoteFlowController(connection);
            }
        }

        public void LifecycleManager(IHttp2LifecycleManager lifecycleManager)
        {
            if (lifecycleManager is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.lifecycleManager); }
            _lifecycleManager = lifecycleManager;
        }

        public IHttp2FrameWriter FrameWriter => _frameWriter;

        public IHttp2Connection Connection => _connection;

        public IHttp2RemoteFlowController FlowController => _connection.Remote.FlowController;

        public Http2Settings PollSentSettings => _outstandingLocalSettingsQueue.RemoveFirst();

        public virtual void RemoteSettings(Http2Settings settings)
        {
            var pushEnabled = settings.PushEnabled();
            var config = Configuration;
            var outboundHeaderConfig = config.HeadersConfiguration;
            var outboundFrameSizePolicy = config.FrameSizePolicy;
            if (pushEnabled.HasValue)
            {
                if (!_connection.IsServer && pushEnabled.Value)
                {
                    ThrowHelper.ThrowConnectionError_ClientReceivedAValueOfEnablePushSpecifiedToOtherThan0();
                }
                _connection.Remote.AllowPushTo(pushEnabled.Value);
            }

            var maxConcurrentStreams = settings.MaxConcurrentStreams();
            if (maxConcurrentStreams.HasValue)
            {
                _connection.Local.SetMaxActiveStreams((int)Math.Min(maxConcurrentStreams.Value, int.MaxValue));
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
                FlowController.SetInitialWindowSize(initialWindowSize.Value);
            }
        }

        public virtual Task WriteDataAsync(IChannelHandlerContext ctx, int streamId,
            IByteBuffer data, int padding, bool endOfStream, IPromise promise)
        {
            IHttp2Stream stream;
            try
            {
                stream = RequireStream(streamId);

                // Verify that the stream is in the appropriate state for sending DATA frames.
                switch (stream.State)
                {
                    case Http2StreamState.Open:
                    case Http2StreamState.HalfClosedRemote:
                        // Allowed sending DATA frames in these states.
                        break;

                    default:
                        ThrowHelper.ThrowInvalidOperationException_StreamInUnexpectedState(stream);
                        break;
                }
            }
            catch (Exception e)
            {
                _ = data.Release();
                promise.SetException(e);
                return promise.Task;
            }

            // Hand control of the frame to the flow controller.
            FlowController.AddFlowControlled(stream,
                    new FlowControlledData(this, stream, data, padding, endOfStream, promise, ctx.Channel));
            return promise.Task;
        }

        public Task WriteHeadersAsync(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers,
            int padding, bool endOfStream, IPromise promise)
        {
            return InternalWriteHeadersAsync(ctx, streamId, headers, false, 0, 0, false, padding, endOfStream, promise);
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
            return InternalWriteHeadersAsync(ctx, streamId, headers, true, streamDependency,
                weight, exclusive, padding, endOfStream, promise);
        }

        /// <summary>
        /// Write headers via <see cref="IHttp2FrameWriter"/>. If <paramref name="hasPriority"/> is <c>false</c> it will ignore the
        /// <paramref name="streamDependency"/>, <paramref name="weight"/> and <paramref name="exclusive"/> parameters.
        /// </summary>
        private static Task SendHeadersAsync(IHttp2FrameWriter frameWriter,
            IChannelHandlerContext ctx, int streamId,
            IHttp2Headers headers, bool hasPriority,
            int streamDependency, short weight,
            bool exclusive, int padding,
            bool endOfStream, IPromise promise)
        {
            if (hasPriority)
            {
                return frameWriter.WriteHeadersAsync(ctx, streamId, headers, streamDependency,
                        weight, exclusive, padding, endOfStream, promise);
            }
            return frameWriter.WriteHeadersAsync(ctx, streamId, headers, padding, endOfStream, promise);
        }

        private Task InternalWriteHeadersAsync(
            IChannelHandlerContext ctx, int streamId,
            IHttp2Headers headers, bool hasPriority,
            int streamDependency, short weight,
            bool exclusive, int padding,
            bool endOfStream, IPromise promise)
        {
            try
            {
                var stream = _connection.Stream(streamId);
                if (stream is null)
                {
                    try
                    {
                        // We don't create the stream in a `halfClosed` state because if this is an initial
                        // HEADERS frame we don't want the connection state to signify that the HEADERS have
                        // been sent until after they have been encoded and placed in the outbound buffer.
                        // Therefore, we let the `LifeCycleManager` will take care of transitioning the state
                        // as appropriate.
                        stream = _connection.Local.CreateStream(streamId, /*endOfStream*/ false);
                    }
                    catch (Http2Exception cause)
                    {
                        if (_connection.Remote.MayHaveCreatedStream(streamId))
                        {
                            _ = promise.TrySetException(ThrowHelper.GetInvalidOperationException_StreamNoLongerExists(streamId, cause));
                            return promise.Task;
                        }
                        throw;
                    }
                }
                else
                {
                    switch (stream.State)
                    {
                        case Http2StreamState.ReservedLocal:
                            _ = stream.Open(endOfStream);
                            break;

                        case Http2StreamState.Open:
                        case Http2StreamState.HalfClosedRemote:
                            // Allowed sending headers in these states.
                            break;

                        default:
                            ThrowHelper.ThrowInvalidOperationException_StreamInUnexpectedState(stream);
                            break;
                    }
                }

                // Trailing headers must go through flow control if there are other frames queued in flow control
                // for this stream.
                var flowController = FlowController;
                if (!endOfStream || !flowController.HasFlowControlled(stream))
                {
                    // The behavior here should mirror that in FlowControlledHeaders

                    promise = promise.Unvoid();
                    var isInformational = ValidateHeadersSentState(stream, headers, _connection.IsServer, endOfStream);

                    var future = SendHeadersAsync(_frameWriter, ctx, streamId, headers, hasPriority, streamDependency,
                            weight, exclusive, padding, endOfStream, promise);

                    // Writing headers may fail during the encode state if they violate HPACK limits.
                    var failureCause = future.Exception;
                    if (failureCause is null)
                    {
                        // Synchronously set the headersSent flag to ensure that we do not subsequently write
                        // other headers containing pseudo-header fields.
                        //
                        // This just sets internal stream state which is used elsewhere in the codec and doesn't
                        // necessarily mean the write will complete successfully.
                        _ = stream.HeadersSent(isInformational);
                        if (!future.IsSuccess())
                        {
                            // Either the future is not done or failed in the meantime.
                            NotifyLifecycleManagerOnError(future, _lifecycleManager, ctx);
                        }
                    }
                    else
                    {
                        _lifecycleManager.OnError(ctx, true, failureCause.InnerException);
                    }

                    if (endOfStream)
                    {
                        // Must handle calling onError before calling closeStreamLocal, otherwise the error handler will
                        // incorrectly think the stream no longer exists and so may not send RST_STREAM or perform similar
                        // appropriate action.
                        _lifecycleManager.CloseStreamLocal(stream, future);
                    }

                    return future;
                }
                else
                {
                    // Pass headers to the flow-controller so it can maintain their sequence relative to DATA frames.
                    flowController.AddFlowControlled(stream,
                            new FlowControlledHeaders(this, stream, headers, hasPriority, streamDependency,
                                    weight, exclusive, padding, true, promise));
                    return promise.Task;
                }
            }
            catch (Exception t)
            {
                _lifecycleManager.OnError(ctx, true, t);
                _ = promise.TrySetException(t);
                return promise.Task;
            }
        }

        public virtual Task WritePriorityAsync(IChannelHandlerContext ctx, int streamId, int streamDependency,
            short weight, bool exclusive, IPromise promise)
        {
            return _frameWriter.WritePriorityAsync(ctx, streamId, streamDependency, weight, exclusive, promise);
        }

        public virtual Task WriteRstStreamAsync(IChannelHandlerContext ctx, int streamId, Http2Error errorCode, IPromise promise)
        {
            // Delegate to the lifecycle manager for proper updating of connection state.
            return _lifecycleManager.ResetStreamAsync(ctx, streamId, errorCode, promise);
        }

        public virtual Task WriteSettingsAsync(IChannelHandlerContext ctx, Http2Settings settings, IPromise promise)
        {
            _outstandingLocalSettingsQueue.AddLast​(settings);
            try
            {
                var pushEnabled = settings.PushEnabled();
                if (pushEnabled.HasValue && _connection.IsServer)
                {
                    ThrowHelper.ThrowConnectionError_ServerSendingSettintsFrameWithEnablePushSpecified();
                }
            }
            catch (Exception e)
            {
                promise.SetException(e);
                return promise.Task;
            }

            return _frameWriter.WriteSettingsAsync(ctx, settings, promise);
        }

        public virtual Task WriteSettingsAckAsync(IChannelHandlerContext ctx, IPromise promise)
        {
            if (_outstandingRemoteSettingsQueue is null)
            {
                return _frameWriter.WriteSettingsAckAsync(ctx, promise);
            }
            Http2Settings settings = _outstandingRemoteSettingsQueue.RemoveFirst();
            if (settings is null)
            {
                _ = promise.TrySetException(ThrowHelper.GetConnectionError_attempted_to_write_a_SETTINGS_ACK_with_no_pending_SETTINGS());
                return promise.Task;
            }
            SimplePromiseAggregator aggregator = new SimplePromiseAggregator(promise); // , ctx.channel(), ctx.executor()
            // Acknowledge receipt of the settings. We should do this before we process the settings to ensure our
            // remote peer applies these settings before any subsequent frames that we may send which depend upon
            // these new settings. See https://github.com/netty/netty/issues/6520.
            _ = _frameWriter.WriteSettingsAckAsync(ctx, aggregator.NewPromise());

            // We create a "new promise" to make sure that status from both the write and the application are taken into
            // account independently.
            var applySettingsPromise = aggregator.NewPromise();
            try
            {
                RemoteSettings(settings);
                applySettingsPromise.Complete();
            }
            catch (Exception e)
            {
                applySettingsPromise.SetException(e);
                _lifecycleManager.OnError(ctx, true, e);
            }
            return aggregator.DoneAllocatingPromises().Task;
        }

        public virtual Task WritePingAsync(IChannelHandlerContext ctx, bool ack, long data, IPromise promise)
        {
            return _frameWriter.WritePingAsync(ctx, ack, data, promise);
        }

        public virtual Task WritePushPromiseAsync(IChannelHandlerContext ctx, int streamId, int promisedStreamId,
            IHttp2Headers headers, int padding, IPromise promise)
        {
            try
            {
                if (_connection.GoAwayReceived())
                {
                    ThrowHelper.ThrowConnectionError_SendingPushPromiseAfterGoAwayReceived();
                }

                var stream = RequireStream(streamId);
                // Reserve the promised stream.
                _ = _connection.Local.ReservePushStream(promisedStreamId, stream);

                promise = promise.Unvoid();
                var future = _frameWriter.WritePushPromiseAsync(ctx, streamId, promisedStreamId, headers, padding, promise);
                // Writing headers may fail during the encode state if they violate HPACK limits.
                var failureCause = future.Exception;
                if (failureCause is null)
                {
                    // This just sets internal stream state which is used elsewhere in the codec and doesn't
                    // necessarily mean the write will complete successfully.
                    _ = stream.PushPromiseSent();

                    if (!future.IsSuccess())
                    {
                        // Either the future is not done or failed in the meantime.
                        NotifyLifecycleManagerOnError(future, _lifecycleManager, ctx);
                    }
                }
                else
                {
                    _lifecycleManager.OnError(ctx, true, failureCause.InnerException);
                }
                return future;
            }
            catch (Exception t)
            {
                _lifecycleManager.OnError(ctx, true, t);
                _ = promise.TrySetException(t);
                return promise.Task;
            }
        }

        public virtual Task WriteGoAwayAsync(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode,
            IByteBuffer debugData, IPromise promise)
        {
            return _lifecycleManager.GoAwayAsync(ctx, lastStreamId, errorCode, debugData, promise);
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
            return _frameWriter.WriteFrameAsync(ctx, frameType, streamId, flags, payload, promise);
        }

        public virtual void Close()
        {
            _frameWriter.Close();
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose() => Close();

        public IHttp2FrameWriterConfiguration Configuration => _frameWriter.Configuration;

        private IHttp2Stream RequireStream(int streamId)
        {
            var stream = _connection.Stream(streamId);
            if (stream is null)
            {
                ThrowHelper.ThrowArgumentException_RequireStream(_connection, streamId);
            }
            return stream;
        }

        public void ConsumeReceivedSettings(Http2Settings settings)
        {
            if (_outstandingRemoteSettingsQueue == null)
            {
                _outstandingRemoteSettingsQueue = new Deque<Http2Settings>(2);
            }
            _outstandingRemoteSettingsQueue.AddLast​(settings);
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
            private readonly CoalescingBufferQueue _queue;
            private int _dataSize;

            public FlowControlledData(DefaultHttp2ConnectionEncoder encoder,
                IHttp2Stream stream, IByteBuffer buf, int padding, bool endOfStream, IPromise promise, IChannel channel)
                : base(encoder, stream, padding, endOfStream, promise)
            {
                _queue = new CoalescingBufferQueue(channel);
                _queue.Add(buf, promise);
                _dataSize = _queue.ReadableBytes();
            }

            public override int Size => _dataSize + _padding;

            public override void Error(IChannelHandlerContext ctx, Exception cause)
            {
                _queue.ReleaseAndFailAll(cause);
                // Don't update dataSize because we need to ensure the size() method returns a consistent size even after
                // error so we don't invalidate flow control when returning bytes to flow control.
                //
                // That said we will set dataSize and padding to 0 in the write(...) method if we cleared the queue
                // because of an error.
                _owner._lifecycleManager.OnError(ctx, true, cause);
            }

            public override void Write(IChannelHandlerContext ctx, int allowedBytes)
            {
                int queuedData = _queue.ReadableBytes();
                if (!_endOfStream)
                {
                    if (0u >= (uint)queuedData)
                    {
                        if (_queue.IsEmpty())
                        {
                            // When the queue is empty it means we did clear it because of an error(...) call
                            // (as otherwise we will have at least 1 entry in there), which will happen either when called
                            // explicit or when the write itself fails. In this case just set dataSize and padding to 0
                            // which will signal back that the whole frame was consumed.
                            //
                            // See https://github.com/netty/netty/issues/8707.
                            _padding = _dataSize = 0;
                        }
                        else
                        {
                            // There's no need to write any data frames because there are only empty data frames in the
                            // queue and it is not end of stream yet. Just complete their promises by getting the buffer
                            // corresponding to 0 bytes and writing it to the channel (to preserve notification order).
                            var writePromise0 = ctx.NewPromise();
                            AddListener(writePromise0);
                            _ = ctx.WriteAsync(_queue.Remove(0, writePromise0), writePromise0);
                        }
                        return;
                    }

                    if (0u >= (uint)allowedBytes)
                    {
                        return;
                    }
                }

                // Determine how much data to write.
                int writableData = Math.Min(queuedData, allowedBytes);
                var writePromise = ctx.NewPromise();
                AddListener(writePromise);
                var toWrite = _queue.Remove(writableData, writePromise);
                _dataSize = _queue.ReadableBytes();

                // Determine how much padding to write.
                int writablePadding = Math.Min(allowedBytes - writableData, _padding);
                _padding -= writablePadding;

                // Write the frame(s).
                _ = _owner._frameWriter.WriteDataAsync(ctx, _stream.Id, toWrite, writablePadding,
                        _endOfStream && 0u >= (uint)Size, writePromise);
            }

            public override bool Merge(IChannelHandlerContext ctx, IHttp2RemoteFlowControlled next)
            {
                if (!(next is FlowControlledData nextData) || (uint)Size > (uint)(int.MaxValue - nextData.Size))
                {
                    return false;
                }
                nextData._queue.CopyTo(_queue);
                _dataSize = _queue.ReadableBytes();
                // Given that we're merging data into a frame it doesn't really make sense to accumulate padding.
                _padding = Math.Max(_padding, nextData._padding);
                _endOfStream = nextData._endOfStream;
                return true;
            }
        }

        private static void NotifyLifecycleManagerOnError(Task future, IHttp2LifecycleManager lm, IChannelHandlerContext ctx)
        {
            _ = future.ContinueWith(NotifyLifecycleManagerOnErrorAction, (lm, ctx), TaskContinuationOptions.ExecuteSynchronously);
        }

        private static readonly Action<Task, object> NotifyLifecycleManagerOnErrorAction = (t, s) => NotifyLifecycleManagerOnError0(t, s);
        private static void NotifyLifecycleManagerOnError0(Task t, object s)
        {
            var (lm, ctx) = ((IHttp2LifecycleManager, IChannelHandlerContext))s;
            var cause = t.Exception;
            if (cause is object)
            {
                lm.OnError(ctx, true, cause.InnerException);
            }
        }

        /// <summary>
        /// Wrap headers so they can be written subject to flow-control. While headers do not have cost against the
        /// flow-control window their order with respect to other frames must be maintained, hence if a DATA frame is
        /// blocked on flow-control a HEADER frame must wait until this frame has been written.
        /// </summary>
        sealed class FlowControlledHeaders : FlowControlledBase
        {
            private readonly IHttp2Headers _headers;
            private readonly bool _hasPriority;
            private readonly int _streamDependency;
            private readonly short _weight;
            private readonly bool _exclusive;

            public FlowControlledHeaders(DefaultHttp2ConnectionEncoder encoder,
                IHttp2Stream stream, IHttp2Headers headers, bool hasPriority,
                int streamDependency, short weight, bool exclusive,
                int padding, bool endOfStream, IPromise promise)
                : base(encoder, stream, padding, endOfStream, promise.Unvoid())
            {
                _headers = headers;
                _hasPriority = hasPriority;
                _streamDependency = streamDependency;
                _weight = weight;
                _exclusive = exclusive;
            }

            public override int Size => 0;

            public override void Error(IChannelHandlerContext ctx, Exception cause)
            {
                if (ctx is object)
                {
                    _owner._lifecycleManager.OnError(ctx, true, cause);
                }
                _ = _promise.TrySetException(cause);
            }

            public override void Write(IChannelHandlerContext ctx, int allowedBytes)
            {
                var isInformational = ValidateHeadersSentState(_stream, _headers, _owner._connection.IsServer, _endOfStream);
                // The code is currently requiring adding this listener before writing, in order to call onError() before
                // closeStreamLocal().
                AddListener(_promise);

                var f = SendHeadersAsync(_owner._frameWriter, ctx, _stream.Id, _headers, _hasPriority, _streamDependency,
                        _weight, _exclusive, _padding, _endOfStream, _promise);
                // Writing headers may fail during the encode state if they violate HPACK limits.
                var failureCause = f.Exception;
                if (failureCause is null)
                {
                    // This just sets internal stream state which is used elsewhere in the codec and doesn't
                    // necessarily mean the write will complete successfully.
                    _ = _stream.HeadersSent(isInformational);
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
            protected readonly DefaultHttp2ConnectionEncoder _owner;
            protected readonly IHttp2Stream _stream;
            protected IPromise _promise;
            protected bool _endOfStream;
            protected int _padding;

            public FlowControlledBase(DefaultHttp2ConnectionEncoder encoder, IHttp2Stream stream, int padding, bool endOfStream, IPromise promise)
            {
                if ((uint)padding > SharedConstants.TooBigOrNegative)
                {
                    ThrowHelper.ThrowArgumentException_PositiveOrZero(ExceptionArgument.padding);
                }
                _owner = encoder;
                _padding = padding;
                _endOfStream = endOfStream;
                _stream = stream;
                _promise = promise;

            }

            private static readonly Action<Task, object> LinkOutcomeContinuationAction = (t, s) => LinkOutcomeContinuation(t, s);
            private static void LinkOutcomeContinuation(Task task, object state)
            {
                if (task.IsFailure())
                {
                    var self = (FlowControlledBase)state;
                    self.Error(self._owner.FlowController.ChannelHandlerContext, task.Exception.InnerException);
                }
            }

            protected void AddListener(IPromise p)
            {
                _ = p.Task.ContinueWith(LinkOutcomeContinuationAction, this, TaskContinuationOptions.ExecuteSynchronously);
            }

            public abstract void Error(IChannelHandlerContext ctx, Exception cause);

            public abstract bool Merge(IChannelHandlerContext ctx, IHttp2RemoteFlowControlled next);

            public abstract int Size { get; }

            public abstract void Write(IChannelHandlerContext ctx, int allowedBytes);

            public void WriteComplete()
            {
                if (_endOfStream)
                {
                    _owner._lifecycleManager.CloseStreamLocal(_stream, _promise.Task);
                }
            }
        }
    }
}