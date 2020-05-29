// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Abstract base class which defines commonly used features required to build <see cref="Http2ConnectionHandler"/> instances.
    ///
    /// <h3>Three ways to build a <see cref="Http2ConnectionHandler"/></h3>
    /// <h4>Let the builder create a <see cref="Http2ConnectionHandler"/></h4>
    /// Simply call all the necessary setter methods, and then use <see cref="Build()"/> to build a new
    /// <see cref="Http2ConnectionHandler"/>. Setting the following properties are prohibited because they are used for
    /// other ways of building a <see cref="Http2ConnectionHandler"/>.
    /// conflicts with this option:
    /// <ul>
    ///   <li><see cref="Connection"/></li>
    ///   <li><see cref="Codec(IHttp2ConnectionDecoder, IHttp2ConnectionEncoder)"/></li>
    /// </ul>
    ///
    ///
    /// <h4>Let the builder use the <see cref="Http2ConnectionHandler"/> you specified</h4>
    /// Call <see cref="Connection"/> to tell the builder that you want to build the handler from the
    /// <see cref="IHttp2Connection"/> you specified. Setting the following properties are prohibited and thus will trigger
    /// an <see cref="InvalidOperationException"/> because they conflict with this option.
    /// <ul>
    ///   <li><see cref="IsServer"/></li>
    ///   <li><see cref="Codec(IHttp2ConnectionDecoder, IHttp2ConnectionEncoder)"/></li>
    /// </ul>
    ///
    /// <h4>Let the builder use the <see cref="IHttp2ConnectionDecoder"/> and <see cref="IHttp2ConnectionEncoder"/> you specified</h4>
    /// Call <see cref="Codec(IHttp2ConnectionDecoder, IHttp2ConnectionEncoder)"/> to tell the builder that you want to built the
    /// handler from the <see cref="IHttp2ConnectionDecoder"/> and <see cref="IHttp2ConnectionEncoder"/> you specified. Setting the
    /// following properties are prohibited and thus will trigger an <see cref="InvalidOperationException"/> because they conflict
    /// with this option:
    /// <ul>
    ///   <li><see cref="IsServer"/></li>
    ///   <li><see cref="Connection"/></li>
    ///   <li><see cref="FrameLogger"/></li>
    ///   <li><see cref="HeaderSensitivityDetector"/></li>
    ///   <li><see cref="EncoderEnforceMaxConcurrentStreams"/></li>
    ///   <li><see cref="EncoderIgnoreMaxHeaderListSize"/></li>
    ///   <li><see cref="InitialHuffmanDecodeCapacity"/></li>
    /// </ul>
    ///
    /// <h3>Exposing necessary methods in a subclass</h3>
    /// <see cref="Build()"/> method and all property access methods are <c>protected</c>. Choose the methods to expose to the
    /// users of your builder implementation and make them <c>public</c>.
    /// </summary>
    /// <typeparam name="THandler">The type of handler created by this builder.</typeparam>
    /// <typeparam name="TBuilder">The concrete type of this builder.</typeparam>
    public abstract class AbstractHttp2ConnectionHandlerBuilder<THandler, TBuilder>
        where THandler : Http2ConnectionHandler
        where TBuilder : AbstractHttp2ConnectionHandlerBuilder<THandler, TBuilder>
    {
        // The properties that can always be set.
        private Http2Settings _initialSettings = Http2Settings.DefaultSettings();
        private IHttp2FrameListener _frameListener;
        private TimeSpan _gracefulShutdownTimeout = Http2CodecUtil.DefaultGracefulShutdownTimeout;
        private bool _decoupleCloseAndGoAway;

        // The property that will prohibit connection() and codec() if set by server(),
        // because this property is used only when this builder creates a Http2Connection.
        private bool _isServer = true;
        private int _maxReservedStreams = Http2CodecUtil.DefaultMaxReservedStreams;

        // The property that will prohibit server() and codec() if set by connection().
        private IHttp2Connection _connection;

        // The properties that will prohibit server() and connection() if set by codec().
        private IHttp2ConnectionDecoder _decoder;
        private IHttp2ConnectionEncoder _encoder;

        // The properties that are:
        // * mutually exclusive against codec() and
        // * OK to use with server() and connection()
        private bool _validateHeaders = true;
        private IHttp2FrameLogger _frameLogger;
        private ISensitivityDetector _headerSensitivityDetector;
        private bool _encoderEnforceMaxConcurrentStreams = false;
        private bool _encoderIgnoreMaxHeaderListSize;
        private IHttp2PromisedRequestVerifier _promisedRequestVerifier = AlwaysVerifyPromisedRequestVerifier.Instance;
        private bool _autoAckSettingsFrame = true;
        private bool _autoAckPingFrame = true;

        /// <summary>
        /// Gets or sets the <see cref="Http2Settings"/> to use for the initial connection settings exchange.
        /// </summary>
        public Http2Settings InitialSettings
        {
            get => _initialSettings;
            set
            {
                if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
                _initialSettings = value;
            }
        }

        /// <summary>
        /// Gets or sets the listener of inbound frames.
        /// </summary>
        /// <remarks>This listener will only be set if the decoder's listener is <c>null</c>.</remarks>
        public IHttp2FrameListener FrameListener
        {
            get => _frameListener;
            set
            {
                if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
                _frameListener = value;
            }
        }

        /// <summary>
        /// Gets or sets the graceful shutdown timeout of the <see cref="IHttp2Connection"/> in milliseconds. Returns -1 if the
        /// timeout is indefinite.
        /// </summary>
        public TimeSpan GracefulShutdownTimeout
        {
            get => _gracefulShutdownTimeout;
            set
            {
                if (value < Timeout.InfiniteTimeSpan)
                {
                    ThrowHelper.ThrowArgumentException_InvalidGracefulShutdownTimeout(value);
                }
                _gracefulShutdownTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets if <see cref="Build()"/> will to create a <see cref="IHttp2Connection"/> in server mode (<c>true</c>)
        /// or client mode (<c>false</c>).
        /// </summary>
        public bool IsServer
        {
            get => _isServer;
            set
            {
                EnforceConstraint("server", "connection", _connection);
                EnforceConstraint("server", "codec", _decoder);
                EnforceConstraint("server", "codec", _encoder);
                _isServer = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of streams which can be in the reserved state at any given time.
        /// 
        /// <para>By default this value will be ignored on the server for local endpoint. This is because the RFC provides
        /// no way to explicitly communicate a limit to how many states can be in the reserved state, and instead relies
        /// on the peer to send RST_STREAM frames when they will be rejected.</para>
        /// </summary>
        public int MaxReservedStreams
        {
            get => _maxReservedStreams;
            set
            {
                EnforceConstraint("server", "connection", _connection);
                EnforceConstraint("server", "codec", _decoder);
                EnforceConstraint("server", "codec", _encoder);
                if (value < 0) { ThrowHelper.ThrowArgumentException_PositiveOrZero(value, ExceptionArgument.value); }

                _maxReservedStreams = value;
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="IHttp2Connection"/> to use.
        /// </summary>
        public IHttp2Connection Connection
        {
            get => _connection;
            set
            {
                //EnforceConstraint("connection", "maxReservedStreams", maxReservedStreams);
                //EnforceConstraint("connection", "server", isServer);
                EnforceConstraint("connection", "codec", _decoder);
                EnforceConstraint("connection", "codec", _encoder);
                if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
                _connection = value;
            }
        }

        /// <summary>
        /// Gets the <see cref="IHttp2ConnectionDecoder"/> to use.
        /// </summary>
        public IHttp2ConnectionDecoder Decoder => _decoder;

        /// <summary>
        /// Gets the <see cref="IHttp2ConnectionEncoder"/> to use.
        /// </summary>
        public IHttp2ConnectionEncoder Encoder => _encoder;

        /// <summary>
        /// Sets the <see cref="IHttp2ConnectionDecoder"/> and <see cref="IHttp2ConnectionEncoder"/> to use.
        /// </summary>
        /// <param name="decoder"></param>
        /// <param name="encoder"></param>
        /// <returns></returns>
        public TBuilder Codec(IHttp2ConnectionDecoder decoder, IHttp2ConnectionEncoder encoder)
        {
            //EnforceConstraint("codec", "server", isServer);
            //EnforceConstraint("codec", "maxReservedStreams", maxReservedStreams);
            EnforceConstraint("codec", "connection", _connection);
            EnforceConstraint("codec", "frameLogger", _frameLogger);
            //EnforceConstraint("codec", "validateHeaders", validateHeaders);
            EnforceConstraint("codec", "headerSensitivityDetector", _headerSensitivityDetector);
            //EnforceConstraint("codec", "encoderEnforceMaxConcurrentStreams", encoderEnforceMaxConcurrentStreams);
            if (decoder is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.decoder); }
            if (encoder is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.encoder); }

            if (decoder.Connection != encoder.Connection)
            {
                ThrowHelper.ThrowArgumentException_DifferentConnections();
            }

            _decoder = decoder;
            _encoder = encoder;

            return Self();
        }

        /// <summary>
        /// Gets or sets if HTTP headers should be validated according to
        /// <a href="https://tools.ietf.org/html/rfc7540#section-8.1.2.6">RFC 7540, 8.1.2.6</a>.
        /// </summary>
        public bool IsValidateHeaders
        {
            get => _validateHeaders;
            set
            {
                EnforceNonCodecConstraints("validateHeaders");
                _validateHeaders = value;
            }
        }

        /// <summary>
        /// Gets or sets the logger that is used for the encoder and decoder.
        /// </summary>
        public IHttp2FrameLogger FrameLogger
        {
            get => _frameLogger;
            set
            {
                EnforceNonCodecConstraints("frameLogger");
                if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
                _frameLogger = value;
            }
        }

        /// <summary>
        /// Gets or sets if the encoder should queue frames if the maximum number of concurrent streams
        /// would otherwise be exceeded.
        /// </summary>
        public bool EncoderEnforceMaxConcurrentStreams
        {
            get => _encoderEnforceMaxConcurrentStreams;
            set
            {
                EnforceNonCodecConstraints("encoderEnforceMaxConcurrentStreams");
                _encoderEnforceMaxConcurrentStreams = value;
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="ISensitivityDetector"/> to use.
        /// </summary>
        public ISensitivityDetector HeaderSensitivityDetector
        {
            get => _headerSensitivityDetector ?? NeverSensitiveDetector.Instance;
            set
            {
                EnforceNonCodecConstraints("headerSensitivityDetector");
                if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
                _headerSensitivityDetector = value;
            }
        }

        /// <summary>
        /// Gets or sets if the <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_HEADER_LIST_SIZE</a>
        /// should be ignored when encoding headers.
        /// <para><c>true</c> to ignore
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_HEADER_LIST_SIZE</a>.</para>
        /// </summary>
        public bool EncoderIgnoreMaxHeaderListSize
        {
            get => _encoderIgnoreMaxHeaderListSize;
            set
            {
                EnforceNonCodecConstraints("encoderIgnoreMaxHeaderListSize");
                _encoderIgnoreMaxHeaderListSize = value;
            }
        }

        /// <summary>
        /// Does nothing, do not call.
        /// </summary>
        [Obsolete("Huffman decoding no longer depends on having a decode capacity.")]
        public int InitialHuffmanDecodeCapacity { get; set; }

        /// <summary>
        /// Gets or sets <see cref="IHttp2PromisedRequestVerifier"/> to use.
        /// </summary>
        public IHttp2PromisedRequestVerifier PromisedRequestVerifier
        {
            get => _promisedRequestVerifier;
            set
            {
                EnforceNonCodecConstraints("promisedRequestVerifier");
                if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
                _promisedRequestVerifier = value;
            }
        }

        /// <summary>
        /// Determine if settings frame should automatically be acknowledged and applied.
        /// </summary>
        public bool AutoAckSettingsFrame
        {
            get => _autoAckSettingsFrame;
            set
            {
                EnforceNonCodecConstraints("autoAckSettingsFrame");
                _autoAckSettingsFrame = value;
            }
        }

        /// <summary>
        /// Determine if PING frame should automatically be acknowledged or not.
        /// </summary>
        public bool AutoAckPingFrame
        {
            get => _autoAckPingFrame;
            set
            {
                EnforceNonCodecConstraints("autoAckPingFrame");
                _autoAckPingFrame = value;
            }
        }

        /// <summary>
        /// Determine if the <see cref="IChannel.CloseAsync()"/> should be coupled with goaway and graceful close.
        /// </summary>
        public bool DecoupleCloseAndGoAway
        {
            get => _decoupleCloseAndGoAway;
            set => _decoupleCloseAndGoAway = value;
        }

        /// <summary>
        /// Create a new <see cref="Http2ConnectionHandler"/>.
        /// </summary>
        /// <returns></returns>
        public virtual THandler Build()
        {
            if (_encoder is object)
            {
                Debug.Assert(_decoder is object);
                return BuildFromCodec(_decoder, _encoder);
            }

            IHttp2Connection connection = _connection;
            if (connection is null)
            {
                connection = new DefaultHttp2Connection(_isServer, _maxReservedStreams);
            }

            return BuildFromConnection(connection);
        }

        private THandler BuildFromConnection(IHttp2Connection connection)
        {
            var maxHeaderListSize = _initialSettings.MaxHeaderListSize();
            IHttp2FrameReader reader = new DefaultHttp2FrameReader(new DefaultHttp2HeadersDecoder(_validateHeaders,
                    maxHeaderListSize ?? Http2CodecUtil.DefaultHeaderListSize,
                    -1));
            IHttp2FrameWriter writer = new DefaultHttp2FrameWriter(HeaderSensitivityDetector, _encoderIgnoreMaxHeaderListSize);

            if (_frameLogger is object)
            {
                reader = new Http2InboundFrameLogger(reader, _frameLogger);
                writer = new Http2OutboundFrameLogger(writer, _frameLogger);
            }

            IHttp2ConnectionEncoder encoder = new DefaultHttp2ConnectionEncoder(connection, writer);

            if (_encoderEnforceMaxConcurrentStreams)
            {
                if (connection.IsServer)
                {
                    encoder.Close();
                    reader.Close();
                    ThrowHelper.ThrowArgumentException_EncoderEnforceMaxConcurrentStreamsNotSupportedForServer(
                        _encoderEnforceMaxConcurrentStreams);
                }
                encoder = new StreamBufferingEncoder(encoder);
            }

            var decoder = new DefaultHttp2ConnectionDecoder(connection, encoder, reader,
                    PromisedRequestVerifier, AutoAckSettingsFrame, AutoAckPingFrame);
            return BuildFromCodec(decoder, encoder);
        }

        private THandler BuildFromCodec(IHttp2ConnectionDecoder decoder, IHttp2ConnectionEncoder encoder)
        {
            THandler handler = null;
            try
            {
                // Call the abstract build method
                handler = Build(decoder, encoder, _initialSettings);
            }
            catch (Exception t)
            {
                encoder.Close();
                decoder.Close();
                ThrowHelper.ThrowInvalidOperationException_FailedToBuildHttp2ConnectionHandler(t);
            }

            // Setup post build options
            handler.GracefulShutdownTimeout = _gracefulShutdownTimeout;
            if (handler.Decoder.FrameListener is null)
            {
                handler.Decoder.FrameListener = _frameListener;
            }
            return handler;
        }

        /// <summary>
        /// Implement this method to create a new <see cref="Http2ConnectionHandler"/> or its subtype instance.
        /// </summary>
        /// <param name="decoder"></param>
        /// <param name="encoder"></param>
        /// <param name="initialSettings"></param>
        /// <returns>The return of this method will be subject to the following:
        /// <para><see cref="FrameListener"/> will be set if not already set in the decoder</para>
        /// <see cref="GracefulShutdownTimeout"/> will always be set
        /// </returns>
        protected abstract THandler Build(IHttp2ConnectionDecoder decoder, IHttp2ConnectionEncoder encoder, Http2Settings initialSettings);

        [MethodImpl(InlineMethod.AggressiveInlining)]
        protected TBuilder Self() => (TBuilder)this;

        private void EnforceNonCodecConstraints(string rejected)
        {
            EnforceConstraint(rejected, "server/connection", _decoder);
            EnforceConstraint(rejected, "server/connection", _encoder);
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        private static void EnforceConstraint(string methodName, string rejectorName, object value)
        {
            if (value is object)
            {
                ThrowHelper.ThrowInvalidOperationException_EnforceConstraint(methodName, rejectorName);
            }
        }
    }
}
