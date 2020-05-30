namespace DotNetty.Codecs.Http.WebSockets
{
    /// <summary>
    /// Frames decoder configuration.
    /// </summary>
    public sealed class WebSocketDecoderConfig
    {
        private WebSocketDecoderConfig(int maxFramePayloadLength, bool expectMaskedFrames, bool allowMaskMismatch,
                                       bool allowExtensions, bool closeOnProtocolViolation, bool withUTF8Validator)
        {
            MaxFramePayloadLength = maxFramePayloadLength;
            ExpectMaskedFrames = expectMaskedFrames;
            AllowMaskMismatch = allowMaskMismatch;
            AllowExtensions = allowExtensions;
            CloseOnProtocolViolation = closeOnProtocolViolation;
            WithUTF8Validator = withUTF8Validator;
        }

        /// <summary>
        /// Maximum length of a frame's payload. Setting this to an appropriate value for you application
        /// helps check for denial of services attacks.
        /// </summary>
        public int MaxFramePayloadLength { get; }

        /// <summary>
        /// Web socket servers must set this to true processed incoming masked payload. Client implementations
        /// must set this to false.
        /// </summary>
        public bool ExpectMaskedFrames { get; }

        /// <summary>
        /// Allows to loosen the masking requirement on received frames. When this is set to false then also
        /// frames which are not masked properly according to the standard will still be accepted.
        /// </summary>
        public bool AllowMaskMismatch { get; }

        /// <summary>
        /// Flag to allow reserved extension bits to be used or not
        /// </summary>
        public bool AllowExtensions { get; }

        /// <summary>
        /// Flag to send close frame immediately on any protocol violation.ion.
        /// </summary>
        public bool CloseOnProtocolViolation { get; }

        /// <summary>
        /// Allows you to avoid adding of Utf8FrameValidator to the pipeline on the
        /// WebSocketServerProtocolHandler creation. This is useful (less overhead)
        /// when you use only BinaryWebSocketFrame within your web socket connection.
        /// </summary>
        public bool WithUTF8Validator { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"WebSocketDecoderConfig [maxFramePayloadLength={MaxFramePayloadLength}, expectMaskedFrames={ExpectMaskedFrames}, allowMaskMismatch={AllowMaskMismatch}, allowExtensions={AllowExtensions}, closeOnProtocolViolation={CloseOnProtocolViolation}, withUTF8Validator={WithUTF8Validator}]";
        }

        public Builder ToBuilder()
        {
            return new Builder(this);
        }

        public static Builder NewBuilder()
        {
            return new Builder();
        }

        public sealed class Builder
        {
            private int _maxFramePayloadLength = 65536;
            private bool _expectMaskedFrames = true;
            private bool _allowMaskMismatch;
            private bool _allowExtensions;
            private bool _closeOnProtocolViolation = true;
            private bool _withUTF8Validator = true;

            internal Builder() { }

            internal Builder(WebSocketDecoderConfig decoderConfig)
            {
                if (decoderConfig is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.decoderConfig); }

                _maxFramePayloadLength = decoderConfig.MaxFramePayloadLength;
                _expectMaskedFrames = decoderConfig.ExpectMaskedFrames;
                _allowMaskMismatch = decoderConfig.AllowMaskMismatch;
                _allowExtensions = decoderConfig.AllowExtensions;
                _closeOnProtocolViolation = decoderConfig.CloseOnProtocolViolation;
                _withUTF8Validator = decoderConfig.WithUTF8Validator;
            }

            public Builder MaxFramePayloadLength(int maxFramePayloadLength)
            {
                _maxFramePayloadLength = maxFramePayloadLength;
                return this;
            }

            public Builder ExpectMaskedFrames(bool expectMaskedFrames)
            {
                _expectMaskedFrames = expectMaskedFrames;
                return this;
            }

            public Builder AllowMaskMismatch(bool allowMaskMismatch)
            {
                _allowMaskMismatch = allowMaskMismatch;
                return this;
            }

            public Builder AllowExtensions(bool allowExtensions)
            {
                _allowExtensions = allowExtensions;
                return this;
            }

            public Builder CloseOnProtocolViolation(bool closeOnProtocolViolation)
            {
                _closeOnProtocolViolation = closeOnProtocolViolation;
                return this;
            }

            public Builder WithUTF8Validator(bool withUTF8Validator)
            {
                _withUTF8Validator = withUTF8Validator;
                return this;
            }

            public WebSocketDecoderConfig Build()
            {
                return new WebSocketDecoderConfig(
                    _maxFramePayloadLength, _expectMaskedFrames, _allowMaskMismatch,
                    _allowExtensions, _closeOnProtocolViolation, _withUTF8Validator);
            }
        }
    }
}
