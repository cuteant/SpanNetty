// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Streams
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class ChunkedWriteHandler<T> : ChannelDuplexHandler
    {
        private static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<ChunkedWriteHandler<T>>();

        private readonly Deque<PendingWrite> _queue = new Deque<PendingWrite>();
        private IChannelHandlerContext _ctx;
        private PendingWrite _currentWrite;

        public override void HandlerAdded(IChannelHandlerContext context) => Interlocked.Exchange(ref _ctx, context);

        public void ResumeTransfer()
        {
            var ctx = Volatile.Read(ref _ctx);
            if (ctx is null) { return; }

            if (ctx.Executor.InEventLoop)
            {
                InvokeDoFlush(ctx);
            }
            else
            {
                ctx.Executor.Execute(InvokeDoFlushAction, Tuple.Create(this, ctx));
            }
        }

        public override void Write(IChannelHandlerContext context, object message, IPromise promise)
        {
            _queue.AddToBack(new PendingWrite(message, promise));
        }

        public override void Flush(IChannelHandlerContext context) => DoFlush(context);

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            DoFlush(context);
            context.FireChannelInactive();
        }

        public override void ChannelWritabilityChanged(IChannelHandlerContext context)
        {
            if (context.Channel.IsWritable)
            {
                // channel is writable again try to continue flushing
                DoFlush(context);
            }

            context.FireChannelWritabilityChanged();
        }

        void Discard(Exception cause = null)
        {
            while (true)
            {
                PendingWrite current = _currentWrite;
                if (current is null)
                {
                    _queue.TryRemoveFromFront(out current);
                }
                else
                {
                    _currentWrite = null;
                }

                if (current is null)
                {
                    break;
                }

                object message = current.Message;
                if (message is IChunkedInput<T> chunks)
                {
                    bool endOfInput;
                    long inputLength;
                    try
                    {
                        endOfInput = chunks.IsEndOfInput;
                        inputLength = chunks.Length;
                        CloseInput(chunks);
                    }
                    catch (Exception exc)
                    {
                        CloseInput(chunks);
                        _currentWrite.Fail(exc);
                        if (Logger.WarnEnabled) { Logger.IsEndOfInputFailed<T>(exc); }
                        continue;
                    }

                    if (!endOfInput)
                    {
                        if (cause is null) { cause = ThrowHelper.GetClosedChannelException(); }
                        _currentWrite.Fail(cause);
                    }
                    else
                    {
                        _currentWrite.Success(inputLength);
                    }
                }
                else
                {
                    if (cause is null)
                    {
                        cause = new ClosedChannelException();
                    }

                    current.Fail(cause);
                }
            }
        }

        void InvokeDoFlush(IChannelHandlerContext context)
        {
            try
            {
                DoFlush(context);
            }
            catch (Exception exception)
            {
                if (Logger.WarnEnabled)
                {
                    Logger.UnexpectedExceptionWhileSendingChunks(exception);
                }
            }
        }

        void DoFlush(IChannelHandlerContext context)
        {
            IChannel channel = context.Channel;
            if (!channel.Active)
            {
                Discard();
                return;
            }

            bool requiresFlush = true;
            IByteBufferAllocator allocator = context.Allocator;
            while (channel.IsWritable)
            {
                if (_currentWrite is null)
                {
                    _queue.TryRemoveFromFront(out _currentWrite);
                }

                if (_currentWrite is null)
                {
                    break;
                }

                if (_currentWrite.Promise.IsCompleted)
                {
                    // This might happen e.g. in the case when a write operation
                    // failed, but there're still unconsumed chunks left.
                    // Most chunked input sources would stop generating chunks
                    // and report end of input, but this doesn't work with any
                    // source wrapped in HttpChunkedInput.
                    // Note, that we're not trying to release the message/chunks
                    // as this had to be done already by someone who resolved the
                    // promise (using ChunkedInput.close method).
                    // See https://github.com/netty/netty/issues/8700.
                    _currentWrite = null;
                    continue;
                }

                PendingWrite current = _currentWrite;
                object pendingMessage = current.Message;

                if (pendingMessage is IChunkedInput<T> chunks)
                {
                    bool endOfInput;
                    bool suspend;
                    object message = null;

                    try
                    {
                        message = chunks.ReadChunk(allocator);
                        endOfInput = chunks.IsEndOfInput;
                        if (message is null)
                        {
                            // No need to suspend when reached at the end.
                            suspend = !endOfInput;
                        }
                        else
                        {
                            suspend = false;
                        }
                    }
                    catch (Exception exception)
                    {
                        _currentWrite = null;

                        if (message is object)
                        {
                            ReferenceCountUtil.Release(message);
                        }

                        CloseInput(chunks);
                        current.Fail(exception);

                        break;
                    }

                    if (suspend)
                    {
                        // ChunkedInput.nextChunk() returned null and it has
                        // not reached at the end of input. Let's wait until
                        // more chunks arrive. Nothing to write or notify.
                        break;
                    }

                    if (message is null)
                    {
                        // If message is null write an empty ByteBuf.
                        // See https://github.com/netty/netty/issues/1671
                        message = Unpooled.Empty;
                    }

                    Task future = context.WriteAsync(message);
                    if (endOfInput)
                    {
                        _currentWrite = null;

                        // Register a listener which will close the input once the write is complete.
                        // This is needed because the Chunk may have some resource bound that can not
                        // be closed before its not written.
                        //
                        // See https://github.com/netty/netty/issues/303
                        future.ContinueWith(LinkOutcomeWhenIsEndOfChunkedInputAction, current, TaskContinuationOptions.ExecuteSynchronously);
                    }
                    else if (channel.IsWritable)
                    {
                        future.ContinueWith(LinkOutcomeWhenChanelIsWritableAction,
                            Tuple.Create(current, chunks), TaskContinuationOptions.ExecuteSynchronously);
                    }
                    else
                    {
                        future.ContinueWith(LinkOutcomeAction,
                            Tuple.Create(this, chunks, channel), TaskContinuationOptions.ExecuteSynchronously);
                    }

                    // Flush each chunk to conserve memory
                    context.Flush();
                    requiresFlush = false;
                }
                else
                {
                    _currentWrite = null;
                    context.WriteAsync(pendingMessage, current.Promise);
                    requiresFlush = true;
                }

                if (!channel.Active)
                {
                    Discard(new ClosedChannelException());
                    break;
                }
            }

            if (requiresFlush)
            {
                context.Flush();
            }
        }

        static readonly Action<Task, object> LinkOutcomeAction = LinkOutcome;
        static void LinkOutcome(Task task, object state)
        {
            var wrapped = (Tuple<ChunkedWriteHandler<T>, IChunkedInput<T>, IChannel>)state;
            var handler = wrapped.Item1;
            if (task.IsSuccess())
            {
                var chunks = wrapped.Item2;
                handler._currentWrite.Progress(chunks.Progress, chunks.Length);
                if (wrapped.Item3.IsWritable)
                {
                    handler.ResumeTransfer();
                }
            }
            else
            {
                CloseInput((IChunkedInput<T>)handler._currentWrite.Message);
                handler._currentWrite.Fail(task.Exception);
            }
        }

        static readonly Action<Task, object> LinkOutcomeWhenIsEndOfChunkedInputAction = LinkOutcomeWhenIsEndOfChunkedInput;
        static void LinkOutcomeWhenIsEndOfChunkedInput(Task task, object state)
        {
            var pendingTask = (PendingWrite)state;
            if (task.IsSuccess())
            {
                var chunks = (IChunkedInput<T>)pendingTask.Message;
                // read state of the input in local variables before closing it
                long inputProgress = chunks.Progress;
                long inputLength = chunks.Length;
                CloseInput(chunks);
                pendingTask.Progress(inputProgress, inputLength);
                pendingTask.Success(inputLength);
            }
            else
            {
                CloseInput((IChunkedInput<T>)pendingTask.Message);
                pendingTask.Fail(task.Exception);
            }
        }

        static readonly Action<Task, object> LinkOutcomeWhenChanelIsWritableAction = LinkOutcomeWhenChanelIsWritable;
        static void LinkOutcomeWhenChanelIsWritable(Task task, object state)
        {
            var wrapped = (Tuple<PendingWrite, IChunkedInput<T>>)state;
            var pendingTask = wrapped.Item1;
            if (task.IsFaulted)
            {
                CloseInput((IChunkedInput<T>)pendingTask.Message);
                pendingTask.Fail(task.Exception);
            }
            else
            {
                var chunks = wrapped.Item2;
                pendingTask.Progress(chunks.Progress, chunks.Length);
            }
        }

        static readonly Action<object> InvokeDoFlushAction = OnInvokeDoFlush;
        static void OnInvokeDoFlush(object state)
        {
            var wrapped = (Tuple<ChunkedWriteHandler<T>, IChannelHandlerContext>)state;
            wrapped.Item1.InvokeDoFlush(wrapped.Item2);
        }

        static void CloseInput(IChunkedInput<T> chunks)
        {
            try
            {
                chunks.Close();
            }
            catch (Exception exception)
            {
                if (Logger.WarnEnabled)
                {
                    Logger.FailedToCloseAChunkedInput(exception);
                }
            }
        }

        sealed class PendingWrite
        {
            internal readonly IPromise Promise;

            public PendingWrite(object msg, IPromise promise)
            {
                Message = msg;
                Promise = promise;
            }

            public object Message { get; }

            public void Success(long total)
            {
                if (Promise.IsCompleted)
                {
                    // No need to notify the progress or fulfill the promise because it's done already.
                    return;
                }
                Progress(total, total);
                Promise.TryComplete();
            }

            public void Fail(Exception error)
            {
                ReferenceCountUtil.Release(Message);
                Promise.TrySetException(error);
            }

            public void Progress(long progress, long total)
            {
                // TODO
                //if (promise instanceof ChannelProgressivePromise) {
                //    ((ChannelProgressivePromise)promise).tryProgress(progress, total);
                //}
            }

            public Task PendingTask => Promise.Task;
        }
    }
}
