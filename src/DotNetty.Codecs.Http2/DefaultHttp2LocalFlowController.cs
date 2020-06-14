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

        private readonly IHttp2Connection _connection;
        private readonly IHttp2ConnectionPropertyKey _stateKey;
        private IHttp2FrameWriter _frameWriter;
        private IChannelHandlerContext _ctx;
        private float _windowUpdateRatio;
        private int _initialWindowSize = Http2CodecUtil.DefaultWindowSize;

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
            if (connection is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.connection); }

            _connection = connection;
            WindowUpdateRatio(windowUpdateRatio);

            // Add a flow state for the connection.
            _stateKey = connection.NewKey();
            IFlowState connectionState = autoRefillConnectionWindow
                ? new AutoRefillState(this, connection.ConnectionStream, _initialWindowSize)
                : new DefaultState(this, connection.ConnectionStream, _initialWindowSize);
            _ = connection.ConnectionStream.SetProperty(_stateKey, connectionState);

            // Register for notification of new streams.
            connection.AddListener(this);
        }

        public override void OnStreamAdded(IHttp2Stream stream)
        {
            // Unconditionally used the reduced flow control state because it requires no object allocation
            // and the DefaultFlowState will be allocated in onStreamActive.
            _ = stream.SetProperty(_stateKey, REDUCED_FLOW_STATE);
        }

        public override void OnStreamActive(IHttp2Stream stream)
        {
            // Need to be sure the stream's initial window is adjusted for SETTINGS
            // frames which may have been exchanged while it was in IDLE
            _ = stream.SetProperty(_stateKey, new DefaultState(this, stream, _initialWindowSize));
        }

        public override void OnStreamClosed(IHttp2Stream stream)
        {
            try
            {
                // When a stream is closed, consume any remaining bytes so that they
                // are restored to the connection window.
                IFlowState state = GetState(stream);
                int unconsumedBytes = state.UnconsumedBytes;
                if (_ctx is object && unconsumedBytes > 0)
                {
                    if (ConsumeAllBytes(state, unconsumedBytes))
                    {
                        // As the user has no real control on when this callback is used we should better
                        // call flush() if we produced any window update to ensure we not stale.
                        _ = _ctx.Flush();
                    }
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
                _ = stream.SetProperty(_stateKey, REDUCED_FLOW_STATE);
            }
        }


        public IHttp2LocalFlowController FrameWriter(IHttp2FrameWriter frameWriter)
        {
            if (frameWriter is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.frameWriter); }
            _frameWriter = frameWriter;
            return this;
        }

        public void SetChannelHandlerContext(IChannelHandlerContext ctx)
        {
            if (ctx is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.ctx); }
            _ctx = ctx;
        }

        public void SetInitialWindowSize(int newWindowSize)
        {
            Debug.Assert(_ctx is null || _ctx.Executor.InEventLoop);
            int delta = newWindowSize - _initialWindowSize;
            _initialWindowSize = newWindowSize;

            WindowUpdateVisitor visitor = new WindowUpdateVisitor(this, delta);
            _ = _connection.ForEachActiveStream(visitor);
            visitor.ThrowIfError();
        }

        public int InitialWindowSize => _initialWindowSize;

        public int GetWindowSize(IHttp2Stream stream) => GetState(stream).WindowSize;

        public int GetInitialWindowSize(IHttp2Stream stream) => GetState(stream).InitialWindowSize();

        public void IncrementWindowSize(IHttp2Stream stream, int delta)
        {
            Debug.Assert(_ctx is object && _ctx.Executor.InEventLoop);
            IFlowState state = GetState(stream);
            // Just add the delta to the stream-specific initial window size so that the next time the window
            // expands it will grow to the new initial size.
            state.IncrementInitialStreamWindow(delta);
            _ = state.WriteWindowUpdateIfNeeded();
        }

        public bool ConsumeBytes(IHttp2Stream stream, int numBytes)
        {
            Debug.Assert(_ctx is object && _ctx.Executor.InEventLoop);
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

                return ConsumeAllBytes(GetState(stream), numBytes);
            }

            return false;
        }

        private bool ConsumeAllBytes(IFlowState state, int numBytes)
        {
            return ConnectionState().ConsumeBytes(numBytes) | state.ConsumeBytes(numBytes);
        }

        public int UnconsumedBytes(IHttp2Stream stream) => GetState(stream).UnconsumedBytes;

        [MethodImpl(InlineMethod.AggressiveInlining)]
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
            Debug.Assert(_ctx is null || _ctx.Executor.InEventLoop);
            CheckValidRatio(ratio);
            _windowUpdateRatio = ratio;
        }

        /// <summary>
        /// The window update ratio is used to determine when a window update must be sent. If the ratio
        /// of bytes processed since the last update has meet or exceeded this ratio then a window update will
        /// be sent. This is the global window update ratio that will be used for new streams.
        /// </summary>
        public float WindowUpdateRatio() => _windowUpdateRatio;

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
            Debug.Assert(_ctx is object && _ctx.Executor.InEventLoop);
            CheckValidRatio(ratio);
            IFlowState state = GetState(stream);
            state.WindowUpdateRatio(ratio);
            _ = state.WriteWindowUpdateIfNeeded();
        }

        /// <summary>
        /// The window update ratio is used to determine when a window update must be sent. If the ratio
        /// of bytes processed since the last update has meet or exceeded this ratio then a window update will
        /// be sent. This window update ratio will only be applied to <c>streamId</c>.
        /// </summary>
        /// <remarks>If no stream corresponding to <paramref name="stream"/> could be found.</remarks>
        public float WindowUpdateRatio(IHttp2Stream stream) => GetState(stream).WindowUpdateRatio();

        public void ReceiveFlowControlledFrame(IHttp2Stream stream, IByteBuffer data, int padding, bool endOfStream)
        {
            Debug.Assert(_ctx is object && _ctx.Executor.InEventLoop);
            int dataLength = data.ReadableBytes + padding;

            // Apply the connection-level flow control
            IFlowState connectionState = ConnectionState();
            connectionState.ReceiveFlowControlledFrame(dataLength);

            if (stream is object && !IsClosed(stream))
            {
                // Apply the stream-level flow control
                IFlowState state = GetState(stream);
                state.EndOfStream(endOfStream);
                state.ReceiveFlowControlledFrame(dataLength);
            }
            else if (dataLength > 0)
            {
                // Immediately consume the bytes for the connection window.
                _ = connectionState.ConsumeBytes(dataLength);
            }
        }

        IFlowState ConnectionState() => _connection.ConnectionStream.GetProperty<IFlowState>(_stateKey);

        IFlowState GetState(IHttp2Stream stream) => stream.GetProperty<IFlowState>(_stateKey);

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
                _ = base.ConsumeBytes(dataLength);
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
            private readonly DefaultHttp2LocalFlowController _controller;
            private readonly IHttp2Stream _stream;

            /// <summary>
            /// The actual flow control window that is decremented as soon as <c>DATA</c> arrives.
            /// </summary>
            private int _window;

            /// <summary>
            /// A view of <see cref="_window"/> that is used to determine when to send <c>WINDOW_UPDATE</c>
            /// frames. Decrementing this window for received <c>DATA</c> frames is delayed until the
            /// application has indicated that the data has been fully processed. This prevents sending
            /// a <c>WINDOW_UPDATE</c> until the number of processed bytes drops below the threshold.
            /// </summary>
            private int _processedWindow;

            /// <summary>
            /// This is what is used to determine how many bytes need to be returned relative to <see cref="_processedWindow"/>.
            /// Each stream has their own initial window size.
            /// </summary>
            private int _initialStreamWindowSize;

            /// <summary>
            /// This is used to determine when <see cref="_processedWindow"/> is sufficiently far away from
            /// <see cref="_initialStreamWindowSize"/> such that a <c>WINDOW_UPDATE</c> should be sent.
            /// Each stream has their own window update ratio.
            /// </summary>
            private float _streamWindowUpdateRatio;

            private int _lowerBound;
            private bool _endOfStream;

            public DefaultState(DefaultHttp2LocalFlowController controller, IHttp2Stream stream, int initialWindowSize)
            {
                _controller = controller;
                _stream = stream;
                Window(initialWindowSize);
                _streamWindowUpdateRatio = controller._windowUpdateRatio;
            }

            public void Window(int initialWindowSize)
            {
                Debug.Assert(_controller._ctx is null || _controller._ctx.Executor.InEventLoop);
                _window = _processedWindow = _initialStreamWindowSize = initialWindowSize;
            }

            public int WindowSize => _window;

            public int InitialWindowSize() => _initialStreamWindowSize;

            public void EndOfStream(bool endOfStream) => _endOfStream = endOfStream;

            public float WindowUpdateRatio() => _streamWindowUpdateRatio;

            public void WindowUpdateRatio(float ratio)
            {
                Debug.Assert(_controller._ctx is null || _controller._ctx.Executor.InEventLoop);
                _streamWindowUpdateRatio = ratio;
            }

            public void IncrementInitialStreamWindow(int delta)
            {
                // Clip the delta so that the resulting initialStreamWindowSize falls within the allowed range.
                int newValue = (int)Math.Min(
                    Http2CodecUtil.MaxInitialWindowSize,
                    Math.Max(Http2CodecUtil.MinInitialWindowSize, _initialStreamWindowSize + (long)delta));
                delta = newValue - _initialStreamWindowSize;

                _initialStreamWindowSize += delta;
            }

            public void IncrementFlowControlWindows(int delta)
            {
                if (delta > 0 && _window > Http2CodecUtil.MaxInitialWindowSize - delta)
                {
                    ThrowHelper.ThrowStreamError_FlowControlWindowOverflowedForStream(_stream.Id);
                }

                _window += delta;
                _processedWindow += delta;
                _lowerBound = delta < 0 ? delta : 0;
            }

            public virtual void ReceiveFlowControlledFrame(int dataLength)
            {
                Debug.Assert(dataLength >= 0);

                // Apply the delta. Even if we throw an exception we want to have taken this delta into account.
                _window -= dataLength;

                // Window size can become negative if we sent a SETTINGS frame that reduces the
                // size of the transfer window after the peer has written data frames.
                // The value is bounded by the length that SETTINGS frame decrease the window.
                // This difference is stored for the connection when writing the SETTINGS frame
                // and is cleared once we send a WINDOW_UPDATE frame.
                if (_window < _lowerBound)
                {
                    ThrowHelper.ThrowStreamError_FlowControlWindowExceededForStream(_stream.Id);
                }
            }

            void ReturnProcessedBytes(int delta)
            {
                if (_processedWindow - delta < _window)
                {
                    ThrowHelper.ThrowStreamError_AttemptingToReturnTooManyBytesForStream(_stream.Id);
                }

                _processedWindow -= delta;
            }

            public virtual bool ConsumeBytes(int numBytes)
            {
                // Return the bytes processed and update the window.
                ReturnProcessedBytes(numBytes);
                return WriteWindowUpdateIfNeeded();
            }

            public int UnconsumedBytes => _processedWindow - _window;

            public bool WriteWindowUpdateIfNeeded()
            {
                if (_endOfStream || _initialStreamWindowSize <= 0 ||
                    // If the stream is already closed there is no need to try to write a window update for it.
                    IsClosed(_stream))
                {
                    return false;
                }

                int threshold = (int)(_initialStreamWindowSize * _streamWindowUpdateRatio);
                if (_processedWindow <= threshold)
                {
                    WriteWindowUpdate();
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
                int deltaWindowSize = _initialStreamWindowSize - _processedWindow;
                try
                {
                    IncrementFlowControlWindows(deltaWindowSize);
                }
                catch (Exception t)
                {
                    ThrowHelper.ThrowConnectionError_AttemptingToReturnTooManyBytesForStream(_stream.Id, t);
                }

                // Send a window update for the stream/connection.
                _ = _controller._frameWriter.WriteWindowUpdateAsync(_controller._ctx, _stream.Id, deltaWindowSize, _controller._ctx.NewPromise());
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
            private CompositeStreamException _compositeException;
            private readonly DefaultHttp2LocalFlowController _controller;
            private readonly int _delta;

            public WindowUpdateVisitor(DefaultHttp2LocalFlowController controller, int delta)
            {
                _controller = controller;
                _delta = delta;
            }

            public bool Visit(IHttp2Stream stream)
            {
                try
                {
                    // Increment flow control window first so state will be consistent if overflow is detected.
                    IFlowState state = _controller.GetState(stream);
                    state.IncrementFlowControlWindows(_delta);
                    state.IncrementInitialStreamWindow(_delta);
                }
                catch (StreamException e)
                {
                    if (_compositeException is null)
                    {
                        _compositeException = new CompositeStreamException(e.Error, 4);
                    }

                    _compositeException.Add(e);
                }

                return true;
            }

            public void ThrowIfError()
            {
                if (_compositeException is object)
                {
                    throw _compositeException;
                }
            }
        }
    }
}