// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Provides the default implementation for processing inbound frame events and delegates to a
    /// <see cref="IHttp2FrameListener"/>
    /// <para>
    /// This class will read HTTP/2 frames and delegate the events to a <see cref="IHttp2FrameListener"/>
    /// </para>
    /// This interface enforces inbound flow control functionality through
    /// <see cref="IHttp2LocalFlowController"/>
    /// </summary>
    public class DefaultHttp2ConnectionDecoder : IHttp2ConnectionDecoder
    {
        private static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<DefaultHttp2ConnectionDecoder>();
        private IHttp2FrameListener _internalFrameListener;
        private readonly IHttp2Connection _connection;
        private IHttp2LifecycleManager _lifecycleManager;
        private readonly IHttp2ConnectionEncoder _encoder;
        private readonly IHttp2FrameReader _frameReader;
        private IHttp2FrameListener _listener;
        private readonly IHttp2PromisedRequestVerifier _requestVerifier;
        private readonly IHttp2SettingsReceivedConsumer _settingsReceivedConsumer;
        private readonly bool _autoAckPing;

        /// <summary>Create a new instance.</summary>
        /// <param name="connection">The <see cref="IHttp2Connection"/> associated with this decoder.</param>
        /// <param name="encoder">The <see cref="IHttp2ConnectionEncoder"/> associated with this decoder.</param>
        /// <param name="frameReader">Responsible for reading/parsing the raw frames. As opposed to this object which applies
        /// h2 semantics on top of the frames.</param>
        public DefaultHttp2ConnectionDecoder(IHttp2Connection connection,
            IHttp2ConnectionEncoder encoder, IHttp2FrameReader frameReader)
            : this(connection, encoder, frameReader, AlwaysVerifyPromisedRequestVerifier.Instance)
        {
        }

        /// <summary>Create a new instance.</summary>
        /// <param name="connection">The <see cref="IHttp2Connection"/> associated with this decoder.</param>
        /// <param name="encoder">The <see cref="IHttp2ConnectionEncoder"/> associated with this decoder.</param>
        /// <param name="frameReader">Responsible for reading/parsing the raw frames. As opposed to this object which applies
        /// h2 semantics on top of the frames.</param>
        /// <param name="requestVerifier">Determines if push promised streams are valid.</param>
        public DefaultHttp2ConnectionDecoder(IHttp2Connection connection,
            IHttp2ConnectionEncoder encoder, IHttp2FrameReader frameReader, IHttp2PromisedRequestVerifier requestVerifier)
            : this(connection, encoder, frameReader, requestVerifier, true, true)
        {
        }

        /// <summary>Create a new instance.</summary>
        /// <param name="connection">The <see cref="IHttp2Connection"/> associated with this decoder.</param>
        /// <param name="encoder">The <see cref="IHttp2ConnectionEncoder"/> associated with this decoder.</param>
        /// <param name="frameReader">Responsible for reading/parsing the raw frames. As opposed to this object which applies
        /// h2 semantics on top of the frames.</param>
        /// <param name="requestVerifier">Determines if push promised streams are valid.</param>
        /// <param name="autoAckSettings"><c>false</c> to disable automatically applying and sending settings acknowledge frame.
        /// The <paramref name="encoder"/> is expected to be an instance of
        /// <see cref="IHttp2SettingsReceivedConsumer"/> and will apply the earliest received but not yet
        /// ACKed SETTINGS when writing the SETTINGS ACKs. <c>true</c> to enable automatically
        /// applying and sending settings acknowledge frame.</param>
        public DefaultHttp2ConnectionDecoder(IHttp2Connection connection,
            IHttp2ConnectionEncoder encoder, IHttp2FrameReader frameReader, IHttp2PromisedRequestVerifier requestVerifier, bool autoAckSettings)
            : this(connection, encoder, frameReader, requestVerifier, autoAckSettings, true)
        {
        }

        /// <summary>Create a new instance.</summary>
        /// <param name="connection">The <see cref="IHttp2Connection"/> associated with this decoder.</param>
        /// <param name="encoder">The <see cref="IHttp2ConnectionEncoder"/> associated with this decoder.</param>
        /// <param name="frameReader">Responsible for reading/parsing the raw frames. As opposed to this object which applies
        /// h2 semantics on top of the frames.</param>
        /// <param name="requestVerifier">Determines if push promised streams are valid.</param>
        /// <param name="autoAckSettings"><c>false</c> to disable automatically applying and sending settings acknowledge frame.
        /// The <paramref name="encoder"/> is expected to be an instance of
        /// <see cref="IHttp2SettingsReceivedConsumer"/> and will apply the earliest received but not yet
        /// ACKed SETTINGS when writing the SETTINGS ACKs. <c>true</c> to enable automatically
        /// applying and sending settings acknowledge frame.</param>
        /// <param name="autoAckPing"><c>false</c> to disable automatically sending ping acknowledge frame. <c>true</c> to enable
        /// automatically sending ping ack frame.</param>
        public DefaultHttp2ConnectionDecoder(IHttp2Connection connection,
            IHttp2ConnectionEncoder encoder, IHttp2FrameReader frameReader, IHttp2PromisedRequestVerifier requestVerifier,
            bool autoAckSettings, bool autoAckPing)
        {
            if (connection is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.connection); }
            if (frameReader is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.frameReader); }
            if (encoder is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.encoder); }
            if (requestVerifier is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.requestVerifier); }

            _autoAckPing = autoAckPing;
            if (autoAckSettings)
            {
                _settingsReceivedConsumer = null;
            }
            else
            {
                var receivedConsumer = encoder as IHttp2SettingsReceivedConsumer;
                if (receivedConsumer is null)
                {
                    ThrowHelper.ThrowInvalidOperationException_disabling_autoAckSettings_requires_encoder_IHttp2SettingsReceivedConsumer();
                }
                _settingsReceivedConsumer = receivedConsumer;
            }
            _connection = connection;
            _frameReader = frameReader;
            _encoder = encoder;
            _requestVerifier = requestVerifier;
            var connLocal = connection.Local;
            if (connLocal.FlowController is null)
            {
                connLocal.FlowController = new DefaultHttp2LocalFlowController(connection);
            }
            connLocal.FlowController.FrameWriter(encoder.FrameWriter);

            _internalFrameListener = new PrefaceFrameListener(this);
        }

        public void LifecycleManager(IHttp2LifecycleManager lifecycleManager)
        {
            if (lifecycleManager is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.lifecycleManager); }
            _lifecycleManager = lifecycleManager;
        }

        public IHttp2Connection Connection => _connection;

        public IHttp2LocalFlowController FlowController => _connection.Local.FlowController;

        public IHttp2FrameListener FrameListener
        {
            get => _listener;
            set
            {
                if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
                _listener = value;
            }
        }

        // Visible for testing
        internal IHttp2FrameListener InternalFrameListener => _internalFrameListener;

        public bool PrefaceReceived => _internalFrameListener.GetType() == typeof(FrameReadListener);

        public virtual void DecodeFrame(IChannelHandlerContext ctx, IByteBuffer input, List<object> output)
        {
            _frameReader.ReadFrame(ctx, input, _internalFrameListener);
        }

        public Http2Settings LocalSettings
        {
            get
            {
                Http2Settings settings = new Http2Settings();
                var config = _frameReader.Configuration;
                var headersConfig = config.HeadersConfiguration;
                var frameSizePolicy = config.FrameSizePolicy;
                settings.InitialWindowSize(FlowController.InitialWindowSize);
                settings.MaxConcurrentStreams(_connection.Remote.MaxActiveStreams);
                settings.HeaderTableSize(headersConfig.MaxHeaderTableSize);
                settings.MaxFrameSize(frameSizePolicy.MaxFrameSize);
                settings.MaxHeaderListSize(headersConfig.MaxHeaderListSize);
                if (!_connection.IsServer)
                {
                    // Only set the pushEnabled flag if this is a client endpoint.
                    settings.PushEnabled(_connection.Local.AllowPushTo());
                }
                return settings;
            }
        }

        public virtual void Close()
        {
            _frameReader.Close();
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose() => Close();

        /// <summary>
        /// Calculate the threshold in bytes which should trigger a <c>GO_AWAY</c> if a set of headers exceeds this amount.
        /// </summary>
        /// <param name="maxHeaderListSize">
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_HEADER_LIST_SIZE</a> for the local endpoint.</param>
        /// <returns>the threshold in bytes which should trigger a <c>GO_AWAY</c> if a set of headers exceeds this amount.</returns>
        protected long CalculateMaxHeaderListSizeGoAway(long maxHeaderListSize)
        {
            return Http2CodecUtil.CalculateMaxHeaderListSizeGoAway(maxHeaderListSize);
        }

        private int UnconsumedBytes(IHttp2Stream stream)
        {
            return FlowController.UnconsumedBytes(stream);
        }

        internal void OnGoAwayRead0(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData)
        {
            _connection.GoAwayReceived(lastStreamId, errorCode, debugData);
            _listener.OnGoAwayRead(ctx, lastStreamId, errorCode, debugData);
        }

        internal void OnUnknownFrame0(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId, Http2Flags flags, IByteBuffer payload)
        {
            _listener.OnUnknownFrame(ctx, frameType, streamId, flags, payload);
        }

        /// <summary>
        /// Handles all inbound frames from the network.
        /// </summary>
        private sealed class FrameReadListener : IHttp2FrameListener
        {
            private readonly DefaultHttp2ConnectionDecoder _owner;

            public FrameReadListener(DefaultHttp2ConnectionDecoder owner) => _owner = owner;

            public int OnDataRead(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream)
            {
                var stream = _owner._connection.Stream(streamId);
                var flowController = _owner.FlowController;
                int bytesToReturn = data.ReadableBytes + padding;

                var shouldIgnore = false;
                try
                {
                    shouldIgnore = ShouldIgnoreHeadersOrDataFrame(ctx, streamId, stream, Http2FrameTypes.Data);
                }
                catch (Http2Exception e)
                {
                    // Ignoring this frame. We still need to count the frame towards the connection flow control
                    // window, but we immediately mark all bytes as consumed.
                    flowController.ReceiveFlowControlledFrame(stream, data, padding, endOfStream);
                    flowController.ConsumeBytes(stream, bytesToReturn);
                    throw e;
                }
                catch (Exception t)
                {
                    ThrowHelper.ThrowConnectionError_UnhandledErrorOnDataStream(streamId, t);
                }

                if (shouldIgnore)
                {
                    // Ignoring this frame. We still need to count the frame towards the connection flow control
                    // window, but we immediately mark all bytes as consumed.
                    flowController.ReceiveFlowControlledFrame(stream, data, padding, endOfStream);
                    flowController.ConsumeBytes(stream, bytesToReturn);

                    // Verify that the stream may have existed after we apply flow control.
                    VerifyStreamMayHaveExisted(streamId);

                    // All bytes have been consumed.
                    return bytesToReturn;
                }

                Http2Exception error = null;
                var streamState = stream.State;
                if (Http2StreamState.Open == streamState || Http2StreamState.HalfClosedLocal == streamState)
                {
                    // nothing to do
                }
                else if (Http2StreamState.Closed == streamState || Http2StreamState.HalfClosedRemote == streamState)
                {
                    error = ThrowHelper.GetStreamError_StreamInUnexpectedState(Http2Error.StreamClosed, stream);
                }
                else
                {
                    error = ThrowHelper.GetStreamError_StreamInUnexpectedState(Http2Error.ProtocolError, stream);
                }

                int unconsumedBytes = _owner.UnconsumedBytes(stream);
                try
                {
                    flowController.ReceiveFlowControlledFrame(stream, data, padding, endOfStream);
                    // Update the unconsumed bytes after flow control is applied.
                    unconsumedBytes = _owner.UnconsumedBytes(stream);

                    // If the stream is in an invalid state to receive the frame, throw the error.
                    if (error is object) { throw error; }

                    // Call back the application and retrieve the number of bytes that have been
                    // immediately processed.
                    bytesToReturn = _owner._listener.OnDataRead(ctx, streamId, data, padding, endOfStream);
                    return bytesToReturn;
                }
                catch (Http2Exception)
                {
                    // If an exception happened during delivery, the listener may have returned part
                    // of the bytes before the error occurred. If that's the case, subtract that from
                    // the total processed bytes so that we don't return too many bytes.
                    int delta = unconsumedBytes - _owner.UnconsumedBytes(stream);
                    bytesToReturn -= delta;
                    throw;
                }
                catch (Exception)
                {
                    // If an exception happened during delivery, the listener may have returned part
                    // of the bytes before the error occurred. If that's the case, subtract that from
                    // the total processed bytes so that we don't return too many bytes.
                    int delta = unconsumedBytes - _owner.UnconsumedBytes(stream);
                    bytesToReturn -= delta;
                    throw;
                }
                finally
                {
                    // If appropriate, return the processed bytes to the flow controller.
                    flowController.ConsumeBytes(stream, bytesToReturn);

                    if (endOfStream)
                    {
                        _owner._lifecycleManager.CloseStreamRemote(stream, TaskUtil.Completed);
                    }
                }
            }

            public void OnGoAwayRead(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData)
            {
                _owner.OnGoAwayRead0(ctx, lastStreamId, errorCode, debugData);
            }

            public void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream)
            {
                OnHeadersRead(ctx, streamId, headers, 0, Http2CodecUtil.DefaultPriorityWeight, false, padding, endOfStream);
            }

            public void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream)
            {
                var connection = _owner._connection;
                var stream = connection.Stream(streamId);
                var allowHalfClosedRemote = false;
                if (stream is null && !connection.StreamMayHaveExisted(streamId))
                {
                    stream = connection.Remote.CreateStream(streamId, endOfStream);
                    // Allow the state to be HALF_CLOSE_REMOTE if we're creating it in that state.
                    allowHalfClosedRemote = stream.State == Http2StreamState.HalfClosedRemote;
                }

                if (ShouldIgnoreHeadersOrDataFrame(ctx, streamId, stream, Http2FrameTypes.Headers))
                {
                    return;
                }

                var isInformational = !connection.IsServer &&
                        HttpStatusClass.ValueOf(headers.Status) == HttpStatusClass.Informational;
                if ((isInformational || !endOfStream) && stream.IsHeadersReceived || stream.IsTrailersReceived)
                {
                    ThrowHelper.ThrowStreamError_StreamReceivedTooManyHeadersEOS(streamId, endOfStream, stream);
                }

                var streamState = stream.State;
                if (Http2StreamState.ReservedRemote == streamState)
                {
                    stream.Open(endOfStream);
                }
                else if (Http2StreamState.Open == streamState || Http2StreamState.HalfClosedLocal == streamState)
                {
                    // Allowed to receive headers in these states.
                }
                else if (Http2StreamState.HalfClosedRemote == streamState)
                {
                    if (!allowHalfClosedRemote)
                    {
                        ThrowHelper.ThrowStreamError_StreamInUnexpectedState(stream);
                    }
                }
                else if (Http2StreamState.Closed == streamState)
                {
                    ThrowHelper.ThrowStreamError_StreamInUnexpectedState(stream);
                }
                else
                {
                    // Connection error.
                    ThrowHelper.ThrowConnectionError_StreamInUnexpectedState(stream);
                }

                stream.HeadersReceived(isInformational);
                _owner._encoder.FlowController.UpdateDependencyTree(streamId, streamDependency, weight, exclusive);

                _owner._listener.OnHeadersRead(ctx, streamId, headers, streamDependency, weight, exclusive, padding, endOfStream);

                // If the headers completes this stream, close it.
                if (endOfStream)
                {
                    _owner._lifecycleManager.CloseStreamRemote(stream, TaskUtil.Completed);
                }
            }

            public void OnPingAckRead(IChannelHandlerContext ctx, long data)
            {
                _owner._listener.OnPingAckRead(ctx, data);
            }

            public void OnPingRead(IChannelHandlerContext ctx, long data)
            {
                if (_owner._autoAckPing)
                {
                    // Send an ack back to the remote client.
                    _owner._encoder.WritePingAsync(ctx, true, data, ctx.NewPromise());
                }

                _owner._listener.OnPingRead(ctx, data);
            }

            public void OnPriorityRead(IChannelHandlerContext ctx, int streamId, int streamDependency, short weight, bool exclusive)
            {
                _owner._encoder.FlowController.UpdateDependencyTree(streamId, streamDependency, weight, exclusive);

                _owner._listener.OnPriorityRead(ctx, streamId, streamDependency, weight, exclusive);
            }

            public void OnPushPromiseRead(IChannelHandlerContext ctx, int streamId, int promisedStreamId, IHttp2Headers headers, int padding)
            {
                // A client cannot push.
                var connection = _owner._connection;
                if (connection.IsServer)
                {
                    ThrowHelper.ThrowConnectionError_AClientCannotPush();
                }

                var parentStream = connection.Stream(streamId);

                if (ShouldIgnoreHeadersOrDataFrame(ctx, streamId, parentStream, Http2FrameTypes.PushPromise))
                {
                    return;
                }

                if (parentStream is null)
                {
                    ThrowHelper.ThrowConnectionError_StreamDoesNotExist(streamId);
                }

                var streamState = parentStream.State;
                if (Http2StreamState.Open == streamState || Http2StreamState.HalfClosedLocal == streamState)
                {
                    // Allowed to receive push promise in these states.
                }
                else
                {
                    // Connection error.
                    ThrowHelper.ThrowConnectionError_StreamInUnexpectedStateForReceivingPushPromise(parentStream);
                }

                var requestVerifier = _owner._requestVerifier;
                if (!requestVerifier.IsAuthoritative(ctx, headers))
                {
                    ThrowHelper.ThrowStreamError_PromisedRequestOnStreamForPromisedStreamIsNotAuthoritative(streamId, promisedStreamId);
                }
                if (!requestVerifier.IsCacheable(headers))
                {
                    ThrowHelper.ThrowStreamError_PromisedRequestOnStreamForPromisedStreamIsNotKnownToBeCacheable(streamId, promisedStreamId);
                }
                if (!requestVerifier.IsSafe(headers))
                {
                    ThrowHelper.ThrowStreamError_PromisedRequestOnStreamForPromisedStreamIsNotKnownToBeSafe(streamId, promisedStreamId);
                }

                // Reserve the push stream based with a priority based on the current stream's priority.
                connection.Remote.ReservePushStream(promisedStreamId, parentStream);

                _owner._listener.OnPushPromiseRead(ctx, streamId, promisedStreamId, headers, padding);
            }

            public void OnRstStreamRead(IChannelHandlerContext ctx, int streamId, Http2Error errorCode)
            {
                var stream = _owner._connection.Stream(streamId);
                if (stream is null)
                {
                    VerifyStreamMayHaveExisted(streamId);
                    return;
                }
                if (Http2StreamState.Idle == stream.State)
                {
                    ThrowHelper.ThrowConnectionError_RstStreamReceivedForIdleStream(streamId);
                }
                else if (Http2StreamState.Closed == stream.State)
                {
                    return; // RST_STREAM frames must be ignored for closed streams.
                }

                _owner._listener.OnRstStreamRead(ctx, streamId, errorCode);

                _owner._lifecycleManager.CloseStream(stream, TaskUtil.Completed);
            }

            public void OnSettingsAckRead(IChannelHandlerContext ctx)
            {
                // Apply oldest outstanding local settings here. This is a synchronization point between endpoints.
                Http2Settings settings = _owner._encoder.PollSentSettings;

                if (settings is object)
                {
                    ApplyLocalSettings(settings);
                }

                _owner._listener.OnSettingsAckRead(ctx);
            }

            /// <summary>
            /// Applies settings sent from the local endpoint.
            /// <para>This method is only called after the local settings have been acknowledged from the remote endpoint.</para>
            /// </summary>
            /// <param name="settings"></param>
            private void ApplyLocalSettings(Http2Settings settings)
            {
                var pushEnabled = settings.PushEnabled();
                var config = _owner._frameReader.Configuration;
                var headerConfig = config.HeadersConfiguration;
                var frameSizePolicy = config.FrameSizePolicy;
                var connection = _owner._connection;
                if (pushEnabled.HasValue)
                {
                    if (connection.IsServer)
                    {
                        ThrowHelper.ThrowConnectionError_ServerSendingSettintsFrameWithEnablePushSpecified();
                    }
                    connection.Local.AllowPushTo(pushEnabled.Value);
                }

                var maxConcurrentStreams = settings.MaxConcurrentStreams();
                if (maxConcurrentStreams.HasValue)
                {
                    connection.Remote.SetMaxActiveStreams((int)Math.Min(maxConcurrentStreams.Value, int.MaxValue));
                }

                var headerTableSize = settings.HeaderTableSize();
                if (headerTableSize.HasValue)
                {
                    headerConfig.SetMaxHeaderTableSize(headerTableSize.Value);
                }

                var maxHeaderListSize = settings.MaxHeaderListSize();
                if (maxHeaderListSize.HasValue)
                {
                    headerConfig.SetMaxHeaderListSize(maxHeaderListSize.Value, _owner.CalculateMaxHeaderListSizeGoAway(maxHeaderListSize.Value));
                }

                var maxFrameSize = settings.MaxFrameSize();
                if (maxFrameSize.HasValue)
                {
                    frameSizePolicy.SetMaxFrameSize(maxFrameSize.Value);
                }

                var initialWindowSize = settings.InitialWindowSize();
                if (initialWindowSize.HasValue)
                {
                    _owner.FlowController.SetInitialWindowSize(initialWindowSize.Value);
                }
            }

            public void OnSettingsRead(IChannelHandlerContext ctx, Http2Settings settings)
            {
                var settingsReceivedConsumer = _owner._settingsReceivedConsumer;
                if (settingsReceivedConsumer is null)
                {
                    // Acknowledge receipt of the settings. We should do this before we process the settings to ensure our
                    // remote peer applies these settings before any subsequent frames that we may send which depend upon
                    // these new settings. See https://github.com/netty/netty/issues/6520.
                    var encoder = _owner._encoder;
                    encoder.WriteSettingsAckAsync(ctx, ctx.NewPromise());

                    encoder.RemoteSettings(settings);
                }
                else
                {
                    settingsReceivedConsumer.ConsumeReceivedSettings(settings);
                }
                _owner._listener.OnSettingsRead(ctx, settings);
            }

            public void OnUnknownFrame(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId, Http2Flags flags, IByteBuffer payload)
            {
                _owner.OnUnknownFrame0(ctx, frameType, streamId, flags, payload);
            }

            public void OnWindowUpdateRead(IChannelHandlerContext ctx, int streamId, int windowSizeIncrement)
            {
                var stream = _owner._connection.Stream(streamId);
                if (stream is null || stream.State == Http2StreamState.Closed || StreamCreatedAfterGoAwaySent(streamId))
                {
                    // Ignore this frame.
                    VerifyStreamMayHaveExisted(streamId);
                    return;
                }

                // Update the outbound flow control window.
                _owner._encoder.FlowController.IncrementWindowSize(stream, windowSizeIncrement);

                _owner._listener.OnWindowUpdateRead(ctx, streamId, windowSizeIncrement);
            }

            /// <summary>
            /// Helper method to determine if a frame that has the semantics of headers or data should be ignored for the
            /// <paramref name="stream"/> (which may be <c>null</c>) associated with <paramref name="streamId"/>.
            /// </summary>
            /// <param name="ctx"></param>
            /// <param name="streamId"></param>
            /// <param name="stream"></param>
            /// <param name="frameName"></param>
            /// <returns></returns>
            private bool ShouldIgnoreHeadersOrDataFrame(IChannelHandlerContext ctx, int streamId, IHttp2Stream stream, Http2FrameTypes frameName)
            {
                if (stream is null)
                {
                    if (StreamCreatedAfterGoAwaySent(streamId))
                    {
                        if (Logger.InfoEnabled) { Logger.IgnoringFrameForStream(ctx, frameName, streamId); }
                        return true;
                    }
                    // Its possible that this frame would result in stream ID out of order creation (PROTOCOL ERROR) and its
                    // also possible that this frame is received on a CLOSED stream (STREAM_CLOSED after a RST_STREAM is
                    // sent). We don't have enough information to know for sure, so we choose the lesser of the two errors.
                    ThrowHelper.ThrowStreamError_ReceivedFrameForAnUnknownStream(streamId, frameName);
                }
                else if (stream.IsResetSent || StreamCreatedAfterGoAwaySent(streamId))
                {
                    // If we have sent a reset stream it is assumed the stream will be closed after the write completes.
                    // If we have not sent a reset, but the stream was created after a GoAway this is not supported by
                    // DefaultHttp2Connection and if a custom Http2Connection is used it is assumed the lifetime is managed
                    // elsewhere so we don't close the stream or otherwise modify the stream's state.

                    if (Logger.InfoEnabled)
                    {
                        Logger.IgnoringFrameForStreamRst(ctx, frameName, stream.IsResetSent,
                            _owner._connection.Remote.LastStreamKnownByPeer());
                    }

                    return true;
                }
                return false;
            }

            /// <summary>
            /// Helper method for determining whether or not to ignore inbound frames. A stream is considered to be created
            /// after a <c>GOAWAY</c> is sent if the following conditions hold:
            /// <para>
            /// <ul>
            ///     <li>A <c>GOAWAY</c> must have been sent by the local endpoint</li>
            ///     <li>The <paramref name="streamId"/> must identify a legitimate stream id for the remote endpoint to be creating</li>
            ///     <li><paramref name="streamId"/> is greater than the Last Known Stream ID which was sent by the local endpoint
            ///     in the last <c>GOAWAY</c> frame</li>
            /// </ul>
            /// </para>
            /// </summary>
            /// <param name="streamId"></param>
            /// <returns></returns>
            private bool StreamCreatedAfterGoAwaySent(int streamId)
            {
                var conn = _owner._connection;
                var remote = conn.Remote;
                return conn.GoAwaySent() && remote.IsValidStreamId(streamId) &&
                    streamId > remote.LastStreamKnownByPeer();
            }

            [MethodImpl(InlineMethod.AggressiveInlining)]
            private void VerifyStreamMayHaveExisted(int streamId)
            {
                if (!_owner._connection.StreamMayHaveExisted(streamId))
                {
                    ThrowHelper.ThrowConnectionError_StreamDoesNotExist(streamId);
                }
            }
        }

        private sealed class PrefaceFrameListener : IHttp2FrameListener
        {
            private readonly DefaultHttp2ConnectionDecoder _owner;

            public PrefaceFrameListener(DefaultHttp2ConnectionDecoder owner) => _owner = owner;

            [MethodImpl(InlineMethod.AggressiveInlining)]
            private void VerifyPrefaceReceived()
            {
                if (!_owner.PrefaceReceived)
                {
                    ThrowHelper.ThrowConnectionError_ReceivedNonSettingsAsFirstFrame();
                }
            }

            public int OnDataRead(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream)
            {
                VerifyPrefaceReceived();
                return _owner._internalFrameListener.OnDataRead(ctx, streamId, data, padding, endOfStream);
            }

            public void OnGoAwayRead(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData)
            {
                _owner.OnGoAwayRead0(ctx, lastStreamId, errorCode, debugData);
            }

            public void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream)
            {
                VerifyPrefaceReceived();
                _owner._internalFrameListener.OnHeadersRead(ctx, streamId, headers, padding, endOfStream);
            }

            public void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream)
            {
                VerifyPrefaceReceived();
                _owner._internalFrameListener.OnHeadersRead(ctx, streamId, headers, streamDependency, weight,
                        exclusive, padding, endOfStream);
            }

            public void OnPingAckRead(IChannelHandlerContext ctx, long data)
            {
                VerifyPrefaceReceived();
                _owner._internalFrameListener.OnPingAckRead(ctx, data);
            }

            public void OnPingRead(IChannelHandlerContext ctx, long data)
            {
                VerifyPrefaceReceived();
                _owner._internalFrameListener.OnPingRead(ctx, data);
            }

            public void OnPriorityRead(IChannelHandlerContext ctx, int streamId, int streamDependency, short weight, bool exclusive)
            {
                VerifyPrefaceReceived();
                _owner._internalFrameListener.OnPriorityRead(ctx, streamId, streamDependency, weight, exclusive);
            }

            public void OnPushPromiseRead(IChannelHandlerContext ctx, int streamId, int promisedStreamId, IHttp2Headers headers, int padding)
            {
                VerifyPrefaceReceived();
                _owner._internalFrameListener.OnPushPromiseRead(ctx, streamId, promisedStreamId, headers, padding);
            }

            public void OnRstStreamRead(IChannelHandlerContext ctx, int streamId, Http2Error errorCode)
            {
                VerifyPrefaceReceived();
                _owner._internalFrameListener.OnRstStreamRead(ctx, streamId, errorCode);
            }

            public void OnSettingsAckRead(IChannelHandlerContext ctx)
            {
                VerifyPrefaceReceived();
                _owner._internalFrameListener.OnSettingsAckRead(ctx);
            }

            public void OnSettingsRead(IChannelHandlerContext ctx, Http2Settings settings)
            {
                // The first settings should change the internalFrameListener to the "real" listener
                // that expects the preface to be verified.
                if (!_owner.PrefaceReceived)
                {
                    _owner._internalFrameListener = new FrameReadListener(_owner);
                }
                _owner._internalFrameListener.OnSettingsRead(ctx, settings);
            }

            public void OnUnknownFrame(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId, Http2Flags flags, IByteBuffer payload)
            {
                _owner.OnUnknownFrame0(ctx, frameType, streamId, flags, payload);
            }

            public void OnWindowUpdateRead(IChannelHandlerContext ctx, int streamId, int windowSizeIncrement)
            {
                VerifyPrefaceReceived();
                _owner._internalFrameListener.OnWindowUpdateRead(ctx, streamId, windowSizeIncrement);
            }
        }
    }
}