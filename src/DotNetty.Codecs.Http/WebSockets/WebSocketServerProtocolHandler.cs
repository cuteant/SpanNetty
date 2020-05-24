// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    using static HttpVersion;

    /// <summary>
    /// This handler does all the heavy lifting for you to run a websocket server.
    ///
    /// It takes care of websocket handshaking as well as processing of control frames (Close, Ping, Pong). Text and Binary
    /// data frames are passed to the next handler in the pipeline (implemented by you) for processing.
    ///
    /// See <tt>io.netty.example.http.websocketx.html5.WebSocketServer</tt> for usage.
    ///
    /// The implementation of this handler assumes that you just want to run  a websocket server and not process other types
    /// HTTP requests (like GET and POST). If you wish to support both HTTP requests and websockets in the one server, refer
    /// to the <tt>io.netty.example.http.websocketx.server.WebSocketServer</tt> example.
    ///
    /// To know once a handshake was done you can intercept the
    /// <see cref="IChannelHandler.UserEventTriggered(IChannelHandlerContext, object)"/> and check if the event was instance
    /// of <see cref="HandshakeComplete"/>, the event will contain extra information about the handshake such as the request and
    /// selected subprotocol.
    /// </summary>
    public class WebSocketServerProtocolHandler : WebSocketProtocolHandler
    {
        /// <summary>
        /// Events that are fired to notify about handshake status
        /// </summary>
        public enum ServerHandshakeStateEvent
        {
            /// <summary>
            /// The Handshake was completed successfully and the channel was upgraded to websockets.
            /// </summary>
            HandshakeComplete,

            /// <summary>
            /// The Handshake was timed out
            /// </summary>
            HandshakeTimeout,
        }

        public sealed class HandshakeComplete
        {
            private readonly string _requestUri;
            private readonly HttpHeaders _requestHeaders;
            private readonly string _selectedSubprotocol;

            internal HandshakeComplete(string requestUri, HttpHeaders requestHeaders, string selectedSubprotocol)
            {
                _requestUri = requestUri;
                _requestHeaders = requestHeaders;
                _selectedSubprotocol = selectedSubprotocol;
            }

            public string RequestUri => _requestUri;

            public HttpHeaders RequestHeaders => _requestHeaders;

            public string SelectedSubprotocol => _selectedSubprotocol;
        }

        private static readonly AttributeKey<WebSocketServerHandshaker> HandshakerAttrKey =
            AttributeKey<WebSocketServerHandshaker>.ValueOf("HANDSHAKER");
        private static readonly long DefaultHandshakeTimeoutMs = 10000L;

        private readonly string _websocketPath;
        private readonly string _subprotocols;
        private readonly bool _checkStartsWith;
        private readonly bool _enableUtf8Validator;
        private readonly long _handshakeTimeoutMillis;
        private readonly WebSocketDecoderConfig _decoderConfig;

        public WebSocketServerProtocolHandler(string websocketPath, bool enableUtf8Validator = true)
            : this(websocketPath, DefaultHandshakeTimeoutMs, enableUtf8Validator)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, long handshakeTimeoutMillis, bool enableUtf8Validator = true)
            : this(websocketPath, null, false, handshakeTimeoutMillis, enableUtf8Validator)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, bool checkStartsWith, bool enableUtf8Validator = true)
            : this(websocketPath, checkStartsWith, DefaultHandshakeTimeoutMs, enableUtf8Validator)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, bool checkStartsWith, long handshakeTimeoutMillis, bool enableUtf8Validator = true)
            : this(websocketPath, null, false, 65536, false, checkStartsWith, handshakeTimeoutMillis, enableUtf8Validator)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols, bool enableUtf8Validator = true)
            : this(websocketPath, subprotocols, DefaultHandshakeTimeoutMs, enableUtf8Validator)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols, long handshakeTimeoutMillis, bool enableUtf8Validator = true)
            : this(websocketPath, subprotocols, false, handshakeTimeoutMillis, enableUtf8Validator)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols, bool allowExtensions, bool enableUtf8Validator = true)
            : this(websocketPath, subprotocols, allowExtensions, DefaultHandshakeTimeoutMs, enableUtf8Validator)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols, bool allowExtensions,
            long handshakeTimeoutMillis, bool enableUtf8Validator = true)
            : this(websocketPath, subprotocols, allowExtensions, 65536, handshakeTimeoutMillis, enableUtf8Validator)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols,
            bool allowExtensions, int maxFrameSize, bool enableUtf8Validator = true)
            : this(websocketPath, subprotocols, allowExtensions, maxFrameSize, DefaultHandshakeTimeoutMs, enableUtf8Validator)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols,
            bool allowExtensions, int maxFrameSize, long handshakeTimeoutMillis, bool enableUtf8Validator = true)
            : this(websocketPath, subprotocols, allowExtensions, maxFrameSize, false, handshakeTimeoutMillis, enableUtf8Validator)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols,
                bool allowExtensions, int maxFrameSize, bool allowMaskMismatch, bool enableUtf8Validator = true)
            : this(websocketPath, subprotocols, allowExtensions, maxFrameSize, allowMaskMismatch,
                 DefaultHandshakeTimeoutMs, enableUtf8Validator)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols, bool allowExtensions,
                                              int maxFrameSize, bool allowMaskMismatch, long handshakeTimeoutMillis,
                                              bool enableUtf8Validator = true)
            : this(websocketPath, subprotocols, allowExtensions, maxFrameSize, allowMaskMismatch, false,
                 handshakeTimeoutMillis, enableUtf8Validator)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols,
                                              bool allowExtensions, int maxFrameSize, bool allowMaskMismatch,
                                              bool checkStartsWith, bool enableUtf8Validator = true)
            : this(websocketPath, subprotocols, allowExtensions, maxFrameSize, allowMaskMismatch, checkStartsWith,
                 DefaultHandshakeTimeoutMs, enableUtf8Validator)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols,
                                              bool allowExtensions, int maxFrameSize, bool allowMaskMismatch,
                                              bool checkStartsWith, long handshakeTimeoutMillis, bool enableUtf8Validator = true)
            : this(websocketPath, subprotocols, allowExtensions, maxFrameSize, allowMaskMismatch, checkStartsWith, true,
                 handshakeTimeoutMillis, enableUtf8Validator)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols,
                                              bool allowExtensions, int maxFrameSize, bool allowMaskMismatch,
                                              bool checkStartsWith, bool dropPongFrames, bool enableUtf8Validator = true)
            : this(websocketPath, subprotocols, allowExtensions, maxFrameSize, allowMaskMismatch, checkStartsWith,
                 dropPongFrames, DefaultHandshakeTimeoutMs, enableUtf8Validator)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols, bool allowExtensions,
                                              int maxFrameSize, bool allowMaskMismatch, bool checkStartsWith,
                                              bool dropPongFrames, long handshakeTimeoutMillis,
                                              bool enableUtf8Validator = true)
            : this(websocketPath, subprotocols, checkStartsWith, dropPongFrames, handshakeTimeoutMillis,
                WebSocketDecoderConfig.NewBuilder()
                    .MaxFramePayloadLength(maxFrameSize)
                    .AllowMaskMismatch(allowMaskMismatch)
                    .AllowExtensions(allowExtensions)
                    .Build(), enableUtf8Validator)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols, bool checkStartsWith,
                                              bool dropPongFrames, long handshakeTimeoutMillis,
                                              WebSocketDecoderConfig decoderConfig, bool enableUtf8Validator = true)
            : base(dropPongFrames)
        {
            if (handshakeTimeoutMillis <= 0L) { ThrowHelper.ThrowArgumentException_Positive(handshakeTimeoutMillis, ExceptionArgument.handshakeTimeoutMillis); }
            if (decoderConfig is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.decoderConfig); }

            _websocketPath = websocketPath;
            _subprotocols = subprotocols;
            _checkStartsWith = checkStartsWith;
            _handshakeTimeoutMillis = handshakeTimeoutMillis;
            _decoderConfig = decoderConfig;
            _enableUtf8Validator = enableUtf8Validator;
        }

        public override void HandlerAdded(IChannelHandlerContext ctx)
        {
            IChannelPipeline cp = ctx.Pipeline;
            if (cp.Get<WebSocketServerProtocolHandshakeHandler>() is null)
            {
                // Add the WebSocketHandshakeHandler before this one.
                cp.AddBefore(ctx.Name, nameof(WebSocketServerProtocolHandshakeHandler),
                    new WebSocketServerProtocolHandshakeHandler(
                        _websocketPath, _subprotocols, _checkStartsWith, _handshakeTimeoutMillis, _decoderConfig));
            }

            if (_enableUtf8Validator && cp.Get<Utf8FrameValidator>() is null)
            {
                // Add the UFT8 checking before this one.
                cp.AddBefore(ctx.Name, nameof(Utf8FrameValidator), new Utf8FrameValidator());
            }
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

                case Opcode.Close:
                    WebSocketServerHandshaker handshaker = GetHandshaker(ctx.Channel);
                    if (handshaker is object)
                    {
                        frame.Retain();
                        handshaker.CloseAsync(ctx.Channel, (CloseWebSocketFrame)frame);
                    }
                    else
                    {
                        ctx.WriteAndFlushAsync(Unpooled.Empty)
                            .ContinueWith(CloseOnCompleteAction, ctx, TaskContinuationOptions.ExecuteSynchronously);
                    }

                    return;

                default:
                    output.Add(frame.Retain());
                    break;
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            if (cause is WebSocketHandshakeException)
            {
                var response = new DefaultFullHttpResponse(Http11, HttpResponseStatus.BadRequest,
                    Unpooled.WrappedBuffer(Encoding.ASCII.GetBytes(cause.Message)));
                ctx.Channel.WriteAndFlushAsync(response)
                    .ContinueWith(CloseOnCompleteAction, ctx, TaskContinuationOptions.ExecuteSynchronously);
            }
            else
            {
                ctx.FireExceptionCaught(cause);
                ctx.CloseAsync();
            }
        }

        static readonly Action<Task, object> CloseOnCompleteAction = CloseOnComplete;
        static void CloseOnComplete(Task t, object c)
        {
            ((IChannelHandlerContext)c).CloseAsync();
        }

        internal static WebSocketServerHandshaker GetHandshaker(IChannel channel) => channel.GetAttribute(HandshakerAttrKey).Get();

        internal static void SetHandshaker(IChannel channel, WebSocketServerHandshaker handshaker) => channel.GetAttribute(HandshakerAttrKey).Set(handshaker);

        internal static IChannelHandler ForbiddenHttpRequestResponder() => new ForbiddenResponseHandler();

        sealed class ForbiddenResponseHandler : ChannelHandlerAdapter
        {
            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                if (msg is IFullHttpRequest request)
                {
                    request.Release();
                    var response = new DefaultFullHttpResponse(Http11, HttpResponseStatus.Forbidden, ctx.Allocator.Buffer(0));
                    ctx.Channel.WriteAndFlushAsync(response);
                }
                else
                {
                    ctx.FireChannelRead(msg);
                }
            }
        }
    }
}
