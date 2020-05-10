// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Bootstrapping
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using CuteAnt.Collections;
    using CuteAnt.Pool;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// A <see cref="Bootstrap"/> sub-class which allows easy bootstrapping of <see cref="IServerChannel"/>.
    /// </summary>
    public partial class ServerBootstrap : AbstractBootstrap<ServerBootstrap, IServerChannel>
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<ServerBootstrap>();

        readonly CachedReadConcurrentDictionary<ChannelOption, ChannelOptionValue> childOptions;
        readonly CachedReadConcurrentDictionary<IConstant, AttributeValue> childAttrs;
        IEventLoopGroup _childGroup;
        IChannelHandler _childHandler;

        public ServerBootstrap()
        {
            this.childOptions = new CachedReadConcurrentDictionary<ChannelOption, ChannelOptionValue>(ChannelOptionComparer.Default);
            this.childAttrs = new CachedReadConcurrentDictionary<IConstant, AttributeValue>(ConstantComparer.Default);
        }

        ServerBootstrap(ServerBootstrap bootstrap)
            : base(bootstrap)
        {
            InternalChildGroup = bootstrap.InternalChildGroup;
            InternalChildHandler = bootstrap.InternalChildHandler;
            this.childOptions = new CachedReadConcurrentDictionary<ChannelOption, ChannelOptionValue>(bootstrap.childOptions, ChannelOptionComparer.Default);
            this.childAttrs = new CachedReadConcurrentDictionary<IConstant, AttributeValue>(bootstrap.childAttrs, ConstantComparer.Default);
        }

        /// <summary>
        /// Specifies the <see cref="IEventLoopGroup"/> which is used for the parent (acceptor) and the child (client).
        /// </summary>
        public override ServerBootstrap Group(IEventLoopGroup group) => this.Group(group, group);

        /// <summary>
        /// Sets the <see cref="IEventLoopGroup"/> for the parent (acceptor) and the child (client). These
        /// <see cref="IEventLoopGroup"/>'s are used to handle all the events and IO for <see cref="IServerChannel"/>
        /// and <see cref="IChannel"/>'s.
        /// </summary>
        public ServerBootstrap Group(IEventLoopGroup parentGroup, IEventLoopGroup childGroup)
        {
            if (childGroup is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.childGroup); }

            base.Group(parentGroup);
            if (InternalChildGroup is object)
            {
                ThrowHelper.ThrowInvalidOperationException_ChildGroupSetAlready();
            }
            InternalChildGroup = childGroup;
            return this;
        }

        /// <summary>
        /// Allows specification of a <see cref="ChannelOption"/> which is used for the <see cref="IChannel"/>
        /// instances once they get created (after the acceptor accepted the <see cref="IChannel"/>). Use a
        /// value of <c>null</c> to remove a previously set <see cref="ChannelOption"/>.
        /// </summary>
        public ServerBootstrap ChildOption<T>(ChannelOption<T> childOption, T value)
        {
            if (childOption is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.childOption); }

            if (value is null)
            {
                //ChannelOptionValue removed;
                this.childOptions.TryRemove(childOption, out _);
            }
            else
            {
                this.childOptions[childOption] = new ChannelOptionValue<T>(childOption, value);
            }
            return this;
        }

        /// <summary>
        /// Sets the specific <see cref="AttributeKey{T}"/> with the given value on every child <see cref="IChannel"/>.
        /// If the value is <c>null</c>, the <see cref="AttributeKey{T}"/> is removed.
        /// </summary>
        public ServerBootstrap ChildAttribute<T>(AttributeKey<T> childKey, T value)
            where T : class
        {
            if (childKey is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.childKey); }

            if (value is null)
            {
                //AttributeValue removed;
                this.childAttrs.TryRemove(childKey, out _);
            }
            else
            {
                this.childAttrs[childKey] = new AttributeValue<T>(childKey, value);
            }
            return this;
        }

        /// <summary>
        /// Sets the <see cref="IChannelHandler"/> which is used to serve the request for the <see cref="IChannel"/>'s.
        /// </summary>
        public ServerBootstrap ChildHandler(IChannelHandler childHandler)
        {
            if (childHandler is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.childHandler); }

            InternalChildHandler = childHandler;
            return this;
        }

        /// <summary>
        /// Returns the configured <see cref="IEventLoopGroup"/> which will be used for the child channels or <c>null</c>
        /// if none is configured yet.
        /// </summary>
        public IEventLoopGroup ChildGroup() => Volatile.Read(ref _childGroup);

        protected override void Init(IChannel channel)
        {
            SetChannelOptions(channel, this.Options, Logger);

            foreach (AttributeValue e in this.Attributes)
            {
                e.Set(channel);
            }

            IChannelPipeline p = channel.Pipeline;
            IChannelHandler channelHandler = this.Handler();
            if (channelHandler is object)
            {
                p.AddLast((string)null, channelHandler);
            }

            IEventLoopGroup currentChildGroup = InternalChildGroup;
            IChannelHandler currentChildHandler = InternalChildHandler;
            ChannelOptionValue[] currentChildOptions = this.childOptions.Values.ToArray();
            AttributeValue[] currentChildAttrs = this.childAttrs.Values.ToArray();

            p.AddLast(new ActionChannelInitializer<IChannel>(ch =>
            {
                ch.Pipeline.AddLast(new ServerBootstrapAcceptor(currentChildGroup, currentChildHandler,
                    currentChildOptions, currentChildAttrs));
            }));
        }

        public override ServerBootstrap Validate()
        {
            base.Validate();
            if (InternalChildHandler is null)
            {
                ThrowHelper.ThrowInvalidOperationException_ChildHandlerNotYet();
            }
            if (InternalChildGroup is null)
            {
                if (Logger.WarnEnabled) Logger.ChildGroupIsNotSetUsingParentGroupInstead();
                InternalChildGroup = this.Group();
            }
            return this;
        }

        class ServerBootstrapAcceptor : ChannelHandlerAdapter
        {
            readonly IEventLoopGroup childGroup;
            readonly IChannelHandler childHandler;
            readonly ChannelOptionValue[] childOptions;
            readonly AttributeValue[] childAttrs;

            public ServerBootstrapAcceptor(
                IEventLoopGroup childGroup, IChannelHandler childHandler,
                ChannelOptionValue[] childOptions, AttributeValue[] childAttrs)
            {
                this.childGroup = childGroup;
                this.childHandler = childHandler;
                this.childOptions = childOptions;
                this.childAttrs = childAttrs;
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                var child = (IChannel)msg;

                child.Pipeline.AddLast((string)null, this.childHandler);

                SetChannelOptions(child, this.childOptions, Logger);

                foreach (AttributeValue attr in this.childAttrs)
                {
                    attr.Set(child);
                }

                // todo: async/await instead?
                try
                {
#if NET40
                    void continuationAction(Task future) => ForceClose(child, future.Exception);
                    this.childGroup.RegisterAsync(child).ContinueWith(
                        continuationAction,
                        TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);
#else
                    this.childGroup.RegisterAsync(child).ContinueWith(
                        s_closeAfterRegisterAction, child,
                        TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);
#endif
                }
                catch (Exception ex)
                {
                    ForceClose(child, ex);
                }
            }

            static readonly Action<Task, object> s_closeAfterRegisterAction = CloseAfterRegisterAction;
            static void CloseAfterRegisterAction(Task future, object state)
            {
                ForceClose((IChannel)state, future.Exception);
            }

            static void ForceClose(IChannel child, Exception ex)
            {
                child.Unsafe.CloseForcibly();
                if (Logger.WarnEnabled) Logger.ChildGroupIsNotSetUsingParentGroupInstead(child, ex);
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                IChannelConfiguration config = ctx.Channel.Configuration;
                if (config.AutoRead)
                {
                    // stop accept new connections for 1 second to allow the channel to recover
                    // See https://github.com/netty/netty/issues/1328
                    config.AutoRead = false;
                    ctx.Channel.EventLoop.ScheduleAsync(c => { ((IChannelConfiguration)c).AutoRead = true; }, config, TimeSpan.FromSeconds(1));
                }
                // still let the ExceptionCaught event flow through the pipeline to give the user
                // a chance to do something with it
                ctx.FireExceptionCaught(cause);
            }
        }

        public override ServerBootstrap Clone() => new ServerBootstrap(this);

        public override string ToString()
        {
            var buf = StringBuilderManager.Allocate().Append(base.ToString());
            buf.Length = buf.Length - 1;
            buf.Append(", ");
            var childGroup = InternalChildGroup;
            if (childGroup is object)
            {
                buf.Append("childGroup: ")
                    .Append(childGroup.GetType().Name)
                    .Append(", ");
            }
            buf.Append("childOptions: ")
                .Append(this.childOptions.ToDebugString())
                .Append(", ");
            // todo: attrs
            //lock (childAttrs)
            //{
            //    if (!childAttrs.isEmpty())
            //    {
            //        buf.Append("childAttrs: ");
            //        buf.Append(childAttrs);
            //        buf.Append(", ");
            //    }
            //}
            var childHandler = InternalChildHandler;
            if (childHandler is object)
            {
                buf.Append("childHandler: ");
                buf.Append(childHandler);
                buf.Append(", ");
            }
            if (buf[buf.Length - 1] == '(')
            {
                buf.Append(')');
            }
            else
            {
                buf[buf.Length - 2] = ')';
                buf.Length = buf.Length - 1;
            }

            return StringBuilderManager.ReturnAndFree(buf);
        }
    }
}