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
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Basic implementation of <see cref="IHttp2RemoteFlowController"/>.
    /// <para>This class is <c>NOT</c> thread safe. The assumption is all methods must be invoked from a single thread.
    /// Typically this thread is the event loop thread for the <see cref="IChannelHandlerContext"/> managed by this class.</para>
    /// </summary>
    public class DefaultHttp2RemoteFlowController : Http2ConnectionAdapter, IHttp2RemoteFlowController
    {
        private static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<DefaultHttp2RemoteFlowController>();
        private const int MinWritableChunk = 32 * 1024;

        private readonly IHttp2Connection _connection;
        private readonly IHttp2ConnectionPropertyKey _stateKey;
        private readonly IStreamByteDistributor _streamByteDistributor;
        private readonly FlowState _connectionState;
        private int _initialWindowSize = Http2CodecUtil.DefaultWindowSize;
        private WritabilityMonitor _monitor;
        private IChannelHandlerContext _ctx;

        public DefaultHttp2RemoteFlowController(IHttp2Connection connection)
            : this(connection, (IHttp2RemoteFlowControllerListener)null)
        {
        }

        public DefaultHttp2RemoteFlowController(IHttp2Connection connection, IStreamByteDistributor streamByteDistributor)
            : this(connection, streamByteDistributor, null)
        {
        }

        public DefaultHttp2RemoteFlowController(IHttp2Connection connection, IHttp2RemoteFlowControllerListener listener)
            : this(connection, new WeightedFairQueueByteDistributor(connection), listener)
        {
        }

        public DefaultHttp2RemoteFlowController(IHttp2Connection connection, IStreamByteDistributor streamByteDistributor, IHttp2RemoteFlowControllerListener listener)
        {
            if (connection is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.connection); }
            if (streamByteDistributor is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.streamByteDistributor); }
            _connection = connection;
            _streamByteDistributor = streamByteDistributor;

            // Add a flow state for the connection.
            _stateKey = connection.NewKey();
            _connectionState = new FlowState(this, _connection.ConnectionStream);
            _ = connection.ConnectionStream.SetProperty(_stateKey, _connectionState);

            // Monitor may depend upon connectionState, and so initialize after connectionState
            Listener(listener);
            _monitor.WindowSize(_connectionState, _initialWindowSize);

            // Register for notification of new streams.
            connection.AddListener(this);
        }

        public override void OnStreamAdded(IHttp2Stream stream)
        {
            // If the stream state is not open then the stream is not yet eligible for flow controlled frames and
            // only requires the ReducedFlowState. Otherwise the full amount of memory is required.
            _ = stream.SetProperty(_stateKey, new FlowState(this, stream));
        }

        public override void OnStreamActive(IHttp2Stream stream)
        {
            // If the object was previously created, but later activated then we have to ensure the proper
            // _initialWindowSize is used.
            _monitor.WindowSize(GetState(stream), _initialWindowSize);
        }

        public override void OnStreamClosed(IHttp2Stream stream)
        {
            // Any pending frames can never be written, cancel and
            // write errors for any pending frames.
            GetState(stream).Cancel(Http2Error.StreamClosed);
        }

        public override void OnStreamHalfClosed(IHttp2Stream stream)
        {
            if (Http2StreamState.HalfClosedLocal == stream.State)
            {
                // When this method is called there should not be any
                // pending frames left if the API is used correctly. However,
                // it is possible that a erroneous application can sneak
                // in a frame even after having already written a frame with the
                // END_STREAM flag set, as the stream state might not transition
                // immediately to HALF_CLOSED_LOCAL / CLOSED due to flow control
                // delaying the write.
                //
                // This is to cancel any such illegal writes.
                GetState(stream).Cancel(Http2Error.StreamClosed);
            }
        }

        /// <inheritdoc />
        /// <remarks>Any queued <see cref="IHttp2RemoteFlowControlled"/> objects will be sent.</remarks>
        public void SetChannelHandlerContext(IChannelHandlerContext ctx)
        {
            if (ctx is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.ctx); }
            _ctx = ctx;

            // Writing the pending bytes will not check writability change and instead a writability change notification
            // to be provided by an explicit call.
            ChannelWritabilityChanged();

            // Don't worry about cleaning up queued frames here if ctx is null. It is expected that all streams will be
            // closed and the queue cleanup will occur when the stream state transitions occur.

            // If any frames have been queued up, we should send them now that we have a channel context.
            if (IsChannelWritable())
            {
                WritePendingBytes();
            }
        }

        public IChannelHandlerContext ChannelHandlerContext => _ctx;

        public void SetInitialWindowSize(int newWindowSize)
        {
            Debug.Assert(_ctx is null || _ctx.Executor.InEventLoop);
            _monitor.InitialWindowSize(newWindowSize);
        }

        public int InitialWindowSize => _initialWindowSize;

        public int GetWindowSize(IHttp2Stream stream)
        {
            return GetState(stream).WindowSize;
        }

        public bool IsWritable(IHttp2Stream stream)
        {
            return _monitor.IsWritable(GetState(stream));
        }

        public void ChannelWritabilityChanged()
        {
            _monitor.ChannelWritabilityChange();
        }

        public void UpdateDependencyTree(int childStreamId, int parentStreamId, short weight, bool exclusive)
        {
            // It is assumed there are all validated at a higher level. For example in the Http2FrameReader.
            Debug.Assert(weight >= Http2CodecUtil.MinWeight && weight <= Http2CodecUtil.MaxWeight, "Invalid weight");
            Debug.Assert(childStreamId != parentStreamId, "A stream cannot depend on itself");
            Debug.Assert(childStreamId > 0 && parentStreamId >= 0, "childStreamId must be > 0. parentStreamId must be >= 0.");

            _streamByteDistributor.UpdateDependencyTree(childStreamId, parentStreamId, weight, exclusive);
        }

        bool IsChannelWritable()
        {
            return _ctx is object && IsChannelWritable0();
        }

        bool IsChannelWritable0()
        {
            return _ctx.Channel.IsWritable;
        }

        public void Listener(IHttp2RemoteFlowControllerListener listener)
        {
            _monitor = listener is null ? new WritabilityMonitor(this) : new ListenerWritabilityMonitor(this, listener);
        }

        public void IncrementWindowSize(IHttp2Stream stream, int delta)
        {
            Debug.Assert(_ctx is null || _ctx.Executor.InEventLoop);
            _monitor.IncrementWindowSize(GetState(stream), delta);
        }

        public void AddFlowControlled(IHttp2Stream stream, IHttp2RemoteFlowControlled frame)
        {
            // The context can be null assuming the frame will be queued and send later when the context is set.
            Debug.Assert(_ctx is null || _ctx.Executor.InEventLoop);
            if (frame is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.frame); }
            try
            {
                _monitor.EnqueueFrame(GetState(stream), frame);
            }
            catch (Exception t)
            {
                frame.Error(_ctx, t);
            }
        }

        public bool HasFlowControlled(IHttp2Stream stream)
        {
            return GetState(stream).HasFrame;
        }

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        private FlowState GetState(IHttp2Stream stream)
        {
            return stream.GetProperty<FlowState>(_stateKey);
        }

        /// <summary>
        /// Returns the flow control window for the entire connection.
        /// </summary>
        /// <returns></returns>
        int ConnectionWindowSize()
        {
            return _connectionState.WindowSize;
        }

        int MinUsableChannelBytes()
        {
            // The current allocation algorithm values "fairness" and doesn't give any consideration to "goodput". It
            // is possible that 1 byte will be allocated to many streams. In an effort to try to make "goodput"
            // reasonable with the current allocation algorithm we have this "cheap" check up front to ensure there is
            // an "adequate" amount of connection window before allocation is attempted. This is not foolproof as if the
            // number of streams is >= this minimal number then we may still have the issue, but the idea is to narrow the
            // circumstances in which this can happen without rewriting the allocation algorithm.
            return Math.Max(_ctx.Channel.Configuration.WriteBufferLowWaterMark, MinWritableChunk);
        }

        int MaxUsableChannelBytes()
        {
            // If the channel isWritable, allow at least minUsableChannelBytes.
            int channelWritableBytes = (int)Math.Min(int.MaxValue, _ctx.Channel.BytesBeforeUnwritable);
            int usableBytes = channelWritableBytes > 0 ? Math.Max(channelWritableBytes, MinUsableChannelBytes()) : 0;

            // Clip the usable bytes by the connection window.
            return Math.Min(_connectionState.WindowSize, usableBytes);
        }

        /// <summary>
        /// The amount of bytes that can be supported by underlying <see cref="IChannel"/> without
        /// queuing "too-much".
        /// </summary>
        /// <returns></returns>
        int WritableBytes()
        {
            return Math.Min(ConnectionWindowSize(), MaxUsableChannelBytes());
        }

        public void WritePendingBytes()
        {
            _monitor.WritePendingBytes();
        }

        /// <summary>
        /// The remote flow control state for a single stream.
        /// </summary>
        sealed class FlowState : IStreamByteDistributorStreamState
        {
            private readonly DefaultHttp2RemoteFlowController _controller;
            private readonly IHttp2Stream _stream;
            private readonly Deque<IHttp2RemoteFlowControlled> _pendingWriteQueue;
            private int _window;
            private long _pendingBytes;
            private bool _markedWritable;

            /// <summary>
            /// Set to true while a frame is being written, false otherwise.
            /// </summary>
            private bool _writing;

            /// <summary>
            /// Set to true if cancel() was called.
            /// </summary>
            private bool _cancelled;

            internal FlowState(DefaultHttp2RemoteFlowController controller, IHttp2Stream stream)
            {
                _controller = controller;
                _stream = stream;
                _pendingWriteQueue = new Deque<IHttp2RemoteFlowControlled>(2);
            }

            /// <summary>
            /// Determine if the stream associated with this object is writable.
            /// </summary>
            /// <returns><c>true</c> if the stream associated with this object is writable.</returns>
            internal bool IsWritable()
            {
                return WindowSize > PendingBytes && !_cancelled;
            }

            /// <summary>
            /// The stream this state is associated with.
            /// </summary>
            public IHttp2Stream Stream => _stream;

            /// <summary>
            /// Returns the parameter from the last call to <see cref="MarkedWritability(bool)"/>.
            /// </summary>
            internal bool MarkedWritability()
            {
                return _markedWritable;
            }

            /// <summary>
            /// Save the state of writability.
            /// </summary>
            internal void MarkedWritability(bool isWritable)
            {
                _markedWritable = isWritable;
            }

            public int WindowSize => _window;

            /// <summary>
            /// Reset the window size for this stream.
            /// </summary>
            /// <param name="initialWindowSize"></param>
            internal void SetWindowSize(int initialWindowSize)
            {
                _window = initialWindowSize;
            }

            /// <summary>
            /// Write the allocated bytes for this stream.
            /// </summary>
            /// <param name="allocated"></param>
            /// <returns>the number of bytes written for a stream or <c>-1</c> if no write occurred.</returns>
            internal int WriteAllocatedBytes(int allocated)
            {
                int initialAllocated = allocated;
                int writtenBytes;
                // In case an exception is thrown we want to remember it and pass it to cancel(Exception).
                Exception cause = null;
                IHttp2RemoteFlowControlled frame;
                try
                {
                    Debug.Assert(!_writing);
                    _writing = true;

                    // Write the remainder of frames that we are allowed to
                    bool writeOccurred = false;
                    while (!_cancelled && (frame = Peek()) is object)
                    {
                        int maxBytes = Math.Min(allocated, WritableWindow());
                        if (maxBytes <= 0 && frame.Size > 0)
                        {
                            // The frame still has data, but the amount of allocated bytes has been exhausted.
                            // Don't write needless empty frames.
                            break;
                        }

                        writeOccurred = true;
                        int initialFrameSize = frame.Size;
                        try
                        {
                            frame.Write(_controller._ctx, Math.Max(0, maxBytes));
                            if (0u >= (uint)frame.Size)
                            {
                                // This frame has been fully written, remove this frame and notify it.
                                // Since we remove this frame first, we're guaranteed that its error
                                // method will not be called when we call cancel.
                                _ = _pendingWriteQueue.TryRemoveFirst(out _);
                                frame.WriteComplete();
                            }
                        }
                        finally
                        {
                            // Decrement allocated by how much was actually written.
                            allocated -= initialFrameSize - frame.Size;
                        }
                    }

                    if (!writeOccurred)
                    {
                        // Either there was no frame, or the amount of allocated bytes has been exhausted.
                        return -1;
                    }
                }
                catch (Exception t)
                {
                    // Mark the state as cancelled, we'll clear the pending queue via cancel() below.
                    _cancelled = true;
                    cause = t;
                }
                finally
                {
                    _writing = false;
                    // Make sure we always decrement the flow control windows
                    // by the bytes written.
                    writtenBytes = initialAllocated - allocated;

                    DecrementPendingBytes(writtenBytes, false);
                    DecrementFlowControlWindow(writtenBytes);

                    // If a cancellation occurred while writing, call cancel again to
                    // clear and error all of the pending writes.
                    if (_cancelled)
                    {
                        Cancel(Http2Error.InternalError, cause);
                    }
                }

                return writtenBytes;
            }

            /// <summary>
            /// Increments the flow control window for this stream by the given delta and returns the new value.
            /// </summary>
            /// <param name="delta"></param>
            /// <returns></returns>
            internal int IncrementStreamWindow(int delta)
            {
                if (delta > 0 && int.MaxValue - delta < _window)
                {
                    ThrowHelper.ThrowStreamError_WindowSizeOverflowForStream(_stream.Id);
                }

                _window += delta;

                _controller._streamByteDistributor.UpdateStreamableBytes(this);
                return _window;
            }

            /// <summary>
            /// Returns the maximum writable window (minimum of the stream and connection windows).
            /// </summary>
            int WritableWindow()
            {
                return Math.Min(_window, _controller.ConnectionWindowSize());
            }

            public long PendingBytes => _pendingBytes;

            /// <summary>
            /// Adds the <paramref name="frame"/> to the pending queue and increments the pending byte count.
            /// </summary>
            /// <param name="frame"></param>
            internal void EnqueueFrame(IHttp2RemoteFlowControlled frame)
            {
                var last = _pendingWriteQueue.LastOrDefault;
                if (last is null)
                {
                    EnqueueFrameWithoutMerge(frame);
                    return;
                }

                int lastSize = last.Size;
                if (last.Merge(_controller._ctx, frame))
                {
                    IncrementPendingBytes(last.Size - lastSize, true);
                    return;
                }

                EnqueueFrameWithoutMerge(frame);
            }

            void EnqueueFrameWithoutMerge(IHttp2RemoteFlowControlled frame)
            {
                _pendingWriteQueue.AddLast​(frame);
                // This must be called after adding to the queue in order so that hasFrame() is
                // updated before updating the stream state.
                IncrementPendingBytes(frame.Size, true);
            }

            public bool HasFrame => _pendingWriteQueue.NonEmpty;

            /// <summary>
            /// Returns the head of the pending queue, or <c>null</c> if empty.
            /// </summary>
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            IHttp2RemoteFlowControlled Peek()
            {
                return _pendingWriteQueue.FirstOrDefault;
            }

            /// <summary>
            /// Clears the pending queue and writes errors for each remaining frame.
            /// </summary>
            /// <param name="error">the <see cref="Http2Error"/> to use.</param>
            /// <param name="cause">the <see cref="Exception"/> that caused this method to be invoked.</param>
            internal void Cancel(Http2Error error, Exception cause = null)
            {
                _cancelled = true;
                // Ensure that the queue can't be modified while we are writing.
                if (_writing) { return; }

                if (_pendingWriteQueue.TryRemoveFirst(out IHttp2RemoteFlowControlled frame))
                {
                    // Only create exception once and reuse to reduce overhead of filling in the stacktrace.
                    Http2Exception exception = ThrowHelper.GetStreamError_StreamClosedBeforeWriteCouldTakePlace(
                        _stream.Id, error, cause);
                    do
                    {
                        WriteError(frame, exception);
                    } while (_pendingWriteQueue.TryRemoveFirst(out frame));
                }

                _controller._streamByteDistributor.UpdateStreamableBytes(this);

                _controller._monitor.StateCancelled(this);
            }

            /// <summary>
            /// Increments the number of pending bytes for this node and optionally updates the <see cref="IStreamByteDistributor"/>
            /// </summary>
            /// <param name="numBytes"></param>
            /// <param name="updateStreamableBytes"></param>
            void IncrementPendingBytes(int numBytes, bool updateStreamableBytes)
            {
                _pendingBytes += numBytes;
                _controller._monitor.IncrementPendingBytes(numBytes);
                if (updateStreamableBytes)
                {
                    _controller._streamByteDistributor.UpdateStreamableBytes(this);
                }
            }

            /// <summary>
            /// If this frame is in the pending queue, decrements the number of pending bytes for the stream.
            /// </summary>
            /// <param name="bytes"></param>
            /// <param name="updateStreamableBytes"></param>
            void DecrementPendingBytes(int bytes, bool updateStreamableBytes)
            {
                IncrementPendingBytes(-bytes, updateStreamableBytes);
            }

            /// <summary>
            /// Decrement the per stream and connection flow control window by <paramref name="bytes"/>.
            /// </summary>
            /// <param name="bytes"></param>
            void DecrementFlowControlWindow(int bytes)
            {
                try
                {
                    int negativeBytes = -bytes;
                    _ = _controller._connectionState.IncrementStreamWindow(negativeBytes);
                    _ = IncrementStreamWindow(negativeBytes);
                }
                catch (Http2Exception e)
                {
                    // Should never get here since we're decrementing.
                    ThrowHelper.ThrowInvalidOperationException_InvalidWindowStateWhenWritingFrame(e);
                }
            }

            /// <summary>
            /// Discards this <see cref="IHttp2RemoteFlowControlled"/>, writing an error. If this frame is in the pending queue,
            /// the unwritten bytes are removed from this branch of the priority tree.
            /// </summary>
            /// <param name="frame"></param>
            /// <param name="cause"></param>
            void WriteError(IHttp2RemoteFlowControlled frame, Http2Exception cause)
            {
                IChannelHandlerContext ctx = _controller._ctx;
                Debug.Assert(ctx is object);
                DecrementPendingBytes(frame.Size, true);
                frame.Error(ctx, cause);
            }
        }

        /// <summary>
        /// Abstract class which provides common functionality for writability monitor implementations.
        /// </summary>
        class WritabilityMonitor : IStreamByteDistributorWriter
        {
            protected readonly DefaultHttp2RemoteFlowController _controller;

            private bool _inWritePendingBytes;
            private long _totalPendingBytes;

            internal WritabilityMonitor(DefaultHttp2RemoteFlowController controller)
            {
                _controller = controller;
            }

            void IStreamByteDistributorWriter.Write(IHttp2Stream stream, int numBytes) => _controller.GetState(stream).WriteAllocatedBytes(numBytes);

            /// <summary>
            /// Called when the writability of the underlying channel changes.
            /// If a write occurs and an exception happens in the write operation.
            /// </summary>
            protected internal virtual void ChannelWritabilityChange() { }

            /// <summary>
            /// Called when the state is cancelled.
            /// </summary>
            /// <param name="state">the state that was cancelled.</param>
            protected internal virtual void StateCancelled(FlowState state) { }

            /// <summary>
            /// Set the initial window size for <paramref name="state"/>.
            /// </summary>
            /// <param name="state">the state to change the initial window size for.</param>
            /// <param name="initialWindowSize">the size of the window in bytes.</param>
            protected internal virtual void WindowSize(FlowState state, int initialWindowSize)
            {
                state.SetWindowSize(initialWindowSize);
            }

            /// <summary>
            /// Increment the window size for a particular stream.
            /// </summary>
            /// <param name="state">the state associated with the stream whose window is being incremented.</param>
            /// <param name="delta">The amount to increment by.</param>
            /// <remarks>If this operation overflows the window for <paramref name="state"/>.</remarks>
            protected internal virtual void IncrementWindowSize(FlowState state, int delta)
            {
                _ = state.IncrementStreamWindow(delta);
            }

            /// <summary>
            /// Add a frame to be sent via flow control.
            /// </summary>
            /// <param name="state">The state associated with the stream which the <paramref name="frame"/> is associated with.</param>
            /// <param name="frame">the frame to enqueue.</param>
            /// <remarks>If a writability error occurs.</remarks>
            protected internal virtual void EnqueueFrame(FlowState state, IHttp2RemoteFlowControlled frame)
            {
                state.EnqueueFrame(frame);
            }

            /// <summary>
            /// Increment the total amount of pending bytes for all streams. When any stream's pending bytes changes
            /// method should be called.
            /// </summary>
            /// <param name="delta">The amount to increment by.</param>
            internal void IncrementPendingBytes(int delta)
            {
                _totalPendingBytes += delta;

                // Notification of writibilty change should be delayed until the end of the top level event.
                // This is to ensure the flow controller is more consistent state before calling external listener methods.
            }

            /// <summary>
            /// Determine if the stream associated with <paramref name="state"/> is writable.
            /// </summary>
            /// <param name="state">The state which is associated with the stream to test writability for.</param>
            /// <returns><c>true</c> if <see cref="FlowState.Stream"/> is writable. <c>false</c> otherwise.</returns>
            protected internal bool IsWritable(FlowState state)
            {
                return IsWritableConnection() && state.IsWritable();
            }

            internal void WritePendingBytes()
            {
                // Reentry is not permitted during the byte distribution process. It may lead to undesirable distribution of
                // bytes and even infinite loops. We protect against reentry and make sure each call has an opportunity to
                // cause a distribution to occur. This may be useful for example if the channel's writability changes from
                // Writable -> Not Writable (because we are writing) -> Writable (because the user flushed to make more room
                // in the channel outbound buffer).
                if (_inWritePendingBytes)
                {
                    return;
                }

                _inWritePendingBytes = true;
                try
                {
                    int bytesToWrite = _controller.WritableBytes();
                    // Make sure we always write at least once, regardless if we have bytesToWrite or not.
                    // This ensures that zero-length frames will always be written.
                    while (true)
                    {
                        if (!_controller._streamByteDistributor.Distribute(bytesToWrite, this) ||
                            (bytesToWrite = _controller.WritableBytes()) <= 0 ||
                            !_controller.IsChannelWritable0())
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    _inWritePendingBytes = false;
                }
            }

            protected internal virtual void InitialWindowSize(int newWindowSize)
            {
                if ((uint)newWindowSize > SharedConstants.TooBigOrNegative)
                {
                    ThrowHelper.ThrowArgumentException_PositiveOrZero(newWindowSize, ExceptionArgument.newWindowSize);
                }

                int delta = newWindowSize - _controller._initialWindowSize;
                _controller._initialWindowSize = newWindowSize;
                _ = _controller._connection.ForEachActiveStream(new Http2StreamVisitor(_controller, delta));

                if (delta > 0 && _controller.IsChannelWritable())
                {
                    // The window size increased, send any pending frames for all streams.
                    WritePendingBytes();
                }
            }

            sealed class Http2StreamVisitor : IHttp2StreamVisitor
            {
                private readonly DefaultHttp2RemoteFlowController _rfc;
                private readonly int _delta;

                public Http2StreamVisitor(DefaultHttp2RemoteFlowController rfc, int delta)
                {
                    _rfc = rfc;
                    _delta = delta;
                }

                public bool Visit(IHttp2Stream stream)
                {
                    _ = _rfc.GetState(stream).IncrementStreamWindow(_delta);
                    return true;
                }
            }

            protected bool IsWritableConnection()
            {
                return _controller._connectionState.WindowSize - _totalPendingBytes > 0 && _controller.IsChannelWritable();
            }
        }

        /// <summary>
        /// Writability of a <c>stream</c> is calculated using the following:
        /// <![CDATA[
        /// Connection Window - Total Queued Bytes > 0 &&
        /// Stream Window - Bytes Queued for Stream > 0 &&
        /// isChannelWritable()
        /// ]]>
        /// </summary>
        sealed class ListenerWritabilityMonitor : WritabilityMonitor, IHttp2StreamVisitor
        {
            readonly IHttp2RemoteFlowControllerListener _listener;

            internal ListenerWritabilityMonitor(DefaultHttp2RemoteFlowController controller, IHttp2RemoteFlowControllerListener listener)
                : base(controller)
            {
                _listener = listener;
            }

            bool IHttp2StreamVisitor.Visit(IHttp2Stream stream)
            {
                FlowState state = _controller.GetState(stream);
                if (IsWritable(state) != state.MarkedWritability())
                {
                    NotifyWritabilityChanged(state);
                }

                return true;
            }

            protected internal override void WindowSize(FlowState state, int initialWindowSize)
            {
                base.WindowSize(state, initialWindowSize);
                try
                {
                    CheckStateWritability(state);
                }
                catch (Http2Exception e)
                {
                    ThrowHelper.ThrowHttp2RuntimeException_CaughtUnexpectedExceptionFromWindow(e);
                }
            }

            protected internal override void IncrementWindowSize(FlowState state, int delta)
            {
                base.IncrementWindowSize(state, delta);
                CheckStateWritability(state);
            }

            protected internal override void InitialWindowSize(int newWindowSize)
            {
                base.InitialWindowSize(newWindowSize);
                if (IsWritableConnection())
                {
                    // If the write operation does not occur we still need to check all streams because they
                    // may have transitioned from writable to not writable.
                    CheckAllWritabilityChanged();
                }
            }

            protected internal override void EnqueueFrame(FlowState state, IHttp2RemoteFlowControlled frame)
            {
                base.EnqueueFrame(state, frame);
                CheckConnectionThenStreamWritabilityChanged(state);
            }

            protected internal override void StateCancelled(FlowState state)
            {
                try
                {
                    CheckConnectionThenStreamWritabilityChanged(state);
                }
                catch (Http2Exception e)
                {
                    ThrowHelper.ThrowHttp2RuntimeException_CaughtUnexpectedExceptionFromCheckAllWritabilityChanged(e);
                }
            }

            protected internal override void ChannelWritabilityChange()
            {
                if (_controller._connectionState.MarkedWritability() != _controller.IsChannelWritable())
                {
                    CheckAllWritabilityChanged();
                }
            }

            void CheckStateWritability(FlowState state)
            {
                if (IsWritable(state) != state.MarkedWritability())
                {
                    if (state == _controller._connectionState)
                    {
                        CheckAllWritabilityChanged();
                    }
                    else
                    {
                        NotifyWritabilityChanged(state);
                    }
                }
            }

            void NotifyWritabilityChanged(FlowState state)
            {
                state.MarkedWritability(!state.MarkedWritability());
                try
                {
                    _listener.WritabilityChanged(state.Stream);
                }
                catch (Exception cause)
                {
                    Logger.CaughtExceptionFromListenerWritabilityChanged(cause);
                }
            }

            void CheckConnectionThenStreamWritabilityChanged(FlowState state)
            {
                // It is possible that the connection window and/or the individual stream writability could change.
                if (IsWritableConnection() != _controller._connectionState.MarkedWritability())
                {
                    CheckAllWritabilityChanged();
                }
                else if (IsWritable(state) != state.MarkedWritability())
                {
                    NotifyWritabilityChanged(state);
                }
            }

            void CheckAllWritabilityChanged()
            {
                // Make sure we mark that we have notified as a result of this change.
                _controller._connectionState.MarkedWritability(IsWritableConnection());
                _ = _controller._connection.ForEachActiveStream(this);
            }
        }
    }
}