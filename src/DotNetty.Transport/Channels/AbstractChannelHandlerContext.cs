// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;

    abstract partial class AbstractChannelHandlerContext : IChannelHandlerContext, IResourceLeakHint
    {
        static readonly Action<object> InvokeChannelReadCompleteAction = OnInvokeChannelReadComplete; // ctx => ((AbstractChannelHandlerContext)ctx).InvokeChannelReadComplete();
        static readonly Action<object> InvokeReadAction = OnInvokeRead; // ctx => ((AbstractChannelHandlerContext)ctx).InvokeRead();
        static readonly Action<object> InvokeChannelWritabilityChangedAction = OnInvokeChannelWritabilityChanged; // ctx => ((AbstractChannelHandlerContext)ctx).InvokeChannelWritabilityChanged();
        static readonly Action<object> InvokeFlushAction = OnInvokeFlush; // ctx => ((AbstractChannelHandlerContext)ctx).InvokeFlush();
        static readonly Action<object, object> InvokeUserEventTriggeredAction = OnInvokeUserEventTriggered; // (ctx, evt) => ((AbstractChannelHandlerContext)ctx).InvokeUserEventTriggered(evt);
        static readonly Action<object, object> InvokeChannelReadAction = OnInvokeChannelRead; // (ctx, msg) => ((AbstractChannelHandlerContext)ctx).InvokeChannelRead(msg);

        [Flags]
        protected internal enum SkipFlags
        {
            HandlerAdded = 1,
            HandlerRemoved = 1 << 1,
            ExceptionCaught = 1 << 2,
            ChannelRegistered = 1 << 3,
            ChannelUnregistered = 1 << 4,
            ChannelActive = 1 << 5,
            ChannelInactive = 1 << 6,
            ChannelRead = 1 << 7,
            ChannelReadComplete = 1 << 8,
            ChannelWritabilityChanged = 1 << 9,
            UserEventTriggered = 1 << 10,
            Bind = 1 << 11,
            Connect = 1 << 12,
            Disconnect = 1 << 13,
            Close = 1 << 14,
            Deregister = 1 << 15,
            Read = 1 << 16,
            Write = 1 << 17,
            Flush = 1 << 18,

            Inbound = ExceptionCaught |
                ChannelRegistered |
                ChannelUnregistered |
                ChannelActive |
                ChannelInactive |
                ChannelRead |
                ChannelReadComplete |
                ChannelWritabilityChanged |
                UserEventTriggered,

            Outbound = Bind |
                Connect |
                Disconnect |
                Close |
                Deregister |
                Read |
                Write |
                Flush,
        }

        static readonly ConditionalWeakTable<Type, Tuple<SkipFlags>> SkipTable = new ConditionalWeakTable<Type, Tuple<SkipFlags>>();

        protected static SkipFlags GetSkipPropagationFlags(IChannelHandler handler)
        {
            Tuple<SkipFlags> skipDirection = SkipTable.GetValue(
                handler.GetType(),
                handlerType => Tuple.Create(CalculateSkipPropagationFlags(handlerType)));

            return skipDirection?.Item1 ?? 0;
        }

        protected static SkipFlags CalculateSkipPropagationFlags(Type handlerType)
        {
            SkipFlags flags = 0;

            // this method should never throw
            if (IsSkippable(handlerType, nameof(IChannelHandler.HandlerAdded)))
            {
                flags |= SkipFlags.HandlerAdded;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.HandlerRemoved)))
            {
                flags |= SkipFlags.HandlerRemoved;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ExceptionCaught), typeof(Exception)))
            {
                flags |= SkipFlags.ExceptionCaught;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelRegistered)))
            {
                flags |= SkipFlags.ChannelRegistered;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelUnregistered)))
            {
                flags |= SkipFlags.ChannelUnregistered;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelActive)))
            {
                flags |= SkipFlags.ChannelActive;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelInactive)))
            {
                flags |= SkipFlags.ChannelInactive;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelRead), typeof(object)))
            {
                flags |= SkipFlags.ChannelRead;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelReadComplete)))
            {
                flags |= SkipFlags.ChannelReadComplete;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelWritabilityChanged)))
            {
                flags |= SkipFlags.ChannelWritabilityChanged;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.UserEventTriggered), typeof(object)))
            {
                flags |= SkipFlags.UserEventTriggered;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.BindAsync), typeof(EndPoint)))
            {
                flags |= SkipFlags.Bind;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ConnectAsync), typeof(EndPoint), typeof(EndPoint)))
            {
                flags |= SkipFlags.Connect;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.Disconnect), typeof(IPromise)))
            {
                flags |= SkipFlags.Disconnect;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.Close), typeof(IPromise)))
            {
                flags |= SkipFlags.Close;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.Deregister), typeof(IPromise)))
            {
                flags |= SkipFlags.Deregister;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.Read)))
            {
                flags |= SkipFlags.Read;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.Write), typeof(object), typeof(IPromise)))
            {
                flags |= SkipFlags.Write;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.Flush)))
            {
                flags |= SkipFlags.Flush;
            }
            return flags;
        }

        protected static bool IsSkippable(Type handlerType, string methodName) => IsSkippable(handlerType, methodName, Type.EmptyTypes);

        protected static bool IsSkippable(Type handlerType, string methodName, params Type[] paramTypes)
        {
            var newParamTypes = new Type[paramTypes.Length + 1];
            newParamTypes[0] = typeof(IChannelHandlerContext);
            Array.Copy(paramTypes, 0, newParamTypes, 1, paramTypes.Length);
            return handlerType.GetMethod(methodName, newParamTypes).GetCustomAttribute<SkipAttribute>(false) is object;
        }

        AbstractChannelHandlerContext _next;
        AbstractChannelHandlerContext _prev;

        internal readonly SkipFlags SkipPropagationFlags;

        static class HandlerState
        {
            /// <summary>Neither <see cref="IChannelHandler.HandlerAdded"/> nor <see cref="IChannelHandler.HandlerRemoved"/> was called.</summary>
            public const int Init = 0;
            /// <summary><see cref="IChannelHandler.HandlerAdded"/> is about to be called.</summary>
            public const int AddPending = 1;
            /// <summary><see cref="IChannelHandler.HandlerAdded"/> was called.</summary>
            public const int AddComplete = 2;
            /// <summary><see cref="IChannelHandler.HandlerRemoved"/> was called.</summary>
            public const int RemoveComplete = 3;
        }

        internal readonly DefaultChannelPipeline pipeline;
        readonly bool ordered;

        // Will be set to null if no child executor should be used, otherwise it will be set to the
        // child executor.
        internal readonly IEventExecutor executor;
        int handlerState = HandlerState.Init;

        protected AbstractChannelHandlerContext(DefaultChannelPipeline pipeline, IEventExecutor executor,
            string name, SkipFlags skipPropagationDirections)
        {
            if (pipeline is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.pipeline); }
            if (name is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.name); }

            this.pipeline = pipeline;
            this.Name = name;
            this.executor = executor;
            this.SkipPropagationFlags = skipPropagationDirections;
            // Its ordered if its driven by the EventLoop or the given Executor is an instanceof OrderedEventExecutor.
            this.ordered = executor is null || executor is IOrderedEventExecutor;
        }

        public IChannel Channel => this.pipeline.Channel;

        public IChannelPipeline Pipeline => this.pipeline;

        public IByteBufferAllocator Allocator => this.Channel.Allocator;

        public abstract IChannelHandler Handler { get; }

        /// <summary>
        ///     Makes best possible effort to detect if <see cref="IChannelHandler.HandlerAdded(IChannelHandlerContext)" /> was
        ///     called
        ///     yet. If not return <c>false</c> and if called or could not detect return <c>true</c>.
        ///     If this method returns <c>true</c> we will not invoke the <see cref="IChannelHandler" /> but just forward the
        ///     event.
        ///     This is needed as <see cref="DefaultChannelPipeline" /> may already put the <see cref="IChannelHandler" /> in the
        ///     linked-list
        ///     but not called <see cref="IChannelHandler.HandlerAdded(IChannelHandlerContext)" />
        /// </summary>
        private bool InvokeHandler
        {
            [MethodImpl(InlineMethod.Value)]
            get
            {
                // Store in local variable to reduce volatile reads.
                var thisState = Volatile.Read(ref this.handlerState);
                return thisState == HandlerState.AddComplete || (!this.ordered && thisState == HandlerState.AddPending);
            }
        }

        public bool Removed => Volatile.Read(ref handlerState) == HandlerState.RemoveComplete;

        internal bool SetAddComplete()
        {
            var prevState = Volatile.Read(ref this.handlerState);
            int oldState;
            do
            {
                if (prevState == HandlerState.RemoveComplete) { return false; }
                oldState = prevState;
                // Ensure we never update when the handlerState is REMOVE_COMPLETE already.
                // oldState is usually ADD_PENDING but can also be REMOVE_COMPLETE when an EventExecutor is used that is not
                // exposing ordering guarantees.
                prevState = Interlocked.CompareExchange(ref this.handlerState, HandlerState.AddComplete, prevState);
            } while (prevState != oldState);
            return true;
        }

        internal void SetRemoved() => Interlocked.Exchange(ref handlerState, HandlerState.RemoveComplete);

        internal void SetAddPending()
        {
            var updated = HandlerState.Init == Interlocked.CompareExchange(ref this.handlerState, HandlerState.AddPending, HandlerState.Init);
            Debug.Assert(updated); // This should always be true as it MUST be called before setAddComplete() or setRemoved().
        }

        internal void CallHandlerAdded()
        {
            // We must call setAddComplete before calling handlerAdded. Otherwise if the handlerAdded method generates
            // any pipeline events ctx.handler() will miss them because the state will not allow it.
            if (this.SetAddComplete())
            {
                this.Handler.HandlerAdded(this);
            }
        }

        internal void CallHandlerRemoved()
        {
            try
            {
                // Only call handlerRemoved(...) if we called handlerAdded(...) before.
                if (Volatile.Read(ref this.handlerState) == HandlerState.AddComplete)
                {
                    this.Handler.HandlerRemoved(this);
                }
            }
            finally
            {
                // Mark the handler as removed in any case.
                this.SetRemoved();
            }
        }

        public IEventExecutor Executor => this.executor ?? this.Channel.EventLoop;

        public string Name { get; }

        public IAttribute<T> GetAttribute<T>(AttributeKey<T> key)
            where T : class
        {
            return this.Channel.GetAttribute(key);
        }

        public bool HasAttribute<T>(AttributeKey<T> key)
            where T : class
        {
            return this.Channel.HasAttribute(key);
        }
        public IChannelHandlerContext FireChannelRegistered()
        {
            InvokeChannelRegistered(this.FindContextInbound());
            return this;
        }

        internal static void InvokeChannelRegistered(AbstractChannelHandlerContext next)
        {
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeChannelRegistered();
            }
            else
            {
                nextExecutor.Execute(InvokeChannelRegisteredAction, next);
            }
        }

        void InvokeChannelRegistered()
        {
            if (this.InvokeHandler)
            {
                try
                {
                    this.Handler.ChannelRegistered(this);
                }
                catch (Exception ex)
                {
                    this.NotifyHandlerException(ex);
                }
            }
            else
            {
                this.FireChannelRegistered();
            }
        }

        public IChannelHandlerContext FireChannelUnregistered()
        {
            InvokeChannelUnregistered(this.FindContextInbound());
            return this;
        }

        internal static void InvokeChannelUnregistered(AbstractChannelHandlerContext next)
        {
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeChannelUnregistered();
            }
            else
            {
                nextExecutor.Execute(InvokeChannelUnregisteredAction, next);
            }
        }

        void InvokeChannelUnregistered()
        {
            if (this.InvokeHandler)
            {
                try
                {
                    this.Handler.ChannelUnregistered(this);
                }
                catch (Exception t)
                {
                    this.NotifyHandlerException(t);
                }
            }
            else
            {
                this.FireChannelUnregistered();
            }
        }

        public IChannelHandlerContext FireChannelActive()
        {
            InvokeChannelActive(this.FindContextInbound());
            return this;
        }

        internal static void InvokeChannelActive(AbstractChannelHandlerContext next)
        {
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeChannelActive();
            }
            else
            {
                nextExecutor.Execute(InvokeChannelActiveAction, next);
            }
        }

        void InvokeChannelActive()
        {
            if (this.InvokeHandler)
            {
                try
                {
                    (this.Handler).ChannelActive(this);
                }
                catch (Exception ex)
                {
                    this.NotifyHandlerException(ex);
                }
            }
            else
            {
                this.FireChannelActive();
            }
        }

        public IChannelHandlerContext FireChannelInactive()
        {
            InvokeChannelInactive(this.FindContextInbound());
            return this;
        }

        internal static void InvokeChannelInactive(AbstractChannelHandlerContext next)
        {
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeChannelInactive();
            }
            else
            {
                nextExecutor.Execute(InvokeChannelInactiveAction, next);
            }
        }

        void InvokeChannelInactive()
        {
            if (this.InvokeHandler)
            {
                try
                {
                    this.Handler.ChannelInactive(this);
                }
                catch (Exception ex)
                {
                    this.NotifyHandlerException(ex);
                }
            }
            else
            {
                this.FireChannelInactive();
            }
        }

        public virtual IChannelHandlerContext FireExceptionCaught(Exception cause)
        {
            InvokeExceptionCaught(this.Next, cause); //InvokeExceptionCaught(this.FindContextInbound(), cause);
            return this;
        }

        internal static void InvokeExceptionCaught(AbstractChannelHandlerContext next, Exception cause)
        {
            if (cause is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.cause); }

            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeExceptionCaught(cause);
            }
            else
            {
                try
                {
                    nextExecutor.Execute(InvokeExceptionCaughtAction, next, cause);
                }
                catch (Exception t)
                {
                    var logger = DefaultChannelPipeline.Logger;
                    if (logger.WarnEnabled)
                    {
                        logger.FailedToSubmitAnExceptionCaughtEvent(t);
                        logger.TheExceptionCaughtEventThatWasFailedToSubmit(cause);
                    }
                }
            }
        }

        void InvokeExceptionCaught(Exception cause)
        {
            if (this.InvokeHandler)
            {
                try
                {
                    this.Handler.ExceptionCaught(this, cause);
                }
                catch (Exception t)
                {
                    var logger = DefaultChannelPipeline.Logger;
                    if (logger.WarnEnabled)
                    {
                        logger.FailedToSubmitAnExceptionCaughtEvent(t);
                        logger.ExceptionCaughtMethodWhileHandlingTheFollowingException(cause);
                    }
                }
            }
            else
            {
                this.FireExceptionCaught(cause);
            }
        }

        public IChannelHandlerContext FireUserEventTriggered(object evt)
        {
            InvokeUserEventTriggered(this.FindContextInbound(), evt);
            return this;
        }

        internal static void InvokeUserEventTriggered(AbstractChannelHandlerContext next, object evt)
        {
            if (evt is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.evt); }
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeUserEventTriggered(evt);
            }
            else
            {
                nextExecutor.Execute(InvokeUserEventTriggeredAction, next, evt);
            }
        }

        void InvokeUserEventTriggered(object evt)
        {
            if (this.InvokeHandler)
            {
                try
                {
                    this.Handler.UserEventTriggered(this, evt);
                }
                catch (Exception ex)
                {
                    this.NotifyHandlerException(ex);
                }
            }
            else
            {
                this.FireUserEventTriggered(evt);
            }
        }

        public IChannelHandlerContext FireChannelRead(object msg)
        {
            InvokeChannelRead(this.FindContextInbound(), msg);
            return this;
        }

        internal static void InvokeChannelRead(AbstractChannelHandlerContext next, object msg)
        {
            if (msg is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.msg); }

            object m = next.pipeline.Touch(msg, next);
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeChannelRead(m);
            }
            else
            {
                nextExecutor.Execute(InvokeChannelReadAction, next, msg);
            }
        }

        void InvokeChannelRead(object msg)
        {
            if (this.InvokeHandler)
            {
                try
                {
                    this.Handler.ChannelRead(this, msg);
                }
                catch (Exception ex)
                {
                    this.NotifyHandlerException(ex);
                }
            }
            else
            {
                this.FireChannelRead(msg);
            }
        }

        public IChannelHandlerContext FireChannelReadComplete()
        {
            InvokeChannelReadComplete(this.FindContextInbound());
            return this;
        }

        internal static void InvokeChannelReadComplete(AbstractChannelHandlerContext next)
        {
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeChannelReadComplete();
            }
            else
            {
                // todo: consider caching task
                nextExecutor.Execute(InvokeChannelReadCompleteAction, next);
            }
        }

        void InvokeChannelReadComplete()
        {
            if (this.InvokeHandler)
            {
                try
                {
                    (this.Handler).ChannelReadComplete(this);
                }
                catch (Exception ex)
                {
                    this.NotifyHandlerException(ex);
                }
            }
            else
            {
                this.FireChannelReadComplete();
            }
        }

        public IChannelHandlerContext FireChannelWritabilityChanged()
        {
            InvokeChannelWritabilityChanged(this.FindContextInbound());
            return this;
        }

        internal static void InvokeChannelWritabilityChanged(AbstractChannelHandlerContext next)
        {
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeChannelWritabilityChanged();
            }
            else
            {
                // todo: consider caching task
                nextExecutor.Execute(InvokeChannelWritabilityChangedAction, next);
            }
        }

        void InvokeChannelWritabilityChanged()
        {
            if (this.InvokeHandler)
            {
                try
                {
                    this.Handler.ChannelWritabilityChanged(this);
                }
                catch (Exception ex)
                {
                    this.NotifyHandlerException(ex);
                }
            }
            else
            {
                this.FireChannelWritabilityChanged();
            }
        }

        public Task BindAsync(EndPoint localAddress)
        {
            if (localAddress is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.localAddress); }
            // todo: check for cancellation
            //if (!validatePromise(ctx, promise, false)) {
            //    // promise cancelled
            //    return;
            //}

            AbstractChannelHandlerContext next = this.FindContextOutbound();
            IEventExecutor nextExecutor = next.Executor;
            return nextExecutor.InEventLoop
                ? next.InvokeBindAsync(localAddress)
                : SafeExecuteOutboundAsync(nextExecutor, () => next.InvokeBindAsync(localAddress));
        }

        Task InvokeBindAsync(EndPoint localAddress)
        {
            if (this.InvokeHandler)
            {
                try
                {
                    return this.Handler.BindAsync(this, localAddress);
                }
                catch (Exception ex)
                {
                    return ComposeExceptionTask(ex);
                }
            }

            return this.BindAsync(localAddress);
        }

        public Task ConnectAsync(EndPoint remoteAddress) => this.ConnectAsync(remoteAddress, null);

        public Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
        {
            AbstractChannelHandlerContext next = this.FindContextOutbound();
            if (remoteAddress is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.remoteAddress); }
            // todo: check for cancellation

            IEventExecutor nextExecutor = next.Executor;
            return nextExecutor.InEventLoop
                ? next.InvokeConnectAsync(remoteAddress, localAddress)
                : SafeExecuteOutboundAsync(nextExecutor, () => next.InvokeConnectAsync(remoteAddress, localAddress));
        }

        Task InvokeConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
        {
            if (this.InvokeHandler)
            {
                try
                {
                    return this.Handler.ConnectAsync(this, remoteAddress, localAddress);
                }
                catch (Exception ex)
                {
                    return ComposeExceptionTask(ex);
                }
            }

            return this.ConnectAsync(remoteAddress, localAddress);
        }

        public Task DisconnectAsync() => this.DisconnectAsync(this.NewPromise());

        public Task DisconnectAsync(IPromise promise)
        {
            if (IsNotValidPromise(promise, false))
            {
                // cancelled
                return promise.Task;
            }

            AbstractChannelHandlerContext next = this.FindContextOutbound();
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                if (!this.Channel.Metadata.HasDisconnect)
                {
                    next.InvokeClose(promise);
                }
                else
                {
                    next.InvokeDisconnect(promise);
                }
            }
            else
            {
                try
                {
                    if (!this.Channel.Metadata.HasDisconnect)
                    {
                        nextExecutor.Execute(InvokeCloseAction, next, promise);
                    }
                    else
                    {
                        nextExecutor.Execute(InvokeDisconnectAction, next, promise);
                    }
                }
                catch (Exception exc)
                {
                    promise.TrySetException(exc);
                }
            }

            return promise.Task;
        }

        void InvokeDisconnect(IPromise promise)
        {
            if (this.InvokeHandler)
            {
                try
                {
                    this.Handler.Disconnect(this, promise);
                }
                catch (Exception ex)
                {
                    Util.SafeSetFailure(promise, ex, DefaultChannelPipeline.Logger);
                }
            }
            else
            {
                this.DisconnectAsync(promise);
            }
        }

        public Task CloseAsync() => this.CloseAsync(this.NewPromise());

        public Task CloseAsync(IPromise promise)
        {
            if (IsNotValidPromise(promise, false))
            {
                // cancelled
                return promise.Task;
            }

            AbstractChannelHandlerContext next = this.FindContextOutbound();
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeClose(promise);
            }
            else
            {
                try
                {
                    nextExecutor.Execute(InvokeCloseAction, next, promise);
                }
                catch (Exception exc)
                {
                    promise.TrySetException(exc);
                }
            }

            return promise.Task;
        }

        void InvokeClose(IPromise promise)
        {
            if (this.InvokeHandler)
            {
                try
                {
                    this.Handler.Close(this, promise);
                }
                catch (Exception ex)
                {
                    Util.SafeSetFailure(promise, ex, DefaultChannelPipeline.Logger);
                }
            }
            else
            {
                this.CloseAsync(promise);
            }
        }

        public Task DeregisterAsync() => this.DeregisterAsync(this.NewPromise());

        public Task DeregisterAsync(IPromise promise)
        {
            if (IsNotValidPromise(promise, false))
            {
                // cancelled
                return promise.Task;
            }

            AbstractChannelHandlerContext next = this.FindContextOutbound();
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeDeregister(promise);
            }
            else
            {
                try
                {
                    nextExecutor.Execute(InvokeDeregisterAction, next, promise);
                }
                catch (Exception exc)
                {
                    promise.TrySetException(exc);
                }
            }

            return promise.Task;
        }

        void InvokeDeregister(IPromise promise)
        {
            if (this.InvokeHandler)
            {
                try
                {
                    this.Handler.Deregister(this, promise);
                }
                catch (Exception ex)
                {
                    Util.SafeSetFailure(promise, ex, DefaultChannelPipeline.Logger);
                }
            }
            else
            {
                this.DeregisterAsync(promise);
            }
        }

        public IChannelHandlerContext Read()
        {
            AbstractChannelHandlerContext next = this.FindContextOutbound();
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeRead();
            }
            else
            {
                // todo: consider caching task
                nextExecutor.Execute(InvokeReadAction, next);
            }
            return this;
        }

        void InvokeRead()
        {
            if (this.InvokeHandler)
            {
                try
                {
                    this.Handler.Read(this);
                }
                catch (Exception ex)
                {
                    this.NotifyHandlerException(ex);
                }
            }
            else
            {
                this.Read();
            }
        }

        public Task WriteAsync(object msg) => this.WriteAsync(msg, this.NewPromise());

        public Task WriteAsync(object msg, IPromise promise)
        {
            if (msg is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.msg); }

            try
            {
                if (IsNotValidPromise(promise, true))
                {
                    ReferenceCountUtil.Release(msg);
                    // cancelled
                    return promise.Task;
                }
            }
            catch(Exception)
            {
                ReferenceCountUtil.Release(msg);
                throw;
            }

            this.Write(msg, false, promise);
            return promise.Task;
        }

        void InvokeWrite(object msg, IPromise promise)
        {
            if (this.InvokeHandler)
            {
                this.InvokeWrite0(msg, promise);
            }
            else
            {
                this.WriteAsync(msg, promise);
            }

        }

        void InvokeWrite0(object msg, IPromise promise)
        {
            try
            {
                this.Handler.Write(this, msg, promise);
            }
            catch (Exception ex)
            {
                Util.SafeSetFailure(promise, ex, DefaultChannelPipeline.Logger);
            }
        }

        public IChannelHandlerContext Flush()
        {
            AbstractChannelHandlerContext next = this.FindContextOutbound();
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeFlush();
            }
            else
            {
                try
                {
                    nextExecutor.Execute(InvokeFlushAction, next);
                }
                catch (Exception exc)
                {
                    this.VoidPromise().TrySetException(exc);
                }
            }
            return this;
        }

        void InvokeFlush()
        {
            if (this.InvokeHandler)
            {
                this.InvokeFlush0();
            }
            else
            {
                this.Flush();
            }
        }

        void InvokeFlush0()
        {
            try
            {
                this.Handler.Flush(this);
            }
            catch (Exception ex)
            {
                this.NotifyHandlerException(ex);
            }
        }

        public Task WriteAndFlushAsync(object message) => this.WriteAndFlushAsync(message, this.NewPromise());

        public Task WriteAndFlushAsync(object message, IPromise promise)
        {
            if (message is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.message); }

            if (IsNotValidPromise(promise, true))
            {
                ReferenceCountUtil.Release(message);
                // cancelled
                return promise.Task;
            }

            this.Write(message, true, promise);
            return promise.Task;
        }

        void InvokeWriteAndFlush(object msg, IPromise promise)
        {
            if (this.InvokeHandler)
            {
                this.InvokeWrite0(msg, promise);
                this.InvokeFlush0();
            }
            else
            {
                this.WriteAndFlushAsync(msg, promise);
            }
        }

        void Write(object msg, bool flush, IPromise promise)
        {
            AbstractChannelHandlerContext next = this.FindContextOutbound();
            object m = this.pipeline.Touch(msg, next);
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                if (flush)
                {
                    next.InvokeWriteAndFlush(m, promise);
                }
                else
                {
                    next.InvokeWrite(m, promise);
                }
            }
            else
            {
                AbstractWriteTask task = flush
                    ? WriteAndFlushTask.NewInstance(next, m, promise)
                    : (AbstractWriteTask)WriteTask.NewInstance(next, m, promise);
                if (!SafeExecuteOutbound(nextExecutor, task, promise, msg))
                {
                    // We failed to submit the AbstractWriteTask. We need to cancel it so we decrement the pending bytes
                    // and put it back in the Recycler for re-use later.
                    //
                    // See https://github.com/netty/netty/issues/8343.
                    task.Cancel();
                }
            }
        }

        public IPromise NewPromise() => new TaskCompletionSource();

        public IPromise NewPromise(object state) => new TaskCompletionSource(state);

        public IPromise VoidPromise() => this.Channel.VoidPromise();

        void NotifyHandlerException(Exception cause)
        {
            if (InExceptionCaught(cause))
            {
                var logger = DefaultChannelPipeline.Logger;
                if (logger.WarnEnabled)
                {
                    logger.ThrowByAUserHandlerWhileHandlingAnExceptionCaughtEvent(cause);
                }
                return;
            }

            this.InvokeExceptionCaught(cause);
        }

        static Task ComposeExceptionTask(Exception cause) => TaskUtil.FromException(cause);

        const string ExceptionCaughtMethodName = nameof(IChannelHandler.ExceptionCaught);

        static bool InExceptionCaught(Exception cause) => cause.StackTrace.IndexOf("." + ExceptionCaughtMethodName + "(", StringComparison.Ordinal) >= 0;

        AbstractChannelHandlerContext FindContextInbound()
        {
            AbstractChannelHandlerContext ctx = this;
            do
            {
                ctx = ctx.Next;
            }
            while ((ctx.SkipPropagationFlags & SkipFlags.Inbound) == SkipFlags.Inbound);
            return ctx;
        }

        AbstractChannelHandlerContext FindContextOutbound()
        {
            AbstractChannelHandlerContext ctx = this;
            do
            {
                ctx = ctx.Prev;
            }
            while ((ctx.SkipPropagationFlags & SkipFlags.Outbound) == SkipFlags.Outbound);
            return ctx;
        }

        static Task SafeExecuteOutboundAsync(IEventExecutor executor, Func<Task> function)
        {
            var promise = executor.NewPromise();
            try
            {
                executor.Execute(SafeExecuteOutboundAsyncAction, promise, function);
            }
            catch (Exception cause)
            {
                promise.TrySetException(cause);
            }
            return promise.Task;
        }

        static bool SafeExecuteOutbound(IEventExecutor executor, IRunnable task, IPromise promise, object msg)
        {
            try
            {
                executor.Execute(task);
                return true;
            }
            catch (Exception cause)
            {
                try
                {
                    promise.TrySetException(cause);
                }
                finally
                {
                    ReferenceCountUtil.Release(msg);
                }
                return false;
            }
        }

        public string ToHintString() => $"\'{this.Name}\' will handle the message from this point.";

        public override string ToString() => $"{typeof(IChannelHandlerContext).Name} ({this.Name}, {this.Channel})";

        static bool IsNotValidPromise(IPromise promise, bool allowVoidPromise)
        {
            if (promise is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.promise); }

            if (promise.IsCompleted)
            {
                // Check if the promise was cancelled and if so signal that the processing of the operation
                // should not be performed.
                //
                // See https://github.com/netty/netty/issues/2349
                if (promise.IsCanceled) { return true; }

                ThrowHelper.ThrowArgumentException_PromiseAlreadyCompleted(promise);
            }

            if (promise.IsVoid)
            {
                if (allowVoidPromise) { return false; }

                ThrowHelper.ThrowArgumentException_VoidPromiseIsNotAllowed();
            }

            return false;
        }

        abstract class AbstractWriteTask : IRunnable
        {
            static readonly bool EstimateTaskSizeOnSubmit =
                SystemPropertyUtil.GetBoolean("io.netty.transport.estimateSizeOnSubmit", true);

            // Assuming a 64-bit .NET VM, 16 bytes object header, 4 reference fields and 2 int field
            static readonly int WriteTaskOverhead =
                SystemPropertyUtil.GetInt("io.netty.transport.writeTaskSizeOverhead", 56);

            ThreadLocalPool.Handle handle;
            AbstractChannelHandlerContext ctx;
            object msg;
            IPromise promise;
            int size;

            protected static void Init(AbstractWriteTask task, AbstractChannelHandlerContext ctx, object msg, IPromise promise)
            {
                task.ctx = ctx;
                task.msg = msg;
                task.promise = promise;

                if (EstimateTaskSizeOnSubmit)
                {
                    task.size = ctx.pipeline.EstimatorHandle.Size(msg) + WriteTaskOverhead;
                    ctx.pipeline.IncrementPendingOutboundBytes(task.size);
                }
                else
                {
                    task.size = 0;
                }
            }

            protected AbstractWriteTask(ThreadLocalPool.Handle handle)
            {
                this.handle = handle;
            }

            public void Run()
            {
                try
                {
                    this.DecrementPendingOutboundBytes();
                    this.Write(this.ctx, this.msg, this.promise);
                }
                finally
                {
                    this.Recycle();
                }
            }

            internal void Cancel()
            {
                try
                {
                    this.DecrementPendingOutboundBytes();
                }
                finally
                {
                    this.Recycle();
                }
            }

            void DecrementPendingOutboundBytes()
            {
                if (EstimateTaskSizeOnSubmit)
                {
                    ctx.pipeline.DecrementPendingOutboundBytes(this.size);
                }
            }

            void Recycle()
            {
                // Set to null so the GC can collect them directly
                this.ctx = null;
                this.msg = null;
                this.promise = null;
                this.handle.Release(this);
            }

            protected virtual void Write(AbstractChannelHandlerContext ctx, object msg, IPromise promise) => ctx.InvokeWrite(msg, promise);
        }
        sealed class WriteTask : AbstractWriteTask
        {

            static readonly ThreadLocalPool<WriteTask> Recycler = new ThreadLocalPool<WriteTask>(handle => new WriteTask(handle));

            public static WriteTask NewInstance(AbstractChannelHandlerContext ctx, object msg, IPromise promise)
            {
                WriteTask task = Recycler.Take();
                Init(task, ctx, msg, promise);
                return task;
            }

            WriteTask(ThreadLocalPool.Handle handle)
                : base(handle)
            {
            }
        }

        sealed class WriteAndFlushTask : AbstractWriteTask
        {

            static readonly ThreadLocalPool<WriteAndFlushTask> Recycler = new ThreadLocalPool<WriteAndFlushTask>(handle => new WriteAndFlushTask(handle));

            public static WriteAndFlushTask NewInstance(
                    AbstractChannelHandlerContext ctx, object msg, IPromise promise)
            {
                WriteAndFlushTask task = Recycler.Take();
                Init(task, ctx, msg, promise);
                return task;
            }

            WriteAndFlushTask(ThreadLocalPool.Handle handle)
                : base(handle)
            {
            }

            protected override void Write(AbstractChannelHandlerContext ctx, object msg, IPromise promise)
            {
                base.Write(ctx, msg, promise);
                ctx.InvokeFlush();
            }
        }
    }
}
