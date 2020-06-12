namespace DotNetty.Codecs.Http.WebSockets
{
    using System;

    /// <summary>
    /// WebSocket server configuration.
    /// </summary>
    public class WebSocketServerProtocolConfig
    {
        internal static readonly WebSocketServerProtocolConfig Default = new WebSocketServerProtocolConfig(
            "/", null, false, 10000L, 0, true, WebSocketCloseStatus.NormalClosure, true, WebSocketDecoderConfig.Default);

        private WebSocketServerProtocolConfig(
            string websocketPath,
            string subprotocols,
            bool checkStartsWith,
            long handshakeTimeoutMillis,
            long forceCloseTimeoutMillis,
            bool handleCloseFrames,
            WebSocketCloseStatus sendCloseFrame,
            bool dropPongFrames,
            WebSocketDecoderConfig decoderConfig
        )
        {
            if (handshakeTimeoutMillis <= 0L) { ThrowHelper.ThrowArgumentException_Positive(ExceptionArgument.handshakeTimeoutMillis); }

            WebsocketPath = websocketPath;
            Subprotocols = subprotocols;
            this.CheckStartsWith = checkStartsWith;
            this.HandshakeTimeoutMillis = handshakeTimeoutMillis;
            this.ForceCloseTimeoutMillis = forceCloseTimeoutMillis;
            this.HandleCloseFrames = handleCloseFrames;
            this.SendCloseFrame = sendCloseFrame;
            this.DropPongFrames = dropPongFrames;
            this.DecoderConfig = decoderConfig ?? WebSocketDecoderConfig.Default;
        }

        /// <summary>
        /// URI path component to handle websocket upgrade requests on.
        /// </summary>
        public readonly string WebsocketPath;

        /// <summary>
        /// CSV of supported protocols
        /// </summary>
        public readonly string Subprotocols;

        /// <summary>
        /// <c>true</c> to handle all requests, where URI path component starts from
        /// <see cref="WebSocketServerProtocolConfig.WebsocketPath"/>, <c>false</c> for exact match (default).
        /// </summary>
        public readonly bool CheckStartsWith;

        /// <summary>
        /// Handshake timeout in mills, when handshake timeout, will trigger user
        /// event <see cref="WebSocketServerProtocolHandler.ServerHandshakeStateEvent.HandshakeTimeout"/>
        /// </summary>
        public readonly long HandshakeTimeoutMillis;

        /// <summary>
        /// Close the connection if it was not closed by the client after timeout specified
        /// </summary>
        public readonly long ForceCloseTimeoutMillis;

        /// <summary>
        /// <c>true</c> if close frames should not be forwarded and just close the channel
        /// </summary>
        public readonly bool HandleCloseFrames;

        /// <summary>
        /// Close frame to send, when close frame was not send manually. Or <c>null</c> to disable proper close.
        /// </summary>
        public readonly WebSocketCloseStatus SendCloseFrame;

        /// <summary>
        /// <c>true</c> if pong frames should not be forwarded
        /// </summary>
        public readonly bool DropPongFrames;

        /// <summary>
        /// Frames decoder configuration.
        /// </summary>
        public readonly WebSocketDecoderConfig DecoderConfig;

        public override string ToString()
        {
            return "WebSocketServerProtocolConfig" +
                " {websocketPath=" + WebsocketPath +
                ", subprotocols=" + Subprotocols +
                ", checkStartsWith=" + CheckStartsWith +
                ", handshakeTimeoutMillis=" + HandshakeTimeoutMillis +
                ", forceCloseTimeoutMillis=" + ForceCloseTimeoutMillis +
                ", handleCloseFrames=" + HandleCloseFrames +
                ", sendCloseFrame=" + SendCloseFrame +
                ", dropPongFrames=" + DropPongFrames +
                ", decoderConfig=" + DecoderConfig +
                "}";
        }

        public Builder ToBuilder()
        {
            return new Builder(this);
        }

        public static Builder NewBuilder()
        {
            return new Builder(Default);
        }

        public sealed class Builder
        {
            private string _websocketPath;
            private string _subprotocols;
            private bool _checkStartsWith;
            private long _handshakeTimeoutMillis;
            private long _forceCloseTimeoutMillis;
            private bool _handleCloseFrames;
            private WebSocketCloseStatus _sendCloseFrame;
            private bool _dropPongFrames;
            private WebSocketDecoderConfig _decoderConfig;
            private WebSocketDecoderConfig.Builder _decoderConfigBuilder;

            internal Builder(WebSocketServerProtocolConfig serverConfig)
            {
                if (serverConfig is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.serverConfig); }

                _websocketPath = serverConfig.WebsocketPath;
                _subprotocols = serverConfig.Subprotocols;
                _checkStartsWith = serverConfig.CheckStartsWith;
                _handshakeTimeoutMillis = serverConfig.HandshakeTimeoutMillis;
                _forceCloseTimeoutMillis = serverConfig.ForceCloseTimeoutMillis;
                _handleCloseFrames = serverConfig.HandleCloseFrames;
                _sendCloseFrame = serverConfig.SendCloseFrame;
                _dropPongFrames = serverConfig.DropPongFrames;
                _decoderConfig = serverConfig.DecoderConfig;
            }

            /// <summary>
            /// URI path component to handle websocket upgrade requests on.
            /// </summary>
            public Builder WebsocketPath(String websocketPath)
            {
                this._websocketPath = websocketPath;
                return this;
            }

            /// <summary>
            /// CSV of supported protocols
            /// </summary>
            public Builder Subprotocols(String subprotocols)
            {
                this._subprotocols = subprotocols;
                return this;
            }

            /// <summary>
            /// <c>true</c> to handle all requests, where URI path component starts from
            /// <see cref="WebSocketServerProtocolConfig.WebsocketPath"/>, <c>false</c> for exact match (default).
            /// </summary>
            public Builder CheckStartsWith(bool checkStartsWith)
            {
                this._checkStartsWith = checkStartsWith;
                return this;
            }

            /// <summary>
            /// Handshake timeout in mills, when handshake timeout, will trigger user
            /// event <see cref="WebSocketServerProtocolHandler.ServerHandshakeStateEvent.HandshakeTimeout"/>
            /// </summary>
            public Builder HandshakeTimeoutMillis(long handshakeTimeoutMillis)
            {
                this._handshakeTimeoutMillis = handshakeTimeoutMillis;
                return this;
            }

            /// <summary>
            /// Close the connection if it was not closed by the client after timeout specified
            /// </summary>
            public Builder ForceCloseTimeoutMillis(long forceCloseTimeoutMillis)
            {
                this._forceCloseTimeoutMillis = forceCloseTimeoutMillis;
                return this;
            }

            /// <summary>
            /// <c>true</c> if close frames should not be forwarded and just close the channel
            /// </summary>
            public Builder HandleCloseFrames(bool handleCloseFrames)
            {
                this._handleCloseFrames = handleCloseFrames;
                return this;
            }

            /// <summary>
            /// Close frame to send, when close frame was not send manually. Or <c>null</c> to disable proper close.
            /// </summary>
            public Builder SendCloseFrame(WebSocketCloseStatus sendCloseFrame)
            {
                this._sendCloseFrame = sendCloseFrame;
                return this;
            }

            /// <summary>
            /// <c>true</c> if pong frames should not be forwarded
            /// </summary>
            public Builder DropPongFrames(bool dropPongFrames)
            {
                this._dropPongFrames = dropPongFrames;
                return this;
            }

            /// <summary>
            /// Frames decoder configuration.
            /// </summary>
            public Builder DecoderConfig(WebSocketDecoderConfig decoderConfig)
            {
                this._decoderConfig = decoderConfig ?? WebSocketDecoderConfig.Default;
                this._decoderConfigBuilder = null;
                return this;
            }

            private WebSocketDecoderConfig.Builder DecoderConfigBuilder()
            {
                if (_decoderConfigBuilder is null)
                {
                    _decoderConfigBuilder = _decoderConfig.ToBuilder();
                }
                return _decoderConfigBuilder;
            }

            /// <summary>
            /// Maximum length of a frame's payload. Setting this to an appropriate value for you application
            /// helps check for denial of services attacks.
            /// </summary>
            public Builder MaxFramePayloadLength(int maxFramePayloadLength)
            {
                DecoderConfigBuilder().MaxFramePayloadLength(maxFramePayloadLength);
                return this;
            }

            /// <summary>
            /// Web socket servers must set this to true processed incoming masked payload. Client implementations
            /// must set this to false.
            /// </summary>
            public Builder ExpectMaskedFrames(bool expectMaskedFrames)
            {
                DecoderConfigBuilder().ExpectMaskedFrames(expectMaskedFrames);
                return this;
            }

            /// <summary>
            /// Allows to loosen the masking requirement on received frames. When this is set to false then also
            /// frames which are not masked properly according to the standard will still be accepted.
            /// </summary>
            public Builder AllowMaskMismatch(bool allowMaskMismatch)
            {
                DecoderConfigBuilder().AllowMaskMismatch(allowMaskMismatch);
                return this;
            }

            /// <summary>
            /// Flag to allow reserved extension bits to be used or not
            /// </summary>
            public Builder AllowExtensions(bool allowExtensions)
            {
                DecoderConfigBuilder().AllowExtensions(allowExtensions);
                return this;
            }

            /// <summary>
            /// Flag to send close frame immediately on any protocol violation.ion.
            /// </summary>
            public Builder CloseOnProtocolViolation(bool closeOnProtocolViolation)
            {
                DecoderConfigBuilder().CloseOnProtocolViolation(closeOnProtocolViolation);
                return this;
            }

            /// <summary>
            /// Allows you to avoid adding of Utf8FrameValidator to the pipeline on the
            /// <see cref="WebSocketServerProtocolHandler"/> creation. This is useful (less overhead)
            /// when you use only BinaryWebSocketFrame within your web socket connection.
            /// </summary>
            public Builder WithUTF8Validator(bool withUTF8Validator)
            {
                DecoderConfigBuilder().WithUTF8Validator(withUTF8Validator);
                return this;
            }

            /// <summary>
            /// Build unmodifiable server protocol configuration.
            /// </summary>
            public WebSocketServerProtocolConfig Build()
            {
                return new WebSocketServerProtocolConfig(
                    _websocketPath,
                    _subprotocols,
                    _checkStartsWith,
                    _handshakeTimeoutMillis,
                    _forceCloseTimeoutMillis,
                    _handleCloseFrames,
                    _sendCloseFrame,
                    _dropPongFrames,
                    _decoderConfigBuilder is null ? _decoderConfig : _decoderConfigBuilder.Build()
                );
            }
        }
    }
}
