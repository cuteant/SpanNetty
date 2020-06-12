// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;

    abstract partial class AbstractChannelHandlerContext : IChannelHandlerContext, IResourceLeakHint
    {
        private AbstractChannelHandlerContext v_next;
        internal AbstractChannelHandlerContext Next
        {
            [MethodImpl(InlineMethod.AggressiveInlining)]
            get => Volatile.Read(ref v_next);
            set => Interlocked.Exchange(ref v_next, value);
        }

        private AbstractChannelHandlerContext v_prev;
        internal AbstractChannelHandlerContext Prev
        {
            [MethodImpl(InlineMethod.AggressiveInlining)]
            get => Volatile.Read(ref v_prev);
            set => Interlocked.Exchange(ref v_prev, value);
        }

        internal readonly SkipFlags SkipPropagationFlags;

        internal readonly DefaultChannelPipeline _pipeline;
        private readonly bool _ordered;

        // Will be set to null if no child executor should be used, otherwise it will be set to the
        // child executor.
        internal readonly IEventExecutor _executor;
        private int v_handlerState = HandlerState.Init;

        protected AbstractChannelHandlerContext(DefaultChannelPipeline pipeline, IEventExecutor executor,
            string name, SkipFlags skipPropagationDirections)
        {
            if (pipeline is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.pipeline); }
            if (name is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.name); }

            _pipeline = pipeline;
            Name = name;
            _executor = executor;
            SkipPropagationFlags = skipPropagationDirections;
            // Its ordered if its driven by the EventLoop or the given Executor is an instanceof OrderedEventExecutor.
            _ordered = executor is null || executor is IOrderedEventExecutor;
        }

        public IChannel Channel => _pipeline.Channel;

        public IChannelPipeline Pipeline => _pipeline;

        public IByteBufferAllocator Allocator => Channel.Allocator;

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
            [MethodImpl(InlineMethod.AggressiveInlining)]
            get
            {
                // Store in local variable to reduce volatile reads.
                var thisState = Volatile.Read(ref v_handlerState);
                return thisState == HandlerState.AddComplete || (!_ordered && thisState == HandlerState.AddPending);
            }
        }

        public bool Removed => Volatile.Read(ref v_handlerState) == HandlerState.RemoveComplete;

        internal bool SetAddComplete()
        {
            var prevState = Volatile.Read(ref v_handlerState);
            int oldState;
            do
            {
                if (prevState == HandlerState.RemoveComplete) { return false; }
                oldState = prevState;
                // Ensure we never update when the handlerState is REMOVE_COMPLETE already.
                // oldState is usually ADD_PENDING but can also be REMOVE_COMPLETE when an EventExecutor is used that is not
                // exposing ordering guarantees.
                prevState = Interlocked.CompareExchange(ref v_handlerState, HandlerState.AddComplete, prevState);
            } while (prevState != oldState);
            return true;
        }

        internal void SetRemoved() => Interlocked.Exchange(ref v_handlerState, HandlerState.RemoveComplete);

        internal void SetAddPending()
        {
            var updated = HandlerState.Init == Interlocked.CompareExchange(ref v_handlerState, HandlerState.AddPending, HandlerState.Init);
            Debug.Assert(updated); // This should always be true as it MUST be called before setAddComplete() or setRemoved().
        }

        internal void CallHandlerAdded()
        {
            // We must call setAddComplete before calling handlerAdded. Otherwise if the handlerAdded method generates
            // any pipeline events ctx.handler() will miss them because the state will not allow it.
            if (SetAddComplete())
            {
                Handler.HandlerAdded(this);
            }
        }

        internal void CallHandlerRemoved()
        {
            try
            {
                // Only call handlerRemoved(...) if we called handlerAdded(...) before.
                if (Volatile.Read(ref v_handlerState) == HandlerState.AddComplete)
                {
                    Handler.HandlerRemoved(this);
                }
            }
            finally
            {
                // Mark the handler as removed in any case.
                SetRemoved();
            }
        }

        public IEventExecutor Executor => _executor ?? Channel.EventLoop;

        public string Name { get; }

        public IAttribute<T> GetAttribute<T>(AttributeKey<T> key)
            where T : class
        {
            return Channel.GetAttribute(key);
        }

        public bool HasAttribute<T>(AttributeKey<T> key)
            where T : class
        {
            return Channel.HasAttribute(key);
        }
        public IChannelHandlerContext FireChannelRegistered()
        {
            InvokeChannelRegistered(FindContextInbound());
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
            if (InvokeHandler)
            {
                try
                {
                    Handler.ChannelRegistered(this);
                }
                catch (Exception ex)
                {
                    NotifyHandlerException(ex);
                }
            }
            else
            {
                FireChannelRegistered();
            }
        }

        public IChannelHandlerContext FireChannelUnregistered()
        {
            InvokeChannelUnregistered(FindContextInbound());
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
            if (InvokeHandler)
            {
                try
                {
                    Handler.ChannelUnregistered(this);
                }
                catch (Exception t)
                {
                    NotifyHandlerException(t);
                }
            }
            else
            {
                FireChannelUnregistered();
            }
        }

        public IChannelHandlerContext FireChannelActive()
        {
            InvokeChannelActive(FindContextInbound());
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
            if (InvokeHandler)
            {
                try
                {
                    (Handler).ChannelActive(this);
                }
                catch (Exception ex)
                {
                    NotifyHandlerException(ex);
                }
            }
            else
            {
                FireChannelActive();
            }
        }

        public IChannelHandlerContext FireChannelInactive()
        {
            InvokeChannelInactive(FindContextInbound());
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
            if (InvokeHandler)
            {
                try
                {
                    Handler.ChannelInactive(this);
                }
                catch (Exception ex)
                {
                    NotifyHandlerException(ex);
                }
            }
            else
            {
                FireChannelInactive();
            }
        }

        public virtual IChannelHandlerContext FireExceptionCaught(Exception cause)
        {
            InvokeExceptionCaught(Next, cause); //InvokeExceptionCaught(this.FindContextInbound(), cause);
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
            if (InvokeHandler)
            {
                try
                {
                    Handler.ExceptionCaught(this, cause);
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
                FireExceptionCaught(cause);
            }
        }

        public IChannelHandlerContext FireUserEventTriggered(object evt)
        {
            InvokeUserEventTriggered(FindContextInbound(), evt);
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
            if (InvokeHandler)
            {
                try
                {
                    Handler.UserEventTriggered(this, evt);
                }
                catch (Exception ex)
                {
                    NotifyHandlerException(ex);
                }
            }
            else
            {
                FireUserEventTriggered(evt);
            }
        }

        public IChannelHandlerContext FireChannelRead(object msg)
        {
            InvokeChannelRead(FindContextInbound(), msg);
            return this;
        }

        internal static void InvokeChannelRead(AbstractChannelHandlerContext next, object msg)
        {
            if (msg is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.msg); }

            object m = next._pipeline.Touch(msg, next);
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
            if (InvokeHandler)
            {
                try
                {
                    Handler.ChannelRead(this, msg);
                }
                catch (Exception ex)
                {
                    NotifyHandlerException(ex);
                }
            }
            else
            {
                FireChannelRead(msg);
            }
        }

        public IChannelHandlerContext FireChannelReadComplete()
        {
            InvokeChannelReadComplete(FindContextInbound());
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
            if (InvokeHandler)
            {
                try
                {
                    (Handler).ChannelReadComplete(this);
                }
                catch (Exception ex)
                {
                    NotifyHandlerException(ex);
                }
            }
            else
            {
                FireChannelReadComplete();
            }
        }

        public IChannelHandlerContext FireChannelWritabilityChanged()
        {
            InvokeChannelWritabilityChanged(FindContextInbound());
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
            if (InvokeHandler)
            {
                try
                {
                    Handler.ChannelWritabilityChanged(this);
                }
                catch (Exception ex)
                {
                    NotifyHandlerException(ex);
                }
            }
            else
            {
                FireChannelWritabilityChanged();
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

            AbstractChannelHandlerContext next = FindContextOutbound();
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                return next.InvokeBindAsync(localAddress);
            }
            else
            {
                var promise = nextExecutor.NewPromise();
                _ = SafeExecuteOutbound(nextExecutor, new BindTask(next, promise, localAddress), promise, null, false);
                return promise.Task;
            }
        }

        Task InvokeBindAsync(EndPoint localAddress)
        {
            if (InvokeHandler)
            {
                try
                {
                    return Handler.BindAsync(this, localAddress);
                }
                catch (Exception ex)
                {
                    return ComposeExceptionTask(ex);
                }
            }

            return BindAsync(localAddress);
        }

        public Task ConnectAsync(EndPoint remoteAddress) => ConnectAsync(remoteAddress, null);

        public Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
        {
            AbstractChannelHandlerContext next = FindContextOutbound();
            if (remoteAddress is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.remoteAddress); }
            // todo: check for cancellation

            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                return next.InvokeConnectAsync(remoteAddress, localAddress);
            }
            else
            {
                var promise = nextExecutor.NewPromise();
                _ = SafeExecuteOutbound(nextExecutor, new ConnectTask(next, promise, remoteAddress, localAddress), promise, null, false);
                return promise.Task;

            }
        }

        Task InvokeConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
        {
            if (InvokeHandler)
            {
                try
                {
                    return Handler.ConnectAsync(this, remoteAddress, localAddress);
                }
                catch (Exception ex)
                {
                    return ComposeExceptionTask(ex);
                }
            }

            return ConnectAsync(remoteAddress, localAddress);
        }

        public Task DisconnectAsync() => DisconnectAsync(NewPromise());

        public Task DisconnectAsync(IPromise promise)
        {
            if (!Channel.Metadata.HasDisconnect)
            {
                // Translate disconnect to close if the channel has no notion of disconnect-reconnect.
                // So far, UDP/IP is the only transport that has such behavior.
                return CloseAsync(promise);
            }
            if (IsNotValidPromise(promise, false))
            {
                // cancelled
                return promise.Task;
            }

            AbstractChannelHandlerContext next = FindContextOutbound();
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeDisconnect(promise);
            }
            else
            {
                _ = SafeExecuteOutbound(nextExecutor, new DisconnectTask(next, promise), promise, null, false);
            }

            return promise.Task;
        }

        void InvokeDisconnect(IPromise promise)
        {
            if (InvokeHandler)
            {
                try
                {
                    Handler.Disconnect(this, promise);
                }
                catch (Exception ex)
                {
                    Util.SafeSetFailure(promise, ex, DefaultChannelPipeline.Logger);
                }
            }
            else
            {
                DisconnectAsync(promise);
            }
        }

        public Task CloseAsync() => CloseAsync(NewPromise());

        public Task CloseAsync(IPromise promise)
        {
            if (IsNotValidPromise(promise, false))
            {
                // cancelled
                return promise.Task;
            }

            AbstractChannelHandlerContext next = FindContextOutbound();
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeClose(promise);
            }
            else
            {
                _ = SafeExecuteOutbound(nextExecutor, new CloseTask(next, promise), promise, null, false);
            }

            return promise.Task;
        }

        void InvokeClose(IPromise promise)
        {
            if (InvokeHandler)
            {
                try
                {
                    Handler.Close(this, promise);
                }
                catch (Exception ex)
                {
                    Util.SafeSetFailure(promise, ex, DefaultChannelPipeline.Logger);
                }
            }
            else
            {
                CloseAsync(promise);
            }
        }

        public Task DeregisterAsync() => DeregisterAsync(NewPromise());

        public Task DeregisterAsync(IPromise promise)
        {
            if (IsNotValidPromise(promise, false))
            {
                // cancelled
                return promise.Task;
            }

            AbstractChannelHandlerContext next = FindContextOutbound();
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeDeregister(promise);
            }
            else
            {
                _ = SafeExecuteOutbound(nextExecutor, new DeregisterTask(next, promise), promise, null, false);
            }

            return promise.Task;
        }

        void InvokeDeregister(IPromise promise)
        {
            if (InvokeHandler)
            {
                try
                {
                    Handler.Deregister(this, promise);
                }
                catch (Exception ex)
                {
                    Util.SafeSetFailure(promise, ex, DefaultChannelPipeline.Logger);
                }
            }
            else
            {
                DeregisterAsync(promise);
            }
        }

        public IChannelHandlerContext Read()
        {
            AbstractChannelHandlerContext next = FindContextOutbound();
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
            if (InvokeHandler)
            {
                try
                {
                    Handler.Read(this);
                }
                catch (Exception ex)
                {
                    NotifyHandlerException(ex);
                }
            }
            else
            {
                Read();
            }
        }

        public Task WriteAsync(object msg) => WriteAsync(msg, NewPromise());

        public Task WriteAsync(object msg, IPromise promise)
        {
            Write(msg, false, promise);
            return promise.Task;
        }

        void InvokeWrite(object msg, IPromise promise)
        {
            if (InvokeHandler)
            {
                InvokeWrite0(msg, promise);
            }
            else
            {
                WriteAsync(msg, promise);
            }

        }

        void InvokeWrite0(object msg, IPromise promise)
        {
            try
            {
                Handler.Write(this, msg, promise);
            }
            catch (Exception ex)
            {
                Util.SafeSetFailure(promise, ex, DefaultChannelPipeline.Logger);
            }
        }

        public IChannelHandlerContext Flush()
        {
            AbstractChannelHandlerContext next = FindContextOutbound();
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeFlush();
            }
            else
            {
                _ = SafeExecuteOutbound(nextExecutor, new FlushTask(next), VoidPromise(), null, false);
            }
            return this;
        }

        void InvokeFlush()
        {
            if (InvokeHandler)
            {
                InvokeFlush0();
            }
            else
            {
                Flush();
            }
        }

        void InvokeFlush0()
        {
            try
            {
                Handler.Flush(this);
            }
            catch (Exception ex)
            {
                NotifyHandlerException(ex);
            }
        }

        public Task WriteAndFlushAsync(object message) => WriteAndFlushAsync(message, NewPromise());

        public Task WriteAndFlushAsync(object message, IPromise promise)
        {
            Write(message, true, promise);
            return promise.Task;
        }

        void InvokeWriteAndFlush(object msg, IPromise promise)
        {
            if (InvokeHandler)
            {
                InvokeWrite0(msg, promise);
                InvokeFlush0();
            }
            else
            {
                WriteAndFlushAsync(msg, promise);
            }
        }

        void Write(object msg, bool flush, IPromise promise)
        {
            if (msg is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.msg); }

            try
            {
                if (IsNotValidPromise(promise, true))
                {
                    ReferenceCountUtil.Release(msg);
                    // cancelled
                    return;
                }
            }
            catch (Exception)
            {
                ReferenceCountUtil.Release(msg);
                throw;
            }

            AbstractChannelHandlerContext next = FindContextOutbound();
            object m = _pipeline.Touch(msg, next);
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
                var task = WriteTask.NewInstance(next, m, promise, flush);
                if (!SafeExecuteOutbound(nextExecutor, task, promise, msg, !flush))
                {
                    // We failed to submit the WriteTask. We need to cancel it so we decrement the pending bytes
                    // and put it back in the Recycler for re-use later.
                    //
                    // See https://github.com/netty/netty/issues/8343.
                    task.Cancel();
                }
            }
        }

        public IPromise NewPromise() => new TaskCompletionSource();

        public IPromise NewPromise(object state) => new TaskCompletionSource(state);

        public IPromise VoidPromise() => Channel.VoidPromise();

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

            InvokeExceptionCaught(cause);
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

        static bool SafeExecuteOutbound(IEventExecutor executor, IRunnable task,
            IPromise promise, object msg, bool lazy)
        {
            try
            {
                if (lazy && executor is AbstractEventExecutor eventExecutor)
                {
                    eventExecutor.LazyExecute(task);
                }
                else
                {
                    executor.Execute(task);
                }
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

        public string ToHintString() => $"\'{Name}\' will handle the message from this point.";

        public override string ToString() => $"{typeof(IChannelHandlerContext).Name} ({Name}, {Channel})";

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

    }
}
