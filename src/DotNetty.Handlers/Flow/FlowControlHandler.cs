// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Flow
{
    using DotNetty.Common;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /**
     * The {@link FlowControlHandler} ensures that only one message per {@code read()} is sent downstream.
     *
     * Classes such as {@link ByteToMessageDecoder} or {@link MessageToByteEncoder} are free to emit as
     * many events as they like for any given input. A channel's auto reading configuration doesn't usually
     * apply in these scenarios. This is causing problems in downstream {@link ChannelHandler}s that would
     * like to hold subsequent events while they're processing one event. It's a common problem with the
     * {@code HttpObjectDecoder} that will very often fire an {@code HttpRequest} that is immediately followed
     * by a {@code LastHttpContent} event.
     *
     * <pre>{@code
     * ChannelPipeline pipeline = ...;
     *
     * pipeline.addLast(new HttpServerCodec());
     * pipeline.addLast(new FlowControlHandler());
     *
     * pipeline.addLast(new MyExampleHandler());
     *
     * class MyExampleHandler extends ChannelInboundHandlerAdapter {
     *   @Override
     *   public void channelRead(IChannelHandlerContext ctx, Object msg) {
     *     if (msg instanceof HttpRequest) {
     *       ctx.channel().config().setAutoRead(false);
     *
     *       // The FlowControlHandler will hold any subsequent events that
     *       // were emitted by HttpObjectDecoder until auto reading is turned
     *       // back on or Channel#read() is being called.
     *     }
     *   }
     * }
     * }</pre>
     *
     * @see ChannelConfig#setAutoRead(bool)
     */
    public class FlowControlHandler : ChannelDuplexHandler
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<FlowControlHandler>();

        static readonly ThreadLocalPool<RecyclableQueue> Recycler = new ThreadLocalPool<RecyclableQueue>(h => new RecyclableQueue(h));

        readonly bool _releaseMessages;

        RecyclableQueue _queue;

        IChannelConfiguration _config;

        bool _shouldConsume;

        public FlowControlHandler()
            : this(true)
        {
        }

        public FlowControlHandler(bool releaseMessages)
        {
            _releaseMessages = releaseMessages;
        }

        /**
         * Determine if the underlying {@link Queue} is empty. This method exists for
         * testing, debugging and inspection purposes and it is not Thread safe!
         */
        public bool IsQueueEmpty => _queue is null || _queue.IsEmpty;

        /**
         * Releases all messages and destroys the {@link Queue}.
         */
        void Destroy()
        {
            if (_queue is object)
            {
                if (_queue.NonEmpty)
                {
                    if (Logger.TraceEnabled) Logger.NonEmptyQueue(_queue);

                    if (_releaseMessages)
                    {
                        while (_queue.TryDequeue(out object msg))
                        {
                            ReferenceCountUtil.SafeRelease(msg);
                        }
                    }
                }

                _queue.Recycle();
                _queue = null;
            }
        }

        public override void HandlerAdded(IChannelHandlerContext ctx)
        {
            _config = ctx.Channel.Configuration;
        }

        public override void ChannelInactive(IChannelHandlerContext ctx)
        {
            Destroy();
            ctx.FireChannelInactive();
        }

        public override void Read(IChannelHandlerContext ctx)
        {
            if (0u >= (uint)Dequeue(ctx, 1))
            {
                // It seems no messages were consumed. We need to read() some
                // messages from upstream and once one arrives it need to be
                // relayed to downstream to keep the flow going.
                _shouldConsume = true;
                ctx.Read();
            }
        }

        public override void ChannelRead(IChannelHandlerContext ctx, object msg)
        {
            if (_queue is null)
            {
                _queue = Recycler.Take();
            }

            _queue.TryEnqueue(msg);

            // We just received one message. Do we need to relay it regardless
            // of the auto reading configuration? The answer is yes if this
            // method was called as a result of a prior read() call.
            int minConsume = _shouldConsume ? 1 : 0;
            _shouldConsume = false;

            Dequeue(ctx, minConsume);
        }

        public override void ChannelReadComplete(IChannelHandlerContext ctx)
        {
            if (IsQueueEmpty)
            {
                ctx.FireChannelReadComplete();
            }
            else
            {
                // Don't relay completion events from upstream as they
                // make no sense in this context. See dequeue() where
                // a new set of completion events is being produced.
            }
        }

        /**
         * Dequeues one or many (or none) messages depending on the channel's auto
         * reading state and returns the number of messages that were consumed from
         * the internal queue.
         *
         * The {@code minConsume} argument is used to force {@code dequeue()} into
         * consuming that number of messages regardless of the channel's auto
         * reading configuration.
         *
         * @see #read(ChannelHandlerContext)
         * @see #channelRead(ChannelHandlerContext, Object)
         */
        int Dequeue(IChannelHandlerContext ctx, int minConsume)
        {
            int consumed = 0;

            // fireChannelRead(...) may call ctx.read() and so this method may reentrance. Because of this we need to
            // check if queue was set to null in the meantime and if so break the loop.
            while (_queue is object && (consumed < minConsume || _config.AutoRead))
            {
                if (!_queue.TryDequeue(out object msg) || msg is null) { break; }

                ++consumed;
                ctx.FireChannelRead(msg);
            }

            // We're firing a completion event every time one (or more)
            // messages were consumed and the queue ended up being drained
            // to an empty state.
            if (_queue is object && _queue.IsEmpty)
            {
                _queue.Recycle();
                _queue = null;

                if (consumed > 0) { ctx.FireChannelReadComplete(); }
            }

            return consumed;
        }
    }

    sealed class RecyclableQueue : CompatibleConcurrentQueue<object>
    {
        readonly ThreadLocalPool.Handle _handle;

        internal RecyclableQueue(ThreadLocalPool.Handle handle)
        {
            _handle = handle;
        }

        public void Recycle()
        {
            ((IQueue<object>)this).Clear();
            _handle.Release(this);
        }
    }
}