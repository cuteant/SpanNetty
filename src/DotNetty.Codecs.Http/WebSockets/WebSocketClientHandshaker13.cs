// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Text;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    public class WebSocketClientHandshaker13 : WebSocketClientHandshaker
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<WebSocketClientHandshaker13>();

        public const string MagicGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        AsciiString expectedChallengeResponseString;

        readonly bool allowExtensions;
        readonly bool performMasking;
        readonly bool allowMaskMismatch;

        public WebSocketClientHandshaker13(Uri webSocketUrl, WebSocketVersion version, string subprotocol,
            bool allowExtensions, HttpHeaders customHeaders, int maxFramePayloadLength)
            : this(webSocketUrl, version, subprotocol, allowExtensions, customHeaders, maxFramePayloadLength, true, false)
        {
        }

        public WebSocketClientHandshaker13(Uri webSocketUrl, WebSocketVersion version, string subprotocol,
            bool allowExtensions, HttpHeaders customHeaders, int maxFramePayloadLength,
            bool performMasking, bool allowMaskMismatch)
            : base(webSocketUrl, version, subprotocol, customHeaders, maxFramePayloadLength)
        {
            
            this.allowExtensions = allowExtensions;
            this.performMasking = performMasking;
            this.allowMaskMismatch = allowMaskMismatch;
        }

        protected internal override IFullHttpRequest NewHandshakeRequest()
        {
            // Get path
            Uri wsUrl = this.Uri;
            string path = RawPath(wsUrl);

            // Get 16 bit nonce and base 64 encode it
            byte[] nonce = WebSocketUtil.RandomBytes(16);
            string key = WebSocketUtil.Base64String(nonce);

            string acceptSeed = key + MagicGuid;
            byte[] sha1 = WebSocketUtil.Sha1(Encoding.ASCII.GetBytes(acceptSeed));
            this.expectedChallengeResponseString = new AsciiString(WebSocketUtil.Base64String(sha1));

            if (Logger.DebugEnabled)
            {
                Logger.WebSocketVersion13ClientHandshakeKey(key, this.expectedChallengeResponseString);
            }

            // Format request
            var request = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, path);
            HttpHeaders headers = request.Headers;

            if (this.CustomHeaders != null)
            {
                headers.Add(this.CustomHeaders);
            }

            headers.Set(HttpHeaderNames.Upgrade, HttpHeaderValues.Websocket)
                .Set(HttpHeaderNames.Connection, HttpHeaderValues.Upgrade)
                .Set(HttpHeaderNames.SecWebsocketKey, key)
                .Set(HttpHeaderNames.Host, WebsocketHostValue(wsUrl))
                .Set(HttpHeaderNames.SecWebsocketOrigin, WebsocketOriginValue(wsUrl));

            string expectedSubprotocol = this.ExpectedSubprotocol;
            if (!string.IsNullOrEmpty(expectedSubprotocol))
            {
                headers.Set(HttpHeaderNames.SecWebsocketProtocol, expectedSubprotocol);
            }

            headers.Set(HttpHeaderNames.SecWebsocketVersion, "13");

            return request;
        }

        protected override void Verify(IFullHttpResponse response)
        {
            HttpResponseStatus status = HttpResponseStatus.SwitchingProtocols;
            HttpHeaders headers = response.Headers;

            if (!response.Status.Equals(status))
            {
                ThrowHelper.ThrowWebSocketHandshakeException_InvalidHandshakeResponseGS(response);
            }

            if (!headers.TryGet(HttpHeaderNames.Upgrade, out ICharSequence upgrade) 
                || !HttpHeaderValues.Websocket.ContentEqualsIgnoreCase(upgrade))
            {
                ThrowHelper.ThrowWebSocketHandshakeException_InvalidHandshakeResponseU(upgrade);
            }

            if (!headers.ContainsValue(HttpHeaderNames.Connection, HttpHeaderValues.Upgrade, true))
            {
                headers.TryGet(HttpHeaderNames.Connection, out upgrade);
                ThrowHelper.ThrowWebSocketHandshakeException_InvalidHandshakeResponseConn(upgrade);
            }

            if (!headers.TryGet(HttpHeaderNames.SecWebsocketAccept, out ICharSequence accept) 
                || !accept.Equals(this.expectedChallengeResponseString))
            {
                ThrowHelper.ThrowWebSocketHandshakeException_InvalidChallenge(accept, this.expectedChallengeResponseString);
            }
        }

        protected internal override IWebSocketFrameDecoder NewWebSocketDecoder() => new WebSocket13FrameDecoder(
            false, this.allowExtensions, this.MaxFramePayloadLength, this.allowMaskMismatch);

        protected internal override IWebSocketFrameEncoder NewWebSocketEncoder() => new WebSocket13FrameEncoder(this.performMasking);
    }
}
