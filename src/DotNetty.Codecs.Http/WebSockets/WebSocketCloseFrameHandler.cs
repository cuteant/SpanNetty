namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Send <see cref="CloseWebSocketFrame"/> message on channel close, if close frame was not sent before.
    /// </summary>
    sealed class WebSocketCloseFrameHandler : ChannelHandlerAdapter
    {
        private static readonly Action<Task, object> CloseOnCompleteAction = CloseOnComplete;
        private static readonly Action<Task, object> AbortCloseSetAction = AbortCloseSet;

        private readonly WebSocketCloseStatus _closeStatus;
        private readonly long _forceCloseTimeoutMillis;
        private IPromise _closeSent;

        public WebSocketCloseFrameHandler(WebSocketCloseStatus closeStatus, long forceCloseTimeoutMillis)
        {
            if (closeStatus is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.closeStatus); }

            _closeStatus = closeStatus;
            _forceCloseTimeoutMillis = forceCloseTimeoutMillis;
        }

        public override void Close(IChannelHandlerContext context, IPromise promise)
        {
            if (!context.Channel.Active)
            {
                context.CloseAsync(promise);
                return;
            }
            if (_closeSent is null)
            {
                Write(context, new CloseWebSocketFrame(_closeStatus), context.NewPromise());
            }
            Flush(context);
            ApplyCloseSentTimeout(context);

            _closeSent.Task.ContinueWith(CloseOnCompleteAction, Tuple.Create(context, promise), TaskContinuationOptions.ExecuteSynchronously);
        }

        private static void CloseOnComplete(Task task, object state)
        {
            var wrapped = (Tuple<IChannelHandlerContext, IPromise>)state;
            wrapped.Item1.CloseAsync(wrapped.Item2);
        }

        public override void Write(IChannelHandlerContext context, object message, IPromise promise)
        {
            if (_closeSent is object)
            {
                ReferenceCountUtil.Release(message);
                promise.TrySetException(ThrowHelper.GetClosedChannelException());
                return;
            }
            if (message is CloseWebSocketFrame)
            {
                promise = promise.Unvoid();
                _closeSent = promise;
            }
            base.Write(context, message, promise);
        }

        private void ApplyCloseSentTimeout(IChannelHandlerContext ctx)
        {
            if (_closeSent.IsCompleted || _forceCloseTimeoutMillis < 0L)
            {
                return;
            }

            var timeoutTask = ctx.Executor.Schedule(new CloseTask(_closeSent), TimeSpan.FromMilliseconds(_forceCloseTimeoutMillis));
            _closeSent.Task.ContinueWith(AbortCloseSetAction, timeoutTask, TaskContinuationOptions.ExecuteSynchronously);
        }

        private static void AbortCloseSet(Task t, object s)
        {
            ((IScheduledTask)s).Cancel();
        }

        sealed class CloseTask : IRunnable
        {
            private readonly IPromise _closeSent;

            public CloseTask(IPromise closeSet)
            {
                _closeSent = closeSet;
            }

            public void Run()
            {
                if (!_closeSent.IsCompleted)
                {
                    _closeSent.TrySetException(ThrowHelper.GetWebSocketHandshakeException_SendCloseFrameTimedOut());
                }
            }
        }
    }
}
