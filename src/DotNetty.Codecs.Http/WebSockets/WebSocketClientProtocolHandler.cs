// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// This handler does all the heavy lifting for you to run a websocket client.
    /// <para>
    /// It takes care of websocket handshaking as well as processing of Ping, Pong frames. Text and Binary
    /// data frames are passed to the next handler in the pipeline (implemented by you) for processing.
    /// Also the close frame is passed to the next handler as you may want inspect it before close the connection if
    /// the <see cref="_handleCloseFrames"/> is <c>false</c>, default is <c>true</c>.
    /// </para>
    /// <para>
    /// This implementation will establish the websocket connection once the connection to the remote server was complete.
    /// </para>
    /// <para>
    /// To know once a handshake was done you can intercept the
    /// <see cref="IChannelHandler.UserEventTriggered(IChannelHandlerContext, object)"/> and check if the event was of type
    /// <see cref="ClientHandshakeStateEvent.HandshakeIssued"/> or <see cref="ClientHandshakeStateEvent.HandshakeComplete"/>
    /// </para>
    /// </summary>
    public class WebSocketClientProtocolHandler : WebSocketProtocolHandler
    {
        private const long DefaultHandshakeTimeoutMs = 10000L;

        private readonly WebSocketClientHandshaker _handshaker;
        private readonly bool _handleCloseFrames;
        private readonly bool _enableUtf8Validator;
        private readonly long _handshakeTimeoutMillis;

        /// <summary>
        /// Returns the used handshaker
        /// </summary>
        public WebSocketClientHandshaker Handshaker => _handshaker;

        /// <summary>
        /// Events that are fired to notify about handshake status
        /// </summary>
        public enum ClientHandshakeStateEvent
        {
            /// <summary>
            /// The Handshake was timed out
            /// </summary>
            HandshakeTimeout,

            /// <summary>
            /// The Handshake was started but the server did not response yet to the request
            /// </summary>
            HandshakeIssued,

            /// <summary>
            /// The Handshake was complete succesful and so the channel was upgraded to websockets
            /// </summary>
            HandshakeComplete
        }

        /// <summary>Base constructor</summary>
        /// <param name="webSocketUrl">URL for web socket communications. e.g "ws://myhost.com/mypath". Subsequent web socket frames will be
        /// sent to this URL.</param>
        /// <param name="version">Version of web socket specification to use to connect to the server</param>
        /// <param name="subprotocol"></param>
        /// <param name="allowExtensions">Sub protocol request sent to the server.</param>
        /// <param name="customHeaders">Map of custom headers to add to the client request</param>
        /// <param name="maxFramePayloadLength">Maximum length of a frame's payload</param>
        /// <param name="enableUtf8Validator"></param>
        public WebSocketClientProtocolHandler(
            Uri webSocketUrl, WebSocketVersion version, String subprotocol,
            bool allowExtensions, HttpHeaders customHeaders, int maxFramePayloadLength,
            bool enableUtf8Validator = true)
            : this(webSocketUrl, version, subprotocol,
                 allowExtensions, customHeaders, maxFramePayloadLength, DefaultHandshakeTimeoutMs, enableUtf8Validator)
        {
        }

        /// <summary>Base constructor</summary>
        /// <param name="webSocketUrl">URL for web socket communications. e.g "ws://myhost.com/mypath". Subsequent web socket frames will be
        /// sent to this URL.</param>
        /// <param name="version">Version of web socket specification to use to connect to the server</param>
        /// <param name="subprotocol"></param>
        /// <param name="allowExtensions">Sub protocol request sent to the server.</param>
        /// <param name="customHeaders">Map of custom headers to add to the client request</param>
        /// <param name="maxFramePayloadLength">Maximum length of a frame's payload</param>
        /// <param name="handshakeTimeoutMillis">Handshake timeout in mills, when handshake timeout, will trigger user
        /// event <see cref="ClientHandshakeStateEvent.HandshakeTimeout"/></param>
        /// <param name="enableUtf8Validator"></param>
        public WebSocketClientProtocolHandler(
            Uri webSocketUrl, WebSocketVersion version, string subprotocol,
            bool allowExtensions, HttpHeaders customHeaders, int maxFramePayloadLength,
            long handshakeTimeoutMillis, bool enableUtf8Validator = true)
            : this(webSocketUrl, version, subprotocol,
                 allowExtensions, customHeaders, maxFramePayloadLength, true, handshakeTimeoutMillis, enableUtf8Validator)
        {
        }

        /// <summary>Base constructor</summary>
        /// <param name="webSocketUrl">URL for web socket communications. e.g "ws://myhost.com/mypath". Subsequent web socket frames will be
        /// sent to this URL.</param>
        /// <param name="version">Version of web socket specification to use to connect to the server</param>
        /// <param name="subprotocol"></param>
        /// <param name="allowExtensions">Sub protocol request sent to the server.</param>
        /// <param name="customHeaders">Map of custom headers to add to the client request</param>
        /// <param name="maxFramePayloadLength">Maximum length of a frame's payload</param>
        /// <param name="handleCloseFrames"><c>true</c> if close frames should not be forwarded and just close the channel</param>
        /// <param name="enableUtf8Validator"></param>
        public WebSocketClientProtocolHandler(
            Uri webSocketUrl, WebSocketVersion version, string subprotocol,
            bool allowExtensions, HttpHeaders customHeaders, int maxFramePayloadLength,
            bool handleCloseFrames, bool enableUtf8Validator = true)
            : this(webSocketUrl, version, subprotocol, allowExtensions, customHeaders, maxFramePayloadLength,
                 handleCloseFrames, DefaultHandshakeTimeoutMs, enableUtf8Validator)
        {
        }

        /// <summary>Base constructor</summary>
        /// <param name="webSocketUrl">URL for web socket communications. e.g "ws://myhost.com/mypath". Subsequent web socket frames will be
        /// sent to this URL.</param>
        /// <param name="version">Version of web socket specification to use to connect to the server</param>
        /// <param name="subprotocol"></param>
        /// <param name="allowExtensions">Sub protocol request sent to the server.</param>
        /// <param name="customHeaders">Map of custom headers to add to the client request</param>
        /// <param name="maxFramePayloadLength">Maximum length of a frame's payload</param>
        /// <param name="handleCloseFrames"><c>true</c> if close frames should not be forwarded and just close the channel</param>
        /// <param name="handshakeTimeoutMillis">Handshake timeout in mills, when handshake timeout, will trigger user
        /// event <see cref="ClientHandshakeStateEvent.HandshakeTimeout"/></param>
        /// <param name="enableUtf8Validator"></param>
        public WebSocketClientProtocolHandler(
            Uri webSocketUrl, WebSocketVersion version, string subprotocol,
            bool allowExtensions, HttpHeaders customHeaders, int maxFramePayloadLength,
            bool handleCloseFrames, long handshakeTimeoutMillis, bool enableUtf8Validator = true)
            : this(webSocketUrl, version, subprotocol, allowExtensions, customHeaders, maxFramePayloadLength,
                 handleCloseFrames, true, false, handshakeTimeoutMillis, enableUtf8Validator)
        {
        }

        /// <summary>Base constructor</summary>
        /// <param name="webSocketUrl">URL for web socket communications. e.g "ws://myhost.com/mypath". Subsequent web socket frames will be
        /// sent to this URL.</param>
        /// <param name="version">Version of web socket specification to use to connect to the server</param>
        /// <param name="subprotocol"></param>
        /// <param name="allowExtensions">Sub protocol request sent to the server.</param>
        /// <param name="customHeaders">Map of custom headers to add to the client request</param>
        /// <param name="maxFramePayloadLength">Maximum length of a frame's payload</param>
        /// <param name="handleCloseFrames"><c>true</c> if close frames should not be forwarded and just close the channel</param>
        /// <param name="performMasking">Whether to mask all written websocket frames. This must be set to true in order to be fully compatible
        /// with the websocket specifications. Client applications that communicate with a non-standard server
        /// which doesn't require masking might set this to false to achieve a higher performance.</param>
        /// <param name="allowMaskMismatch">When set to true, frames which are not masked properly according to the standard will still be
        /// accepted.</param>
        /// <param name="enableUtf8Validator"></param>
        public WebSocketClientProtocolHandler(
            Uri webSocketUrl, WebSocketVersion version, string subprotocol,
            bool allowExtensions, HttpHeaders customHeaders, int maxFramePayloadLength,
            bool handleCloseFrames, bool performMasking, bool allowMaskMismatch,
            bool enableUtf8Validator = true)
            : this(webSocketUrl, version, subprotocol, allowExtensions, customHeaders,
                 maxFramePayloadLength, handleCloseFrames, performMasking, allowMaskMismatch,
                 DefaultHandshakeTimeoutMs, enableUtf8Validator)
        {
        }

        /// <summary>Base constructor</summary>
        /// <param name="webSocketUrl">URL for web socket communications. e.g "ws://myhost.com/mypath". Subsequent web socket frames will be
        /// sent to this URL.</param>
        /// <param name="version">Version of web socket specification to use to connect to the server</param>
        /// <param name="subprotocol"></param>
        /// <param name="allowExtensions">Sub protocol request sent to the server.</param>
        /// <param name="customHeaders">Map of custom headers to add to the client request</param>
        /// <param name="maxFramePayloadLength">Maximum length of a frame's payload</param>
        /// <param name="handleCloseFrames"><c>true</c> if close frames should not be forwarded and just close the channel</param>
        /// <param name="performMasking">Whether to mask all written websocket frames. This must be set to true in order to be fully compatible
        /// with the websocket specifications. Client applications that communicate with a non-standard server
        /// which doesn't require masking might set this to false to achieve a higher performance.</param>
        /// <param name="allowMaskMismatch">When set to true, frames which are not masked properly according to the standard will still be
        /// accepted.</param>
        /// <param name="handshakeTimeoutMillis">Handshake timeout in mills, when handshake timeout, will trigger user
        /// event <see cref="ClientHandshakeStateEvent.HandshakeTimeout"/></param>
        /// <param name="enableUtf8Validator"></param>
        public WebSocketClientProtocolHandler(
            Uri webSocketUrl, WebSocketVersion version, string subprotocol,
            bool allowExtensions, HttpHeaders customHeaders, int maxFramePayloadLength,
            bool handleCloseFrames, bool performMasking, bool allowMaskMismatch,
            long handshakeTimeoutMillis, bool enableUtf8Validator = true)
            : this(WebSocketClientHandshakerFactory.NewHandshaker(webSocketUrl, version, subprotocol,
                allowExtensions, customHeaders, maxFramePayloadLength,
                performMasking, allowMaskMismatch), handleCloseFrames, true, handshakeTimeoutMillis, enableUtf8Validator)
        {
        }

        /// <summary>Base constructor</summary>
        /// <param name="handshaker">The <see cref="WebSocketClientHandshaker"/> which will be used to issue the handshake once the connection
        /// was established to the remote peer.</param>
        /// <param name="enableUtf8Validator"></param>
        public WebSocketClientProtocolHandler(WebSocketClientHandshaker handshaker, bool enableUtf8Validator = true)
            : this(handshaker, true, true, DefaultHandshakeTimeoutMs, enableUtf8Validator)
        {
        }

        /// <summary>Base constructor</summary>
        /// <param name="handshaker">The <see cref="WebSocketClientHandshaker"/> which will be used to issue the handshake once the connection
        /// was established to the remote peer.</param>
        /// <param name="handshakeTimeoutMillis">Handshake timeout in mills, when handshake timeout, will trigger user
        /// event <see cref="ClientHandshakeStateEvent.HandshakeTimeout"/></param>
        /// <param name="enableUtf8Validator"></param>
        public WebSocketClientProtocolHandler(WebSocketClientHandshaker handshaker,
            long handshakeTimeoutMillis, bool enableUtf8Validator = true)
            : this(handshaker, true, true, handshakeTimeoutMillis, enableUtf8Validator)
        {
        }

        /// <summary>Base constructor</summary>
        /// <param name="handshaker">The <see cref="WebSocketClientHandshaker"/> which will be used to issue the handshake once the connection
        /// was established to the remote peer.</param>
        /// <param name="handleCloseFrames"><c>true</c> if close frames should not be forwarded and just close the channel</param>
        /// <param name="enableUtf8Validator"></param>
        public WebSocketClientProtocolHandler(WebSocketClientHandshaker handshaker,
            bool handleCloseFrames, bool enableUtf8Validator = true)
            : this(handshaker, handleCloseFrames, true, DefaultHandshakeTimeoutMs, enableUtf8Validator)
        {
        }

        /// <summary>Base constructor</summary>
        /// <param name="handshaker">The <see cref="WebSocketClientHandshaker"/> which will be used to issue the handshake once the connection
        /// was established to the remote peer.</param>
        /// <param name="handleCloseFrames"><c>true</c> if close frames should not be forwarded and just close the channel</param>
        /// <param name="handshakeTimeoutMillis">Handshake timeout in mills, when handshake timeout, will trigger user
        /// event <see cref="ClientHandshakeStateEvent.HandshakeTimeout"/></param>
        /// <param name="enableUtf8Validator"></param>
        public WebSocketClientProtocolHandler(WebSocketClientHandshaker handshaker,
            bool handleCloseFrames, long handshakeTimeoutMillis, bool enableUtf8Validator = true)
            : this(handshaker, handleCloseFrames, true, handshakeTimeoutMillis, enableUtf8Validator)
        {
        }

        /// <summary>Base constructor</summary>
        /// <param name="handshaker">The <see cref="WebSocketClientHandshaker"/> which will be used to issue the handshake once the connection
        /// was established to the remote peer.</param>
        /// <param name="handleCloseFrames"><c>true</c> if close frames should not be forwarded and just close the channel</param>
        /// <param name="dropPongFrames"><c>true</c> if pong frames should not be forwarded</param>
        /// <param name="enableUtf8Validator"></param>
        public WebSocketClientProtocolHandler(WebSocketClientHandshaker handshaker,
            bool handleCloseFrames, bool dropPongFrames, bool enableUtf8Validator = true)
            : this(handshaker, handleCloseFrames, dropPongFrames, DefaultHandshakeTimeoutMs, enableUtf8Validator)
        {
        }

        /// <summary>Base constructor</summary>
        /// <param name="handshaker">The <see cref="WebSocketClientHandshaker"/> which will be used to issue the handshake once the connection
        /// was established to the remote peer.</param>
        /// <param name="handleCloseFrames"><c>true</c> if close frames should not be forwarded and just close the channel</param>
        /// <param name="dropPongFrames"><c>true</c> if pong frames should not be forwarded</param>
        /// <param name="handshakeTimeoutMillis">Handshake timeout in mills, when handshake timeout, will trigger user
        /// event <see cref="ClientHandshakeStateEvent.HandshakeTimeout"/></param>
        /// <param name="enableUtf8Validator"></param>
        public WebSocketClientProtocolHandler(WebSocketClientHandshaker handshaker,
            bool handleCloseFrames, bool dropPongFrames, long handshakeTimeoutMillis, bool enableUtf8Validator = true)
            : base(dropPongFrames)
        {
            if (handshakeTimeoutMillis <= 0L) { ThrowHelper.ThrowArgumentException_Positive(handshakeTimeoutMillis, ExceptionArgument.handshakeTimeoutMillis); }

            _handshaker = handshaker;
            _handleCloseFrames = handleCloseFrames;
            _handshakeTimeoutMillis = handshakeTimeoutMillis;
            _enableUtf8Validator = enableUtf8Validator;
        }

        protected override void Decode(IChannelHandlerContext ctx, WebSocketFrame frame, List<object> output)
        {
            switch (frame.Opcode)
            {
                case Opcode.Ping:
                    var contect = frame.Content;
                    contect.Retain();
                    ctx.Channel.WriteAndFlushAsync(new PongWebSocketFrame(contect));
                    return;

                case Opcode.Pong when DropPongFrames:
                    // Pong frames need to get ignored
                    return;

                case Opcode.Close when _handleCloseFrames:
                    ctx.CloseAsync();
                    return;

                default:
                    output.Add(frame.Retain());
                    break;
            }
        }

        public override void HandlerAdded(IChannelHandlerContext ctx)
        {
            var cp = ctx.Pipeline;
            if (cp.Get<WebSocketClientProtocolHandshakeHandler>() is null)
            {
                // Add the WebSocketClientProtocolHandshakeHandler before this one.
                cp.AddBefore(ctx.Name, nameof(WebSocketClientProtocolHandshakeHandler),
                    new WebSocketClientProtocolHandshakeHandler(_handshaker, _handshakeTimeoutMillis));
            }
            if (_enableUtf8Validator && cp.Get<Utf8FrameValidator>() is null)
            {
                // Add the UFT8 checking before this one.
                cp.AddBefore(ctx.Name, nameof(Utf8FrameValidator),
                    new Utf8FrameValidator());
            }
        }
    }
}
