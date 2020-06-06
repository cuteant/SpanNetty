// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;

    /// <summary>
    /// A FIFO queue of bytes where producers add bytes by repeatedly adding <see cref="IByteBuffer"/> and consumers take bytes in
    /// arbitrary lengths. This allows producers to add lots of small buffers and the consumer to take all the bytes
    /// out in a single buffer. Conversely the producer may add larger buffers and the consumer could take the bytes in
    /// many small buffers.
    /// <para>
    /// Bytes are added and removed with promises. If the last byte of a buffer added with a promise is removed then
    /// that promise will complete when the promise passed to <see cref="Remove(int, IPromise)"/> completes.
    /// </para>
    /// <para>This functionality is useful for aggregating or partitioning writes into fixed size buffers for framing protocols
    /// such as HTTP2.</para>
    /// </summary>
    public sealed class CoalescingBufferQueue : AbstractCoalescingBufferQueue
    {
        readonly IChannel _channel;

        public CoalescingBufferQueue(IChannel channel)
            : this(channel, 4)
        {
        }

        public CoalescingBufferQueue(IChannel channel, int initSize)
            : this(channel, initSize, false)
        {
        }

        public CoalescingBufferQueue(IChannel channel, int initSize, bool updateWritability)
            : base(updateWritability ? channel : null, initSize)
        {
            if (channel is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.channel); }
            _channel = channel;
        }

        /// <summary>
        /// Remove a <see cref="IByteBuffer"/> from the queue with the specified number of bytes. Any added buffer who's bytes are
        /// fully consumed during removal will have it's promise completed when the passed aggregate <see cref="IPromise"/>
        /// completes.
        /// </summary>
        /// <param name="bytes">the maximum number of readable bytes in the returned <see cref="IByteBuffer"/>, if <paramref name="bytes"/> is greater
        /// than <see cref="AbstractCoalescingBufferQueue.ReadableBytes"/> then a buffer of length <see cref="AbstractCoalescingBufferQueue.ReadableBytes"/> is returned.</param>
        /// <param name="aggregatePromise">used to aggregate the promises and listeners for the constituent buffers.</param>
        /// <returns>a <see cref="IByteBuffer"/> composed of the enqueued buffers.</returns>
        public IByteBuffer Remove(int bytes, IPromise aggregatePromise)
        {
            return Remove(_channel.Allocator, bytes, aggregatePromise);
        }

        protected override IByteBuffer Compose(IByteBufferAllocator alloc, IByteBuffer cumulation, IByteBuffer next)
        {
            if (cumulation is CompositeByteBuffer composite)
            {
                composite.AddComponent(true, next);
                return composite;
            }
            return ComposeIntoComposite(alloc, cumulation, next);
        }

        protected override IByteBuffer RemoveEmptyValue()
        {
            return Unpooled.Empty;
        }
    }
}
