// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using CuteAnt.Collections;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    public abstract class AbstractCoalescingBufferQueue
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<AbstractCoalescingBufferQueue>();
        readonly Deque<object> bufAndListenerPairs;
        readonly PendingBytesTracker tracker;
        int readableBytes;

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="channel">the <see cref="IChannel"/> which will have the <see cref="IChannel.IsWritable"/> reflect the amount of queued
        /// buffers or <c>null</c> if there is no writability state updated.</param>
        /// <param name="initSize">the initial size of the underlying queue.</param>
        protected AbstractCoalescingBufferQueue(IChannel channel, int initSize)
        {
            this.bufAndListenerPairs = new Deque<object>(initSize);
            this.tracker = channel == null ? null : PendingBytesTracker.NewTracker(channel);
        }

        public void AddFirst(IByteBuffer buf, IPromise promise)
        {
            if (promise is object && !promise.IsVoid)
            {
                this.bufAndListenerPairs.AddToFront(promise);
            }
            this.bufAndListenerPairs.AddToFront(buf);
            this.IncrementReadableBytes(buf.ReadableBytes);
        }

        /// <summary>
        /// Add a buffer to the end of the queue.
        /// </summary>
        /// <param name="buf"></param>
        public void Add(IByteBuffer buf)
        {
            this.Add(buf, null);
        }

        /// <summary>
        /// Add a buffer to the end of the queue and associate a promise with it that should be completed when
        /// all the buffer's bytes have been consumed from the queue and written.
        /// </summary>
        /// <param name="buf">to add to the tail of the queue</param>
        /// <param name="promise">to complete when all the bytes have been consumed and written, can be void.</param>
        public void Add(IByteBuffer buf, IPromise promise)
        {
            // buffers are added before promises so that we naturally 'consume' the entire buffer during removal
            // before we complete it's promise.
            this.bufAndListenerPairs.AddToBack(buf);
            if (promise is object && !promise.IsVoid)
            {
                this.bufAndListenerPairs.AddToBack(promise);
            }
            IncrementReadableBytes(buf.ReadableBytes);
        }

        /// <summary>
        /// Remove the first <see cref="IByteBuffer"/> from the queue.
        /// </summary>
        /// <param name="aggregatePromise">used to aggregate the promises and listeners for the returned buffer.</param>
        /// <returns>the first <see cref="IByteBuffer"/> from the queue.</returns>
        public IByteBuffer RemoveFirst(IPromise aggregatePromise)
        {
            if (!this.bufAndListenerPairs.TryRemoveFromFront(out var entry)) { return null; }
            Debug.Assert(entry is IByteBuffer);
            var result = (IByteBuffer)entry;

            DecrementReadableBytes(result.ReadableBytes);

            entry = this.bufAndListenerPairs.FirstOrDefault();

            if (entry is IPromise promise)
            {
                aggregatePromise.Task.CascadeTo(promise, Logger);
                bufAndListenerPairs.RemoveFromFront();
            }
            return result;
        }

        /// <summary>
        /// Remove a <see cref="IByteBuffer"/> from the queue with the specified number of bytes. Any added buffer who's bytes are
        /// fully consumed during removal will have it's promise completed when the passed aggregate <see cref="IPromise"/>
        /// completes.
        /// </summary>
        /// <param name="alloc">The allocator used if a new <see cref="IByteBuffer"/> is generated during the aggregation process.</param>
        /// <param name="bytes">the maximum number of readable bytes in the returned <see cref="IByteBuffer"/>, if {@code bytes} is greater
        /// than <see cref="ReadableBytes"/> then a buffer of length <see cref="ReadableBytes"/> is returned.</param>
        /// <param name="aggregatePromise">used to aggregate the promises and listeners for the constituent buffers.</param>
        /// <returns>a <see cref="IByteBuffer"/> composed of the enqueued buffers.</returns>
        public IByteBuffer Remove(IByteBufferAllocator alloc, int bytes, IPromise aggregatePromise)
        {
            if (bytes < 0) { ThrowHelper.ThrowArgumentException_PositiveOrZero(bytes, ExceptionArgument.bytes); }
            if (null == aggregatePromise) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.aggregatePromise); }

            // Use isEmpty rather than readableBytes==0 as we may have a promise associated with an empty buffer.
            if (bufAndListenerPairs.IsEmpty)
            {
                return RemoveEmptyValue();
            }
            bytes = Math.Min(bytes, readableBytes);

            IByteBuffer toReturn = null;
            IByteBuffer entryBuffer = null;
            int originalBytes = bytes;
            try
            {
                while (bufAndListenerPairs.TryRemoveFromFront(out var entry))
                {
                    if (entry is IPromise promise)
                    {
                        aggregatePromise.Task.CascadeTo(promise, Logger);
                        continue;
                    }
                    entryBuffer = (IByteBuffer)entry;
                    if (entryBuffer.ReadableBytes > bytes)
                    {
                        // Add the buffer back to the queue as we can't consume all of it.
                        bufAndListenerPairs.AddToFront(entryBuffer);
                        if (bytes > 0)
                        {
                            // Take a slice of what we can consume and retain it.
                            entryBuffer = entryBuffer.ReadRetainedSlice(bytes);
                            toReturn = toReturn == null ? ComposeFirst(alloc, entryBuffer)
                                                        : Compose(alloc, toReturn, entryBuffer);
                            bytes = 0;
                        }
                        break;
                    }
                    else
                    {
                        bytes -= entryBuffer.ReadableBytes;
                        toReturn = toReturn == null ? ComposeFirst(alloc, entryBuffer)
                                                    : Compose(alloc, toReturn, entryBuffer);
                    }
                    entryBuffer = null;
                }
            }
            catch (Exception cause)
            {
                ReferenceCountUtil.SafeRelease(entryBuffer);
                ReferenceCountUtil.SafeRelease(toReturn);
                aggregatePromise.SetException(cause);
                throw cause;
            }
            DecrementReadableBytes(originalBytes - bytes);
            return toReturn;
        }

        /// <summary>
        /// The number of readable bytes.
        /// </summary>
        public int ReadableBytes()
        {
            return this.readableBytes;
        }

        /// <summary>
        /// Are there pending buffers in the queue.
        /// </summary>
        public bool IsEmpty()
        {
            return this.bufAndListenerPairs.IsEmpty;
        }

        /// <summary>
        /// Release all buffers in the queue and complete all listeners and promises.
        /// </summary>
        public void ReleaseAndFailAll(Exception cause)
        {
            ReleaseAndCompleteAll(TaskUtil.FromException(cause));
        }

        /// <summary>
        /// Copy all pending entries in this queue into the destination queue.
        /// </summary>
        /// <param name="dest">to copy pending buffers to.</param>
        public void CopyTo(AbstractCoalescingBufferQueue dest)
        {
            foreach (var item in this.bufAndListenerPairs)
            {
                dest.bufAndListenerPairs.AddToBack(item);
            }
            dest.IncrementReadableBytes(this.readableBytes);
        }

        /// <summary>
        /// Writes all remaining elements in this queue.
        /// </summary>
        /// <param name="ctx">The context to write all elements to.</param>
        public void WriteAndRemoveAll(IChannelHandlerContext ctx)
        {
            DecrementReadableBytes(this.readableBytes);
            Exception pending = null;
            IByteBuffer previousBuf = null;
            while (true)
            {
                bufAndListenerPairs.TryRemoveFromFront(out var entry);
                try
                {
                    if (entry == null)
                    {
                        if (previousBuf is object)
                        {
                            ctx.WriteAsync(previousBuf, ctx.VoidPromise());
                        }
                        break;
                    }

                    if (entry is IByteBuffer byteBuffer)
                    {
                        if (previousBuf is object)
                        {
                            ctx.WriteAsync(previousBuf, ctx.VoidPromise());
                        }
                        previousBuf = byteBuffer;
                    }
                    else if (entry is IPromise)
                    {
                        ctx.WriteAsync(previousBuf, (IPromise)entry);
                        previousBuf = null;
                    }
                    //else
                    //{
                    //    // todo
                    //    //ctx.WriteAsync(previousBuf).addListener((ChannelFutureListener)entry);
                    //    previousBuf = null;
                    //}
                }
                catch (Exception t)
                {
                    if (pending == null)
                    {
                        pending = t;
                    }
                    else
                    {
                        if (Logger.InfoEnabled)                        {                            Logger.ThrowableBeingSuppressedBecauseIsAlreadyPending(pending, t);                        }
                    }
                }
            }
            if (pending is object)
            {
                ThrowHelper.ThrowInvalidOperationException_CoalescingBufferQueuePending(pending);
            }
        }

        /// <summary>
        /// Calculate the result of {@code current + next}.
        /// </summary>
        /// <param name="alloc"></param>
        /// <param name="cumulation"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        protected abstract IByteBuffer Compose(IByteBufferAllocator alloc, IByteBuffer cumulation, IByteBuffer next);

        /// <summary>
        /// Compose <paramref name="cumulation"/> and <paramref name="next"/> into a new <see cref="CompositeByteBuffer"/>.
        /// </summary>
        /// <param name="alloc"></param>
        /// <param name="cumulation"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        protected IByteBuffer ComposeIntoComposite(IByteBufferAllocator alloc, IByteBuffer cumulation, IByteBuffer next)
        {
            // Create a composite buffer to accumulate this pair and potentially all the buffers
            // in the queue. Using +2 as we have already dequeued current and next.
            var composite = alloc.CompositeBuffer(Size() + 2);
            try
            {
                composite.AddComponent(true, cumulation);
                composite.AddComponent(true, next);
            }
            catch (Exception cause)
            {
                composite.Release();
                ReferenceCountUtil.SafeRelease(next);
                throw cause;
            }
            return composite;
        }

        /// <summary>
        /// Compose <paramref name="cumulation"/> and <paramref name="next"/> into a new <see cref="IByteBufferAllocator.Buffer(int)"/>.
        /// </summary>
        /// <param name="alloc">The allocator to use to allocate the new buffer.</param>
        /// <param name="cumulation">The current cumulation.</param>
        /// <param name="next">The next buffer.</param>
        /// <returns>The result of <code>cumulation + next</code>.</returns>
        protected IByteBuffer CopyAndCompose(IByteBufferAllocator alloc, IByteBuffer cumulation, IByteBuffer next)
        {
            var newCumulation = alloc.Buffer(cumulation.ReadableBytes + next.ReadableBytes);
            try
            {
                newCumulation.WriteBytes(cumulation).WriteBytes(next);
            }
            catch (Exception cause)
            {
                newCumulation.Release();
                ReferenceCountUtil.SafeRelease(next);
                throw cause;
            }
            cumulation.Release();
            next.Release();
            return newCumulation;
        }

        /// <summary>
        /// Calculate the first <see cref="IByteBuffer"/> which will be used in subsequent calls to
        /// <see cref="Compose(IByteBufferAllocator, IByteBuffer, IByteBuffer)"/>
        /// </summary>
        /// <param name="allocator"></param>
        /// <param name="first"></param>
        /// <returns></returns>
        protected virtual IByteBuffer ComposeFirst(IByteBufferAllocator allocator, IByteBuffer first)
        {
            return first;
        }

        /// <summary>
        /// The value to return when <see cref="Remove(IByteBufferAllocator, int, IPromise)"/> is called but the queue is empty.
        /// </summary>
        /// <returns>the <see cref="IByteBuffer"/> which represents an empty queue.</returns>
        protected abstract IByteBuffer RemoveEmptyValue();

        /// <summary>
        /// Get the number of elements in this queue added via one of the <see cref="Add(IByteBuffer)"/> methods.
        /// </summary>
        /// <returns>the number of elements in this queue.</returns>
        protected int Size()
        {
            return this.bufAndListenerPairs.Count;
        }

        private void ReleaseAndCompleteAll(Task future)
        {
            DecrementReadableBytes(this.readableBytes);
            Exception pending = null;
            while (bufAndListenerPairs.TryRemoveFromFront(out var entry))
            {
                try
                {
                    if (entry is IByteBuffer)
                    {
                        ReferenceCountUtil.SafeRelease(entry);
                    }
                    else
                    {
                        future.CascadeTo((IPromise)entry, Logger);
                    }
                }
                catch (Exception t)
                {
                    if (pending == null)
                    {
                        pending = t;
                    }
                    else
                    {
                        if (Logger.InfoEnabled) { Logger.ThrowableBeingSuppressedBecauseIsAlreadyPending(pending, t); }
                    }
                }
            }
            if (pending is object)
            {
                ThrowHelper.ThrowInvalidOperationException_CoalescingBufferQueuePending(pending);
            }
        }

        private void IncrementReadableBytes(int increment)
        {
            int nextReadableBytes = readableBytes + increment;
            if (nextReadableBytes < readableBytes)
            {
                ThrowHelper.ThrowInvalidOperationException_BufferQueueLengthOverflow(readableBytes, increment);
            }
            readableBytes = nextReadableBytes;
            if (tracker is object)
            {
                tracker.IncrementPendingOutboundBytes(increment);
            }
        }

        private void DecrementReadableBytes(int decrement)
        {
            this.readableBytes -= decrement;
            Debug.Assert(this.readableBytes >= 0);
            if (tracker is object)
            {
                tracker.DecrementPendingOutboundBytes(decrement);
            }
        }
    }
}
