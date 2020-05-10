// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Basic implementation of <see cref="IHttp2LocalFlowController"/>.
    /// <para>This class is <c>NOT</c> thread safe. The assumption is all methods must be invoked from a single thread.
    /// Typically this thread is the event loop thread for the <see cref="IChannelHandlerContext"/> managed by this class.</para>
    /// </summary>
    public class DefaultHttp2LocalFlowController : Http2ConnectionAdapter, IHttp2LocalFlowController
    {
        /// <summary>
        /// The default ratio of window size to initial window size below which a <c>WINDOW_UPDATE</c>
        /// is sent to expand the window.
        /// </summary>
        public static readonly float DefaultWindowUpdateRatio = 0.5f;

        readonly IHttp2Connection connection;
        readonly IHttp2ConnectionPropertyKey stateKey;
        IHttp2FrameWriter frameWriter;
        IChannelHandlerContext ctx;
        float windowUpdateRatio;
        int initialWindowSize = Http2CodecUtil.DefaultWindowSize;

        public DefaultHttp2LocalFlowController(IHttp2Connection connection)
            : this(connection, DefaultWindowUpdateRatio, false)
        {
        }

        /// <summary>
        /// Constructs a controller with the given settings.
        /// </summary>
        /// <param name="connection">the connection state.</param>
        /// <param name="windowUpdateRatio">the window percentage below which to send a <c>WINDOW_UPDATE</c>.</param>
        /// <param name="autoRefillConnectionWindow">if <c>true</c>, effectively disables the connection window
        /// in the flow control algorithm as they will always refill automatically without requiring the
        /// application to consume the bytes. When enabled, the maximum bytes you must be prepared to
        /// queue is proportional to <c>maximum number of concurrent streams * the initial window
        /// size per stream</c>
        /// (<a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_CONCURRENT_STREAMS</a>
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_INITIAL_WINDOW_SIZE</a>).
        /// </param>
        public DefaultHttp2LocalFlowController(IHttp2Connection connection, float windowUpdateRatio, bool autoRefillConnectionWindow)
        {
            if (null == connection) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.connection); }

            this.connection = connection;
            this.WindowUpdateRatio(windowUpdateRatio);

            // Add a flow state for the connection.
            this.stateKey = connection.NewKey();
            IFlowState connectionState = autoRefillConnectionWindow
                ? new AutoRefillState(this, connection.ConnectionStream, this.initialWindowSize)
                : new DefaultState(this, connection.ConnectionStream, this.initialWindowSize);
            connection.ConnectionStream.SetProperty(this.stateKey, connectionState);

            // Register for notification of new streams.
            connection.AddListener(this);
        }

        public override void OnStreamAdded(IHttp2Stream stream)
        {
            // Unconditionally used the reduced flow control state because it requires no object allocation
            // and the DefaultFlowState will be allocated in onStreamActive.
            stream.SetProperty(this.stateKey, REDUCED_FLOW_STATE);
        }

        public override void OnStreamActive(IHttp2Stream stream)
        {
            // Need to be sure the stream's initial window is adjusted for SETTINGS
            // frames which may have been exchanged while it was in IDLE
            stream.SetProperty(this.stateKey, new DefaultState(this, stream, this.initialWindowSize));
        }

        public override void OnStreamClosed(IHttp2Stream stream)
        {
            try
            {
                // When a stream is closed, consume any remaining bytes so that they
                // are restored to the connection window.
                IFlowState state = this.GetState(stream);
                int unconsumedBytes = state.UnconsumedBytes;
                if (this.ctx is object && unconsumedBytes > 0)
                {
                    this.ConnectionState().ConsumeBytes(unconsumedBytes);
                    state.ConsumeBytes(unconsumedBytes);
                }
            }
            catch (Http2Exception)
            {
                throw;
            }
            finally
            {
                // Unconditionally reduce the amount of memory required for flow control because there is no
                // object allocation costs associated with doing so and the stream will not have any more
                // local flow control state to keep track of anymore.
                stream.SetProperty(this.stateKey, REDUCED_FLOW_STATE);
            }
        }


        public IHttp2LocalFlowController FrameWriter(IHttp2FrameWriter frameWriter)
        {
            if (null == frameWriter) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.frameWriter); }
            this.frameWriter = frameWriter;
            return this;
        }

        public void SetChannelHandlerContext(IChannelHandlerContext ctx)
        {
            if (null == ctx) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.ctx); }
            this.ctx = ctx;
        }

        public void SetInitialWindowSize(int newWindowSize)
        {
            Debug.Assert(this.ctx == null || this.ctx.Executor.InEventLoop);
            int delta = newWindowSize - this.initialWindowSize;
            this.initialWindowSize = newWindowSize;

            WindowUpdateVisitor visitor = new WindowUpdateVisitor(this, delta);
            this.connection.ForEachActiveStream(visitor);
            visitor.ThrowIfError();
        }

        public int InitialWindowSize => this.initialWindowSize;

        public int GetWindowSize(IHttp2Stream stream) => this.GetState(stream).WindowSize;

        public int GetInitialWindowSize(IHttp2Stream stream) => this.GetState(stream).InitialWindowSize();

        public void IncrementWindowSize(IHttp2Stream stream, int delta)
        {
            Debug.Assert(this.ctx is object && this.ctx.Executor.InEventLoop);
            IFlowState state = this.GetState(stream);
            // Just add the delta to the stream-specific initial window size so that the next time the window
            // expands it will grow to the new initial size.
            state.IncrementInitialStreamWindow(delta);
            state.WriteWindowUpdateIfNeeded();
        }

        public bool ConsumeBytes(IHttp2Stream stream, int numBytes)
        {
            Debug.Assert(this.ctx is object && this.ctx.Executor.InEventLoop);
            uint uNumBytes = (uint)numBytes;
            if (uNumBytes > SharedConstants.TooBigOrNegative) // < 0
            {
                ThrowHelper.ThrowArgumentException_NumBytesMustNotBeNegative();
            }

            if (0u >= uNumBytes) { return false; }

            // Streams automatically consume all remaining bytes when they are closed, so just ignore
            // if already closed.
            if (stream is object && !IsClosed(stream))
            {
                if (stream.Id == Http2CodecUtil.ConnectionStreamId)
                {
                    ThrowHelper.ReturningBytesForTheConnectionWindowIsNotSupported();
                }

                bool windowUpdateSent = this.ConnectionState().ConsumeBytes(numBytes);
                windowUpdateSent |= this.GetState(stream).ConsumeBytes(numBytes);
                return windowUpdateSent;
            }

            return false;
        }

        public int UnconsumedBytes(IHttp2Stream stream) => this.GetState(stream).UnconsumedBytes;

        [MethodImpl(InlineMethod.Value)]
        static void CheckValidRatio(float ratio)
        {
            if (ratio <= 0.0 || ratio >= 1.0)
            {
                ThrowHelper.ThrowArgumentException_InvalidRatio(ratio);
            }
        }

        /// <summary>
        /// The window update ratio is used to determine when a window update must be sent. If the ratio
        /// of bytes processed since the last update has meet or exceeded this ratio then a window update will
        /// be sent. This is the global window update ratio that will be used for new streams.
        /// </summary>
        /// <param name="ratio">the ratio to use when checking if a <c>WINDOW_UPDATE</c> is determined necessary for new streams.</param>
        /// <exception cref="ArgumentException">If the ratio is out of bounds (0, 1).</exception>
        public void WindowUpdateRatio(float ratio)
        {
            Debug.Assert(this.ctx == null || this.ctx.Executor.InEventLoop);
            CheckValidRatio(ratio);
            this.windowUpdateRatio = ratio;
        }

        /// <summary>
        /// The window update ratio is used to determine when a window update must be sent. If the ratio
        /// of bytes processed since the last update has meet or exceeded this ratio then a window update will
        /// be sent. This is the global window update ratio that will be used for new streams.
        /// </summary>
        public float WindowUpdateRatio() => this.windowUpdateRatio;

        /// <summary>
        /// The window update ratio is used to determine when a window update must be sent. If the ratio
        /// of bytes processed since the last update has meet or exceeded this ratio then a window update will
        /// be sent. This window update ratio will only be applied to <c>streamId</c>.
        /// <para>Note it is the responsibly of the caller to ensure that the
        /// initial <c>SETTINGS</c> frame is sent before this is called. It would
        /// be considered a <see cref="Http2Error.ProtocolError"/> if a <c>WINDOW_UPDATE</c>
        /// was generated by this method before the initial <c>SETTINGS</c> frame is sent.</para>
        /// </summary>
        /// <param name="stream">the stream for which <paramref name="ratio"/> applies to.</param>
        /// <param name="ratio">the ratio to use when checking if a <c>WINDOW_UPDATE</c> is determined necessary.</param>
        /// <remarks>If a protocol-error occurs while generating <c>WINDOW_UPDATE</c> frames</remarks>
        public void WindowUpdateRatio(IHttp2Stream stream, float ratio)
        {
            Debug.Assert(this.ctx is object && this.ctx.Executor.InEventLoop);
            CheckValidRatio(ratio);
            IFlowState state = this.GetState(stream);
            state.WindowUpdateRatio(ratio);
            state.WriteWindowUpdateIfNeeded();
        }

        /// <summary>
        /// The window update ratio is used to determine when a window update must be sent. If the ratio
        /// of bytes processed since the last update has meet or exceeded this ratio then a window update will
        /// be sent. This window update ratio will only be applied to <c>streamId</c>.
        /// </summary>
        /// <remarks>If no stream corresponding to <paramref name="stream"/> could be found.</remarks>
        public float WindowUpdateRatio(IHttp2Stream stream) => this.GetState(stream).WindowUpdateRatio();

        public void ReceiveFlowControlledFrame(IHttp2Stream stream, IByteBuffer data, int padding, bool endOfStream)
        {
            Debug.Assert(this.ctx is object && this.ctx.Executor.InEventLoop);
            int dataLength = data.ReadableBytes + padding;

            // Apply the connection-level flow control
            IFlowState connectionState = this.ConnectionState();
            connectionState.ReceiveFlowControlledFrame(dataLength);

            if (stream is object && !IsClosed(stream))
            {
                // Apply the stream-level flow control
                IFlowState state = this.GetState(stream);
                state.EndOfStream(endOfStream);
                state.ReceiveFlowControlledFrame(dataLength);
            }
            else if (dataLength > 0)
            {
                // Immediately consume the bytes for the connection window.
                connectionState.ConsumeBytes(dataLength);
            }
        }

        IFlowState ConnectionState() => this.connection.ConnectionStream.GetProperty<IFlowState>(this.stateKey);

        IFlowState GetState(IHttp2Stream stream) => stream.GetProperty<IFlowState>(this.stateKey);

        static bool IsClosed(IHttp2Stream stream) => stream.State == Http2StreamState.Closed;

        /// <summary>
        /// Flow control state that does autorefill of the flow control window when the data is received.
        /// </summary>
        sealed class AutoRefillState : DefaultState
        {
            public AutoRefillState(DefaultHttp2LocalFlowController controller, IHttp2Stream stream, int initialWindowSize)
                : base(controller, stream, initialWindowSize)
            {
            }

            public override void ReceiveFlowControlledFrame(int dataLength)
            {
                base.ReceiveFlowControlledFrame(dataLength);
                // Need to call the base to consume the bytes, since this.consumeBytes does nothing.
                base.ConsumeBytes(dataLength);
            }

            public override bool ConsumeBytes(int numBytes)
            {
                // Do nothing, since the bytes are already consumed upon receiving the data.
                return false;
            }
        }

        /// <summary>
        /// Flow control window state for an individual stream.
        /// </summary>
        class DefaultState : IFlowState
        {
            readonly DefaultHttp2LocalFlowController controller;
            readonly IHttp2Stream stream;

            /// <summary>
            /// The actual flow control window that is decremented as soon as <c>DATA</c> arrives.
            /// </summary>
            int window;

            /// <summary>
            /// A view of <see cref="window"/> that is used to determine when to send <c>WINDOW_UPDATE</c>
            /// frames. Decrementing this window for received <c>DATA</c> frames is delayed until the
            /// application has indicated that the data has been fully processed. This prevents sending
            /// a <c>WINDOW_UPDATE</c> until the number of processed bytes drops below the threshold.
            /// </summary>
            int processedWindow;

            /// <summary>
            /// This is what is used to determine how many bytes need to be returned relative to <see cref="processedWindow"/>.
            /// Each stream has their own initial window size.
            /// </summary>
            int initialStreamWindowSize;

            /// <summary>
            /// This is used to determine when <see cref="processedWindow"/> is sufficiently far away from
            /// <see cref="initialStreamWindowSize"/> such that a <c>WINDOW_UPDATE</c> should be sent.
            /// Each stream has their own window update ratio.
            /// </summary>
            float streamWindowUpdateRatio;

            int lowerBound;
            bool endOfStream;

            public DefaultState(DefaultHttp2LocalFlowController controller, IHttp2Stream stream, int initialWindowSize)
            {
                this.controller = controller;
                this.stream = stream;
                this.Window(initialWindowSize);
                this.streamWindowUpdateRatio = controller.windowUpdateRatio;
            }

            public void Window(int initialWindowSize)
            {
                Debug.Assert(this.controller.ctx == null || this.controller.ctx.Executor.InEventLoop);
                this.window = this.processedWindow = this.initialStreamWindowSize = initialWindowSize;
            }

            public int WindowSize => this.window;

            public int InitialWindowSize() => this.initialStreamWindowSize;

            public void EndOfStream(bool endOfStream) => this.endOfStream = endOfStream;

            public float WindowUpdateRatio() => this.streamWindowUpdateRatio;

            public void WindowUpdateRatio(float ratio)
            {
                Debug.Assert(this.controller.ctx == null || this.controller.ctx.Executor.InEventLoop);
                this.streamWindowUpdateRatio = ratio;
            }

            public void IncrementInitialStreamWindow(int delta)
            {
                // Clip the delta so that the resulting initialStreamWindowSize falls within the allowed range.
                int newValue = (int)Math.Min(
                    Http2CodecUtil.MaxInitialWindowSize,
                    Math.Max(Http2CodecUtil.MinInitialWindowSize, this.initialStreamWindowSize + (long)delta));
                delta = newValue - this.initialStreamWindowSize;

                this.initialStreamWindowSize += delta;
            }

            public void IncrementFlowControlWindows(int delta)
            {
                if (delta > 0 && this.window > Http2CodecUtil.MaxInitialWindowSize - delta)
                {
                    ThrowHelper.ThrowStreamError_FlowControlWindowOverflowedForStream(this.stream.Id);
                }

                this.window += delta;
                this.processedWindow += delta;
                this.lowerBound = delta < 0 ? delta : 0;
            }

            public virtual void ReceiveFlowControlledFrame(int dataLength)
            {
                Debug.Assert(dataLength >= 0);

                // Apply the delta. Even if we throw an exception we want to have taken this delta into account.
                this.window -= dataLength;

                // Window size can become negative if we sent a SETTINGS frame that reduces the
                // size of the transfer window after the peer has written data frames.
                // The value is bounded by the length that SETTINGS frame decrease the window.
                // This difference is stored for the connection when writing the SETTINGS frame
                // and is cleared once we send a WINDOW_UPDATE frame.
                if (this.window < this.lowerBound)
                {
                    ThrowHelper.ThrowStreamError_FlowControlWindowExceededForStream(this.stream.Id);
                }
            }

            void ReturnProcessedBytes(int delta)
            {
                if (this.processedWindow - delta < this.window)
                {
                    ThrowHelper.ThrowStreamError_AttemptingToReturnTooManyBytesForStream(this.stream.Id);
                }

                this.processedWindow -= delta;
            }

            public virtual bool ConsumeBytes(int numBytes)
            {
                // Return the bytes processed and update the window.
                this.ReturnProcessedBytes(numBytes);
                return this.WriteWindowUpdateIfNeeded();
            }

            public int UnconsumedBytes => this.processedWindow - this.window;

            public bool WriteWindowUpdateIfNeeded()
            {
                if (this.endOfStream || this.initialStreamWindowSize <= 0)
                {
                    return false;
                }

                int threshold = (int)(this.initialStreamWindowSize * this.streamWindowUpdateRatio);
                if (this.processedWindow <= threshold)
                {
                    this.WriteWindowUpdate();
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Called to perform a window update for this stream (or connection). Updates the window size back
            /// to the size of the initial window and sends a window update frame to the remote endpoint.
            /// </summary>
            void WriteWindowUpdate()
            {
                // Expand the window for this stream back to the size of the initial window.
                int deltaWindowSize = this.initialStreamWindowSize - this.processedWindow;
                try
                {
                    this.IncrementFlowControlWindows(deltaWindowSize);
                }
                catch (Exception t)
                {
                    ThrowHelper.ThrowConnectionError_AttemptingToReturnTooManyBytesForStream(this.stream.Id, t);
                }

                // Send a window update for the stream/connection.
                this.controller.frameWriter.WriteWindowUpdateAsync(this.controller.ctx, this.stream.Id, deltaWindowSize, this.controller.ctx.NewPromise());
            }
        }

        /// <summary>
        /// The local flow control state for a single stream that is not in a state where flow controlled frames cannot
        /// be exchanged.
        /// </summary>
        static readonly IFlowState REDUCED_FLOW_STATE = new ReducedFlowState();

        /// <summary>
        /// An abstraction which provides specific extensions used by local flow control.
        /// </summary>
        interface IFlowState
        {
            int WindowSize { get; }

            int InitialWindowSize();

            void Window(int initialWindowSize);

            /// <summary>
            /// Increment the initial window size for this stream.
            /// </summary>
            /// <param name="delta">The amount to increase the initial window size by.</param>
            void IncrementInitialStreamWindow(int delta);

            /// <summary>
            /// Updates the flow control window for this stream if it is appropriate.
            /// </summary>
            /// <returns><c>true</c> if <c>WINDOW_UPDATE</c> was written, <c>false</c> otherwise.</returns>
            bool WriteWindowUpdateIfNeeded();

            /// <summary>
            /// Indicates that the application has consumed <paramref name="numBytes"/> from the connection or stream and is
            /// ready to receive more data.
            /// </summary>
            /// <param name="numBytes">the number of bytes to be returned to the flow control window.</param>
            /// <returns><c>true</c> if <c>WINDOW_UPDATE</c> was written, <c>false</c> otherwise.</returns>
            bool ConsumeBytes(int numBytes);

            int UnconsumedBytes { get; }

            float WindowUpdateRatio();

            void WindowUpdateRatio(float ratio);

            /// <summary>
            /// A flow control event has occurred and we should decrement the amount of available bytes for this stream.
            /// </summary>
            /// <param name="dataLength">The amount of data to for which this stream is no longer eligible to use for flow control.</param>
            /// <remarks>If too much data is used relative to how much is available.</remarks>
            void ReceiveFlowControlledFrame(int dataLength);

            /// <summary>
            /// Increment the windows which are used to determine many bytes have been processed.
            /// </summary>
            /// <param name="delta">The amount to increment the window by.</param>
            /// <remarks>if integer overflow occurs on the window.</remarks>
            void IncrementFlowControlWindows(int delta);

            void EndOfStream(bool endOfStream);
        }

        sealed class ReducedFlowState : IFlowState
        {
            public int WindowSize => 0;

            public int InitialWindowSize() => 0;

            public void Window(int initialWindowSize) => throw new NotSupportedException();

            public void IncrementInitialStreamWindow(int delta)
            {
                // This operation needs to be supported during the initial settings exchange when
                // the peer has not yet acknowledged this peer being activated.
            }

            public bool WriteWindowUpdateIfNeeded() => throw new NotSupportedException();

            public bool ConsumeBytes(int numBytes) => false;

            public int UnconsumedBytes => 0;

            public float WindowUpdateRatio() => throw new NotSupportedException();

            public void WindowUpdateRatio(float ratio) => throw new NotSupportedException();

            public void ReceiveFlowControlledFrame(int dataLength) => throw new NotSupportedException();

            public void IncrementFlowControlWindows(int delta)
            {
                // This operation needs to be supported during the initial settings exchange when
                // the peer has not yet acknowledged this peer being activated.
            }

            public void EndOfStream(bool endOfStream) => throw new NotSupportedException();
        }

        /// <summary>
        /// Provides a means to iterate over all active streams and increment the flow control windows.
        /// </summary>
        sealed class WindowUpdateVisitor : IHttp2StreamVisitor
        {
            CompositeStreamException compositeException;
            readonly DefaultHttp2LocalFlowController controller;
            readonly int delta;

            public WindowUpdateVisitor(DefaultHttp2LocalFlowController controller, int delta)
            {
                this.controller = controller;
                this.delta = delta;
            }

            public bool Visit(IHttp2Stream stream)
            {
                try
                {
                    // Increment flow control window first so state will be consistent if overflow is detected.
                    IFlowState state = this.controller.GetState(stream);
                    state.IncrementFlowControlWindows(this.delta);
                    state.IncrementInitialStreamWindow(this.delta);
                }
                catch (StreamException e)
                {
                    if (this.compositeException == null)
                    {
                        this.compositeException = new CompositeStreamException(e.Error, 4);
                    }

                    this.compositeException.Add(e);
                }

                return true;
            }

            public void ThrowIfError()
            {
                if (this.compositeException is object)
                {
                    throw this.compositeException;
                }
            }
        }
    }
}