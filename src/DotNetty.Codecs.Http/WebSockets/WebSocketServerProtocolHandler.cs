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

    public class WebSocketServerProtocolHandler : WebSocketProtocolHandler
    {
        public sealed class HandshakeComplete
        {
            readonly string requestUri;
            readonly HttpHeaders requestHeaders;
            readonly string selectedSubprotocol;

            internal HandshakeComplete(string requestUri, HttpHeaders requestHeaders, string selectedSubprotocol)
            {
                this.requestUri = requestUri;
                this.requestHeaders = requestHeaders;
                this.selectedSubprotocol = selectedSubprotocol;
            }

            public string RequestUri => this.requestUri;

            public HttpHeaders RequestHeaders => this.requestHeaders;

            public string SelectedSubprotocol => this.selectedSubprotocol;
        }

        static readonly AttributeKey<WebSocketServerHandshaker> HandshakerAttrKey = 
            AttributeKey<WebSocketServerHandshaker>.ValueOf("HANDSHAKER");

        readonly string websocketPath;
        readonly string subprotocols;
        readonly bool allowExtensions;
        readonly int maxFramePayloadLength;
        readonly bool allowMaskMismatch;
        readonly bool checkStartsWith;
        readonly bool enableUtf8Validator;

        public WebSocketServerProtocolHandler(string websocketPath)
            : this(websocketPath, null, false)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, bool checkStartsWith)
            : this(websocketPath, null, false, 65536, false, checkStartsWith)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols)
            : this(websocketPath, subprotocols, false)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols, bool allowExtensions)
            : this(websocketPath, subprotocols, allowExtensions, 65536)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols,
            bool allowExtensions, int maxFrameSize)
            : this(websocketPath, subprotocols, allowExtensions, maxFrameSize, false)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols,
            bool allowExtensions, int maxFrameSize, bool allowMaskMismatch)
            : this(websocketPath, subprotocols, allowExtensions, maxFrameSize, allowMaskMismatch, false)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols, bool allowExtensions,
            int maxFrameSize, bool allowMaskMismatch, bool checkStartsWith)
            : this(websocketPath, subprotocols, allowExtensions, maxFrameSize, allowMaskMismatch, checkStartsWith, true, false)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols, bool allowExtensions, 
            int maxFrameSize, bool allowMaskMismatch, bool checkStartsWith, bool dropPongFrames, bool enableUtf8Validator)
            : base(dropPongFrames)
        {
            this.websocketPath = websocketPath;
            this.subprotocols = subprotocols;
            this.allowExtensions = allowExtensions;
            this.maxFramePayloadLength = maxFrameSize;
            this.allowMaskMismatch = allowMaskMismatch;
            this.checkStartsWith = checkStartsWith;
            this.enableUtf8Validator = enableUtf8Validator;
        }

        public override void HandlerAdded(IChannelHandlerContext ctx)
        {
            IChannelPipeline cp = ctx.Pipeline;
            if (cp.Get<WebSocketServerProtocolHandshakeHandler>() == null)
            {
                // Add the WebSocketHandshakeHandler before this one.
                cp.AddBefore(ctx.Name, nameof(WebSocketServerProtocolHandshakeHandler),
                    new WebSocketServerProtocolHandshakeHandler(
                        this.websocketPath, 
                        this.subprotocols,
                        this.allowExtensions,
                        this.maxFramePayloadLength,
                        this.allowMaskMismatch,
                        this.checkStartsWith));
            }

            if (this.enableUtf8Validator && cp.Get<Utf8FrameValidator>() == null)
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

                case Opcode.Pong when this.dropPongFrames:
                    // Pong frames need to get ignored
                    return;

                case Opcode.Close:
                    WebSocketServerHandshaker handshaker = GetHandshaker(ctx.Channel);
                    if (handshaker != null)
                    {
                        frame.Retain();
                        handshaker.CloseAsync(ctx.Channel, (CloseWebSocketFrame)frame);
                    }
                    else
                    {
#if NET40
                        void closeOnComplete(Task t) => ctx.CloseAsync();
                        ctx.WriteAndFlushAsync(Unpooled.Empty)
                            .ContinueWith(closeOnComplete, TaskContinuationOptions.ExecuteSynchronously);
#else
                        ctx.WriteAndFlushAsync(Unpooled.Empty)
                            .ContinueWith(CloseOnComplete, ctx, TaskContinuationOptions.ExecuteSynchronously);
#endif
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
#if NET40
                void closeOnComplete(Task t) => ctx.CloseAsync();
                ctx.Channel.WriteAndFlushAsync(response)
                    .ContinueWith(closeOnComplete, TaskContinuationOptions.ExecuteSynchronously);
#else
                ctx.Channel.WriteAndFlushAsync(response)
                    .ContinueWith(CloseOnComplete, ctx, TaskContinuationOptions.ExecuteSynchronously);
#endif
            }
            else
            {
                ctx.FireExceptionCaught(cause);
                ctx.CloseAsync();
            }
        }

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
                    var response = new DefaultFullHttpResponse(Http11, HttpResponseStatus.Forbidden);
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
