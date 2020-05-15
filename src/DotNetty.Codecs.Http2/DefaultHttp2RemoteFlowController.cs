// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Diagnostics;
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
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<DefaultHttp2RemoteFlowController>();
        const int MinWritableChunk = 32 * 1024;

        readonly IHttp2Connection connection;
        readonly IHttp2ConnectionPropertyKey stateKey;
        readonly IStreamByteDistributor streamByteDistributor;
        readonly FlowState connectionState;
        int initialWindowSize = Http2CodecUtil.DefaultWindowSize;
        WritabilityMonitor monitor;
        IChannelHandlerContext ctx;

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
            this.connection = connection;
            this.streamByteDistributor = streamByteDistributor;

            // Add a flow state for the connection.
            this.stateKey = connection.NewKey();
            this.connectionState = new FlowState(this, this.connection.ConnectionStream);
            connection.ConnectionStream.SetProperty(this.stateKey, this.connectionState);

            // Monitor may depend upon connectionState, and so initialize after connectionState
            this.Listener(listener);
            this.monitor.WindowSize(this.connectionState, this.initialWindowSize);

            // Register for notification of new streams.
            connection.AddListener(this);
        }

        public override void OnStreamAdded(IHttp2Stream stream)
        {
            // If the stream state is not open then the stream is not yet eligible for flow controlled frames and
            // only requires the ReducedFlowState. Otherwise the full amount of memory is required.
            stream.SetProperty(this.stateKey, new FlowState(this, stream));
        }

        public override void OnStreamActive(IHttp2Stream stream)
        {
            // If the object was previously created, but later activated then we have to ensure the proper
            // _initialWindowSize is used.
            this.monitor.WindowSize(this.GetState(stream), this.initialWindowSize);
        }

        public override void OnStreamClosed(IHttp2Stream stream)
        {
            // Any pending frames can never be written, cancel and
            // write errors for any pending frames.
            this.GetState(stream).Cancel(Http2Error.StreamClosed);
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
                this.GetState(stream).Cancel(Http2Error.StreamClosed);
            }
        }

        /// <inheritdoc />
        /// <remarks>Any queued <see cref="IHttp2RemoteFlowControlled"/> objects will be sent.</remarks>
        public void SetChannelHandlerContext(IChannelHandlerContext ctx)
        {
            if (ctx is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.ctx); }
            this.ctx = ctx;

            // Writing the pending bytes will not check writability change and instead a writability change notification
            // to be provided by an explicit call.
            this.ChannelWritabilityChanged();

            // Don't worry about cleaning up queued frames here if ctx is null. It is expected that all streams will be
            // closed and the queue cleanup will occur when the stream state transitions occur.

            // If any frames have been queued up, we should send them now that we have a channel context.
            if (this.IsChannelWritable())
            {
                this.WritePendingBytes();
            }
        }

        public IChannelHandlerContext ChannelHandlerContext => this.ctx;

        public void SetInitialWindowSize(int newWindowSize)
        {
            Debug.Assert(this.ctx is null || this.ctx.Executor.InEventLoop);
            this.monitor.InitialWindowSize(newWindowSize);
        }

        public int InitialWindowSize => this.initialWindowSize;

        public int GetWindowSize(IHttp2Stream stream)
        {
            return this.GetState(stream).WindowSize;
        }

        public bool IsWritable(IHttp2Stream stream)
        {
            return this.monitor.IsWritable(this.GetState(stream));
        }

        public void ChannelWritabilityChanged()
        {
            this.monitor.ChannelWritabilityChange();
        }

        public void UpdateDependencyTree(int childStreamId, int parentStreamId, short weight, bool exclusive)
        {
            // It is assumed there are all validated at a higher level. For example in the Http2FrameReader.
            Debug.Assert(weight >= Http2CodecUtil.MinWeight && weight <= Http2CodecUtil.MaxWeight, "Invalid weight");
            Debug.Assert(childStreamId != parentStreamId, "A stream cannot depend on itself");
            Debug.Assert(childStreamId > 0 && parentStreamId >= 0, "childStreamId must be > 0. parentStreamId must be >= 0.");

            this.streamByteDistributor.UpdateDependencyTree(childStreamId, parentStreamId, weight, exclusive);
        }

        bool IsChannelWritable()
        {
            return this.ctx is object && this.IsChannelWritable0();
        }

        bool IsChannelWritable0()
        {
            return this.ctx.Channel.IsWritable;
        }

        public void Listener(IHttp2RemoteFlowControllerListener listener)
        {
            this.monitor = listener is null ? new WritabilityMonitor(this) : new ListenerWritabilityMonitor(this, listener);
        }

        public void IncrementWindowSize(IHttp2Stream stream, int delta)
        {
            Debug.Assert(this.ctx is null || this.ctx.Executor.InEventLoop);
            this.monitor.IncrementWindowSize(this.GetState(stream), delta);
        }

        public void AddFlowControlled(IHttp2Stream stream, IHttp2RemoteFlowControlled frame)
        {
            // The context can be null assuming the frame will be queued and send later when the context is set.
            Debug.Assert(this.ctx is null || this.ctx.Executor.InEventLoop);
            if (frame is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.frame); }
            try
            {
                this.monitor.EnqueueFrame(this.GetState(stream), frame);
            }
            catch (Exception t)
            {
                frame.Error(this.ctx, t);
            }
        }

        public bool HasFlowControlled(IHttp2Stream stream)
        {
            return this.GetState(stream).HasFrame;
        }

        FlowState GetState(IHttp2Stream stream)
        {
            return stream.GetProperty<FlowState>(this.stateKey);
        }

        /// <summary>
        /// Returns the flow control window for the entire connection.
        /// </summary>
        /// <returns></returns>
        int ConnectionWindowSize()
        {
            return this.connectionState.WindowSize;
        }

        int MinUsableChannelBytes()
        {
            // The current allocation algorithm values "fairness" and doesn't give any consideration to "goodput". It
            // is possible that 1 byte will be allocated to many streams. In an effort to try to make "goodput"
            // reasonable with the current allocation algorithm we have this "cheap" check up front to ensure there is
            // an "adequate" amount of connection window before allocation is attempted. This is not foolproof as if the
            // number of streams is >= this minimal number then we may still have the issue, but the idea is to narrow the
            // circumstances in which this can happen without rewriting the allocation algorithm.
            return Math.Max(this.ctx.Channel.Configuration.WriteBufferLowWaterMark, MinWritableChunk);
        }

        int MaxUsableChannelBytes()
        {
            // If the channel isWritable, allow at least minUsableChannelBytes.
            int channelWritableBytes = (int)Math.Min(int.MaxValue, this.ctx.Channel.BytesBeforeUnwritable);
            int usableBytes = channelWritableBytes > 0 ? Math.Max(channelWritableBytes, this.MinUsableChannelBytes()) : 0;

            // Clip the usable bytes by the connection window.
            return Math.Min(this.connectionState.WindowSize, usableBytes);
        }

        /// <summary>
        /// The amount of bytes that can be supported by underlying <see cref="IChannel"/> without
        /// queuing "too-much".
        /// </summary>
        /// <returns></returns>
        int WritableBytes()
        {
            return Math.Min(this.ConnectionWindowSize(), this.MaxUsableChannelBytes());
        }

        public void WritePendingBytes()
        {
            this.monitor.WritePendingBytes();
        }

        /// <summary>
        /// The remote flow control state for a single stream.
        /// </summary>
        sealed class FlowState : IStreamByteDistributorStreamState
        {
            readonly DefaultHttp2RemoteFlowController controller;
            readonly IHttp2Stream stream;
            readonly Deque<IHttp2RemoteFlowControlled> pendingWriteQueue;
            int window;
            long pendingBytes;
            bool markedWritable;

            /// <summary>
            /// Set to true while a frame is being written, false otherwise.
            /// </summary>
            bool writing;

            /// <summary>
            /// Set to true if cancel() was called.
            /// </summary>
            bool cancelled;

            internal FlowState(DefaultHttp2RemoteFlowController controller, IHttp2Stream stream)
            {
                this.controller = controller;
                this.stream = stream;
                this.pendingWriteQueue = new Deque<IHttp2RemoteFlowControlled>(2);
            }

            /// <summary>
            /// Determine if the stream associated with this object is writable.
            /// </summary>
            /// <returns><c>true</c> if the stream associated with this object is writable.</returns>
            internal bool IsWritable()
            {
                return this.WindowSize > this.PendingBytes && !this.cancelled;
            }

            /// <summary>
            /// The stream this state is associated with.
            /// </summary>
            public IHttp2Stream Stream => this.stream;

            /// <summary>
            /// Returns the parameter from the last call to <see cref="MarkedWritability(bool)"/>.
            /// </summary>
            internal bool MarkedWritability()
            {
                return this.markedWritable;
            }

            /// <summary>
            /// Save the state of writability.
            /// </summary>
            internal void MarkedWritability(bool isWritable)
            {
                this.markedWritable = isWritable;
            }

            public int WindowSize => this.window;

            /// <summary>
            /// Reset the window size for this stream.
            /// </summary>
            /// <param name="initialWindowSize"></param>
            internal void SetWindowSize(int initialWindowSize)
            {
                this.window = initialWindowSize;
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
                    Debug.Assert(!this.writing);
                    this.writing = true;

                    // Write the remainder of frames that we are allowed to
                    bool writeOccurred = false;
                    while (!this.cancelled && (frame = this.Peek()) is object)
                    {
                        int maxBytes = Math.Min(allocated, this.WritableWindow());
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
                            frame.Write(this.controller.ctx, Math.Max(0, maxBytes));
                            if (0u >= (uint)frame.Size)
                            {
                                // This frame has been fully written, remove this frame and notify it.
                                // Since we remove this frame first, we're guaranteed that its error
                                // method will not be called when we call cancel.
                                this.pendingWriteQueue.TryRemoveFromFront(out var _);//.remove();
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
                    this.cancelled = true;
                    cause = t;
                }
                finally
                {
                    this.writing = false;
                    // Make sure we always decrement the flow control windows
                    // by the bytes written.
                    writtenBytes = initialAllocated - allocated;

                    this.DecrementPendingBytes(writtenBytes, false);
                    this.DecrementFlowControlWindow(writtenBytes);

                    // If a cancellation occurred while writing, call cancel again to
                    // clear and error all of the pending writes.
                    if (this.cancelled)
                    {
                        this.Cancel(Http2Error.InternalError, cause);
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
                if (delta > 0 && int.MaxValue - delta < this.window)
                {
                    ThrowHelper.ThrowStreamError_WindowSizeOverflowForStream(this.stream.Id);
                }

                this.window += delta;

                this.controller.streamByteDistributor.UpdateStreamableBytes(this);
                return this.window;
            }

            /// <summary>
            /// Returns the maximum writable window (minimum of the stream and connection windows).
            /// </summary>
            int WritableWindow()
            {
                return Math.Min(this.window, this.controller.ConnectionWindowSize());
            }

            public long PendingBytes => this.pendingBytes;

            /// <summary>
            /// Adds the <paramref name="frame"/> to the pending queue and increments the pending byte count.
            /// </summary>
            /// <param name="frame"></param>
            internal void EnqueueFrame(IHttp2RemoteFlowControlled frame)
            {
                var last = this.pendingWriteQueue.LastOrDefault;
                if (last is null)
                {
                    this.EnqueueFrameWithoutMerge(frame);
                    return;
                }

                int lastSize = last.Size;
                if (last.Merge(this.controller.ctx, frame))
                {
                    this.IncrementPendingBytes(last.Size - lastSize, true);
                    return;
                }

                this.EnqueueFrameWithoutMerge(frame);
            }

            void EnqueueFrameWithoutMerge(IHttp2RemoteFlowControlled frame)
            {
                this.pendingWriteQueue.AddToBack(frame);
                // This must be called after adding to the queue in order so that hasFrame() is
                // updated before updating the stream state.
                this.IncrementPendingBytes(frame.Size, true);
            }

            public bool HasFrame => this.pendingWriteQueue.NonEmpty;

            /// <summary>
            /// Returns the head of the pending queue, or <c>null</c> if empty.
            /// </summary>
            IHttp2RemoteFlowControlled Peek()
            {
                return this.pendingWriteQueue.FirstOrDefault;
            }

            /// <summary>
            /// Clears the pending queue and writes errors for each remaining frame.
            /// </summary>
            /// <param name="error">the <see cref="Http2Error"/> to use.</param>
            /// <param name="cause">the <see cref="Exception"/> that caused this method to be invoked.</param>
            internal void Cancel(Http2Error error, Exception cause = null)
            {
                this.cancelled = true;
                // Ensure that the queue can't be modified while we are writing.
                if (this.writing) { return; }

                if (this.pendingWriteQueue.TryRemoveFromFront(out IHttp2RemoteFlowControlled frame))
                {
                    // Only create exception once and reuse to reduce overhead of filling in the stacktrace.
                    Http2Exception exception = ThrowHelper.GetStreamError_StreamClosedBeforeWriteCouldTakePlace(
                        this.stream.Id, error, cause);
                    do
                    {
                        this.WriteError(frame, exception);
                    } while (this.pendingWriteQueue.TryRemoveFromFront(out frame));
                }

                this.controller.streamByteDistributor.UpdateStreamableBytes(this);

                this.controller.monitor.StateCancelled(this);
            }

            /// <summary>
            /// Increments the number of pending bytes for this node and optionally updates the <see cref="IStreamByteDistributor"/>
            /// </summary>
            /// <param name="numBytes"></param>
            /// <param name="updateStreamableBytes"></param>
            void IncrementPendingBytes(int numBytes, bool updateStreamableBytes)
            {
                this.pendingBytes += numBytes;
                this.controller.monitor.IncrementPendingBytes(numBytes);
                if (updateStreamableBytes)
                {
                    this.controller.streamByteDistributor.UpdateStreamableBytes(this);
                }
            }

            /// <summary>
            /// If this frame is in the pending queue, decrements the number of pending bytes for the stream.
            /// </summary>
            /// <param name="bytes"></param>
            /// <param name="updateStreamableBytes"></param>
            void DecrementPendingBytes(int bytes, bool updateStreamableBytes)
            {
                this.IncrementPendingBytes(-bytes, updateStreamableBytes);
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
                    this.controller.connectionState.IncrementStreamWindow(negativeBytes);
                    this.IncrementStreamWindow(negativeBytes);
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
                IChannelHandlerContext ctx = this.controller.ctx;
                Debug.Assert(ctx is object);
                this.DecrementPendingBytes(frame.Size, true);
                frame.Error(ctx, cause);
            }
        }

        /// <summary>
        /// Abstract class which provides common functionality for writability monitor implementations.
        /// </summary>
        class WritabilityMonitor : IStreamByteDistributorWriter
        {
            protected readonly DefaultHttp2RemoteFlowController controller;

            bool inWritePendingBytes;
            long totalPendingBytes;

            internal WritabilityMonitor(DefaultHttp2RemoteFlowController controller)
            {
                this.controller = controller;
            }

            void IStreamByteDistributorWriter.Write(IHttp2Stream stream, int numBytes) => this.controller.GetState(stream).WriteAllocatedBytes(numBytes);

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
                state.IncrementStreamWindow(delta);
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
                this.totalPendingBytes += delta;

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
                return this.IsWritableConnection() && state.IsWritable();
            }

            internal void WritePendingBytes()
            {
                // Reentry is not permitted during the byte distribution process. It may lead to undesirable distribution of
                // bytes and even infinite loops. We protect against reentry and make sure each call has an opportunity to
                // cause a distribution to occur. This may be useful for example if the channel's writability changes from
                // Writable -> Not Writable (because we are writing) -> Writable (because the user flushed to make more room
                // in the channel outbound buffer).
                if (this.inWritePendingBytes)
                {
                    return;
                }

                this.inWritePendingBytes = true;
                try
                {
                    int bytesToWrite = this.controller.WritableBytes();
                    // Make sure we always write at least once, regardless if we have bytesToWrite or not.
                    // This ensures that zero-length frames will always be written.
                    while (true)
                    {
                        if (!this.controller.streamByteDistributor.Distribute(bytesToWrite, this) ||
                            (bytesToWrite = this.controller.WritableBytes()) <= 0 ||
                            !this.controller.IsChannelWritable0())
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    this.inWritePendingBytes = false;
                }
            }

            protected internal virtual void InitialWindowSize(int newWindowSize)
            {
                if (newWindowSize < 0)
                {
                    ThrowHelper.ThrowArgumentException_InvalidInitialWindowSize(newWindowSize);
                }

                int delta = newWindowSize - this.controller.initialWindowSize;
                this.controller.initialWindowSize = newWindowSize;
                this.controller.connection.ForEachActiveStream(Visit);

                if (delta > 0 && this.controller.IsChannelWritable())
                {
                    // The window size increased, send any pending frames for all streams.
                    this.WritePendingBytes();
                }

                bool Visit(IHttp2Stream stream)
                {
                    this.controller.GetState(stream).IncrementStreamWindow(delta);
                    return true;
                }
            }

            protected bool IsWritableConnection()
            {
                return this.controller.connectionState.WindowSize - this.totalPendingBytes > 0 && this.controller.IsChannelWritable();
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
            readonly IHttp2RemoteFlowControllerListener listener;

            internal ListenerWritabilityMonitor(DefaultHttp2RemoteFlowController controller, IHttp2RemoteFlowControllerListener listener)
                : base(controller)
            {
                this.listener = listener;
            }

            bool IHttp2StreamVisitor.Visit(IHttp2Stream stream)
            {
                FlowState state = this.controller.GetState(stream);
                if (this.IsWritable(state) != state.MarkedWritability())
                {
                    this.NotifyWritabilityChanged(state);
                }

                return true;
            }

            protected internal override void WindowSize(FlowState state, int initialWindowSize)
            {
                base.WindowSize(state, initialWindowSize);
                try
                {
                    this.CheckStateWritability(state);
                }
                catch (Http2Exception e)
                {
                    ThrowHelper.ThrowHttp2RuntimeException_CaughtUnexpectedExceptionFromWindow(e);
                }
            }

            protected internal override void IncrementWindowSize(FlowState state, int delta)
            {
                base.IncrementWindowSize(state, delta);
                this.CheckStateWritability(state);
            }

            protected internal override void InitialWindowSize(int newWindowSize)
            {
                base.InitialWindowSize(newWindowSize);
                if (this.IsWritableConnection())
                {
                    // If the write operation does not occur we still need to check all streams because they
                    // may have transitioned from writable to not writable.
                    this.CheckAllWritabilityChanged();
                }
            }

            protected internal override void EnqueueFrame(FlowState state, IHttp2RemoteFlowControlled frame)
            {
                base.EnqueueFrame(state, frame);
                this.CheckConnectionThenStreamWritabilityChanged(state);
            }

            protected internal override void StateCancelled(FlowState state)
            {
                try
                {
                    this.CheckConnectionThenStreamWritabilityChanged(state);
                }
                catch (Http2Exception e)
                {
                    ThrowHelper.ThrowHttp2RuntimeException_CaughtUnexpectedExceptionFromCheckAllWritabilityChanged(e);
                }
            }

            protected internal override void ChannelWritabilityChange()
            {
                if (this.controller.connectionState.MarkedWritability() != this.controller.IsChannelWritable())
                {
                    this.CheckAllWritabilityChanged();
                }
            }

            void CheckStateWritability(FlowState state)
            {
                if (this.IsWritable(state) != state.MarkedWritability())
                {
                    if (state == this.controller.connectionState)
                    {
                        this.CheckAllWritabilityChanged();
                    }
                    else
                    {
                        this.NotifyWritabilityChanged(state);
                    }
                }
            }

            void NotifyWritabilityChanged(FlowState state)
            {
                state.MarkedWritability(!state.MarkedWritability());
                try
                {
                    this.listener.WritabilityChanged(state.Stream);
                }
                catch (Exception cause)
                {
                    Logger.CaughtExceptionFromListenerWritabilityChanged(cause);
                }
            }

            void CheckConnectionThenStreamWritabilityChanged(FlowState state)
            {
                // It is possible that the connection window and/or the individual stream writability could change.
                if (this.IsWritableConnection() != this.controller.connectionState.MarkedWritability())
                {
                    this.CheckAllWritabilityChanged();
                }
                else if (this.IsWritable(state) != state.MarkedWritability())
                {
                    this.NotifyWritabilityChanged(state);
                }
            }

            void CheckAllWritabilityChanged()
            {
                // Make sure we mark that we have notified as a result of this change.
                this.controller.connectionState.MarkedWritability(this.IsWritableConnection());
                this.controller.connection.ForEachActiveStream(this);
            }
        }
    }
}