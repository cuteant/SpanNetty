// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Streams
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using CuteAnt.Collections;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class ChunkedWriteHandler<T> : ChannelDuplexHandler
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<ChunkedWriteHandler<T>>();
        static readonly Action<object> InvokeDoFlushAction = OnInvokeDoFlush;
        static readonly Action<Task, object> LinkNonChunkedOutcomeAction = LinkNonChunkedOutcome;
        static readonly Action<Task, object> LinkOutcomeWhenChanelIsWritableAction = LinkOutcomeWhenChanelIsWritable;
        static readonly Action<Task, object> LinkOutcomeWhenIsEndOfChunkedInputAction = LinkOutcomeWhenIsEndOfChunkedInput;
        static readonly Action<Task, object> LinkOutcomeAction = LinkOutcome;

        readonly Deque<PendingWrite> queue = new Deque<PendingWrite>();
        IChannelHandlerContext ctx;
        PendingWrite currentWrite;

        public override void HandlerAdded(IChannelHandlerContext context) => Interlocked.Exchange(ref this.ctx, context);

        public void ResumeTransfer()
        {
            var ctx = Volatile.Read(ref this.ctx);
            if (null == ctx) { return; }

            if (ctx.Executor.InEventLoop)
            {
                this.InvokeDoFlush(ctx);
            }
            else
            {
                ctx.Executor.Execute(InvokeDoFlushAction, Tuple.Create(this, ctx));
            }
        }

        public override Task WriteAsync(IChannelHandlerContext context, object message)
        {
            var pendingWrite = new PendingWrite(message);
            this.queue.AddToBack(pendingWrite);
            return pendingWrite.PendingTask;
        }

        public override void Flush(IChannelHandlerContext context) => this.DoFlush(context);

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            this.DoFlush(context);
            context.FireChannelInactive();
        }

        public override void ChannelWritabilityChanged(IChannelHandlerContext context)
        {
            if (context.Channel.IsWritable)
            {
                // channel is writable again try to continue flushing
                this.DoFlush(context);
            }

            context.FireChannelWritabilityChanged();
        }

        void Discard(Exception cause = null)
        {
            while (true)
            {
                PendingWrite current = this.currentWrite;
                if (current == null)
                {
                    this.queue.TryRemoveFromFront(out current);
                }
                else
                {
                    this.currentWrite = null;
                }

                if (current == null)
                {
                    break;
                }

                object message = current.Message;
                if (message is IChunkedInput<T> chunks)
                {
                    try
                    {
                        if (!chunks.IsEndOfInput)
                        {
                            if (cause == null)
                            {
                                cause = new ClosedChannelException();
                            }

                            current.Fail(cause);
                        }
                        else
                        {
                            current.Success();
                        }
                    }
                    catch (Exception exception)
                    {
                        current.Fail(exception);
                        if (Logger.WarnEnabled) Logger.IsEndOfInputFailed<T>(exception);
                    }
                    finally
                    {
                        CloseInput(chunks);
                    }
                }
                else
                {
                    if (cause == null)
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
                this.DoFlush(context);
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
                this.Discard();
                return;
            }

            bool requiresFlush = true;
            IByteBufferAllocator allocator = context.Allocator;
            while (channel.IsWritable)
            {
                if (this.currentWrite == null)
                {
                    this.queue.TryRemoveFromFront(out currentWrite);
                }

                if (this.currentWrite == null)
                {
                    break;
                }

                PendingWrite current = this.currentWrite;
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
                        if (message == null)
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
                        this.currentWrite = null;

                        if (message != null)
                        {
                            ReferenceCountUtil.Release(message);
                        }

                        current.Fail(exception);
                        CloseInput(chunks);

                        break;
                    }

                    if (suspend)
                    {
                        // ChunkedInput.nextChunk() returned null and it has
                        // not reached at the end of input. Let's wait until
                        // more chunks arrive. Nothing to write or notify.
                        break;
                    }

                    if (message == null)
                    {
                        // If message is null write an empty ByteBuf.
                        // See https://github.com/netty/netty/issues/1671
                        message = Unpooled.Empty;
                    }

                    Task future = context.WriteAsync(message);
                    if (endOfInput)
                    {
                        this.currentWrite = null;

                        // Register a listener which will close the input once the write is complete.
                        // This is needed because the Chunk may have some resource bound that can not
                        // be closed before its not written.
                        //
                        // See https://github.com/netty/netty/issues/303
#if NET40
                        void linkOutcomeWhenIsEndOfChunkedInput(Task task)
                        {
                            var pendingTask = current;
                            CloseInput((IChunkedInput<T>)pendingTask.Message);
                            pendingTask.Success();
                        }
                        future.ContinueWith(linkOutcomeWhenIsEndOfChunkedInput, TaskContinuationOptions.ExecuteSynchronously);
#else
                        future.ContinueWith(LinkOutcomeWhenIsEndOfChunkedInputAction, current, TaskContinuationOptions.ExecuteSynchronously);
#endif
                    }
                    else if (channel.IsWritable)
                    {
#if NET40
                        void linkOutcomeWhenChanelIsWritable(Task task)
                        {
                            var pendingTask = current;
                            if (task.IsFaulted)
                            {
                                CloseInput((IChunkedInput<T>)pendingTask.Message);
                                pendingTask.Fail(task.Exception);
                            }
                            else
                            {
                                pendingTask.Progress(chunks.Progress, chunks.Length);
                            }
                        }
                        future.ContinueWith(linkOutcomeWhenChanelIsWritable, TaskContinuationOptions.ExecuteSynchronously);
#else
                        future.ContinueWith(LinkOutcomeWhenChanelIsWritableAction,
                            Tuple.Create(current, chunks), TaskContinuationOptions.ExecuteSynchronously);
#endif
                    }
                    else
                    {
#if NET40
                        void linkOutcome(Task task)
                        {
                            var handler = this;
                            if (task.IsFaulted)
                            {
                                CloseInput((IChunkedInput<T>)handler.currentWrite.Message);
                                handler.currentWrite.Fail(task.Exception);
                            }
                            else
                            {
                                handler.currentWrite.Progress(chunks.Progress, chunks.Length);
                                if (channel.IsWritable)
                                {
                                    handler.ResumeTransfer();
                                }
                            }
                        }
                        future.ContinueWith(linkOutcome, TaskContinuationOptions.ExecuteSynchronously);
#else
                        future.ContinueWith(LinkOutcomeAction,
                            Tuple.Create(this, chunks, channel), TaskContinuationOptions.ExecuteSynchronously);
#endif
                    }

                    // Flush each chunk to conserve memory
                    context.Flush();
                    requiresFlush = false;
                }
                else
                {
#if NET40
                    void linkNonChunkedOutcome(Task task)
                    {
                        var pendingTask = current;
                        if (task.IsFaulted)
                        {
                            pendingTask.Fail(task.Exception);
                        }
                        else
                        {
                            pendingTask.Success();
                        }
                    }
                    context.WriteAsync(pendingMessage)
                        .ContinueWith(linkNonChunkedOutcome, TaskContinuationOptions.ExecuteSynchronously);
#else
                    context.WriteAsync(pendingMessage)
                        .ContinueWith(LinkNonChunkedOutcomeAction, current, TaskContinuationOptions.ExecuteSynchronously);
#endif

                    this.currentWrite = null;
                    requiresFlush = true;
                }

                if (!channel.Active)
                {
                    this.Discard(new ClosedChannelException());
                    break;
                }
            }

            if (requiresFlush)
            {
                context.Flush();
            }
        }

        static void LinkOutcome(Task task, object state)
        {
            var wrapped = (Tuple<ChunkedWriteHandler<T>, IChunkedInput<T>, IChannel>)state;
            var handler = wrapped.Item1;
            if (task.IsFaulted)
            {
                CloseInput((IChunkedInput<T>)handler.currentWrite.Message);
                handler.currentWrite.Fail(task.Exception);
            }
            else
            {
                var chunks = wrapped.Item2;
                handler.currentWrite.Progress(chunks.Progress, chunks.Length);
                if (wrapped.Item3.IsWritable)
                {
                    handler.ResumeTransfer();
                }
            }
        }

        static void LinkOutcomeWhenIsEndOfChunkedInput(Task task, object state)
        {
            var pendingTask = (PendingWrite)state;
            CloseInput((IChunkedInput<T>)pendingTask.Message);
            pendingTask.Success();
        }

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

        static void LinkNonChunkedOutcome(Task task, object state)
        {
            var pendingTask = (PendingWrite)state;
            if (task.IsFaulted)
            {
                pendingTask.Fail(task.Exception);
            }
            else
            {
                pendingTask.Success();
            }
        }

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
            readonly TaskCompletionSource promise;

            public PendingWrite(object msg)
            {
                this.Message = msg;
                this.promise = new TaskCompletionSource();
            }

            public object Message { get; }

            public void Success() => this.promise.TryComplete();

            public void Fail(Exception error)
            {
                ReferenceCountUtil.Release(this.Message);
                this.promise.TrySetException(error);
            }

            public void Progress(long progress, long total)
            {
                if (progress < total)
                {
                    return;
                }

                this.Success();
            }

            public Task PendingTask => this.promise.Task;
        }
    }
}
