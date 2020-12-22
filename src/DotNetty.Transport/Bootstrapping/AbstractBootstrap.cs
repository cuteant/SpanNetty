﻿/*
 * Copyright 2012 The Netty Project
 *
 * The Netty Project licenses this file to you under the Apache License,
 * version 2.0 (the "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at:
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * Copyright (c) The DotNetty Project (Microsoft). All rights reserved.
 *
 *   https://github.com/azure/dotnetty
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Transport.Bootstrapping
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// This is a helper class that makes it easy to bootstrap an <see cref="IChannel"/>. It supports method-
    /// chaining to provide an easy way to configure the <see cref="AbstractBootstrap{TBootstrap,TChannel}"/>.
    /// 
    /// When not used in a <see cref="ServerBootstrap"/> context, the <see cref="BindAsync(EndPoint)"/> methods
    /// are useful for connectionless transports such as datagram (UDP).
    /// </summary>
    public abstract class AbstractBootstrap<TBootstrap, TChannel>
        where TBootstrap : AbstractBootstrap<TBootstrap, TChannel>
        where TChannel : IChannel
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<AbstractBootstrap<TBootstrap, TChannel>>();

        private IEventLoopGroup v_group;
        private Func<TChannel> v_channelFactory;
        private EndPoint v_localAddress;
        private readonly CachedReadConcurrentDictionary<ChannelOption, ChannelOptionValue> _options;
        private readonly CachedReadConcurrentDictionary<IConstant, AttributeValue> _attrs;
        private IChannelHandler v_handler;

        internal IEventLoopGroup InternalGroup
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => Volatile.Read(ref v_group);
            set => Interlocked.Exchange(ref v_group, value);
        }

        private Func<TChannel> InternalChannelFactory
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => Volatile.Read(ref v_channelFactory);
            set => Interlocked.Exchange(ref v_channelFactory, value);
        }

        private EndPoint InternalLocalAddress
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => Volatile.Read(ref v_localAddress);
            set => Interlocked.Exchange(ref v_localAddress, value);
        }

        private IChannelHandler InternalHandler
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => Volatile.Read(ref v_handler);
            set => Interlocked.Exchange(ref v_handler, value);
        }

        protected internal AbstractBootstrap()
        {
            _options = new CachedReadConcurrentDictionary<ChannelOption, ChannelOptionValue>(ChannelOptionComparer.Default);
            _attrs = new CachedReadConcurrentDictionary<IConstant, AttributeValue>(ConstantComparer.Default);
            // Disallow extending from a different package.
        }

        protected internal AbstractBootstrap(AbstractBootstrap<TBootstrap, TChannel> bootstrap)
        {
            InternalGroup = bootstrap.InternalGroup;
            InternalChannelFactory = bootstrap.InternalChannelFactory;
            InternalHandler = bootstrap.InternalHandler;
            InternalLocalAddress = bootstrap.InternalLocalAddress;
            _options = new CachedReadConcurrentDictionary<ChannelOption, ChannelOptionValue>(bootstrap._options, ChannelOptionComparer.Default);
            _attrs = new CachedReadConcurrentDictionary<IConstant, AttributeValue>(bootstrap._attrs, ConstantComparer.Default);
        }

        /// <summary>
        /// Specifies the <see cref="IEventLoopGroup"/> which will handle events for the <see cref="IChannel"/> being built.
        /// </summary>
        /// <param name="group">The <see cref="IEventLoopGroup"/> which is used to handle all the events for the to-be-created <see cref="IChannel"/>.</param>
        /// <returns>The <see cref="AbstractBootstrap{TBootstrap,TChannel}"/> instance.</returns>
        public virtual TBootstrap Group(IEventLoopGroup group)
        {
            if (group is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.group); }

            if (InternalGroup is object)
            {
                ThrowHelper.ThrowInvalidOperationException_GroupHasAlreadyBeenSet();
            }
            InternalGroup = group;
            return (TBootstrap)this;
        }

        /// <summary>
        /// Specifies the <see cref="Type"/> of <see cref="IChannel"/> which will be created.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> which is used to create <see cref="IChannel"/> instances from.</typeparam>
        /// <returns>The <see cref="AbstractBootstrap{TBootstrap,TChannel}"/> instance.</returns>
        public TBootstrap Channel<T>() where T : TChannel, new() => ChannelFactory(() => new T());

        public TBootstrap ChannelFactory(Func<TChannel> channelFactory)
        {
            if (channelFactory is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.channelFactory); }
            if (InternalChannelFactory is object)
            {
                ThrowHelper.ThrowInvalidOperationException_ChannelFactoryHasAlreadyBeenSet();
            }
            InternalChannelFactory = channelFactory;
            return (TBootstrap)this;
        }

        /// <summary>
        /// Assigns the <see cref="EndPoint"/> which is used to bind the local "end" to.
        /// </summary>
        /// <param name="localAddress">The <see cref="EndPoint"/> instance to bind the local "end" to.</param>
        /// <returns>The <see cref="AbstractBootstrap{TBootstrap,TChannel}"/> instance.</returns>
        public TBootstrap LocalAddress(EndPoint localAddress)
        {
            InternalLocalAddress = localAddress;
            return (TBootstrap)this;
        }

        /// <summary>
        /// Assigns the local <see cref="EndPoint"/> which is used to bind the local "end" to.
        /// This overload binds to a <see cref="IPEndPoint"/> for any IP address on the local machine, given a specific port.
        /// </summary>
        /// <param name="inetPort">The port to bind the local "end" to.</param>
        /// <returns>The <see cref="AbstractBootstrap{TBootstrap,TChannel}"/> instance.</returns>
        public TBootstrap LocalAddress(int inetPort) => LocalAddress(new IPEndPoint(IPAddress.Any, inetPort));

        /// <summary>
        /// Assigns the local <see cref="EndPoint"/> which is used to bind the local "end" to.
        /// This overload binds to a <see cref="DnsEndPoint"/> for a given hostname and port.
        /// </summary>
        /// <param name="inetHost">The hostname to bind the local "end" to.</param>
        /// <param name="inetPort">The port to bind the local "end" to.</param>
        /// <returns>The <see cref="AbstractBootstrap{TBootstrap,TChannel}"/> instance.</returns>
        public TBootstrap LocalAddress(string inetHost, int inetPort) => LocalAddress(new DnsEndPoint(inetHost, inetPort));

        /// <summary>
        /// Assigns the local <see cref="EndPoint"/> which is used to bind the local "end" to.
        /// This overload binds to a <see cref="IPEndPoint"/> for a given <see cref="IPAddress"/> and port.
        /// </summary>
        /// <param name="inetHost">The <see cref="IPAddress"/> to bind the local "end" to.</param>
        /// <param name="inetPort">The port to bind the local "end" to.</param>
        /// <returns>The <see cref="AbstractBootstrap{TBootstrap,TChannel}"/> instance.</returns>
        public TBootstrap LocalAddress(IPAddress inetHost, int inetPort) => LocalAddress(new IPEndPoint(inetHost, inetPort));

        /// <summary>
        /// Allows the specification of a <see cref="ChannelOption{T}"/> which is used for the
        /// <see cref="IChannel"/> instances once they get created. Use a value of <c>null</c> to remove
        /// a previously set <see cref="ChannelOption{T}"/>.
        /// </summary>
        /// <param name="option">The <see cref="ChannelOption{T}"/> to configure.</param>
        /// <param name="value">The value to set the given option.</param>
        public TBootstrap Option<T>(ChannelOption<T> option, T value)
        {
            if (option is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.option); }

            if (value is null)
            {
                //ChannelOptionValue removed;
                _options.TryRemove(option, out _);
            }
            else
            {
                _options[option] = new ChannelOptionValue<T>(option, value);
            }
            return (TBootstrap)this;
        }

        /// <summary>
        /// Allows specification of an initial attribute of the newly created <see cref="IChannel" />. If the <c>value</c> is
        /// <c>null</c>, the attribute of the specified <c>key</c> is removed.
        /// </summary>
        public TBootstrap Attribute<T>(AttributeKey<T> key, T value)
            where T : class
        {
            if (key is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key); }

            if (value is null)
            {
                //AttributeValue removed;
                _attrs.TryRemove(key, out _);
            }
            else
            {
                _attrs[key] = new AttributeValue<T>(key, value);
            }
            return (TBootstrap)this;
        }

        /// <summary>
        /// Validates all the parameters. Sub-classes may override this, but should call the super method in that case.
        /// </summary>
        public virtual TBootstrap Validate()
        {
            if (InternalGroup is null)
            {
                ThrowHelper.ThrowInvalidOperationException_GroupNotSet();
            }
            if (InternalChannelFactory is null)
            {
                ThrowHelper.ThrowInvalidOperationException_ChannelOrFactoryNotSet();
            }
            return (TBootstrap)this;
        }

        /// <summary>
        /// Returns a deep clone of this bootstrap which has the identical configuration.  This method is useful when making
        /// multiple <see cref="IChannel"/>s with similar settings.  Please note that this method does not clone the
        /// <see cref="IEventLoopGroup"/> deeply but shallowly, making the group a shared resource.
        /// </summary>
        public abstract TBootstrap Clone();

        /// <summary>
        /// Creates a new <see cref="IChannel"/> and registers it with an <see cref="IEventLoop"/>.
        /// </summary>
        public async Task<IChannel> RegisterAsync()
        {
            Validate();
            return await InitAndRegisterAsync();
        }

        /// <summary>
        /// Creates a new <see cref="IChannel"/> and binds it to the endpoint specified via the <see cref="LocalAddress(EndPoint)"/> methods.
        /// </summary>
        /// <returns>The bound <see cref="IChannel"/>.</returns>
        public async Task<IChannel> BindAsync()
        {
            Validate();
            EndPoint address = InternalLocalAddress;
            if (address is null)
            {
                ThrowHelper.ThrowInvalidOperationException_LocalAddrMustBeSetBeforehand();
            }
            return await DoBindAsync(address);
        }

        /// <summary>
        /// Creates a new <see cref="IChannel"/> and binds it.
        /// This overload binds to a <see cref="IPEndPoint"/> for any IP address on the local machine, given a specific port.
        /// </summary>
        /// <param name="inetPort">The port to bind the local "end" to.</param>
        /// <returns>The bound <see cref="IChannel"/>.</returns>
        public Task<IChannel> BindAsync(int inetPort) => BindAsync(new IPEndPoint(IPAddress.Any, inetPort));

        /// <summary>
        /// Creates a new <see cref="IChannel"/> and binds it.
        /// This overload binds to a <see cref="DnsEndPoint"/> for a given hostname and port.
        /// </summary>
        /// <param name="inetHost">The hostname to bind the local "end" to.</param>
        /// <param name="inetPort">The port to bind the local "end" to.</param>
        /// <returns>The bound <see cref="IChannel"/>.</returns>
        public Task<IChannel> BindAsync(string inetHost, int inetPort) => BindAsync(new DnsEndPoint(inetHost, inetPort));

        /// <summary>
        /// Creates a new <see cref="IChannel"/> and binds it.
        /// This overload binds to a <see cref="IPEndPoint"/> for a given <see cref="IPAddress"/> and port.
        /// </summary>
        /// <param name="inetHost">The <see cref="IPAddress"/> to bind the local "end" to.</param>
        /// <param name="inetPort">The port to bind the local "end" to.</param>
        /// <returns>The bound <see cref="IChannel"/>.</returns>
        public Task<IChannel> BindAsync(IPAddress inetHost, int inetPort) => BindAsync(new IPEndPoint(inetHost, inetPort));

        /// <summary>
        /// Creates a new <see cref="IChannel"/> and binds it.
        /// </summary>
        /// <param name="localAddress">The <see cref="EndPoint"/> instance to bind the local "end" to.</param>
        /// <returns>The bound <see cref="IChannel"/>.</returns>
        public async Task<IChannel> BindAsync(EndPoint localAddress)
        {
            Validate();
            if (localAddress is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.localAddress); }

            return await DoBindAsync(localAddress);
        }

        async Task<IChannel> DoBindAsync(EndPoint localAddress)
        {
            IChannel channel = await InitAndRegisterAsync();
            await DoBind0Async(channel, localAddress);

            return channel;
        }

        protected async Task<IChannel> InitAndRegisterAsync()
        {
            IChannel channel = null;
            try
            {
                channel = InternalChannelFactory();
                Init(channel);
            }
            catch (Exception)
            {
                // channel can be null if newChannel crashed (eg SocketException("too many open files"))
                channel?.Unsafe.CloseForcibly();
                // as the Channel is not registered yet we need to force the usage of the GlobalEventExecutor
                throw;
            }

            try
            {
                await Group().GetNext().RegisterAsync(channel);
            }
            catch (Exception)
            {
                if (channel.IsRegistered)
                {
                    try
                    {
                        await channel.CloseAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.FailedToCloseChannel(channel, ex);
                    }
                }
                else
                {
                    channel.Unsafe.CloseForcibly();
                }
                throw;
            }

            // If we are here and the promise is not failed, it's one of the following cases:
            // 1) If we attempted registration from the event loop, the registration has been completed at this point.
            //    i.e. It's safe to attempt bind() or connect() now because the channel has been registered.
            // 2) If we attempted registration from the other thread, the registration request has been successfully
            //    added to the event loop's task queue for later execution.
            //    i.e. It's safe to attempt bind() or connect() now:
            //         because bind() or connect() will be executed *after* the scheduled registration task is executed
            //         because register(), bind(), and connect() are all bound to the same thread.

            return channel;
        }

        static Task DoBind0Async(IChannel channel, EndPoint localAddress)
        {
            // This method is invoked before channelRegistered() is triggered.  Give user handlers a chance to set up
            // the pipeline in its channelRegistered() implementation.
            var promise = channel.NewPromise();
            channel.EventLoop.Execute(BindlocalAddressAction, (channel, localAddress, promise));
            return promise.Task;
        }

        static readonly Action<object> BindlocalAddressAction = s => OnBindlocalAddress(s);
        private static void OnBindlocalAddress(object state)
        {
            var wrapped = ((IChannel, EndPoint, IPromise))state;
            try
            {
                wrapped.Item1.BindAsync(wrapped.Item2).LinkOutcome(wrapped.Item3);
            }
            catch (Exception ex)
            {
                wrapped.Item1.CloseSafe();
                wrapped.Item3.TrySetException(ex);
            }
        }

        protected abstract void Init(IChannel channel);

        /// <summary>
        /// Specifies the <see cref="IChannelHandler"/> to use for serving the requests.
        /// </summary>
        /// <param name="handler">The <see cref="IChannelHandler"/> to use for serving requests.</param>
        /// <returns>The <see cref="AbstractBootstrap{TBootstrap,TChannel}"/> instance.</returns>
        public TBootstrap Handler(IChannelHandler handler)
        {
            if (handler is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.handler); }
            InternalHandler = handler;
            return (TBootstrap)this;
        }

        protected EndPoint LocalAddress() => InternalLocalAddress;

        protected IChannelHandler Handler() => InternalHandler;

        /// <summary>
        /// Returns the configured <see cref="IEventLoopGroup"/> or <c>null</c> if none is configured yet.
        /// </summary>
        public IEventLoopGroup Group() => InternalGroup;

        protected ICollection<ChannelOptionValue> Options => _options.Values;

        protected ICollection<AttributeValue> Attributes => _attrs.Values;

        protected static void SetChannelOptions(IChannel channel, ICollection<ChannelOptionValue> options, IInternalLogger logger)
        {
            foreach (var e in options)
            {
                SetChannelOption(channel, e, logger);
            }
        }

        protected static void SetChannelOptions(IChannel channel, ChannelOptionValue[] options, IInternalLogger logger)
        {
            foreach (var e in options)
            {
                SetChannelOption(channel, e, logger);
            }
        }

        protected static void SetChannelOption(IChannel channel, ChannelOptionValue option, IInternalLogger logger)
        {
            var warnEnabled = logger.WarnEnabled;
            try
            {
                if (!option.Set(channel.Configuration))
                {
                    if (warnEnabled) UnknownChannelOptionForChannel(logger, channel, option);
                }
            }
            catch (Exception ex)
            {
                if (warnEnabled) FailedToSetChannelOptionWithValueForChannel(logger, channel, option, ex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UnknownChannelOptionForChannel(IInternalLogger logger, IChannel channel, ChannelOptionValue option)
        {
            logger.Warn("Unknown channel option '{}' for channel '{}'", option.Option, channel);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FailedToSetChannelOptionWithValueForChannel(IInternalLogger logger, IChannel channel, ChannelOptionValue option, Exception ex)
        {
            logger.Warn("Failed to set channel option '{}' with value '{}' for channel '{}'", option.Option, option, channel, ex);
        }

        public override string ToString()
        {
            var buf = StringBuilderManager.Allocate()
                .Append(GetType().Name)
                .Append('(');

            var group = InternalGroup;
            if (group is object)
            {
                buf.Append("group: ")
                    .Append(group.GetType().Name)
                    .Append(", ");
            }

            var channelFactory = InternalChannelFactory;
            if (channelFactory is object)
            {
                buf.Append("channelFactory: ")
                    .Append(channelFactory)
                    .Append(", ");
            }

            var localAddress = InternalLocalAddress;
            if (localAddress is object)
            {
                buf.Append("localAddress: ")
                    .Append(localAddress)
                    .Append(", ");
            }

            if ((uint)_options.Count > 0u)
            {
                buf.Append("options: ")
                    .Append(_options.ToDebugString())
                    .Append(", ");
            }

            if ((uint)_attrs.Count > 0u)
            {
                buf.Append("attrs: ")
                    .Append(_attrs.ToDebugString())
                    .Append(", ");
            }

            var handler = InternalHandler;
            if (handler is object)
            {
                buf.Append("handler: ")
                    .Append(handler)
                    .Append(", ");
            }

            if (buf[buf.Length - 1] == '(')
            {
                buf.Append(')');
            }
            else
            {
                buf[buf.Length - 2] = ')';
                buf.Length -= 1;
            }
            return StringBuilderManager.ReturnAndFree(buf);
        }

        //static class PendingRegistrationPromise : DefaultChannelPromise
        //{
        //    // Is set to the correct EventExecutor once the registration was successful. Otherwise it will
        //    // stay null and so the GlobalEventExecutor.INSTANCE will be used for notifications.
        //    volatile EventExecutor executor;

        //    PendingRegistrationPromise(Channel channel)
        //    {
        //        super(channel);
        //    }

        //    protected EventExecutor executor()
        //    {
        //        EventExecutor executor = this.executor;
        //        if (executor is object)
        //        {
        //            // If the registration was a success executor is set.
        //            //
        //            // See https://github.com/netty/netty/issues/2586
        //            return executor;
        //        }
        //        // The registration failed so we can only use the GlobalEventExecutor as last resort to notify.
        //        return GlobalEventExecutor.INSTANCE;
        //    }
        //}

        protected abstract class ChannelOptionValue
        {
            public abstract ChannelOption Option { get; }
            public abstract bool Set(IChannelConfiguration config);
        }

        protected sealed class ChannelOptionValue<T> : ChannelOptionValue
        {
            public override ChannelOption Option { get; }
            private readonly T _value;

            public ChannelOptionValue(ChannelOption<T> option, T value)
            {
                Option = option;
                _value = value;
            }

            public override bool Set(IChannelConfiguration config) => config.SetOption(Option, _value);

            public override string ToString() => _value.ToString();
        }

        protected abstract class AttributeValue
        {
            public abstract void Set(IAttributeMap map);
        }

        protected sealed class AttributeValue<T> : AttributeValue
            where T : class
        {
            private readonly AttributeKey<T> _key;
            private readonly T _value;

            public AttributeValue(AttributeKey<T> key, T value)
            {
                _key = key;
                _value = value;
            }

            public override void Set(IAttributeMap config) => config.GetAttribute(_key).Set(_value);
        }
    }
}