// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Channels;

    using static HttpUtil;
    using static HttpMethod;
    using static HttpVersion;
    using static HttpResponseStatus;

    sealed class WebSocketServerProtocolHandshakeHandler : ChannelHandlerAdapter
    {
        private readonly WebSocketServerProtocolConfig _serverConfig;
        private IChannelHandlerContext _ctx;
        private IPromise _handshakePromise;

        internal WebSocketServerProtocolHandshakeHandler(WebSocketServerProtocolConfig serverConfig)
        {
            if (serverConfig is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.serverConfig); }

            _serverConfig = serverConfig;
        }

        public override void HandlerAdded(IChannelHandlerContext context)
        {
            _ctx = context;
            _handshakePromise = context.NewPromise();
        }

        public override void ChannelRead(IChannelHandlerContext ctx, object msg)
        {
            var req = (IFullHttpRequest)msg;
            if (IsNotWebSocketPath(req))
            {
                ctx.FireChannelRead(msg);
                return;
            }

            try
            {
                if (!Equals(Get, req.Method))
                {
                    SendHttpResponse(ctx, req, new DefaultFullHttpResponse(Http11, Forbidden, ctx.Allocator.Buffer(0)));
                    return;
                }

                var wsFactory = new WebSocketServerHandshakerFactory(
                    GetWebSocketLocation(ctx.Pipeline, req, _serverConfig.WebsocketPath), _serverConfig.Subprotocols, _serverConfig.DecoderConfig);
                WebSocketServerHandshaker handshaker = wsFactory.NewHandshaker(req);
                if (handshaker is null)
                {
                    WebSocketServerHandshakerFactory.SendUnsupportedVersionResponse(ctx.Channel);
                }
                else
                {
                    // Ensure we set the handshaker and replace this handler before we
                    // trigger the actual handshake. Otherwise we may receive websocket bytes in this handler
                    // before we had a chance to replace it.
                    //
                    // See https://github.com/netty/netty/issues/9471.
                    WebSocketServerProtocolHandler.SetHandshaker(ctx.Channel, handshaker);
                    ctx.Pipeline.Replace(this, "WS403Responder",
                            WebSocketServerProtocolHandler.ForbiddenHttpRequestResponder());

                    Task task = handshaker.HandshakeAsync(ctx.Channel, req);
                    task.ContinueWith(FireUserEventTriggeredAction, Tuple.Create(ctx, req, handshaker, _handshakePromise), TaskContinuationOptions.ExecuteSynchronously);
                    ApplyHandshakeTimeout();
                }
            }
            finally
            {
                req.Release();
            }
        }

        static readonly Action<Task, object> FireUserEventTriggeredAction = OnFireUserEventTriggered;
        static void OnFireUserEventTriggered(Task t, object state)
        {
            var wrapped = (Tuple<IChannelHandlerContext, IFullHttpRequest, WebSocketServerHandshaker, IPromise>)state;
            if (t.IsSuccess())
            {
                wrapped.Item4.TryComplete();
                var ctx = wrapped.Item1;
                var req = wrapped.Item2;
                // Kept for compatibility
                ctx.FireUserEventTriggered(
                        WebSocketServerProtocolHandler.ServerHandshakeStateEvent.HandshakeComplete);
                ctx.FireUserEventTriggered(new WebSocketServerProtocolHandler.HandshakeComplete(
                    req.Uri, req.Headers, wrapped.Item3.SelectedSubprotocol));
            }
            else
            {
                wrapped.Item4.TrySetException(t.Exception);
                wrapped.Item1.FireExceptionCaught(t.Exception);
            }
        }

        private bool IsNotWebSocketPath(IFullHttpRequest req)
        {
            string websocketPath = _serverConfig.WebsocketPath;
            return _serverConfig.CheckStartsWith
                ? !req.Uri.StartsWith(websocketPath, StringComparison.Ordinal)
                : !string.Equals(req.Uri, websocketPath
#if NETCOREAPP_3_0_GREATER || NETSTANDARD_2_0_GREATER
                    );
#else
                    , StringComparison.Ordinal);
#endif
        }

        static void SendHttpResponse(IChannelHandlerContext ctx, IHttpRequest req, IHttpResponse res)
        {
            Task task = ctx.Channel.WriteAndFlushAsync(res);
            if (!IsKeepAlive(req) || res.Status.Code != StatusCodes.Status200OK)
            {
                task.ContinueWith(CloseOnCompleteAction, ctx.Channel, TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        static readonly Action<Task, object> CloseOnCompleteAction = CloseOnComplete;
        static void CloseOnComplete(Task t, object c) => ((IChannel)c).CloseAsync();

        static string GetWebSocketLocation(IChannelPipeline cp, IHttpRequest req, string path)
        {
            string protocol = "ws";
            if (cp.Get<TlsHandler>() is object)
            {
                // SSL in use so use Secure WebSockets
                protocol = "wss";
            }

            string host = null;
            if (req.Headers.TryGet(HttpHeaderNames.Host, out ICharSequence value))
            {
                host = value.ToString();
            }
            return $"{protocol}://{host}{path}";
        }

        private void ApplyHandshakeTimeout()
        {
            var localHandshakePromise = _handshakePromise;
            var handshakeTimeoutMillis = _serverConfig.HandshakeTimeoutMillis;
            if (handshakeTimeoutMillis <= 0L || localHandshakePromise.IsCompleted) { return; }

            var timeoutTask = _ctx.Executor.Schedule(FireHandshakeTimeoutAction, _ctx, localHandshakePromise, TimeSpan.FromMilliseconds(handshakeTimeoutMillis));

            // Cancel the handshake timeout when handshake is finished.
            localHandshakePromise.Task.ContinueWith(AbortHandshakeTimeoutAfterHandshakeCompletedAction, timeoutTask, TaskContinuationOptions.ExecuteSynchronously);
        }

        private static readonly Action<object, object> FireHandshakeTimeoutAction = FireHandshakeTimeout;
        private static void FireHandshakeTimeout(object c, object p)
        {
            var handshakePromise = (IPromise)p;
            if (handshakePromise.IsCompleted) { return; }
            if (handshakePromise.TrySetException(new WebSocketHandshakeException("handshake timed out")))
            {
                ((IChannelHandlerContext)c)
                    .Flush()
                    .FireUserEventTriggered(WebSocketServerProtocolHandler.ServerHandshakeStateEvent.HandshakeTimeout)
                    .CloseAsync();
            }
        }

        private static readonly Action<Task, object> AbortHandshakeTimeoutAfterHandshakeCompletedAction = AbortHandshakeTimeoutAfterHandshakeCompleted;
        private static void AbortHandshakeTimeoutAfterHandshakeCompleted(Task t, object s)
        {
            ((IScheduledTask)s).Cancel();
        }
    }
}
