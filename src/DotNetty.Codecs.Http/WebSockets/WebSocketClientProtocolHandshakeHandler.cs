// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    sealed class WebSocketClientProtocolHandshakeHandler : ChannelHandlerAdapter
    {
        private const long DefaultHandshakeTimeoutMs = 10000L;

        private readonly WebSocketClientHandshaker _handshaker;
        private readonly long _handshakeTimeoutMillis;
        private IChannelHandlerContext _ctx;
        private IPromise _handshakePromise;

        internal WebSocketClientProtocolHandshakeHandler(WebSocketClientHandshaker handshaker)
            : this(handshaker, DefaultHandshakeTimeoutMs)
        {
        }

        internal WebSocketClientProtocolHandshakeHandler(WebSocketClientHandshaker handshaker, long handshakeTimeoutMillis)
        {
            if (handshakeTimeoutMillis <= 0L) { ThrowHelper.ThrowArgumentException_Positive(handshakeTimeoutMillis, ExceptionArgument.handshakeTimeoutMillis); }

            _handshaker = handshaker;
            _handshakeTimeoutMillis = handshakeTimeoutMillis;
        }

        /// <inheritdoc/>
        public override void HandlerAdded(IChannelHandlerContext context)
        {
            _ctx = context;
            _handshakePromise = context.NewPromise();
        }

        /// <inheritdoc/>
        public override void ChannelActive(IChannelHandlerContext context)
        {
            base.ChannelActive(context);

            _ = _handshaker.HandshakeAsync(context.Channel)
                .ContinueWith(FireUserEventTriggeredAction, Tuple.Create(context, _handshakePromise), TaskContinuationOptions.ExecuteSynchronously);

            ApplyHandshakeTimeout();
        }

        static readonly Action<Task, object> FireUserEventTriggeredAction = OnFireUserEventTriggered;
        static void OnFireUserEventTriggered(Task t, object state)
        {
            var wrapped = (Tuple<IChannelHandlerContext, IPromise>)state;
            if (t.IsSuccess())
            {
                _ = wrapped.Item2.TrySetException(t.Exception);
                _ = wrapped.Item1.FireUserEventTriggered(WebSocketClientProtocolHandler.ClientHandshakeStateEvent.HandshakeIssued);
            }
            else
            {
                _ = wrapped.Item1.FireExceptionCaught(t.Exception);
            }
        }

        /// <inheritdoc/>
        public override void ChannelRead(IChannelHandlerContext ctx, object msg)
        {
            if (!(msg is IFullHttpResponse response))
            {
                _ = ctx.FireChannelRead(msg);
                return;
            }

            try
            {
                if (!_handshaker.IsHandshakeComplete)
                {
                    _handshaker.FinishHandshake(ctx.Channel, response);
                    _ = _handshakePromise.TryComplete();
                    _ = ctx.FireUserEventTriggered(WebSocketClientProtocolHandler.ClientHandshakeStateEvent.HandshakeComplete);
                    _ = ctx.Pipeline.Remove(this);
                    return;
                }

                ThrowHelper.ThrowInvalidOperationException_WebSocketClientHandshaker();
            }
            finally
            {
                _ = response.Release();
            }
        }

        private void ApplyHandshakeTimeout()
        {
            var localHandshakePromise = _handshakePromise;
            if (_handshakeTimeoutMillis <= 0 || localHandshakePromise.IsCompleted) { return; }

            var timeoutTask = _ctx.Executor.Schedule(FireHandshakeTimeoutAction, _ctx, localHandshakePromise, TimeSpan.FromMilliseconds(_handshakeTimeoutMillis));

            // Cancel the handshake timeout when handshake is finished.
            _ = localHandshakePromise.Task.ContinueWith(AbortHandshakeTimeoutAfterHandshakeCompletedAction, timeoutTask, TaskContinuationOptions.ExecuteSynchronously);
        }

        private static readonly Action<object, object> FireHandshakeTimeoutAction = FireHandshakeTimeout;
        private static void FireHandshakeTimeout(object c, object p)
        {
            var handshakePromise = (IPromise)p;
            if (handshakePromise.IsCompleted) { return; }
            if (handshakePromise.TrySetException(new WebSocketHandshakeException("handshake timed out")))
            {
                _ = ((IChannelHandlerContext)c)
                    .Flush()
                    .FireUserEventTriggered(WebSocketClientProtocolHandler.ClientHandshakeStateEvent.HandshakeTimeout)
                    .CloseAsync();
            }
        }

        private static readonly Action<Task, object> AbortHandshakeTimeoutAfterHandshakeCompletedAction = AbortHandshakeTimeoutAfterHandshakeCompleted;
        private static void AbortHandshakeTimeoutAfterHandshakeCompleted(Task t, object s)
        {
            _ = ((IScheduledTask)s).Cancel();
        }

        /// <summary>
        /// This method is visible for testing.
        /// </summary>
        /// <returns>current handshake future</returns>
        internal Task GetHandshakeFuture() => _handshakePromise.Task;
    }
}
