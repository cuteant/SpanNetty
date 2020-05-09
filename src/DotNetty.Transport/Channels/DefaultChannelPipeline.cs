// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using CuteAnt.Pool;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using Thread = DotNetty.Common.Concurrency.XThread;

    public partial class DefaultChannelPipeline : IChannelPipeline
    {
        internal static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<DefaultChannelPipeline>();

        static readonly Action<object, object> CallHandlerAddedAction = OnCallHandlerAdded; // (self, ctx) => ((DefaultChannelPipeline)self).CallHandlerAdded0((AbstractChannelHandlerContext)ctx);

        static readonly NameCachesLocal NameCaches = new NameCachesLocal();

        class NameCachesLocal : FastThreadLocal<ConditionalWeakTable<Type, string>>
        {
            protected override ConditionalWeakTable<Type, string> GetInitialValue() => new ConditionalWeakTable<Type, string>();
        }

        readonly IChannel channel;
        readonly VoidChannelPromise voidPromise;

        readonly AbstractChannelHandlerContext head;
        readonly AbstractChannelHandlerContext tail;

        readonly bool touch = ResourceLeakDetector.Enabled;

        private Dictionary<IEventExecutorGroup, IEventExecutor> childExecutors;
        private IMessageSizeEstimatorHandle estimatorHandle;
        private bool firstRegistration = true;

        /// <summary>
        /// This is the head of a linked list that is processed by <see cref="CallHandlerAddedForAllHandlers" /> and so
        /// process all the pending <see cref="CallHandlerAdded0" />. We only keep the head because it is expected that
        /// the list is used infrequently and its size is small. Thus full iterations to do insertions is assumed to be
        /// a good compromised to saving memory and tail management complexity.
        /// </summary>
        PendingHandlerCallback pendingHandlerCallbackHead;

        /// <summary>
        /// Set to <c>true</c> once the <see cref="AbstractChannel{TChannel, TUnsafe}" /> is registered. Once set to <c>true</c>, the
        /// value will never change.
        /// </summary>
        bool registered;

        public DefaultChannelPipeline(IChannel channel)
        {
            if (null == channel) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.channel); }

            this.channel = channel;
            this.voidPromise = new VoidChannelPromise(channel, true);

            this.tail = new TailContext(this);
            this.head = new HeadContext(this);

            this.head.Next = this.tail;
            this.tail.Prev = this.head;
        }

        internal IMessageSizeEstimatorHandle EstimatorHandle
        {
            get
            {
                var handle = Volatile.Read(ref this.estimatorHandle);
                if (null == handle)
                {
                    handle = this.channel.Configuration.MessageSizeEstimator.NewHandle();
                    var current = Interlocked.CompareExchange(ref this.estimatorHandle, handle, null);
                    if (current != null) { return current; }
                }
                return handle;
            }
        }

        internal object Touch(object msg, AbstractChannelHandlerContext next) => this.touch ? ReferenceCountUtil.Touch(msg, next) : msg;

        public IChannel Channel => this.channel;

        IEnumerator<IChannelHandler> IEnumerable<IChannelHandler>.GetEnumerator()
        {
            AbstractChannelHandlerContext current = this.head;
            while (current != null)
            {
                yield return current.Handler;
                current = current.Next;
            }
        }

        AbstractChannelHandlerContext NewContext(IEventExecutorGroup group, string name, IChannelHandler handler) => new DefaultChannelHandlerContext(this, this.GetChildExecutor(group), name, handler);

        AbstractChannelHandlerContext NewContext(IEventExecutor executor, string name, IChannelHandler handler) => new DefaultChannelHandlerContext(this, executor, name, handler);

        IEventExecutor GetChildExecutor(IEventExecutorGroup group)
        {
            if (group == null)
            {
                return null;
            }
            //var pinEventExecutor = channel.Configuration.GetOption(ChannelOption.SINGLE_EVENTEXECUTOR_PER_GROUP);
            //if (pinEventExecutor != null && !pinEventExecutor)
            //{
            //    return group.next();
            //}
            // Use size of 4 as most people only use one extra EventExecutor.
            Dictionary<IEventExecutorGroup, IEventExecutor> executorMap = this.childExecutors
                ?? (this.childExecutors = new Dictionary<IEventExecutorGroup, IEventExecutor>(4, ReferenceEqualityComparer.Default));

            // Pin one of the child executors once and remember it so that the same child executor
            // is used to fire events for the same channel.
            if (!executorMap.TryGetValue(group, out var childExecutor))
            {
                childExecutor = group.GetNext();
                executorMap[group] = childExecutor;
            }
            return childExecutor;
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<IChannelHandler>)this).GetEnumerator();

        public IChannelPipeline AddFirst(string name, IChannelHandler handler) => this.AddFirst(null, name, handler);

        public IChannelPipeline AddFirst(IEventExecutorGroup group, string name, IChannelHandler handler)
        {
            if (null == handler) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.handler); }

            AbstractChannelHandlerContext newCtx;
            lock (this)
            {
                CheckMultiplicity(handler);

                newCtx = this.NewContext(group, this.FilterName(name, handler), handler);

                this.AddFirst0(newCtx);

                // If the registered is false it means that the channel was not registered on an eventloop yet.
                // In this case we add the context to the pipeline and add a task that will call
                // ChannelHandler.handlerAdded(...) once the channel is registered.
                if (!this.registered)
                {
                    newCtx.SetAddPending();
                    this.CallHandlerCallbackLater(newCtx, true);
                    return this;
                }

                var executor = newCtx.Executor;
                if (!executor.InEventLoop)
                {
                    newCtx.SetAddPending();
                    executor.Execute(CallHandlerAddedAction, this, newCtx);
                    return this;
                }
            }

            this.CallHandlerAdded0(newCtx);
            return this;
        }

        void AddFirst0(AbstractChannelHandlerContext newCtx)
        {
            var nextCtx = this.head.Next;
            newCtx.Prev = this.head;
            newCtx.Next = nextCtx;
            this.head.Next = newCtx;
            nextCtx.Prev = newCtx;
        }

        public IChannelPipeline AddLast(string name, IChannelHandler handler) => this.AddLast(null, name, handler);

        public IChannelPipeline AddLast(IEventExecutorGroup group, string name, IChannelHandler handler)
        {
            if (null == handler) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.handler); }

            AbstractChannelHandlerContext newCtx;
            lock (this)
            {
                CheckMultiplicity(handler);

                newCtx = this.NewContext(group, this.FilterName(name, handler), handler);

                this.AddLast0(newCtx);

                // If the registered is false it means that the channel was not registered on an eventloop yet.
                // In this case we add the context to the pipeline and add a task that will call
                // ChannelHandler.handlerAdded(...) once the channel is registered.
                if (!this.registered)
                {
                    newCtx.SetAddPending();
                    this.CallHandlerCallbackLater(newCtx, true);
                    return this;
                }

                var executor = newCtx.Executor;
                if (!executor.InEventLoop)
                {
                    newCtx.SetAddPending();
                    executor.Execute(CallHandlerAddedAction, this, newCtx);
                    return this;
                }
            }
            this.CallHandlerAdded0(newCtx);
            return this;
        }

        void AddLast0(AbstractChannelHandlerContext newCtx)
        {
            var prev = this.tail.Prev;
            newCtx.Prev = prev;
            newCtx.Next = this.tail;
            prev.Next = newCtx;
            this.tail.Prev = newCtx;
        }

        public IChannelPipeline AddBefore(string baseName, string name, IChannelHandler handler) => this.AddBefore(null, baseName, name, handler);

        public IChannelPipeline AddBefore(IEventExecutorGroup group, string baseName, string name, IChannelHandler handler)
        {
            if (null == handler) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.handler); }

            AbstractChannelHandlerContext newCtx;
            lock (this)
            {
                CheckMultiplicity(handler);
                AbstractChannelHandlerContext ctx = this.GetContextOrThrow(baseName);

                newCtx = this.NewContext(group, this.FilterName(name, handler), handler);

                AddBefore0(ctx, newCtx);

                // If the registered is false it means that the channel was not registered on an eventloop yet.
                // In this case we add the context to the pipeline and add a task that will call
                // ChannelHandler.handlerAdded(...) once the channel is registered.
                if (!this.registered)
                {
                    newCtx.SetAddPending();
                    this.CallHandlerCallbackLater(newCtx, true);
                    return this;
                }

                var executor = newCtx.Executor;
                if (!executor.InEventLoop)
                {
                    newCtx.SetAddPending();
                    executor.Execute(CallHandlerAddedAction, this, newCtx);
                    return this;
                }
            }
            this.CallHandlerAdded0(newCtx);
            return this;
        }

        static void AddBefore0(AbstractChannelHandlerContext ctx, AbstractChannelHandlerContext newCtx)
        {
            var ctxPrev = ctx.Prev;
            newCtx.Prev = ctxPrev;
            newCtx.Next = ctx;
            ctxPrev.Next = newCtx;
            ctx.Prev = newCtx;
        }

        public IChannelPipeline AddAfter(string baseName, string name, IChannelHandler handler) => this.AddAfter(null, baseName, name, handler);

        public IChannelPipeline AddAfter(IEventExecutorGroup group, string baseName, string name, IChannelHandler handler)
        {
            if (null == handler) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.handler); }

            AbstractChannelHandlerContext newCtx;

            lock (this)
            {
                CheckMultiplicity(handler);
                AbstractChannelHandlerContext ctx = this.GetContextOrThrow(baseName);

                newCtx = this.NewContext(group, this.FilterName(name, handler), handler);

                AddAfter0(ctx, newCtx);

                // If the registered is false it means that the channel was not registered on an eventloop yet.
                // In this case we remove the context from the pipeline and add a task that will call
                // ChannelHandler.handlerRemoved(...) once the channel is registered.
                if (!this.registered)
                {
                    newCtx.SetAddPending();
                    this.CallHandlerCallbackLater(newCtx, true);
                    return this;
                }

                var executor = newCtx.Executor;
                if (!executor.InEventLoop)
                {
                    newCtx.SetAddPending();
                    executor.Execute(CallHandlerAddedAction, this, newCtx);
                    return this;
                }
            }
            this.CallHandlerAdded0(newCtx);
            return this;
        }

        static void AddAfter0(AbstractChannelHandlerContext ctx, AbstractChannelHandlerContext newCtx)
        {
            newCtx.Prev = ctx;
            var ctxNext = ctx.Next;
            newCtx.Next = ctxNext;
            ctxNext.Prev = newCtx;
            ctx.Next = newCtx;
        }

        public IChannelPipeline AddFirst(IChannelHandler handler) => this.AddFirst(group: null, name: null, handler: handler);

        public IChannelPipeline AddFirst(params IChannelHandler[] handlers) => this.AddFirst(null, handlers);

        public IChannelPipeline AddFirst(IEventExecutorGroup group, params IChannelHandler[] handlers)
        {
            if (null == handlers) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.handlers); }

            for (int i = handlers.Length - 1; i >= 0; i--)
            {
                IChannelHandler h = handlers[i];
                this.AddFirst(group: group, name: null, handler: h);
            }

            return this;
        }

        public IChannelPipeline AddLast(IChannelHandler handler) => this.AddLast(group: null, name: null, handler: handler);

        public IChannelPipeline AddLast(params IChannelHandler[] handlers) => this.AddLast(null, handlers);

        public IChannelPipeline AddLast(IEventExecutorGroup group, params IChannelHandler[] handlers)
        {
            foreach (IChannelHandler h in handlers)
            {
                this.AddLast(group: group, name: null, handler: h);
            }
            return this;
        }

        string GenerateName(IChannelHandler handler)
        {
            ConditionalWeakTable<Type, string> cache = NameCaches.Value;
            Type handlerType = handler.GetType();
            string name = cache.GetValue(handlerType, t => GenerateName0(t));

            // It's not very likely for a user to put more than one handler of the same type, but make sure to avoid
            // any name conflicts.  Note that we don't cache the names generated here.
            if (this.Context0(name) != null)
            {
                string baseName = name.Substring(0, name.Length - 1); // Strip the trailing '0'.
                for (int i = 1; ; i++)
                {
                    string newName = baseName + i;
                    if (this.Context0(newName) == null)
                    {
                        name = newName;
                        break;
                    }
                }
            }
            return name;
        }

        static string GenerateName0(Type handlerType) => StringUtil.SimpleClassName(handlerType) + "#0";

        public IChannelPipeline Remove(IChannelHandler handler)
        {
            this.Remove(this.GetContextOrThrow(handler));
            return this;
        }

        public IChannelHandler Remove(string name) => this.Remove(this.GetContextOrThrow(name)).Handler;

        public T Remove<T>() where T : class, IChannelHandler => (T)this.Remove(this.GetContextOrThrow<T>()).Handler;

        public T RemoveIfExists<T>(string name) where T : class, IChannelHandler
        {
            return this.RemoveIfExists<T>(this.Context(name));
        }

        public T RemoveIfExists<T>() where T : class, IChannelHandler
        {
            return this.RemoveIfExists<T>(this.Context<T>());
        }

        public T RemoveIfExists<T>(IChannelHandler handler) where T : class, IChannelHandler
        {
            return this.RemoveIfExists<T>(this.Context(handler));
        }

        T RemoveIfExists<T>(IChannelHandlerContext ctx) where T : class, IChannelHandler
        {
            if (ctx == null)
            {
                return null;
            }
            return (T)Remove((AbstractChannelHandlerContext)ctx).Handler;
        }

        AbstractChannelHandlerContext Remove(AbstractChannelHandlerContext ctx)
        {
            Debug.Assert(ctx != this.head && ctx != this.tail);

            lock (this)
            {
                Remove0(ctx);

                // If the registered is false it means that the channel was not registered on an eventloop yet.
                // In this case we remove the context from the pipeline and add a task that will call
                // ChannelHandler.handlerRemoved(...) once the channel is registered.
                if (!this.registered)
                {
                    this.CallHandlerCallbackLater(ctx, false);
                    return ctx;
                }
                var executor = ctx.Executor;
                if (!executor.InEventLoop)
                {
                    executor.Execute(CallHandlerRemovedAction, this, ctx);
                    return ctx;
                }
            }
            this.CallHandlerRemoved0(ctx);
            return ctx;
        }

        static void Remove0(AbstractChannelHandlerContext context)
        {
            var prev = context.Prev;
            var next = context.Next;
            prev.Next = next;
            next.Prev = prev;
        }

        public IChannelHandler RemoveFirst()
        {
            var headNext = this.head.Next;
            if (headNext == this.tail)
            {
                ThrowHelper.ThrowInvalidOperationException_Pipeline();
            }
            return this.Remove(headNext).Handler;
        }

        public IChannelHandler RemoveLast()
        {
            if (this.head.Next == this.tail)
            {
                ThrowHelper.ThrowInvalidOperationException_Pipeline();
            }
            return this.Remove(this.tail.Prev).Handler;
        }

        public IChannelPipeline Replace(IChannelHandler oldHandler, string newName, IChannelHandler newHandler)
        {
            this.Replace(this.GetContextOrThrow(oldHandler), newName, newHandler);
            return this;
        }

        public IChannelHandler Replace(string oldName, string newName, IChannelHandler newHandler) => this.Replace(this.GetContextOrThrow(oldName), newName, newHandler);

        public T Replace<T>(string newName, IChannelHandler newHandler)
            where T : class, IChannelHandler => (T)this.Replace(this.GetContextOrThrow<T>(), newName, newHandler);

        IChannelHandler Replace(AbstractChannelHandlerContext ctx, string newName, IChannelHandler newHandler)
        {
            if (null == newHandler) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.newHandler); }
            Debug.Assert(ctx != this.head && ctx != this.tail);

            AbstractChannelHandlerContext newCtx;
            lock (this)
            {
                CheckMultiplicity(newHandler);
                if (newName == null)
                {
                    newName = this.GenerateName(newHandler);
                }
                else
                {
                    bool sameName = string.Equals(ctx.Name, newName
#if NETCOREAPP_3_0_GREATER || NETSTANDARD_2_0_GREATER
                        );
#else
                        , StringComparison.Ordinal);
#endif
                    if (!sameName)
                    {
                        this.CheckDuplicateName(newName);
                    }
                }

                newCtx = this.NewContext(ctx.executor, newName, newHandler);

                Replace0(ctx, newCtx);

                // If the registered is false it means that the channel was not registered on an eventloop yet.
                // In this case we replace the context in the pipeline
                // and add a task that will signal handler it was added or removed
                // once the channel is registered.
                if (!this.registered)
                {
                    this.CallHandlerCallbackLater(newCtx, true);
                    this.CallHandlerCallbackLater(ctx, false);
                    return ctx.Handler;
                }

                var executor = ctx.Executor;
                if (!executor.InEventLoop)
                {
                    executor.Execute(() =>
                    {
                        // Indicate new handler was added first (i.e. before old handler removed)
                        // because "removed" will trigger ChannelRead() or Flush() on newHandler and
                        // those event handlers must be called after handler was signaled "added".
                        this.CallHandlerAdded0(newCtx);
                        this.CallHandlerRemoved0(ctx);
                    });
                    return ctx.Handler;
                }
            }
            // Indicate new handler was added first (i.e. before old handler removed)
            // because "removed" will trigger ChannelRead() or Flush() on newHandler and
            // those event handlers must be called after handler was signaled "added".
            this.CallHandlerAdded0(newCtx);
            this.CallHandlerRemoved0(ctx);
            return ctx.Handler;
        }

        static void Replace0(AbstractChannelHandlerContext oldCtx, AbstractChannelHandlerContext newCtx)
        {
            var prev = oldCtx.Prev;
            var next = oldCtx.Next;
            newCtx.Prev = prev;
            newCtx.Next = next;

            // Finish the replacement of oldCtx with newCtx in the linked list.
            // Note that this doesn't mean events will be sent to the new handler immediately
            // because we are currently at the event handler thread and no more than one handler methods can be invoked
            // at the same time (we ensured that in replace().)
            prev.Next = newCtx;
            next.Prev = newCtx;

            // update the reference to the replacement so forward of buffered content will work correctly
            oldCtx.Prev = newCtx;
            oldCtx.Next = newCtx;
        }

        static void CheckMultiplicity(IChannelHandler handler)
        {
            if (handler is ChannelHandlerAdapter adapter)
            {
                ChannelHandlerAdapter h = adapter;
                if (!h.IsSharable && h.Added)
                {
                    ThrowHelper.ThrowChannelPipelineException(h);
                }
                h.Added = true;
            }
        }

        void CallHandlerAdded0(AbstractChannelHandlerContext ctx)
        {
            try
            {
                ctx.CallHandlerAdded();
            }
            catch (Exception ex)
            {
                bool removed = false;
                try
                {
                    Remove0(ctx);
                    ctx.CallHandlerRemoved();
                    removed = true;
                }
                catch (Exception ex2)
                {
                    if (Logger.WarnEnabled)
                    {
                        Logger.FailedToRemoveAHandler(ctx, ex2);
                    }
                }

                if (removed)
                {
                    this.FireExceptionCaught(ThrowHelper.GetChannelPipelineException_HandlerAddedThrowRemovedExc(ctx, ex));
                }
                else
                {
                    this.FireExceptionCaught(ThrowHelper.GetChannelPipelineException_HandlerAddedThrowAlsoFailedToRemovedExc(ctx, ex));
                }
            }
        }

        void CallHandlerRemoved0(AbstractChannelHandlerContext ctx)
        {
            // Notify the complete removal.
            try
            {
                ctx.CallHandlerRemoved();
            }
            catch (Exception ex)
            {
                this.FireExceptionCaught(ThrowHelper.GetChannelPipelineException_HandlerRemovedThrowExc(ctx, ex));
            }
        }

        internal void InvokeHandlerAddedIfNeeded()
        {
            Debug.Assert(this.channel.EventLoop.InEventLoop);
            if (firstRegistration)
            {
                firstRegistration = false;
                // We are now registered to the EventLoop. It's time to call the callbacks for the ChannelHandlers,
                // that were added before the registration was done.
                this.CallHandlerAddedForAllHandlers();
            }
        }

        public IChannelHandler First() => this.FirstContext()?.Handler;

        public IChannelHandlerContext FirstContext()
        {
            var first = this.head.Next;
            return first == this.tail ? null : first;
        }

        public IChannelHandler Last() => this.LastContext()?.Handler;

        public IChannelHandlerContext LastContext()
        {
            var last = this.tail.Prev;
            return last == this.head ? null : last;
        }

        public IChannelHandler Get(string name) => this.Context(name)?.Handler;

        public T Get<T>() where T : class, IChannelHandler => (T)this.Context<T>()?.Handler;

        public IChannelHandlerContext Context(string name)
        {
            if (null == name) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.name); }

            return this.Context0(name);
        }

        public IChannelHandlerContext Context(IChannelHandler handler)
        {
            if (null == handler) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.handler); }

            var ctx = this.head.Next;
            while (true)
            {
                if (ctx == null)
                {
                    return null;
                }

                if (ctx.Handler == handler)
                {
                    return ctx;
                }

                ctx = ctx.Next;
            }
        }

        public IChannelHandlerContext Context<T>() where T : class, IChannelHandler
        {
            var ctx = this.head.Next;
            while (true)
            {
                if (ctx == null)
                {
                    return null;
                }
                if (ctx.Handler is T)
                {
                    return ctx;
                }
                ctx = ctx.Next;
            }
        }

        /// <summary>
        /// Returns the string representation of this pipeline.
        /// </summary>
        public sealed override string ToString()
        {
            var buf = StringBuilderManager.Allocate()
                .Append(this.GetType().Name)
                .Append('{');
            AbstractChannelHandlerContext ctx = this.head.Next;
            while (true)
            {
                if (ctx == this.tail)
                {
                    break;
                }

                buf.Append('(')
                    .Append(ctx.Name)
                    .Append(" = ")
                    .Append(ctx.Handler.GetType().Name)
                    .Append(')');

                ctx = ctx.Next;
                if (ctx == this.tail)
                {
                    break;
                }

                buf.Append(", ");
            }
            buf.Append('}');
            return StringBuilderManager.ReturnAndFree(buf);
        }

        public IChannelPipeline FireChannelRegistered()
        {
            AbstractChannelHandlerContext.InvokeChannelRegistered(this.head);
            return this;
        }

        public IChannelPipeline FireChannelUnregistered()
        {
            AbstractChannelHandlerContext.InvokeChannelUnregistered(this.head);
            return this;
        }

        /// <summary>
        /// Removes all handlers from the pipeline one by one from tail (exclusive) to head (exclusive) to trigger
        /// <see cref="IChannelHandler.HandlerRemoved"/>. Note that we traverse up the pipeline <see cref="DestroyUp"/>
        /// before traversing down <see cref="DestroyDown"/> so that the handlers are removed after all events are
        /// handled.
        /// See: https://github.com/netty/netty/issues/3156
        /// </summary>
        void Destroy()
        {
            lock (this)
            {
                this.DestroyUp(this.head.Next, false);
            }
        }

        void DestroyUp(AbstractChannelHandlerContext ctx, bool inEventLoop)
        {
            var currentThread = Thread.CurrentThread;
            var tailContext = this.tail;
            while (true)
            {
                if (ctx == tailContext)
                {
                    this.DestroyDown(currentThread, tailContext.Prev, inEventLoop);
                    break;
                }

                IEventExecutor executor = ctx.Executor;
                if (!inEventLoop && !executor.IsInEventLoop(currentThread))
                {
                    executor.Execute(DestroyUpAction, this, ctx);
                    break;
                }

                ctx = ctx.Next;
                inEventLoop = false;
            }
        }

        void DestroyDown(Thread currentThread, AbstractChannelHandlerContext ctx, bool inEventLoop)
        {
            // We have reached at tail; now traverse backwards.
            var headContext = this.head;
            while (true)
            {
                if (ctx == headContext)
                {
                    break;
                }

                IEventExecutor executor = ctx.Executor;
                if (inEventLoop || executor.IsInEventLoop(currentThread))
                {
                    lock (this)
                    {
                        Remove0(ctx);
                    }
                    this.CallHandlerRemoved0(ctx);
                }
                else
                {
                    executor.Execute(DestroyDownAction, this, ctx);
                    break;
                }

                ctx = ctx.Prev;
                inEventLoop = false;
            }
        }

        public IChannelPipeline FireChannelActive()
        {
            this.head.FireChannelActive();

            if (this.channel.Configuration.AutoRead)
            {
                this.channel.Read();
            }

            return this;
        }

        public IChannelPipeline FireChannelInactive()
        {
            this.head.FireChannelInactive();
            return this;
        }

        public IChannelPipeline FireExceptionCaught(Exception cause)
        {
            this.head.FireExceptionCaught(cause);
            return this;
        }

        public IChannelPipeline FireUserEventTriggered(object evt)
        {
            this.head.FireUserEventTriggered(evt);
            return this;
        }

        public IChannelPipeline FireChannelRead(object msg)
        {
            this.head.FireChannelRead(msg);
            return this;
        }

        public IChannelPipeline FireChannelReadComplete()
        {
            this.head.FireChannelReadComplete();
            if (this.channel.Configuration.AutoRead)
            {
                this.Read();
            }
            return this;
        }

        public IChannelPipeline FireChannelWritabilityChanged()
        {
            this.head.FireChannelWritabilityChanged();
            return this;
        }

        public Task BindAsync(EndPoint localAddress) => this.tail.BindAsync(localAddress);

        public Task ConnectAsync(EndPoint remoteAddress) => this.tail.ConnectAsync(remoteAddress);

        public Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress) => this.tail.ConnectAsync(remoteAddress, localAddress);

        public Task DisconnectAsync() => this.tail.DisconnectAsync();

        public Task DisconnectAsync(IPromise promise) => this.tail.DisconnectAsync(promise);

        public Task CloseAsync() => this.tail.CloseAsync();

        public Task CloseAsync(IPromise promise) => this.tail.CloseAsync(promise);

        public Task DeregisterAsync() => this.tail.DeregisterAsync();

        public Task DeregisterAsync(IPromise promise) => this.tail.DeregisterAsync(promise);

        public IChannelPipeline Read()
        {
            this.tail.Read();
            return this;
        }

        public Task WriteAsync(object msg) => this.tail.WriteAsync(msg);

        public Task WriteAsync(object msg, IPromise promise) => this.tail.WriteAsync(msg, promise);

        public IChannelPipeline Flush()
        {
            this.tail.Flush();
            return this;
        }

        public Task WriteAndFlushAsync(object msg) => this.tail.WriteAndFlushAsync(msg);

        public Task WriteAndFlushAsync(object msg, IPromise promise) => this.tail.WriteAndFlushAsync(msg, promise);

        public IPromise NewPromise() => new TaskCompletionSource();

        public IPromise NewPromise(object state) => new TaskCompletionSource(state);

        public IPromise VoidPromise() => this.voidPromise;

        string FilterName(string name, IChannelHandler handler)
        {
            if (name == null)
            {
                return this.GenerateName(handler);
            }
            this.CheckDuplicateName(name);
            return name;
        }

        void CheckDuplicateName(string name)
        {
            if (this.Context0(name) != null)
            {
                ThrowHelper.ThrowArgumentException_DuplicateHandler(name);
            }
        }

        AbstractChannelHandlerContext Context0(string name)
        {
            var context = this.head.Next;
            while (context != this.tail)
            {
                if (string.Equals(context.Name, name
#if NETCOREAPP_3_0_GREATER || NETSTANDARD_2_0_GREATER
                    ))
#else
                    , StringComparison.Ordinal))
#endif
                {
                    return context;
                }
                context = context.Next;
            }
            return null;
        }

        AbstractChannelHandlerContext GetContextOrThrow(string name)
        {
            var ctx = (AbstractChannelHandlerContext)this.Context(name);
            if (ctx == null)
            {
                ThrowHelper.ThrowArgumentException_Context(name);
            }

            return ctx;
        }

        AbstractChannelHandlerContext GetContextOrThrow(IChannelHandler handler)
        {
            var ctx = (AbstractChannelHandlerContext)this.Context(handler);
            if (ctx == null)
            {
                ThrowHelper.ThrowArgumentException_Context(handler);
            }

            return ctx;
        }

        AbstractChannelHandlerContext GetContextOrThrow<T>() where T : class, IChannelHandler
        {
            var ctx = (AbstractChannelHandlerContext)this.Context<T>();
            if (ctx == null)
            {
                ThrowHelper.ThrowArgumentException_Context<T>();
            }

            return ctx;
        }

        void CallHandlerAddedForAllHandlers()
        {
            PendingHandlerCallback pendingHandlerCallbackHead;
            lock (this)
            {
                Debug.Assert(!this.registered);

                // This Channel itself was registered.
                this.registered = true;

                pendingHandlerCallbackHead = this.pendingHandlerCallbackHead;
                // Null out so it can be GC'ed.
                this.pendingHandlerCallbackHead = null;
            }

            // This must happen outside of the synchronized(...) block as otherwise handlerAdded(...) may be called while
            // holding the lock and so produce a deadlock if handlerAdded(...) will try to add another handler from outside
            // the EventLoop.
            PendingHandlerCallback task = pendingHandlerCallbackHead;
            while (task != null)
            {
                task.Execute();
                task = task.Next;
            }
        }

        void CallHandlerCallbackLater(AbstractChannelHandlerContext ctx, bool added)
        {
            Debug.Assert(!this.registered);

            PendingHandlerCallback task = added ? (PendingHandlerCallback)new PendingHandlerAddedTask(this, ctx) : new PendingHandlerRemovedTask(this, ctx);
            PendingHandlerCallback pending = this.pendingHandlerCallbackHead;
            if (pending == null)
            {
                this.pendingHandlerCallbackHead = task;
            }
            else
            {
                // Find the tail of the linked-list.
                while (pending.Next != null)
                {
                    pending = pending.Next;
                }
                pending.Next = task;
            }
        }

        /// <summary>
        /// Called once an <see cref="Exception" /> hits the end of the <see cref="IChannelPipeline" /> without being
        /// handled by the user in <see cref="IChannelHandler.ExceptionCaught(IChannelHandlerContext, Exception)" />.
        /// </summary>
        protected virtual void OnUnhandledInboundException(Exception cause)
        {
            try
            {
                Logger.AnExceptionCaughtEventWasFired(cause);
            }
            finally
            {
                ReferenceCountUtil.Release(cause);
            }
        }

        /// <summary>
        /// Called once the <see cref="IChannelHandler.ChannelActive(IChannelHandlerContext)" /> event hit
        /// the end of the <see cref="IChannelPipeline" />.
        /// </summary>
        protected virtual void OnUnhandledInboundChannelActive()
        {
        }

        /// <summary>
        /// Called once the <see cref="IChannelHandler.ChannelInactive(IChannelHandlerContext)" /> event hit
        /// the end of the <see cref="IChannelPipeline" />.
        /// </summary>
        protected virtual void OnUnhandledInboundChannelInactive()
        {
        }

        /// <summary>
        /// Called once a message hits the end of the <see cref="IChannelPipeline" /> without being handled by the user
        /// in <see cref="IChannelHandler.ChannelRead(IChannelHandlerContext, object)" />. This method is responsible
        /// for calling <see cref="ReferenceCountUtil.Release(object)" /> on the given msg at some point.
        /// </summary>
        protected virtual void OnUnhandledInboundMessage(object msg)
        {
            try
            {
                if (Logger.DebugEnabled) Logger.DiscardedInboundMessage(msg);
            }
            finally
            {
                ReferenceCountUtil.Release(msg);
            }
        }

        /// <summary>
        /// Called once the <see cref="IChannelHandler.ChannelReadComplete(IChannelHandlerContext)" /> event hit
        /// the end of the <see cref="IChannelPipeline" />.
        /// </summary>
        protected virtual void OnUnhandledInboundChannelReadComplete()
        {
        }

        /// <summary>
        /// Called once an user event hit the end of the <see cref="IChannelPipeline" /> without been handled by the user
        /// in <see cref="IChannelHandler.UserEventTriggered(IChannelHandlerContext, object)" />. This method is responsible
        /// to call <see cref="ReferenceCountUtil.Release(object)" /> on the given event at some point.
        /// </summary>
        /// <param name="evt"></param>
        protected virtual void OnUnhandledInboundUserEventTriggered(object evt)
        {
            // This may not be a configuration error and so don't log anything.
            // The event may be superfluous for the current pipeline configuration.
            ReferenceCountUtil.Release(evt);
        }

        /// <summary>
        /// Called once the <see cref="IChannelHandler.ChannelWritabilityChanged(IChannelHandlerContext)" /> event hit
        /// the end of the <see cref="IChannelPipeline" />.
        /// </summary>
        protected virtual void OnUnhandledChannelWritabilityChanged()
        {
        }

        internal protected virtual void IncrementPendingOutboundBytes(long size)
        {
            ChannelOutboundBuffer buffer = this.channel.Unsafe.OutboundBuffer;
            if (buffer != null)
            {
                buffer.IncrementPendingOutboundBytes(size);
            }
        }

        internal protected virtual void DecrementPendingOutboundBytes(long size)
        {
            ChannelOutboundBuffer buffer = this.channel.Unsafe.OutboundBuffer;
            if (buffer != null)
            {
                buffer.DecrementPendingOutboundBytes(size);
            }
        }

        sealed class TailContext : AbstractChannelHandlerContext, IChannelHandler
        {
            static readonly string TailName = GenerateName0(typeof(TailContext));
            static readonly SkipFlags s_skipFlags = CalculateSkipPropagationFlags(typeof(TailContext));

            public TailContext(DefaultChannelPipeline pipeline)
                : base(pipeline, null, TailName, s_skipFlags)
            {
                this.SetAddComplete();
            }

            public override IChannelHandler Handler => this;

            public void ChannelRegistered(IChannelHandlerContext context) { }

            public void ChannelUnregistered(IChannelHandlerContext context) { }

            public void ChannelActive(IChannelHandlerContext context) => this.pipeline.OnUnhandledInboundChannelActive();

            public void ChannelInactive(IChannelHandlerContext context) => this.pipeline.OnUnhandledInboundChannelInactive();

            public void ExceptionCaught(IChannelHandlerContext context, Exception exception) => this.pipeline.OnUnhandledInboundException(exception);

            public void ChannelRead(IChannelHandlerContext context, object message) => this.pipeline.OnUnhandledInboundMessage(message);

            public void ChannelReadComplete(IChannelHandlerContext context) => this.pipeline.OnUnhandledInboundChannelReadComplete();

            public void ChannelWritabilityChanged(IChannelHandlerContext context) => this.pipeline.OnUnhandledChannelWritabilityChanged();

            [Skip]
            public void HandlerAdded(IChannelHandlerContext context) { }

            [Skip]
            public void HandlerRemoved(IChannelHandlerContext context) { }

            [Skip]
            public void Deregister(IChannelHandlerContext context, IPromise promise) => context.DeregisterAsync(promise);

            [Skip]
            public void Disconnect(IChannelHandlerContext context, IPromise promise) => context.DisconnectAsync(promise);

            [Skip]
            public void Close(IChannelHandlerContext context, IPromise promise) => context.CloseAsync(promise);

            [Skip]
            public void Read(IChannelHandlerContext context) => context.Read();

            public void UserEventTriggered(IChannelHandlerContext context, object evt) => this.pipeline.OnUnhandledInboundUserEventTriggered(evt);

            [Skip]
            public void Write(IChannelHandlerContext ctx, object message, IPromise promise) => ctx.WriteAsync(message, promise);

            [Skip]
            public void Flush(IChannelHandlerContext context) => context.Flush();

            [Skip]
            public Task BindAsync(IChannelHandlerContext context, EndPoint localAddress) => context.BindAsync(localAddress);

            [Skip]
            public Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress) => context.ConnectAsync(remoteAddress, localAddress);
        }

        sealed class HeadContext : AbstractChannelHandlerContext, IChannelHandler
        {
            static readonly string HeadName = GenerateName0(typeof(HeadContext));
            static readonly SkipFlags s_skipFlags = CalculateSkipPropagationFlags(typeof(HeadContext));

            readonly IChannelUnsafe channelUnsafe;

            public HeadContext(DefaultChannelPipeline pipeline)
                : base(pipeline, null, HeadName, s_skipFlags)
            {
                this.channelUnsafe = pipeline.Channel.Unsafe;
                this.SetAddComplete();
            }

            public override IChannelHandler Handler => this;

            public void Flush(IChannelHandlerContext context) => this.channelUnsafe.Flush();

            public Task BindAsync(IChannelHandlerContext context, EndPoint localAddress) => this.channelUnsafe.BindAsync(localAddress);

            public Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress) => this.channelUnsafe.ConnectAsync(remoteAddress, localAddress);

            public void Disconnect(IChannelHandlerContext context, IPromise promise) => this.channelUnsafe.Disconnect(promise);

            public void Close(IChannelHandlerContext context, IPromise promise) => this.channelUnsafe.Close(promise);

            public void Deregister(IChannelHandlerContext context, IPromise promise) => this.channelUnsafe.Deregister(promise);

            public void Read(IChannelHandlerContext context) => this.channelUnsafe.BeginRead();

            public void Write(IChannelHandlerContext context, object message, IPromise promise) => this.channelUnsafe.Write(message, promise);

            [Skip]
            public void HandlerAdded(IChannelHandlerContext context) { }

            [Skip]
            public void HandlerRemoved(IChannelHandlerContext context) { }

            [Skip]
            public void ExceptionCaught(IChannelHandlerContext ctx, Exception exception) => ctx.FireExceptionCaught(exception);

            public void ChannelRegistered(IChannelHandlerContext context)
            {
                this.pipeline.InvokeHandlerAddedIfNeeded();
                context.FireChannelRegistered();
            }

            public void ChannelUnregistered(IChannelHandlerContext context)
            {
                context.FireChannelUnregistered();

                // Remove all handlers sequentially if channel is closed and unregistered.
                if (!this.pipeline.channel.Open)
                {
                    this.pipeline.Destroy();
                }
            }

            public void ChannelActive(IChannelHandlerContext context)
            {
                context.FireChannelActive();

                this.ReadIfIsAutoRead();
            }

            //[Skip]
            public void ChannelInactive(IChannelHandlerContext context) => context.FireChannelInactive();

            //[Skip]
            public void ChannelRead(IChannelHandlerContext ctx, object msg) => ctx.FireChannelRead(msg);

            public void ChannelReadComplete(IChannelHandlerContext ctx)
            {
                ctx.FireChannelReadComplete();

                this.ReadIfIsAutoRead();
            }

            void ReadIfIsAutoRead()
            {
                if (this.pipeline.channel.Configuration.AutoRead)
                {
                    this.pipeline.channel.Read();
                }
            }

            //[Skip]
            public void UserEventTriggered(IChannelHandlerContext context, object evt) => this.FireUserEventTriggered(evt);

            //[Skip]
            public void ChannelWritabilityChanged(IChannelHandlerContext context) => context.FireChannelWritabilityChanged();
        }

        abstract class PendingHandlerCallback : IRunnable
        {
            protected readonly DefaultChannelPipeline Pipeline;
            protected readonly AbstractChannelHandlerContext Ctx;
            internal PendingHandlerCallback Next;

            protected PendingHandlerCallback(DefaultChannelPipeline pipeline, AbstractChannelHandlerContext ctx)
            {
                this.Pipeline = pipeline;
                this.Ctx = ctx;
            }

            public abstract void Run();

            internal abstract void Execute();
        }

        sealed class PendingHandlerAddedTask : PendingHandlerCallback
        {
            public PendingHandlerAddedTask(DefaultChannelPipeline pipeline, AbstractChannelHandlerContext ctx)
                : base(pipeline, ctx)
            {
            }

            public override void Run() => this.Pipeline.CallHandlerAdded0(this.Ctx);

            internal override void Execute()
            {
                IEventExecutor executor = this.Ctx.Executor;
                if (executor.InEventLoop)
                {
                    this.Pipeline.CallHandlerAdded0(this.Ctx);
                }
                else
                {
                    try
                    {
                        executor.Execute(this);
                    }
                    catch (RejectedExecutionException e)
                    {
                        if (Logger.WarnEnabled)
                        {
                            Logger.CannotInvokeHandlerAddedAsTheIEventExecutorRejectedIt(executor, this.Ctx, e);
                        }
                        Remove0(this.Ctx);
                        this.Ctx.SetRemoved();
                    }
                }
            }
        }

        sealed class PendingHandlerRemovedTask : PendingHandlerCallback
        {
            public PendingHandlerRemovedTask(DefaultChannelPipeline pipeline, AbstractChannelHandlerContext ctx)
                : base(pipeline, ctx)
            {
            }

            public override void Run() => this.Pipeline.CallHandlerRemoved0(this.Ctx);

            internal override void Execute()
            {
                IEventExecutor executor = this.Ctx.Executor;
                if (executor.InEventLoop)
                {
                    this.Pipeline.CallHandlerRemoved0(this.Ctx);
                }
                else
                {
                    try
                    {
                        executor.Execute(this);
                    }
                    catch (RejectedExecutionException e)
                    {
                        if (Logger.WarnEnabled)
                        {
                            Logger.CannotInvokeHandlerRemovedAsTheIEventExecutorRejectedIt(executor, this.Ctx, e);
                        }
                        // remove0(...) was call before so just call AbstractChannelHandlerContext.setRemoved().
                        this.Ctx.SetRemoved();
                    }
                }
            }
        }
    }
}