// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Collections.Concurrent;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Bootstrapping;

    /// <summary>
    /// A special <see cref="IChannelHandler"/> which offers an easy way to initialize a <see cref="IChannel"/> once it was
    /// registered to its <see cref="IEventLoop"/>.
    /// <para>
    /// Implementations are most often used in the context of <see cref="AbstractBootstrap{TBootstrap,TChannel}.Handler(IChannelHandler)"/>
    /// and <see cref="ServerBootstrap.ChildHandler"/> to setup the <see cref="IChannelPipeline"/> of a <see cref="IChannel"/>.
    /// </para>
    /// Be aware that this class is marked as Sharable (via <see cref="IsSharable"/>) and so the implementation must be safe to be re-used.
    /// </summary>
    /// <example>
    /// <code>
    /// public class MyChannelInitializer extends <see cref="ChannelInitializer{T}"/> {
    ///     public void InitChannel(<see cref="IChannel"/> channel) {
    ///         channel.Pipeline().AddLast("myHandler", new MyHandler());
    ///     }
    /// }
    /// <see cref="ServerBootstrap"/> bootstrap = ...;
    /// ...
    /// bootstrap.childHandler(new MyChannelInitializer());
    /// ...
    /// </code>
    /// </example>
    /// <typeparam name="T">A sub-type of <see cref="IChannel"/>.</typeparam>
    public abstract class ChannelInitializer<T> : ChannelHandlerAdapter
        where T : IChannel
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<ChannelInitializer<T>>();

        readonly ConcurrentDictionary<IChannelHandlerContext, bool> initMap = new ConcurrentDictionary<IChannelHandlerContext, bool>(ChannelHandlerContextComparer.Default);

        /// <summary>
        /// This method will be called once the <see cref="IChannel"/> was registered. After the method returns this instance
        /// will be removed from the <see cref="IChannelPipeline"/> of the <see cref="IChannel"/>.
        /// </summary>
        /// <param name="channel">The <see cref="IChannel"/> which was registered.</param>
        protected abstract void InitChannel(T channel);

        public override bool IsSharable => true;

        public sealed override void ChannelRegistered(IChannelHandlerContext ctx)
        {
            // Normally this method will never be called as handlerAdded(...) should call initChannel(...) and remove
            // the handler.
            if (this.InitChannel(ctx))
            {
                // we called InitChannel(...) so we need to call now pipeline.fireChannelRegistered() to ensure we not
                // miss an event.
                ctx.Pipeline.FireChannelRegistered();

                // We are done with init the Channel, removing all the state for the Channel now.
                this.RemoveState(ctx);
            }
            else
            {
                // Called InitChannel(...) before which is the expected behavior, so just forward the event.
                ctx.FireChannelRegistered();
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            if (Logger.WarnEnabled) Logger.FailedToInitializeAChannel(ctx, cause);
            ctx.CloseAsync();
        }

        public override void HandlerAdded(IChannelHandlerContext ctx)
        {
            if (ctx.Channel.Registered)
            {
                // This should always be true with our current DefaultChannelPipeline implementation.
                // The good thing about calling InitChannel(...) in HandlerAdded(...) is that there will be no ordering
                // surprises if a ChannelInitializer will add another ChannelInitializer. This is as all handlers
                // will be added in the expected order.
                if (this.InitChannel(ctx))
                {

                    // We are done with init the Channel, removing the initializer now.
                    this.RemoveState(ctx);
                }
            }
        }

        public override void HandlerRemoved(IChannelHandlerContext ctx)
        {
            this.initMap.TryRemove(ctx, out _);
        }

        bool InitChannel(IChannelHandlerContext ctx)
        {
            if (this.initMap.TryAdd(ctx, true)) // Guard against re-entrance.
            {
                try
                {
                    this.InitChannel((T)ctx.Channel);
                }
                catch (Exception cause)
                {
                    // Explicitly call exceptionCaught(...) as we removed the handler before calling initChannel(...).
                    // We do so to prevent multiple calls to initChannel(...).
                    this.ExceptionCaught(ctx, cause);
                }
                finally
                {
                    var pipeline = ctx.Pipeline;
                    if (pipeline.Context(this) is object)
                    {
                        pipeline.Remove(this);
                    }
                }
                return true;
            }
            return false;
        }

        void RemoveState(IChannelHandlerContext ctx)
        {
            // The removal may happen in an async fashion if the EventExecutor we use does something funky.
            if (ctx.Removed)
            {
                this.initMap.TryRemove(ctx, out _);
            }
            else
            {
                // The context is not removed yet which is most likely the case because a custom EventExecutor is used.
                // Let's schedule it on the EventExecutor to give it some more time to be completed in case it is offloaded.
                ctx.Executor.Execute(() => this.initMap.TryRemove(ctx, out _));
            }
        }
    }
}