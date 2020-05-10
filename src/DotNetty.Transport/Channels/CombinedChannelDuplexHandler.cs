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
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    public partial class CombinedChannelDuplexHandler<TIn, TOut> : ChannelDuplexHandler
        where TIn : IChannelHandler
        where TOut : IChannelHandler
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<CombinedChannelDuplexHandler<TIn, TOut>>();

        DelegatingChannelHandlerContext inboundCtx;
        DelegatingChannelHandlerContext outboundCtx;
        int handlerAdded;

        protected CombinedChannelDuplexHandler()
        {
            this.EnsureNotSharable();
        }

        public CombinedChannelDuplexHandler(TIn inboundHandler, TOut outboundHandler)
        {
            if (inboundHandler is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.inboundHandler); }
            if (outboundHandler is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.outboundHandler); }

            this.EnsureNotSharable();
            this.Init(inboundHandler, outboundHandler);
        }

        protected void Init(TIn inbound, TOut outbound)
        {
            this.Validate(inbound, outbound);

            this.InboundHandler = inbound;
            this.OutboundHandler = outbound;
        }

        protected TIn InboundHandler { get; private set; }

        protected TOut OutboundHandler { get; private set; }

        void Validate(TIn inbound, TOut outbound)
        {
            if (this.InboundHandler is object)
            {
                ThrowHelper.ThrowInvalidOperationException_InitCannotBeInvokedIf(this);
            }

            if (inbound is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.inbound);
            }

            if (outbound is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.outbound);
            }
        }

        [MethodImpl(InlineMethod.Value)]
        void CheckAdded()
        {
            if (SharedConstants.False == Volatile.Read(ref this.handlerAdded))
            {
                ThrowHelper.ThrowInvalidOperationException_HandlerNotAddedToPipeYet();
            }
        }

        public void RemoveInboundHandler()
        {
            this.CheckAdded();
            this.inboundCtx.Remove();
        }

        public void RemoveOutboundHandler()
        {
            this.CheckAdded();
            this.outboundCtx.Remove();
        }

        public override void HandlerAdded(IChannelHandlerContext context)
        {
            if (this.InboundHandler is null)
            {
                ThrowHelper.ThrowInvalidOperationException_InitMustBeInvokedBefore(this);
            }

            this.outboundCtx = new DelegatingChannelHandlerContext(context, this.OutboundHandler);
            this.inboundCtx = new DelegatingChannelHandlerContext(context, this.InboundHandler, OnExceptionCaught);
                //cause =>
                //{
                //    try
                //    {
                //        this.OutboundHandler.ExceptionCaught(this.outboundCtx, cause);
                //    }
                //    catch (Exception error)
                //    {
                //        if (Logger.DebugEnabled)
                //        {
                //            Logger.FreedThreadLocalBufferFromThreadFull(error, cause);
                //        }
                //        else if (Logger.WarnEnabled)
                //        {
                //            Logger.FreedThreadLocalBufferFromThread(error, cause);
                //        }
                //    }
                //});

            // The inboundCtx and outboundCtx were created and set now it's safe to call removeInboundHandler() and
            // removeOutboundHandler().
            Interlocked.Exchange(ref this.handlerAdded, SharedConstants.True);

            try
            {
                this.InboundHandler.HandlerAdded(this.inboundCtx);
            }
            finally
            {
                this.OutboundHandler.HandlerAdded(this.outboundCtx);
            }
        }

        public override void HandlerRemoved(IChannelHandlerContext context)
        {
            try
            {
                this.inboundCtx.Remove();
            }
            finally
            {
                this.outboundCtx.Remove();
            }
        }

        public override void ChannelRegistered(IChannelHandlerContext context)
        {
            Debug.Assert(context == this.inboundCtx.InnerContext);

            if (!this.inboundCtx.Removed)
            {
                this.InboundHandler.ChannelRegistered(this.inboundCtx);
            }
            else
            {
                this.inboundCtx.FireChannelRegistered();
            }
        }

        public override void ChannelUnregistered(IChannelHandlerContext context)
        {
            Debug.Assert(context == this.inboundCtx.InnerContext);

            if (!this.inboundCtx.Removed)
            {
                this.InboundHandler.ChannelUnregistered(this.inboundCtx);
            }
            else
            {
                this.inboundCtx.FireChannelUnregistered();
            }
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            Debug.Assert(context == this.inboundCtx.InnerContext);

            if (!this.inboundCtx.Removed)
            {
                this.InboundHandler.ChannelActive(this.inboundCtx);
            }
            else
            {
                this.inboundCtx.FireChannelActive();
            }
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            Debug.Assert(context == this.inboundCtx.InnerContext);

            if (!this.inboundCtx.Removed)
            {
                this.InboundHandler.ChannelInactive(this.inboundCtx);
            }
            else
            {
                this.inboundCtx.FireChannelInactive();
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Debug.Assert(context == this.inboundCtx.InnerContext);

            if (!this.inboundCtx.Removed)
            {
                this.InboundHandler.ExceptionCaught(this.inboundCtx, exception);
            }
            else
            {
                this.inboundCtx.FireExceptionCaught(exception);
            }
        }

        public override void UserEventTriggered(IChannelHandlerContext context, object evt)
        {
            Debug.Assert(context == this.inboundCtx.InnerContext);

            if (!this.inboundCtx.Removed)
            {
                this.InboundHandler.UserEventTriggered(this.inboundCtx, evt);
            }
            else
            {
                this.inboundCtx.FireUserEventTriggered(evt);
            }
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            Debug.Assert(context == this.inboundCtx.InnerContext);

            if (!this.inboundCtx.Removed)
            {
                this.InboundHandler.ChannelRead(this.inboundCtx, message);
            }
            else
            {
                this.inboundCtx.FireChannelRead(message);
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            Debug.Assert(context == this.inboundCtx.InnerContext);

            if (!this.inboundCtx.Removed)
            {
                this.InboundHandler.ChannelReadComplete(this.inboundCtx);
            }
            else
            {
                this.inboundCtx.FireChannelReadComplete();
            }
        }

        public override void ChannelWritabilityChanged(IChannelHandlerContext context)
        {
            Debug.Assert(context == this.inboundCtx.InnerContext);

            if (!this.inboundCtx.Removed)
            {
                this.InboundHandler.ChannelWritabilityChanged(this.inboundCtx);
            }
            else
            {
                this.inboundCtx.FireChannelWritabilityChanged();
            }
        }

        public override Task BindAsync(IChannelHandlerContext context, EndPoint localAddress)
        {
            Debug.Assert(context == this.outboundCtx.InnerContext);

            if (!this.outboundCtx.Removed)
            {
                return this.OutboundHandler.BindAsync(this.outboundCtx, localAddress);
            }
            else
            {
                return this.outboundCtx.BindAsync(localAddress);
            }
        }

        public override Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress)
        {
            Debug.Assert(context == this.outboundCtx.InnerContext);

            if (!this.outboundCtx.Removed)
            {
                return this.OutboundHandler.ConnectAsync(this.outboundCtx, remoteAddress, localAddress);
            }
            else
            {
                return this.outboundCtx.ConnectAsync(localAddress);
            }
        }

        public override void Disconnect(IChannelHandlerContext context, IPromise promise)
        {
            Debug.Assert(context == this.outboundCtx.InnerContext);

            if (!this.outboundCtx.Removed)
            {
                this.OutboundHandler.Disconnect(this.outboundCtx, promise);
            }
            else
            {
                this.outboundCtx.DisconnectAsync(promise);
            }
        }

        public override void Close(IChannelHandlerContext context, IPromise promise)
        {
            Debug.Assert(context == this.outboundCtx.InnerContext);

            if (!this.outboundCtx.Removed)
            {
                this.OutboundHandler.Close(this.outboundCtx, promise);
            }
            else
            {
                this.outboundCtx.CloseAsync(promise);
            }
        }

        public override void Deregister(IChannelHandlerContext context, IPromise promise)
        {
            Debug.Assert(context == this.outboundCtx.InnerContext);

            if (!this.outboundCtx.Removed)
            {
                this.OutboundHandler.Deregister(this.outboundCtx, promise);
            }
            else
            {
                this.outboundCtx.DeregisterAsync(promise);
            }
        }

        public override void Read(IChannelHandlerContext context)
        {
            Debug.Assert(context == this.outboundCtx.InnerContext);

            if (!this.outboundCtx.Removed)
            {
                this.OutboundHandler.Read(this.outboundCtx);
            }
            else
            {
                this.outboundCtx.Read();
            }
        }

        public override void Write(IChannelHandlerContext context, object message, IPromise promise)
        {
            Debug.Assert(context == this.outboundCtx.InnerContext);

            if (!this.outboundCtx.Removed)
            {
                this.OutboundHandler.Write(this.outboundCtx, message, promise);
            }
            else
            {
                this.outboundCtx.WriteAsync(message, promise);
            }
        }

        public override void Flush(IChannelHandlerContext context)
        {
            Debug.Assert(context == this.outboundCtx.InnerContext);

            if (!this.outboundCtx.Removed)
            {
                this.OutboundHandler.Flush(this.outboundCtx);
            }
            else
            {
                this.outboundCtx.Flush();
            }
        }

        sealed partial class DelegatingChannelHandlerContext : IChannelHandlerContext
        {

            readonly IChannelHandlerContext ctx;
            readonly IChannelHandler handler;
            readonly Action<Exception> onError;
            bool removed;

            public DelegatingChannelHandlerContext(IChannelHandlerContext ctx, IChannelHandler handler, Action<Exception> onError = null)
            {
                this.ctx = ctx;
                this.handler = handler;
                this.onError = onError;
            }

            public IChannelHandlerContext InnerContext => this.ctx;

            public IChannel Channel => this.ctx.Channel;

            public IChannelPipeline Pipeline => this.ctx.Pipeline;

            public IByteBufferAllocator Allocator => this.ctx.Allocator;

            public IEventExecutor Executor => this.ctx.Executor;

            public string Name => this.ctx.Name;

            public IChannelHandler Handler => this.ctx.Handler;

            public bool Removed => this.removed || this.ctx.Removed;

            public IChannelHandlerContext FireChannelRegistered()
            {
                this.ctx.FireChannelRegistered();
                return this;
            }

            public IChannelHandlerContext FireChannelUnregistered()
            {
                this.ctx.FireChannelUnregistered();
                return this;
            }

            public IChannelHandlerContext FireChannelActive()
            {
                this.ctx.FireChannelActive();
                return this;
            }

            public IChannelHandlerContext FireChannelInactive()
            {
                this.ctx.FireChannelInactive();
                return this;
            }

            public IChannelHandlerContext FireExceptionCaught(Exception ex)
            {
                if (this.onError is object)
                {
                    this.onError(ex);
                }
                else
                {
                    this.ctx.FireExceptionCaught(ex);
                }

                return this;
            }

            public IChannelHandlerContext FireUserEventTriggered(object evt)
            {
                this.ctx.FireUserEventTriggered(evt);
                return this;
            }

            public IChannelHandlerContext FireChannelRead(object message)
            {
                this.ctx.FireChannelRead(message);
                return this;
            }

            public IChannelHandlerContext FireChannelReadComplete()
            {
                this.ctx.FireChannelReadComplete();
                return this;
            }

            public IChannelHandlerContext FireChannelWritabilityChanged()
            {
                this.ctx.FireChannelWritabilityChanged();
                return this;
            }

            public Task BindAsync(EndPoint localAddress) => this.ctx.BindAsync(localAddress);

            public Task ConnectAsync(EndPoint remoteAddress) => this.ctx.ConnectAsync(remoteAddress);

            public Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress) => this.ctx.ConnectAsync(remoteAddress, localAddress);

            public Task DisconnectAsync() => this.ctx.DisconnectAsync();

            public Task DisconnectAsync(IPromise promise) => this.ctx.DisconnectAsync(promise);

            public Task CloseAsync() => this.ctx.CloseAsync();

            public Task CloseAsync(IPromise promise) => this.ctx.CloseAsync(promise);

            public Task DeregisterAsync() => this.ctx.DeregisterAsync();

            public Task DeregisterAsync(IPromise promise) => this.ctx.DeregisterAsync(promise);

            public IChannelHandlerContext Read()
            {
                this.ctx.Read();
                return this;
            }

            public Task WriteAsync(object message) => this.ctx.WriteAsync(message);

            public Task WriteAsync(object message, IPromise promise) => this.ctx.WriteAsync(message, promise);

            public IChannelHandlerContext Flush()
            {
                this.ctx.Flush();
                return this;
            }

            public Task WriteAndFlushAsync(object message) => this.ctx.WriteAndFlushAsync(message);

            public Task WriteAndFlushAsync(object message, IPromise promise) => this.ctx.WriteAndFlushAsync(message, promise);

            public IAttribute<T> GetAttribute<T>(AttributeKey<T> key) where T : class => this.ctx.GetAttribute(key);

            public bool HasAttribute<T>(AttributeKey<T> key) where T : class => this.ctx.HasAttribute(key);

            public IPromise NewPromise() => this.ctx.NewPromise();

            public IPromise NewPromise(object state) => this.ctx.NewPromise(state);

            public IPromise VoidPromise() => this.ctx.VoidPromise();

            internal void Remove()
            {
                IEventExecutor executor = this.Executor;
                if (executor.InEventLoop)
                {
                    this.Remove0();
                }
                else
                {
                    executor.Execute(RemoveAction, this);
                }
            }

            void Remove0()
            {
                if (this.removed)
                {
                    return;
                }

                this.removed = true;
                try
                {
                    this.handler.HandlerRemoved(this);
                }
                catch (Exception cause)
                {
                    this.FireExceptionCaught(
                        new ChannelPipelineException($"{StringUtil.SimpleClassName(this.handler)}.handlerRemoved() has thrown an exception.", cause));
                }
            }
        }
    }
}
