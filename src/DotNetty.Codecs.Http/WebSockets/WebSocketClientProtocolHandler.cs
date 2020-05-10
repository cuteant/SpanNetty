// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
// ReSharper disable once ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoPropertyWhenPossible
namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Transport.Channels;

    public class WebSocketClientProtocolHandler : WebSocketProtocolHandler
    {
        readonly WebSocketClientHandshaker handshaker;
        readonly bool handleCloseFrames;
        readonly bool enableUtf8Validator;

        public WebSocketClientHandshaker Handshaker => this.handshaker;

        /// <summary>
        /// Events that are fired to notify about handshake status
        /// </summary>
        public enum ClientHandshakeStateEvent
        {
            /// <summary>
            /// The Handshake was started but the server did not response yet to the request
            /// </summary>
            HandshakeIssued,

            /// <summary>
            /// The Handshake was complete succesful and so the channel was upgraded to websockets
            /// </summary>
            HandshakeComplete
        }

        public WebSocketClientProtocolHandler(Uri webSocketUrl, WebSocketVersion version, string subprotocol, bool allowExtensions,
            HttpHeaders customHeaders, int maxFramePayloadLength)
            : this(webSocketUrl, version, subprotocol, allowExtensions, customHeaders, maxFramePayloadLength, true)
        {
        }

        public WebSocketClientProtocolHandler(Uri webSocketUrl, WebSocketVersion version, string subprotocol, bool allowExtensions,
            HttpHeaders customHeaders, int maxFramePayloadLength, bool handleCloseFrames)
            : this(webSocketUrl, version, subprotocol, allowExtensions, customHeaders, maxFramePayloadLength,
                handleCloseFrames, true, false, false)
        {
        }

        public WebSocketClientProtocolHandler(Uri webSocketUrl, WebSocketVersion version, string subprotocol, bool allowExtensions,
            HttpHeaders customHeaders, int maxFramePayloadLength, bool handleCloseFrames, bool performMasking, bool allowMaskMismatch, bool enableUtf8Validator)
            : this(WebSocketClientHandshakerFactory.NewHandshaker(webSocketUrl, version, subprotocol,
                allowExtensions, customHeaders, maxFramePayloadLength,
                performMasking, allowMaskMismatch), handleCloseFrames, true, enableUtf8Validator)
        {
        }

        public WebSocketClientProtocolHandler(WebSocketClientHandshaker handshaker)
            : this(handshaker, true)
        {
        }

        public WebSocketClientProtocolHandler(WebSocketClientHandshaker handshaker, bool handleCloseFrames)
            : this(handshaker, handleCloseFrames, true, true)
        {
        }

        public WebSocketClientProtocolHandler(WebSocketClientHandshaker handshaker, bool handleCloseFrames, bool dropPongFrames, bool enableUtf8Validator)
            : base(dropPongFrames)
        {
            this.handshaker = handshaker;
            this.handleCloseFrames = handleCloseFrames;
            this.enableUtf8Validator = enableUtf8Validator;
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

                case Opcode.Pong when this.dropPongFrames:
                    // Pong frames need to get ignored
                    return;

                case Opcode.Close when this.handleCloseFrames:
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
                    new WebSocketClientProtocolHandshakeHandler(this.handshaker));
            }
            if (this.enableUtf8Validator && cp.Get<Utf8FrameValidator>() is null)
            {
                // Add the UFT8 checking before this one.
                cp.AddBefore(ctx.Name, nameof(Utf8FrameValidator),
                    new Utf8FrameValidator());
            }
        }
    }
}
