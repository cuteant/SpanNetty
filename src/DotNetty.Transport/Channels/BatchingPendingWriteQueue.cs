// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    /// <summary>
    ///     A queue of write operations which are pending for later execution. It also updates the
    ///     <see cref="IChannel.IsWritable">writability</see> of the associated <see cref="IChannel" />, so that
    ///     the pending write operations are also considered to determine the writability.
    /// </summary>
    public sealed class BatchingPendingWriteQueue
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<BatchingPendingWriteQueue>();

        readonly IChannelHandlerContext ctx;
        readonly int maxSize;
        readonly ChannelOutboundBuffer buffer;
        readonly IMessageSizeEstimatorHandle estimatorHandle;

        // head and tail pointers for the linked-list structure. If empty head and tail are null.
        PendingWrite head;
        PendingWrite tail;
        int size;

        public BatchingPendingWriteQueue(IChannelHandlerContext ctx, int maxSize)
        {
            if (null == ctx) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.ctx); }

            this.ctx = ctx;
            this.maxSize = maxSize;
            this.buffer = ctx.Channel.Unsafe.OutboundBuffer;
            this.estimatorHandle = ctx.Channel.Configuration.MessageSizeEstimator.NewHandle();
        }

        /// <summary>Returns <c>true</c> if there are no pending write operations left in this queue.</summary>
        public bool IsEmpty
        {
            get
            {
                Debug.Assert(this.ctx.Executor.InEventLoop);

                return this.head == null;
            }
        }

        /// <summary>Returns the number of pending write operations.</summary>
        public int Size
        {
            get
            {
                Debug.Assert(this.ctx.Executor.InEventLoop);

                return this.size;
            }
        }

        /// <summary>Add the given <c>msg</c> and returns <see cref="Task" /> for completion of processing <c>msg</c>.</summary>
        public void Add(object msg, IPromise promise)
        {
            Debug.Assert(this.ctx.Executor.InEventLoop);
            if (null == msg) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.msg); }

            int messageSize = this.estimatorHandle.Size(msg);
            if (messageSize < 0)
            {
                // Size may be unknow so just use 0
                messageSize = 0;
            }
            PendingWrite currentTail = this.tail;
            if (currentTail is object)
            {
                bool canBundle = this.CanBatch(msg, messageSize, currentTail.Size);
                if (canBundle)
                {
                    currentTail.Add(msg, messageSize);
                    if (!promise.IsVoid)
                    {
                        currentTail.Promise.Task.LinkOutcome(promise);
                    }
                    return;
                }
            }

            PendingWrite write;
            if (promise.IsVoid || promise is SimplePromiseAggregator)
            {
                var headPromise = this.ctx.NewPromise();
                headPromise.Task.LinkOutcome(promise);
                write = PendingWrite.NewInstance(msg, messageSize, headPromise);
            }
            else
            {
                write = PendingWrite.NewInstance(msg, messageSize, promise);
            }
            if (currentTail == null)
            {
                this.tail = this.head = write;
            }
            else
            {
                currentTail.Next = write;
                this.tail = write;
            }
            this.size++;
            // We need to guard against null as channel.Unsafe.OutboundBuffer may returned null
            // if the channel was already closed when constructing the PendingWriteQueue.
            // See https://github.com/netty/netty/issues/3967
            this.buffer?.IncrementPendingOutboundBytes(messageSize);
        }

        /// <summary>
        ///     Remove all pending write operation and fail them with the given <see cref="Exception" />. The messages will be
        ///     released
        ///     via <see cref="ReferenceCountUtil.SafeRelease(object)" />.
        /// </summary>
        public void RemoveAndFailAll(Exception cause)
        {
            Debug.Assert(this.ctx.Executor.InEventLoop);
            if (null == cause) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.cause); }

            // It is possible for some of the failed promises to trigger more writes. The new writes
            // will "revive" the queue, so we need to clean them up until the queue is empty.
            for (PendingWrite write = this.head; write is object; write = this.head)
            {
                this.head = this.tail = null;
                this.size = 0;
                while (write is object)
                {
                    PendingWrite next = write.Next;
                    ReferenceCountUtil.SafeRelease(write.Messages);
                    IPromise promise = write.Promise;
                    this.Recycle(write, false);
                    Util.SafeSetFailure(promise, cause, Logger);
                    write = next;
                }
            }
            this.AssertEmpty();
        }

        /// <summary>
        ///     Remove a pending write operation and fail it with the given <see cref="Exception" />. The message will be released
        ///     via
        ///     <see cref="ReferenceCountUtil.SafeRelease(object)" />.
        /// </summary>
        public void RemoveAndFail(Exception cause)
        {
            Debug.Assert(this.ctx.Executor.InEventLoop);
            if (null == cause) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.cause); }

            PendingWrite write = this.head;

            if (write == null)
            {
                return;
            }
            ReleaseMessages(write.Messages);
            IPromise promise = write.Promise;
            Util.SafeSetFailure(promise, cause, Logger);
            this.Recycle(write, true);
        }

        /// <summary>
        ///     Remove all pending write operation and performs them via
        ///     <see cref="IChannelHandlerContext.WriteAsync(object, IPromise)" />.
        /// </summary>
        /// <returns>
        ///     <see cref="Task" /> if something was written and <c>null</c> if the <see cref="BatchingPendingWriteQueue" />
        ///     is empty.
        /// </returns>
        public Task RemoveAndWriteAllAsync()
        {
            Debug.Assert(this.ctx.Executor.InEventLoop);

            if (IsEmpty) { return TaskUtil.Completed; }

            // Guard against re-entrance by directly reset
            int currentSize = this.size;
            var tasks = new List<Task>(currentSize);

            // It is possible for some of the written promises to trigger more writes. The new writes
            // will "revive" the queue, so we need to write them up until the queue is empty.
            for (PendingWrite write = this.head; write is object; write = this.head)
            {
                this.head = this.tail = null;
                this.size = 0;

                while (write is object)
                {
                    PendingWrite next = write.Next;
                    object msg = write.Messages;
                    IPromise promise = write.Promise;
                    this.Recycle(write, false);
                    if (!promise.IsVoid) { tasks.Add(promise.Task); }
                    this.ctx.WriteAsync(msg, promise);
                    write = next;
                }
            }
            this.AssertEmpty();
#if NET40
            return TaskEx.WhenAll(tasks);
#else
            return Task.WhenAll(tasks);
#endif
        }

        [Conditional("DEBUG")]
        void AssertEmpty() => Debug.Assert(this.tail == null && this.head == null && this.size == 0);

        /// <summary>
        ///     Removes a pending write operation and performs it via
        ///     <see cref="IChannelHandlerContext.WriteAsync(object, IPromise)"/>.
        /// </summary>
        /// <returns>
        ///     <see cref="Task" /> if something was written and <c>null</c> if the <see cref="BatchingPendingWriteQueue" />
        ///     is empty.
        /// </returns>
        public Task RemoveAndWriteAsync()
        {
            Debug.Assert(this.ctx.Executor.InEventLoop);

            PendingWrite write = this.head;
            if (write == null)
            {
                return null;
            }
            object msg = write.Messages;
            IPromise promise = write.Promise;
            this.Recycle(write, true);
            return this.ctx.WriteAsync(msg, promise);
        }

        /// <summary>
        ///     Removes a pending write operation and release it's message via <see cref="ReferenceCountUtil.SafeRelease(object)"/>.
        /// </summary>
        /// <returns><see cref="IPromise" /> of the pending write or <c>null</c> if the queue is empty.</returns>
        public IPromise Remove()
        {
            Debug.Assert(this.ctx.Executor.InEventLoop);

            PendingWrite write = this.head;
            if (write == null)
            {
                return null;
            }
            IPromise promise = write.Promise;
            ReferenceCountUtil.SafeRelease(write.Messages);
            this.Recycle(write, true);
            return promise;
        }

        /// <summary>
        ///     Return the current message or <c>null</c> if empty.
        /// </summary>
        public List<object> Current
        {
            get
            {
                Debug.Assert(this.ctx.Executor.InEventLoop);

                return this.head?.Messages;
            }
        }

        public long? CurrentSize
        {
            get
            {
                Debug.Assert(this.ctx.Executor.InEventLoop);

                return this.head?.Size;
            }
        }

        bool CanBatch(object message, int size, long currentBatchSize)
        {
            if (size < 0)
            {
                return false;
            }

            if (currentBatchSize + size > this.maxSize)
            {
                return false;
            }

            return true;
        }

        void Recycle(PendingWrite write, bool update)
        {
            PendingWrite next = write.Next;
            long writeSize = write.Size;

            if (update)
            {
                if (next == null)
                {
                    // Handled last PendingWrite so rest head and tail
                    // Guard against re-entrance by directly reset
                    this.head = this.tail = null;
                    this.size = 0;
                }
                else
                {
                    this.head = next;
                    this.size--;
                    Debug.Assert(this.size > 0);
                }
            }

            write.Recycle();
            // We need to guard against null as channel.unsafe().outboundBuffer() may returned null
            // if the channel was already closed when constructing the PendingWriteQueue.
            // See https://github.com/netty/netty/issues/3967
            this.buffer?.DecrementPendingOutboundBytes(writeSize);
        }

        static void ReleaseMessages(List<object> messages)
        {
            foreach (object msg in messages)
            {
                ReferenceCountUtil.SafeRelease(msg);
            }
        }

        /// <summary>Holds all meta-data and construct the linked-list structure.</summary>
        sealed class PendingWrite
        {
            static readonly ThreadLocalPool<PendingWrite> Pool = new ThreadLocalPool<PendingWrite>(handle => new PendingWrite(handle));

            readonly ThreadLocalPool.Handle handle;
            public PendingWrite Next;
            public long Size;
            public IPromise Promise;
            public readonly List<object> Messages;

            PendingWrite(ThreadLocalPool.Handle handle)
            {
                this.Messages = new List<object>();
                this.handle = handle;
            }

            public static PendingWrite NewInstance(object msg, int size, IPromise promise)
            {
                PendingWrite write = Pool.Take();
                write.Add(msg, size);
                write.Promise = promise;
                return write;
            }

            public void Add(object msg, int size)
            {
                this.Messages.Add(msg);
                this.Size += size;
            }

            public void Recycle()
            {
                this.Size = 0;
                this.Next = null;
                this.Messages.Clear();
                this.Promise = null;
                this.handle.Release(this);
            }
        }
    }
}