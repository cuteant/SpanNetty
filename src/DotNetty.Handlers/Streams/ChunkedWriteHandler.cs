// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Streams
{
    using System;
    using System.Runtime.CompilerServices;
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
        private static readonly Action<Task, object> LinkOutcomeWhenIsEndOfChunkedInputAction = LinkOutcomeWhenIsEndOfChunkedInput;
        private static readonly Action<Task, object> LinkOutcomeAction = LinkOutcome;
        private static readonly Action<object> InvokeDoFlushAction = OnInvokeDoFlush;

        private readonly Deque<PendingWrite> _queue = new Deque<PendingWrite>();
        private IChannelHandlerContext _ctx;

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
            _ = context.FireChannelInactive();
        }

        public override void ChannelWritabilityChanged(IChannelHandlerContext context)
        {
            if (context.Channel.IsWritable)
            {
                // channel is writable again try to continue flushing
                DoFlush(context);
            }

            _ = context.FireChannelWritabilityChanged();
        }

        void Discard(Exception cause = null)
        {
            while (true)
            {
                if (!_queue.TryRemoveFromFront(out PendingWrite currentWrite))
                {
                    break;
                }

                object message = currentWrite.Message;
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
                        currentWrite.Fail(exc);
                        if (Logger.WarnEnabled) { Logger.IsEndOfInputFailed<T>(exc); }
                        continue;
                    }

                    if (!endOfInput)
                    {
                        if (cause is null) { cause = ThrowHelper.GetClosedChannelException(); }
                        currentWrite.Fail(cause);
                    }
                    else
                    {
                        currentWrite.Success(inputLength);
                    }
                }
                else
                {
                    if (cause is null)
                    {
                        cause = new ClosedChannelException();
                    }

                    currentWrite.Fail(cause);
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
            if (!channel.IsActive)
            {
                Discard();
                return;
            }

            bool requiresFlush = true;
            IByteBufferAllocator allocator = context.Allocator;
            while (channel.IsWritable)
            {
                PendingWrite currentWrite = _queue.FirstOrDefault;
                if (currentWrite is null) { break; }

                if (currentWrite.Promise.IsCompleted)
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
                    _ = _queue.RemoveFromFront();
                    continue;
                }

                object pendingMessage = currentWrite.Message;

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
                        _ = _queue.RemoveFromFront();

                        if (message is object)
                        {
                            _ = ReferenceCountUtil.Release(message);
                        }

                        CloseInput(chunks);
                        currentWrite.Fail(exception);

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

                    // Flush each chunk to conserve memory
                    Task future = context.WriteAndFlushAsync(message);
                    if (endOfInput)
                    {
                        _ = _queue.RemoveFromFront();

                        if (future.IsCompleted)
                        {
                            HandleEndOfInputFuture(future, currentWrite);
                        }
                        else
                        {
                            // Register a listener which will close the input once the write is complete.
                            // This is needed because the Chunk may have some resource bound that can not
                            // be closed before its not written.
                            //
                            // See https://github.com/netty/netty/issues/303
                            _ = future.ContinueWith(LinkOutcomeWhenIsEndOfChunkedInputAction, currentWrite, TaskContinuationOptions.ExecuteSynchronously);
                        }
                    }
                    else
                    {
                        var resume = !channel.IsWritable;
                        if (future.IsCompleted)
                        {
                            HandleFuture(future, this, channel, currentWrite, resume);
                        }
                        else
                        {
                            _ = future.ContinueWith(LinkOutcomeAction,
                                Tuple.Create(this, channel, currentWrite, resume), TaskContinuationOptions.ExecuteSynchronously);
                        }
                    }

                    requiresFlush = false;
                }
                else
                {
                    _ = _queue.RemoveFromFront();
                    _ = context.WriteAsync(pendingMessage, currentWrite.Promise);
                    requiresFlush = true;
                }

                if (!channel.IsActive)
                {
                    Discard(new ClosedChannelException());
                    break;
                }
            }

            if (requiresFlush)
            {
                _ = context.Flush();
            }
        }

        private static void LinkOutcome(Task task, object state)
        {
            var wrapped = (Tuple<ChunkedWriteHandler<T>, IChannel, PendingWrite, bool>)state;
            HandleFuture(task, wrapped.Item1, wrapped.Item2, wrapped.Item3, wrapped.Item4);
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        private static void HandleFuture(Task task, ChunkedWriteHandler<T> owner, IChannel channel, PendingWrite currentWrite, bool resume)
        {
            if (task.IsSuccess())
            {
                var chunks = (IChunkedInput<T>)currentWrite.Message;
                currentWrite.Progress(chunks.Progress, chunks.Length);
                if (resume && channel.IsWritable)
                {
                    owner.ResumeTransfer();
                }
            }
            else
            {
                CloseInput((IChunkedInput<T>)currentWrite.Message);
                currentWrite.Fail(task.Exception);
            }
        }

        private static void LinkOutcomeWhenIsEndOfChunkedInput(Task task, object state)
        {
            HandleEndOfInputFuture(task, (PendingWrite)state);
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        private static void HandleEndOfInputFuture(Task task, PendingWrite currentWrite)
        {
            if (task.IsSuccess())
            {
                var chunks = (IChunkedInput<T>)currentWrite.Message;
                // read state of the input in local variables before closing it
                long inputProgress = chunks.Progress;
                long inputLength = chunks.Length;
                CloseInput(chunks);
                currentWrite.Progress(inputProgress, inputLength);
                currentWrite.Success(inputLength);
            }
            else
            {
                CloseInput((IChunkedInput<T>)currentWrite.Message);
                currentWrite.Fail(task.Exception);
            }
        }

        private static void OnInvokeDoFlush(object state)
        {
            var wrapped = (Tuple<ChunkedWriteHandler<T>, IChannelHandlerContext>)state;
            wrapped.Item1.InvokeDoFlush(wrapped.Item2);
        }

        private static void CloseInput(IChunkedInput<T> chunks)
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
                _ = Promise.TryComplete();
            }

            public void Fail(Exception error)
            {
                _ = ReferenceCountUtil.Release(Message);
                _ = Promise.TrySetException(error);
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
