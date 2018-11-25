// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Codecs.Http.Cors;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public abstract partial class WebSocketServerHandshaker
    {
        protected static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<WebSocketServerHandshaker>();
        static readonly ClosedChannelException ClosedChannelException = new ClosedChannelException();

        readonly string uri;

        readonly string[] subprotocols;

        readonly WebSocketVersion version;

        readonly int maxFramePayloadLength;

        string selectedSubprotocol;

        // Use this as wildcard to support all requested sub-protocols
        public const string SubProtocolWildcard = "*";

        protected WebSocketServerHandshaker(WebSocketVersion version, string uri, string subprotocols, int maxFramePayloadLength)
        {
            this.version = version;
            this.uri = uri;
            if (subprotocols != null)
            {
                string[] subprotocolArray = subprotocols.Split(',');
                for (int i = 0; i < subprotocolArray.Length; i++)
                {
                    subprotocolArray[i] = subprotocolArray[i].Trim();
                }
                this.subprotocols = subprotocolArray;
            }
            else
            {
                this.subprotocols = EmptyArrays.EmptyStrings;
            }
            this.maxFramePayloadLength = maxFramePayloadLength;
        }

        public string Uri => this.uri;

        public ISet<string> Subprotocols()
        {
            var ret = new HashSet<string>(this.subprotocols, StringComparer.Ordinal);
            return ret;
        }

        public WebSocketVersion Version => this.version;

        public int MaxFramePayloadLength => this.maxFramePayloadLength;

        public Task HandshakeAsync(IChannel channel, IFullHttpRequest req) => this.HandshakeAsync(channel, req, null);

        public Task HandshakeAsync(IChannel channel, IFullHttpRequest req, HttpHeaders responseHeaders)
        {
            var completion = channel.NewPromise();
            this.Handshake(channel, req, responseHeaders, completion);
            return completion.Task;
        }

        public void Handshake(IChannel channel, IFullHttpRequest req, HttpHeaders responseHeaders, IPromise completion)
        {
            if (Logger.DebugEnabled)
            {
                Logger.WebSocketVersionServerHandshake(channel, this.version);
            }

            IFullHttpResponse response = this.NewHandshakeResponse(req, responseHeaders);
            IChannelPipeline p = channel.Pipeline;

            if (p.Get<HttpObjectAggregator>() != null) { p.Remove<HttpObjectAggregator>(); }
            if (p.Get<HttpContentCompressor>() != null) { p.Remove<HttpContentCompressor>(); }
            if (p.Get<CorsHandler>() != null) { p.Remove<CorsHandler>(); }
            if (p.Get<HttpServerExpectContinueHandler>() != null) { p.Remove<HttpServerExpectContinueHandler>(); }
            if (p.Get<HttpServerKeepAliveHandler>() != null) { p.Remove<HttpServerKeepAliveHandler>(); }

            IChannelHandlerContext ctx = p.Context<HttpRequestDecoder>();
            string encoderName;
            if (ctx == null)
            {
                // this means the user use a HttpServerCodec
                ctx = p.Context<HttpServerCodec>();
                if (ctx == null)
                {
                    completion.TrySetException(ThrowHelper.GetInvalidOperationException_NoHttpDecoderAndServerCodec());
                    return;
                }

                encoderName = ctx.Name;
                p.AddBefore(encoderName, "wsdecoder", this.NewWebsocketDecoder());
                p.AddBefore(encoderName, "wsencoder", this.NewWebSocketEncoder());
            }
            else
            {
                p.Replace(ctx.Name, "wsdecoder", this.NewWebsocketDecoder());

                encoderName = p.Context<HttpResponseEncoder>().Name;
                p.AddBefore(encoderName, "wsencoder", this.NewWebSocketEncoder());
            }

#if NET40
            void removeHandlerAfterWrite(Task t)
            {
                if (t.Status == TaskStatus.RanToCompletion)
                {
                    p.Remove(encoderName);
                    completion.TryComplete();
                }
                else
                {
                    completion.TrySetException(t.Exception.InnerExceptions);
                }
            }
            channel.WriteAndFlushAsync(response).ContinueWith(removeHandlerAfterWrite, TaskContinuationOptions.ExecuteSynchronously);
#else
            channel.WriteAndFlushAsync(response).ContinueWith(RemoveHandlerAfterWriteAction, Tuple.Create(completion, p, encoderName), TaskContinuationOptions.ExecuteSynchronously);
#endif
        }

        public Task HandshakeAsync(IChannel channel, IHttpRequest req, HttpHeaders responseHeaders)
        {
            if (req is IFullHttpRequest request)
            {
                return this.HandshakeAsync(channel, request, responseHeaders);
            }
            if (Logger.DebugEnabled)
            {
                Logger.WebSocketVersionServerHandshake(channel, this.version);
            }
            IChannelPipeline p = channel.Pipeline;
            IChannelHandlerContext ctx = p.Context<HttpRequestDecoder>();
            if (ctx == null)
            {
                // this means the user use a HttpServerCodec
                ctx = p.Context<HttpServerCodec>();
                if (ctx == null)
                {
                    return ThrowHelper.ThrowInvalidOperationException_NoHttpDecoderAndServerCodec();
                }
            }

            // Add aggregator and ensure we feed the HttpRequest so it is aggregated. A limit o 8192 should be more then
            // enough for the websockets handshake payload.
            //
            // TODO: Make handshake work without HttpObjectAggregator at all.
            string aggregatorName = "httpAggregator";
            p.AddAfter(ctx.Name, aggregatorName, new HttpObjectAggregator(8192));
            var completion = channel.NewPromise();
            p.AddAfter(aggregatorName, "handshaker", new Handshaker(this, channel, responseHeaders, completion));
            try
            {
                ctx.FireChannelRead(ReferenceCountUtil.Retain(req));
            }
            catch (Exception cause)
            {
                completion.TrySetException(cause);
            }
            return completion.Task;
        }

        sealed class Handshaker : SimpleChannelInboundHandler<IFullHttpRequest>
        {
            readonly WebSocketServerHandshaker serverHandshaker;
            readonly IChannel channel;
            readonly HttpHeaders responseHeaders;
            readonly IPromise completion;

            public Handshaker(WebSocketServerHandshaker serverHandshaker, IChannel channel, HttpHeaders responseHeaders, IPromise completion)
            {
                this.serverHandshaker = serverHandshaker;
                this.channel = channel;
                this.responseHeaders = responseHeaders;
                this.completion = completion;
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, IFullHttpRequest msg)
            {
                // Remove ourself and do the actual handshake
                ctx.Pipeline.Remove(this);
                this.serverHandshaker.Handshake(this.channel, msg, this.responseHeaders, this.completion);
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                // Remove ourself and fail the handshake promise.
                ctx.Pipeline.Remove(this);
                this.completion.TrySetException(cause);
                ctx.FireExceptionCaught(cause);
            }

            public override void ChannelInactive(IChannelHandlerContext ctx)
            {
                // Fail promise if Channel was closed
                this.completion.TrySetException(ClosedChannelException);
                ctx.FireChannelInactive();
            }
        }

        protected abstract IFullHttpResponse NewHandshakeResponse(IFullHttpRequest req, HttpHeaders responseHeaders);

        public virtual Task CloseAsync(IChannel channel, CloseWebSocketFrame frame)
        {
            if (null == channel) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.channel); }

#if NET40
            void closeOnComplete(Task t) => channel.CloseAsync();
            return channel.WriteAndFlushAsync(frame).ContinueWith(closeOnComplete, TaskContinuationOptions.ExecuteSynchronously);
#else
            return channel.WriteAndFlushAsync(frame).ContinueWith(CloseOnCompleteAction, channel, TaskContinuationOptions.ExecuteSynchronously);
#endif
        }

        protected string SelectSubprotocol(string requestedSubprotocols)
        {
            if (requestedSubprotocols == null || this.subprotocols.Length == 0)
            {
                return null;
            }

            string[] requestedSubprotocolArray = requestedSubprotocols.Split(',');
            foreach (string p in requestedSubprotocolArray)
            {
                string requestedSubprotocol = p.Trim();

                foreach (string supportedSubprotocol in this.subprotocols)
                {
                    if (string.Equals(SubProtocolWildcard, supportedSubprotocol, StringComparison.Ordinal)
                        || string.Equals(requestedSubprotocol, supportedSubprotocol, StringComparison.Ordinal))
                    {
                        this.selectedSubprotocol = requestedSubprotocol;
                        return requestedSubprotocol;
                    }
                }
            }

            // No match found
            return null;
        }

        public string SelectedSubprotocol => this.selectedSubprotocol;

        protected internal abstract IWebSocketFrameDecoder NewWebsocketDecoder();

        protected internal abstract IWebSocketFrameEncoder NewWebSocketEncoder();
    }
}
