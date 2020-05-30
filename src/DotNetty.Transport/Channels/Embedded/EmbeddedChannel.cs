// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Embedded
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Runtime.ExceptionServices;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    public class EmbeddedChannel : AbstractChannel<EmbeddedChannel, EmbeddedChannel.WrappingEmbeddedUnsafe>
    {
        static readonly EndPoint LOCAL_ADDRESS = new EmbeddedSocketAddress();
        static readonly EndPoint REMOTE_ADDRESS = new EmbeddedSocketAddress();

        enum State
        {
            Open,
            Active,
            Closed
        };

        static readonly IChannelHandler[] EMPTY_HANDLERS = EmptyArray<IChannelHandler>.Instance;

        static readonly IInternalLogger logger = InternalLoggerFactory.GetInstance<EmbeddedChannel>();

        static readonly ChannelMetadata METADATA_NO_DISCONNECT = new ChannelMetadata(false);
        static readonly ChannelMetadata METADATA_DISCONNECT = new ChannelMetadata(true);

        readonly EmbeddedEventLoop loop = new EmbeddedEventLoop();

        readonly QueueX<object> inboundMessages = new QueueX<object>();
        readonly QueueX<object> outboundMessages = new QueueX<object>();
        Exception lastException;
        State state;

        /// <summary>
        ///     Create a new instance with an empty pipeline.
        /// </summary>
        public EmbeddedChannel()
            : this(EmbeddedChannelId.Instance, EMPTY_HANDLERS)
        {
        }

        /// <summary>
        ///     Create a new instance with the pipeline initialized with the specified handlers.
        /// </summary>
        /// <param name="handlers">
        ///     The <see cref="IChannelHandler" />s that will be added to the <see cref="IChannelPipeline" />
        /// </param>
        public EmbeddedChannel(params IChannelHandler[] handlers)
            : this(EmbeddedChannelId.Instance, handlers)
        {
        }
        public EmbeddedChannel(bool hasDisconnect, params IChannelHandler[] handlers)
            : this(EmbeddedChannelId.Instance, hasDisconnect, handlers)
        {
        }

        public EmbeddedChannel(bool hasDisconnect, bool register, params IChannelHandler[] handlers)
            : this(EmbeddedChannelId.Instance, hasDisconnect, register, handlers)
        {
        }

        /// <summary>
        ///     Create a new instance with an empty pipeline with the specified <see cref="IChannelId" />.
        /// </summary>
        /// <param name="channelId">The <see cref="IChannelId" /> of this channel. </param>
        public EmbeddedChannel(IChannelId channelId)
            : this(channelId, EMPTY_HANDLERS)
        {
        }

        public EmbeddedChannel(IChannelId id, params IChannelHandler[] handlers)
            : this(id, false, handlers)
        {
        }

        /// <summary>Create a new instance with the pipeline initialized with the specified handlers.</summary>
        /// <param name="id">The <see cref="IChannelId" /> of this channel.</param>
        /// <param name="hasDisconnect">
        ///     <c>false</c> if this <see cref="IChannel" /> will delegate <see cref="DisconnectAsync()" />
        ///     to <see cref="CloseAsync()" />, <c>true</c> otherwise.
        /// </param>
        /// <param name="handlers">
        ///     The <see cref="IChannelHandler" />s that will be added to the <see cref="IChannelPipeline" />
        /// </param>
        public EmbeddedChannel(IChannelId id, bool hasDisconnect, params IChannelHandler[] handlers)
            : this(id, hasDisconnect, true, handlers)
        {
        }

        public EmbeddedChannel(IChannelId id, bool hasDisconnect, bool register, params IChannelHandler[] handlers)
            : this(null, id, hasDisconnect, register, handlers)
        {
        }

        public EmbeddedChannel(IChannel parent, IChannelId id, bool hasDisconnect, bool register, params IChannelHandler[] handlers)
            : base(parent, id)
        {
            this.Metadata = GetMetadata(hasDisconnect);
            this.Configuration = new DefaultChannelConfiguration(this);
            this.Setup(register, handlers);
        }

        public EmbeddedChannel(IChannelId id, bool hasDisconnect, IChannelConfiguration config, params IChannelHandler[] handlers)
            : base(null, id)
        {
            if (config is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.config); }

            this.Metadata = GetMetadata(hasDisconnect);
            this.Configuration = config;
            this.Setup(true, handlers);
        }

        static ChannelMetadata GetMetadata(bool hasDisconnect) => hasDisconnect ? METADATA_DISCONNECT : METADATA_NO_DISCONNECT;

        void Setup(bool register, params IChannelHandler[] handlers)
        {
            if (handlers is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.handlers); }

            IChannelPipeline p = this.Pipeline;
            p.AddLast(new ActionChannelInitializer<IChannel>(channel =>
            {
                IChannelPipeline pipeline = channel.Pipeline;
                foreach (IChannelHandler h in handlers)
                {
                    if (h is null) { break; }
                    pipeline.AddLast(h);
                }
            }));

            if (register)
            {
                Task future = this.loop.RegisterAsync(this);
                Debug.Assert(future.IsCompleted);
            }
        }

        public void Register()
        {
            Task future = this.loop.RegisterAsync(this);
            Debug.Assert(future.IsCompleted);
            if (!future.IsSuccess())
            {
                throw future.Exception.InnerException;
            }
            this.Pipeline.AddLast(new LastInboundHandler(this));
        }

        protected sealed override DefaultChannelPipeline NewChannelPipeline() => new EmbeddedChannelPipeline(this);

        public override ChannelMetadata Metadata { get; }

        public override IChannelConfiguration Configuration { get; }

        /// <summary>
        ///     Returns the <see cref="Queue{T}" /> which holds all of the <see cref="object" />s that
        ///     were received by this <see cref="IChannel" />.
        /// </summary>
        public QueueX<object> InboundMessages => this.inboundMessages;

        /// <summary>
        ///     Returns the <see cref="Queue{T}" /> which holds all of the <see cref="object" />s that
        ///     were written by this <see cref="IChannel" />.
        /// </summary>
        public QueueX<object> OutboundMessages => this.outboundMessages;

        /// <summary>
        /// Return received data from this <see cref="IChannel"/>.
        /// </summary>
        public T ReadInbound<T>()
        {
#if DEBUG
            var message = (T)Poll(this.inboundMessages);
            if (message is object)
            {
                ReferenceCountUtil.Touch(message, "Caller of readInbound() will handle the message from this point");
            }
            return message;
#else
            return (T)Poll(this.inboundMessages);
#endif
        }

        /// <summary>
        /// Read data from the outbound. This may return <c>null</c> if nothing is readable.
        /// </summary>
        public T ReadOutbound<T>()
        {
#if DEBUG
            var message = (T)Poll(this.outboundMessages);
            if (message is object)
            {
                ReferenceCountUtil.Touch(message, "Caller of readOutbound() will handle the message from this point.");
            }
            return message;
#else
            return (T)Poll(this.outboundMessages);
#endif
        }

        protected override EndPoint LocalAddressInternal => this.Active ? LOCAL_ADDRESS : null;

        protected override EndPoint RemoteAddressInternal => this.Active ? REMOTE_ADDRESS : null;

        //protected override IChannelUnsafe NewUnsafe() => new EmbeddedUnsafe(this); ## 苦竹 屏蔽 ##

        protected override bool IsCompatible(IEventLoop eventLoop) => eventLoop is EmbeddedEventLoop;

        protected override void DoBind(EndPoint localAddress)
        {
            //NOOP
        }

        protected override void DoRegister() => this.state = State.Active;

        protected override void DoDisconnect()
        {
            if (!this.Metadata.HasDisconnect) { this.DoClose(); }
        }

        protected override void DoClose() => this.state = State.Closed;

        protected override void DoBeginRead()
        {
            //NOOP
        }

        protected override void DoWrite(ChannelOutboundBuffer input)
        {
            while (true)
            {
                object msg = input.Current;
                if (msg is null)
                {
                    break;
                }

                ReferenceCountUtil.Retain(msg);
                this.HandleOutboundMessage(msg);
                input.Remove();
            }
        }

        public override bool Open => this.state != State.Closed;

        public override bool Active => this.state == State.Active;

        /// <summary>
        ///     Run all tasks (which also includes scheduled tasks) that are pending in the <see cref="IEventLoop" />
        ///     for this <see cref="IChannel" />.
        /// </summary>
        public void RunPendingTasks()
        {
            try
            {
                this.loop.RunTasks();
            }
            catch (Exception ex)
            {
                this.RecordException(ex);
            }

            try
            {
                this.loop.RunScheduledTasks();
            }
            catch (Exception ex)
            {
                this.RecordException(ex);
            }
        }

        /// <summary>
        ///     Run all pending scheduled tasks in the <see cref="IEventLoop" /> for this <see cref="IChannel" />.
        /// </summary>
        /// <returns>
        ///     The <see cref="PreciseTimeSpan" /> when the next scheduled task is ready to run. If no other task is
        ///     scheduled then it will return <see cref="PreciseTimeSpan.Zero" />.
        /// </returns>
        public PreciseTimeSpan RunScheduledPendingTasks()
        {
            try
            {
                return this.loop.RunScheduledTasks();
            }
            catch (Exception ex)
            {
                this.RecordException(ex);
                return this.loop.NextScheduledTask();
            }
        }

        /// <summary>
        ///     Write messages to the inbound of this <see cref="IChannel" />
        /// </summary>
        /// <param name="msgs">The messages to be written.</param>
        /// <returns><c>true</c> if the write operation did add something to the inbound buffer</returns>
        public bool WriteInbound(params object[] msgs)
        {
            this.EnsureOpen();
            if (0u >= (uint)msgs.Length)
            {
                return this.inboundMessages.NonEmpty;
            }

            IChannelPipeline p = this.Pipeline;
            foreach (object m in msgs)
            {
                p.FireChannelRead(m);
            }

            this.FlushInbound(false, this.VoidPromise());
            return this.inboundMessages.NonEmpty;
        }

        public Task WriteOneInbound(object msg) => this.WriteOneInbound(msg, this.NewPromise());

        public Task WriteOneInbound(object msg, IPromise promise)
        {
            if (this.CheckOpen(true))
            {
                this.Pipeline.FireChannelRead(msg);
            }
            this.CheckException(promise);
            return promise.Task;
        }

        /// <summary>Flushes the inbound of this <see cref="IChannel"/>. This method is conceptually equivalent to Flush.</summary>
        public EmbeddedChannel FlushInbound()
        {
            this.FlushInbound(true, this.VoidPromise());
            return this;
        }

        /// <summary>Flushes the inbound of this <see cref="IChannel"/>. This method is conceptually equivalent to Flush.</summary>
        /// <returns></returns>
        public void FlushInbound(bool recordException, IPromise promise)
        {
            if (this.CheckOpen(recordException))
            {
                this.Pipeline.FireChannelReadComplete();
                this.RunPendingTasks();
            }

            this.CheckException(promise);
        }

        /// <summary>
        ///     Write messages to the outbound of this <see cref="IChannel" />.
        /// </summary>
        /// <param name="msgs">The messages to be written.</param>
        /// <returns><c>true</c> if the write operation did add something to the inbound buffer</returns>
        public bool WriteOutbound(params object[] msgs)
        {
            this.EnsureOpen();
            if (0u >= (uint)msgs.Length)
            {
                return this.outboundMessages.NonEmpty;
            }

            ThreadLocalObjectList futures = ThreadLocalObjectList.NewInstance(msgs.Length);

            try
            {
                foreach (object m in msgs)
                {
                    if (m is null)
                    {
                        break;
                    }
                    futures.Add(this.WriteAsync(m));
                }

                this.FlushOutbound0();

                int size = futures.Count;
                for (int i = 0; i < size; i++)
                {
                    var future = (Task)futures[i];
                    if (future.IsCompleted)
                    {
                        this.RecordException(future);
                    }
                    else
                    {
                        // The write may be delayed to run later by RunPendingTasks()
                        future.ContinueWith(t => this.RecordException(t));
                    }
                }

                this.CheckException();
                return this.outboundMessages.NonEmpty;
            }
            finally
            {
                futures.Return();
            }
        }

        void RecordException(Task future)
        {
            if (future.IsCanceled || future.IsFaulted)
            {
                this.RecordException(future.Exception);
            }
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        void RecordException(Exception cause)
        {
            if (this.lastException is null)
            {
                this.lastException = cause;
            }
            else
            {
                logger.Warn("More than one exception was raised. " + "Will report only the first one and log others.", cause);
            }
        }

        /// <summary>
        /// Writes one message to the outbound of this <see cref="IChannel"/> and does not flush it. This
        /// method is conceptually equivalent to WriteAsync.
        /// </summary>
        public Task WriteOneOutbound(object msg) => this.WriteOneOutbound(msg, this.NewPromise());

        public Task WriteOneOutbound(object msg, IPromise promise)
        {
            if (this.CheckOpen(true))
            {
                return this.WriteAsync(msg, promise);
            }
            this.CheckException(promise);
            return promise.Task;
        }

        /// <summary>Flushes the outbound of this <see cref="IChannel"/>.
        /// This method is conceptually equivalent to <see cref="Finish()"/>.</summary>
        /// <returns></returns>
        public EmbeddedChannel FlushOutbound()
        {
            if (this.CheckOpen(true))
            {
                this.FlushOutbound0();
            }
            this.CheckException(this.VoidPromise());
            return this;
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        void FlushOutbound0()
        {
            // We need to call RunPendingTasks first as a IChannelHandler may have used IEventLoop.Execute(...) to
            // delay the write on the next event loop run.
            this.RunPendingTasks();

            this.Flush();
        }

        /// <summary>
        ///     Mark this <see cref="IChannel" /> as finished. Any further try to write data to it will fail.
        /// </summary>
        /// <returns>bufferReadable returns <c>true</c></returns>
        public bool Finish() => this.Finish(false);

        /// <summary>
        /// Marks this <see cref="IChannel"/> as finished and releases all pending message in the inbound and outbound
        /// buffer. Any futher try to write data to it will fail.
        /// </summary>
        /// <returns><c>true</c> if any of the used buffers has something left to read, otherwise <c>false</c>.</returns>
        public bool FinishAndReleaseAll() => this.Finish(true);

        /// <summary>
        /// Marks this <see cref="IChannel"/> as finished. Any futher attempt to write data to it will fail.
        /// </summary>
        /// <param name="releaseAll">If <c>true</c>, all pending messages in the inbound and outbound buffer are released.</param>
        /// <returns><c>true</c> if any of the used buffers has something left to read, otherwise <c>false</c>.</returns>
        bool Finish(bool releaseAll)
        {
            this.CloseSafe();
            try
            {
                this.CheckException();
                return this.inboundMessages.NonEmpty || this.outboundMessages.NonEmpty;
            }
            finally
            {
                if (releaseAll)
                {
                    ReleaseAll(this.inboundMessages);
                    ReleaseAll(this.outboundMessages);
                }
            }
        }

        /// <summary>
        /// Releases all buffered inbound messages.
        /// </summary>
        /// <returns><c>true</c> if any were in the inbound buffer, otherwise <c>false</c>.</returns>
        public bool ReleaseInbound() => ReleaseAll(this.inboundMessages);

        /// <summary>
        /// Releases all buffered outbound messages.
        /// </summary>
        /// <returns><c>true</c> if any were in the outbound buffer, otherwise <c>false</c>.</returns>
        public bool ReleaseOutbound() => ReleaseAll(this.outboundMessages);

        static bool ReleaseAll(QueueX<object> queue)
        {
            if (queue.IsEmpty) { return false; }

            while (queue.TryDequeue(out var msg))
            {
                ReferenceCountUtil.Release(msg);
            }
            return true;
        }

        void FinishPendingTasks(bool cancel)
        {
            this.RunPendingTasks();
            if (cancel)
            {
                // Cancel all scheduled tasks that are left.
                this.loop.CancelScheduledTasks();
            }
        }

        public override Task CloseAsync()
        {
            return this.CloseAsync(this.NewPromise());
        }

        public override Task CloseAsync(IPromise promise)
        {
            // We need to call RunPendingTasks() before calling super.CloseAsync() as there may be something in the queue
            // that needs to be run before the actual close takes place.
            this.RunPendingTasks();
            Task future = base.CloseAsync(promise);

            // Now finish everything else and cancel all scheduled tasks that were not ready set.
            this.FinishPendingTasks(true);
            return future;
        }

        public override Task DisconnectAsync()
        {
            return this.DisconnectAsync(this.NewPromise());
        }

        public override Task DisconnectAsync(IPromise promise)
        {
            Task future = base.DisconnectAsync(promise);
            this.FinishPendingTasks(!this.Metadata.HasDisconnect);
            return future;
        }

        /// <summary>
        ///     Check to see if there was any <see cref="Exception" /> and rethrow if so.
        /// </summary>
        public void CheckException(IPromise promise)
        {
            Exception e = this.lastException;
            if (e is null)
            {
                promise.TryComplete();
                return;
            }

            this.lastException = null;
            if (promise.IsVoid)
            {
                ExceptionDispatchInfo.Capture(e).Throw();
            }
            promise.TrySetException(e);
        }

        public void CheckException()
        {
            this.CheckException(this.VoidPromise());
        }

        /// <summary>Returns <c>true</c> if the <see cref="IChannel" /> is open and records optionally
        /// an <see cref="Exception" /> if it isn't.</summary>
        /// <param name="recordException"></param>
        /// <returns></returns>
        [MethodImpl(InlineMethod.AggressiveInlining)]
        bool CheckOpen(bool recordException)
        {
            if (!this.Open)
            {
                if (recordException)
                {
                    this.RecordException(ThrowHelper.GetClosedChannelException());
                }
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Ensure the <see cref="IChannel" /> is open and if not throw an exception.
        /// </summary>
        protected void EnsureOpen()
        {
            if (!this.CheckOpen(true))
            {
                this.CheckException();
            }
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        static object Poll(QueueX<object> queue)
        {
            queue.TryDequeue(out var result);
            return result;
        }

        /// <summary>Called for each outbound message.</summary>
        /// <param name="msg"></param>
        protected virtual void HandleOutboundMessage(object msg)
        {
            this.outboundMessages.Enqueue(msg);
        }

        /// <summary>Called for each inbound message.</summary>
        /// <param name="msg"></param>
        protected virtual void HandleInboundMessage(object msg)
        {
            this.inboundMessages.Enqueue(msg);
        }

        protected override WrappingEmbeddedUnsafe NewUnsafe()
        {
            var @unsafe = new WrappingEmbeddedUnsafe();
            @unsafe.Initialize(this);
            return @unsafe;
        }

        public sealed class EmbeddedUnsafe : AbstractUnsafe
        {
            public EmbeddedUnsafe() //AbstractChannel channel)
                : base() //channel)
            {
            }

            public override Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress) => TaskUtil.Completed;
        }

        public sealed class WrappingEmbeddedUnsafe : IChannelUnsafe
        {
            EmbeddedUnsafe innerUnsafe;
            EmbeddedChannel embeddedChannel;

            public WrappingEmbeddedUnsafe() { }

            public void Initialize(IChannel channel)
            {
                this.embeddedChannel = (EmbeddedChannel)channel;
                this.innerUnsafe = new EmbeddedUnsafe();
                this.innerUnsafe.Initialize(embeddedChannel);
            }

            public IRecvByteBufAllocatorHandle RecvBufAllocHandle => this.innerUnsafe.RecvBufAllocHandle;

            public ChannelOutboundBuffer OutboundBuffer => this.innerUnsafe.OutboundBuffer;

            public void BeginRead()
            {
                this.innerUnsafe.BeginRead();
                this.embeddedChannel.RunPendingTasks();
            }

            public async Task RegisterAsync(IEventLoop eventLoop)
            {
                await this.innerUnsafe.RegisterAsync(eventLoop);
                this.embeddedChannel.RunPendingTasks();
            }

            public async Task BindAsync(EndPoint localAddress)
            {
                await this.innerUnsafe.BindAsync(localAddress);
                this.embeddedChannel.RunPendingTasks();
            }

            public async Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
            {
                await this.innerUnsafe.ConnectAsync(remoteAddress, localAddress);
                this.embeddedChannel.RunPendingTasks();
            }

            public void Disconnect(IPromise promise)
            {
                this.innerUnsafe.Disconnect(promise);
                this.embeddedChannel.RunPendingTasks();
            }

            public void Close(IPromise promise)
            {
                this.innerUnsafe.Close(promise);
                this.embeddedChannel.RunPendingTasks();
            }

            public void CloseForcibly()
            {
                this.innerUnsafe.CloseForcibly();
                this.embeddedChannel.RunPendingTasks();
            }

            public void Deregister(IPromise promise)
            {
                this.innerUnsafe.Deregister(promise);
                this.embeddedChannel.RunPendingTasks();
            }

            public void Write(object message, IPromise promise)
            {
                this.innerUnsafe.Write(message, promise);
                this.embeddedChannel.RunPendingTasks();
            }

            public void Flush()
            {
                this.innerUnsafe.Flush();
                this.embeddedChannel.RunPendingTasks();
            }

            public IPromise VoidPromise()
            {
                return this.innerUnsafe.VoidPromise();
            }
        }

        internal sealed class LastInboundHandler : ChannelHandlerAdapter
        {
            readonly EmbeddedChannel embeddedChannel;

            public LastInboundHandler(EmbeddedChannel channel)
            {
                this.embeddedChannel = channel;
            }

            public override void ChannelRead(IChannelHandlerContext context, object message) => this.embeddedChannel.HandleInboundMessage(message);

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) => this.embeddedChannel.RecordException(exception);
        }

        sealed class EmbeddedChannelPipeline : DefaultChannelPipeline
        {
            readonly EmbeddedChannel embeddedChannel;
            public EmbeddedChannelPipeline(EmbeddedChannel channel)
                : base(channel)
            {
                this.embeddedChannel = channel;
            }

            protected override void OnUnhandledInboundException(Exception cause) => this.embeddedChannel.RecordException(cause);

            protected override void OnUnhandledInboundMessage(IChannelHandlerContext ctx, object msg) => this.embeddedChannel.HandleInboundMessage(msg);
        }
    }
}