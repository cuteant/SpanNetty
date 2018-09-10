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
    /// A queue of write operations which are pending for later execution. It also updates the writability of the
    /// associated <see cref="IChannel"/> (<see cref="IChannel.IsWritable"/>), so that the pending write operations are
    /// also considered to determine the writability.
    /// </summary>
    public sealed class PendingWriteQueue
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<PendingWriteQueue>();

        readonly IChannelHandlerContext ctx;
        readonly PendingBytesTracker tracker;

        // head and tail pointers for the linked-list structure. If empty head and tail are null.
        PendingWrite head;
        PendingWrite tail;
        int size;
        long bytes;

        public PendingWriteQueue(IChannelHandlerContext ctx)
        {
            if (null == ctx) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.ctx); }

            this.tracker = PendingBytesTracker.NewTracker(ctx.Channel);
            this.ctx = ctx;
        }

        /// <summary>
        /// Returns <c>true</c> if there are no pending write operations left in this queue.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                Debug.Assert(this.ctx.Executor.InEventLoop);

                return this.head == null;
            }
        }

        /// <summary>
        /// Returns the number of pending write operations.
        /// </summary>
        public int Size
        {
            get
            {
                Debug.Assert(this.ctx.Executor.InEventLoop);

                return this.size;
            }
        }

        /// <summary>
        /// Returns the total number of bytes that are pending because of pending messages. This is only an estimate so
        /// it should only be treated as a hint.
        /// </summary>
        public long Bytes
        {
            get
            {
                Debug.Assert(this.ctx.Executor.InEventLoop);

                return this.bytes;
            }
        }

        /// <summary>
        /// Adds the given message to this <see cref="PendingWriteQueue"/>.
        /// </summary>
        /// <param name="msg">The message to add to the <see cref="PendingWriteQueue"/>.</param>
        /// <returns>An await-able task.</returns>
        public Task Add(object msg)
        {
            Debug.Assert(this.ctx.Executor.InEventLoop);
            if (null == msg) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.msg); }

            // It is possible for writes to be triggered from removeAndFailAll(). To preserve ordering,
            // we should add them to the queue and let removeAndFailAll() fail them later.
            int messageSize = this.tracker.Size(msg);
            if (messageSize < 0)
            {
                // Size may be unknow so just use 0
                messageSize = 0;
            }
            var promise = new TaskCompletionSource();
            PendingWrite write = PendingWrite.NewInstance(msg, messageSize, promise);
            PendingWrite currentTail = this.tail;
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
            this.bytes += messageSize;
            this.tracker.IncrementPendingOutboundBytes(write.Size);
            return promise.Task;
        }

        /// <summary>
        /// Removes all pending write operations, and fail them with the given <see cref="Exception"/>. The messages
        /// will be released via <see cref="ReferenceCountUtil.SafeRelease(object)"/>.
        /// </summary>
        /// <param name="cause">The <see cref="Exception"/> to fail with.</param>
        public void RemoveAndFailAll(Exception cause)
        {
            Debug.Assert(this.ctx.Executor.InEventLoop);
            if (null == cause) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.cause); }

            // It is possible for some of the failed promises to trigger more writes. The new writes
            // will "revive" the queue, so we need to clean them up until the queue is empty.
            for (PendingWrite write = this.head; write != null; write = this.head)
            {
                this.head = this.tail = null;
                this.size = 0;
                this.bytes = 0;
                while (write != null)
                {
                    PendingWrite next = write.Next;
                    ReferenceCountUtil.SafeRelease(write.Msg);
                    TaskCompletionSource promise = write.Promise;
                    this.Recycle(write, false);
                    Util.SafeSetFailure(promise, cause, Logger);
                    write = next;
                }
            }
            this.AssertEmpty();
        }

        /// <summary>
        /// Remove a pending write operation and fail it with the given <see cref="Exception"/>. The message will be
        /// released via <see cref="ReferenceCountUtil.SafeRelease(object)"/>.
        /// </summary>
        /// <param name="cause">The <see cref="Exception"/> to fail with.</param>
        public void RemoveAndFail(Exception cause)
        {
            Debug.Assert(this.ctx.Executor.InEventLoop);
            if (null == cause) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.cause); }

            PendingWrite write = this.head;

            if (write == null)
            {
                return;
            }
            ReferenceCountUtil.SafeRelease(write.Msg);
            TaskCompletionSource promise = write.Promise;
            Util.SafeSetFailure(promise, cause, Logger);
            this.Recycle(write, true);
        }

        /// <summary>
        /// Removes all pending write operation and performs them via <see cref="IChannelHandlerContext.WriteAsync"/>
        /// </summary>
        /// <returns>An await-able task.</returns>
        public Task RemoveAndWriteAllAsync()
        {
            Debug.Assert(this.ctx.Executor.InEventLoop);

            if (IsEmpty) { return TaskUtil.Completed; }

            // Guard against re-entrance by directly reset
            int currentSize = this.size;
            var tasks = new List<Task>(currentSize);

            // It is possible for some of the written promises to trigger more writes. The new writes
            // will "revive" the queue, so we need to write them up until the queue is empty.
            for (PendingWrite write = this.head; write != null; write = this.head)
            {
                this.head = this.tail = null;
                this.size = 0;
                this.bytes = 0;

                while (write != null)
                {
                    PendingWrite next = write.Next;
                    object msg = write.Msg;
                    TaskCompletionSource promise = write.Promise;
                    this.Recycle(write, false);
                    if (promise != TaskCompletionSource.Void) { tasks.Add(promise.Task); }
                    this.ctx.WriteAsync(msg).LinkOutcome(promise);
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
        /// Removes a pending write operation and performs it via <see cref="IChannelHandlerContext.WriteAsync"/>.
        /// </summary>
        /// <returns>An await-able task.</returns>
        public Task RemoveAndWriteAsync()
        {
            Debug.Assert(this.ctx.Executor.InEventLoop);

            PendingWrite write = this.head;
            if (write == null)
            {
                return null;
            }
            object msg = write.Msg;
            TaskCompletionSource promise = write.Promise;
            this.Recycle(write, true);
            this.ctx.WriteAsync(msg).LinkOutcome(promise);
            return promise.Task;
        }

        /// <summary>
        /// Removes a pending write operation and releases it's message via
        /// <see cref="ReferenceCountUtil.SafeRelease(object)"/>.
        /// </summary>
        /// <returns>
        /// The <see cref="TaskCompletionSource" /> of the pending write, or <c>null</c> if the queue is empty.
        /// </returns>
        public TaskCompletionSource Remove()
        {
            Debug.Assert(this.ctx.Executor.InEventLoop);

            PendingWrite write = this.head;
            if (write == null)
            {
                return null;
            }
            TaskCompletionSource promise = write.Promise;
            ReferenceCountUtil.SafeRelease(write.Msg);
            this.Recycle(write, true);
            return promise;
        }

        /// <summary>
        /// Return the current message, or <c>null</c> if the queue is empty.
        /// </summary>
        public object Current
        {
            get
            {
                Debug.Assert(this.ctx.Executor.InEventLoop);

                return this.head?.Msg;
            }
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
                    this.bytes = 0;
                }
                else
                {
                    this.head = next;
                    this.size--;
                    this.bytes -= writeSize;
                    Debug.Assert(this.size > 0 && this.bytes >= 0);
                }
            }

            write.Recycle();
            this.tracker.DecrementPendingOutboundBytes(writeSize);
        }

        /// <summary>
        /// Holds all meta-data and constructs the linked-list structure.
        /// </summary>
        sealed class PendingWrite
        {
            static readonly ThreadLocalPool<PendingWrite> Pool = new ThreadLocalPool<PendingWrite>(handle => new PendingWrite(handle));

            readonly ThreadLocalPool.Handle handle;
            public PendingWrite Next;
            public long Size;
            public TaskCompletionSource Promise;
            public object Msg;

            PendingWrite(ThreadLocalPool.Handle handle)
            {
                this.handle = handle;
            }

            public static PendingWrite NewInstance(object msg, int size, TaskCompletionSource promise)
            {
                PendingWrite write = Pool.Take();
                write.Size = size;
                write.Msg = msg;
                write.Promise = promise;
                return write;
            }

            public void Recycle()
            {
                this.Size = 0;
                this.Next = null;
                this.Msg = null;
                this.Promise = null;
                this.handle.Release(this);
            }
        }
    }
}