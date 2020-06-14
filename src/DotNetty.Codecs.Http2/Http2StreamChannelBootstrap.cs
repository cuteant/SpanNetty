// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public sealed class Http2StreamChannelBootstrap
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<Http2StreamChannelBootstrap>();

        private readonly CachedReadConcurrentDictionary<ChannelOption, ChannelOptionValue> _options =
            new CachedReadConcurrentDictionary<ChannelOption, ChannelOptionValue>(ChannelOptionComparer.Default);
        private readonly CachedReadConcurrentDictionary<IConstant, AttributeValue> _attrs =
            new CachedReadConcurrentDictionary<IConstant, AttributeValue>(ConstantComparer.Default);

        private readonly IChannel _channel;

        // Cache the ChannelHandlerContext to speed up open(...) operations.
        private IChannelHandlerContext v_multiplexCtx;
        private IChannelHandlerContext InternalMultiplexContext
        {
            get => Volatile.Read(ref v_multiplexCtx);
            set => Interlocked.Exchange(ref v_multiplexCtx, value);
        }

        private IChannelHandler v_handler;
        private IChannelHandler InternalHandler
        {
            get => Volatile.Read(ref v_handler);
            set => Interlocked.Exchange(ref v_handler, value);
        }

        public Http2StreamChannelBootstrap(IChannel channel)
        {
            if (channel is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.channel); }
            _channel = channel;
        }

        /// <summary>
        /// Allow to specify a <see cref="ChannelOption"/> which is used for the <see cref="IHttp2StreamChannel"/> instances once they got
        /// created. Use a value of <c>null</c> to remove a previous set <see cref="ChannelOption"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="option"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public Http2StreamChannelBootstrap Option<T>(ChannelOption<T> option, T value)
        {
            if (option is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.option); }

            if (value is null)
            {
                _ = _options.TryRemove(option, out _);
            }
            else
            {
                _options[option] = new ChannelOptionValue<T>(option, value);
            }
            return this;
        }

        /// <summary>
        /// Allow to specify an initial attribute of the newly created <see cref="IHttp2StreamChannel"/>.  If the <paramref name="value"/>is
        /// <c>null</c>, the attribute of the specified <paramref name="key"/> is removed.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public Http2StreamChannelBootstrap Attr<T>(AttributeKey<T> key, T value)
            where T : class
        {
            if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }

            if (value is null)
            {
                _ = _attrs.TryRemove(key, out _);
            }
            else
            {
                _attrs[key] = new AttributeValue<T>(key, value);
            }
            return this;
        }

        /// <summary>
        /// The <see cref="IChannelHandler"/> to use for serving the requests.
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public Http2StreamChannelBootstrap Handler(IChannelHandler handler)
        {
            if (handler is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.handler); }

            InternalHandler = handler;
            return this;
        }

        /// <summary>
        /// Open a new <see cref="IHttp2StreamChannel"/> to use.
        /// </summary>
        /// <returns>the <see cref="Task{IHttp2StreamChannel}"/> that will be notified once the channel was opened successfully or it failed.</returns>
        public Task<IHttp2StreamChannel> OpenAsync()
        {
            return OpenAsync(new TaskCompletionSource<IHttp2StreamChannel>());
        }

        /// <summary>
        /// Open a new <see cref="IHttp2StreamChannel"/> to use and notifies the given <paramref name="promise"/>.
        /// </summary>
        /// <param name="promise"></param>
        /// <returns>the <see cref="Task{IHttp2StreamChannel}"/> that will be notified once the channel was opened successfully or it failed.</returns>
        public Task<IHttp2StreamChannel> OpenAsync(TaskCompletionSource<IHttp2StreamChannel> promise)
        {
            try
            {
                var ctx = FindCtx();
                var executor = ctx.Executor;
                if (executor.InEventLoop)
                {
                    InternalOpen(ctx, promise);
                }
                else
                {
                    executor.Execute(() => InternalOpen(ctx, promise));
                }
            }
            catch (Exception exc)
            {
                _ = promise.TrySetException(exc);
            }
            return promise.Task;
        }

        private IChannelHandlerContext FindCtx()
        {
            // First try to use cached context and if this not work lets try to lookup the context.
            var ctx = InternalMultiplexContext;
            if (ctx is object && !ctx.Removed)
            {
                return ctx;
            }
            var pipeline = _channel.Pipeline;
            ctx = pipeline.Context<Http2MultiplexHandler>();
            if (ctx is null) { ctx = pipeline.Context<Http2MultiplexCodec>(); }
            if (ctx is null)
            {
                if (_channel.Active)
                {
                    ThrowHelper.ThrowInvalidOperationException_Multiplex_CodecOrHandler_must_be_in_pipeline_of_channel(_channel);
                }
                else
                {
                    ThrowHelper.ThrowClosedChannelException();
                }
            }
            InternalMultiplexContext = ctx;
            return ctx;
        }

        [Obsolete("should not be used directly. Use OpenAsync")]
        public void Open0(IChannelHandlerContext ctx, TaskCompletionSource<IHttp2StreamChannel> promise) => InternalOpen(ctx, promise);

        public void InternalOpen(IChannelHandlerContext ctx, TaskCompletionSource<IHttp2StreamChannel> promise)
        {
            Debug.Assert(ctx.Executor.InEventLoop);
            // netty-4.1.40 Support cancellation in the Http2StreamChannelBootstrap (#9519)
            // https://github.com/netty/netty/pull/9519
            // 使用 TaskCompletionSource<IHttp2StreamChannel>.TrySetResult 可避免
            //if (!promise.setUncancellable())
            //{
            //    return;
            //}
            IHttp2StreamChannel streamChannel;
            if (ctx.Handler is Http2MultiplexHandler multiplexHandler)
            {
                streamChannel = multiplexHandler.NewOutboundStream();
            }
            else
            {
                streamChannel = ((Http2MultiplexCodec)ctx.Handler).NewOutboundStream();
            }
            try
            {
                Init(streamChannel);
            }
            catch (Exception e)
            {
                streamChannel.Unsafe.CloseForcibly();
                promise.SetException(e);
                return;
            }

            var future = ctx.Channel.EventLoop.RegisterAsync(streamChannel);
            LinkOutcome(future, promise, streamChannel);
        }

        static readonly Action<Task, object> LinkOutcomeContinuationAction = LinkOutcomeContinuation;
        private static void LinkOutcomeContinuation(Task future, object state)
        {
            var wrapped = (Tuple<TaskCompletionSource<IHttp2StreamChannel>, IHttp2StreamChannel>)state;
            var streamChannel = wrapped.Item2;
            if (future.IsSuccess())
            {
                _ = wrapped.Item1.TrySetResult(streamChannel);
            }
            else if (future.IsCanceled)
            {
                _ = wrapped.Item1.TrySetCanceled();
            }
            else
            {
                if (streamChannel.Registered)
                {
                    _ = streamChannel.CloseAsync();
                }
                else
                {
                    streamChannel.Unsafe.CloseForcibly();
                }
                _ = wrapped.Item1.TrySetException(future.Exception.InnerExceptions);
            }
        }

        private static void LinkOutcome(Task future, TaskCompletionSource<IHttp2StreamChannel> promise, IHttp2StreamChannel streamChannel)
        {
            if (!future.IsCompleted)
            {
                _ = future.ContinueWith(LinkOutcomeContinuationAction,
                    Tuple.Create(promise, streamChannel), TaskContinuationOptions.ExecuteSynchronously);
                return;
            }
            if (future.IsSuccess())
            {
                _ = promise.TrySetResult(streamChannel);
            }
            else if (future.IsCanceled)
            {
                _ = promise.TrySetCanceled();
            }
            else
            {
                if (streamChannel.Registered)
                {
                    _ = streamChannel.CloseAsync();
                }
                else
                {
                    streamChannel.Unsafe.CloseForcibly();
                }
                _ = promise.TrySetException(future.Exception.InnerExceptions);
            }
        }

        void Init(IChannel channel)
        {
            var p = channel.Pipeline;
            var handler = InternalHandler;
            if (handler is object)
            {
                _ = p.AddLast(handler);
            }

            var options = _options.Values;
            foreach (var item in options)
            {
                SetChannelOption(channel, item);
            }
            var attrs = _attrs.Values;
            foreach (var item in attrs)
            {
                item.Set(channel);
            }
        }

        static void SetChannelOption(IChannel channel, ChannelOptionValue option)
        {
            var warnEnabled = Logger.WarnEnabled;
            try
            {
                if (!option.Set(channel.Configuration))
                {
                    if (warnEnabled) UnknownChannelOptionForChannel(channel, option);
                }
            }
            catch (Exception ex)
            {
                if (warnEnabled) FailedToSetChannelOptionWithValueForChannel(channel, option, ex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UnknownChannelOptionForChannel(IChannel channel, ChannelOptionValue option)
        {
            Logger.Warn("Unknown channel option '{}' for channel '{}'", option.Option, channel);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FailedToSetChannelOptionWithValueForChannel(IChannel channel, ChannelOptionValue option, Exception ex)
        {
            Logger.Warn("Failed to set channel option '{}' with value '{}' for channel '{}'", option.Option, option, channel, ex);
        }

        abstract class ChannelOptionValue
        {
            public abstract ChannelOption Option { get; }
            public abstract bool Set(IChannelConfiguration config);
        }

        sealed class ChannelOptionValue<T> : ChannelOptionValue
        {
            public override ChannelOption Option { get; }
            readonly T _value;

            public ChannelOptionValue(ChannelOption<T> option, T value)
            {
                Option = option;
                _value = value;
            }

            public override bool Set(IChannelConfiguration config) => config.SetOption(Option, _value);

            public override string ToString() => _value.ToString();
        }

        abstract class AttributeValue
        {
            public abstract void Set(IAttributeMap map);
        }

        sealed class AttributeValue<T> : AttributeValue
           where T : class
        {
            readonly AttributeKey<T> _key;
            readonly T _value;

            public AttributeValue(AttributeKey<T> key, T value)
            {
                _key = key;
                _value = value;
            }

            public override void Set(IAttributeMap config) => config.GetAttribute(_key).Set(_value);
        }

    }
}
