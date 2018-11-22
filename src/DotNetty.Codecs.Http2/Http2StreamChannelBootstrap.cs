// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using CuteAnt.Collections;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public sealed class Http2StreamChannelBootstrap
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<Http2StreamChannelBootstrap>();

        readonly CachedReadConcurrentDictionary<ChannelOption, ChannelOptionValue> options =
            new CachedReadConcurrentDictionary<ChannelOption, ChannelOptionValue>(ChannelOptionComparer.Default);
        readonly CachedReadConcurrentDictionary<IConstant, AttributeValue> attrs =
            new CachedReadConcurrentDictionary<IConstant, AttributeValue>(ConstantComparer.Default);

        readonly IChannel channel;

        IChannelHandler _handler;
        private IChannelHandler InternalHandler { get => Volatile.Read(ref _handler); set => Interlocked.Exchange(ref _handler, value); }

        public Http2StreamChannelBootstrap(IChannel channel)
        {
            if (null == channel) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.channel); }
            this.channel = channel;
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
            if (null == option) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.option); }

            if (value == null)
            {
                this.options.TryRemove(option, out _);
            }
            else
            {
                this.options[option] = new ChannelOptionValue<T>(option, value);
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
            if (null == value) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }

            if (value == null)
            {
                this.attrs.TryRemove(key, out _);
            }
            else
            {
                this.attrs[key] = new AttributeValue<T>(key, value);
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
            if (null == handler) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.handler); }

            this.InternalHandler = handler;
            return this;
        }

        public Task<IHttp2StreamChannel> OpenAsync()
        {
            return OpenAsync(new TaskCompletionSource<IHttp2StreamChannel>());
        }

        public Task<IHttp2StreamChannel> OpenAsync(TaskCompletionSource<IHttp2StreamChannel> promise)
        {
            var ctx = channel.Pipeline.Context<Http2MultiplexCodec>();
            if (ctx == null)
            {
                if (channel.Active)
                {
                    promise.SetException(ThrowHelper.GetInvalidOperationException_MustBeInTheChannelPipelineOfChannel(channel));
                }
                else
                {
                    promise.SetException(new ClosedChannelException());
                }
            }
            else
            {
                var executor = ctx.Executor;
                if (executor.InEventLoop)
                {
                    Open0(ctx, promise);
                }
                else
                {
                    executor.Execute(() => Open0(ctx, promise));
                }
            }
            return promise.Task;
        }

        public void Open0(IChannelHandlerContext ctx, TaskCompletionSource<IHttp2StreamChannel> promise)
        {
            Debug.Assert(ctx.Executor.InEventLoop);

            var streamChannel = ((Http2MultiplexCodec)ctx.Handler).NewOutboundStream();
            try
            {
                this.Init(streamChannel);
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
            switch (future.Status)
            {
                case TaskStatus.RanToCompletion:
                    wrapped.Item1.TrySetResult(streamChannel);
                    break;
                case TaskStatus.Canceled:
                    wrapped.Item1.TrySetCanceled();
                    break;
                case TaskStatus.Faulted:
                    if (streamChannel.Registered)
                    {
                        streamChannel.CloseAsync();
                    }
                    else
                    {
                        streamChannel.Unsafe.CloseForcibly();
                    }
                    wrapped.Item1.TryUnwrap(future.Exception);
                    break;
                default:
                    ThrowHelper.ThrowArgumentOutOfRangeException(); break;
            }
        }

        private static void LinkOutcome(Task future, TaskCompletionSource<IHttp2StreamChannel> promise, IHttp2StreamChannel streamChannel)
        {
            switch (future.Status)
            {
                case TaskStatus.RanToCompletion:
                    promise.TrySetResult(streamChannel);
                    break;
                case TaskStatus.Canceled:
                    promise.TrySetCanceled();
                    break;
                case TaskStatus.Faulted:
                    if (streamChannel.Registered)
                    {
                        streamChannel.CloseAsync();
                    }
                    else
                    {
                        streamChannel.Unsafe.CloseForcibly();
                    }
                    promise.TryUnwrap(future.Exception);
                    break;
                default:
                    future.ContinueWith(LinkOutcomeContinuationAction,
                        Tuple.Create(promise, streamChannel), TaskContinuationOptions.ExecuteSynchronously);
                    break;
            }
        }

        void Init(IChannel channel)
        {
            var p = channel.Pipeline;
            var handler = this.InternalHandler;
            if (handler != null)
            {
                p.AddLast(handler);
            }

            var options = this.options.Values;
            foreach (var item in options)
            {
                SetChannelOption(channel, item, Logger);
            }
            var attrs = this.attrs.Values;
            foreach (var item in attrs)
            {
                item.Set(channel);
            }
        }

        static void SetChannelOption(IChannel channel, ChannelOptionValue option, IInternalLogger logger)
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

        abstract class ChannelOptionValue
        {
            public abstract ChannelOption Option { get; }
            public abstract bool Set(IChannelConfiguration config);
        }

        sealed class ChannelOptionValue<T> : ChannelOptionValue
        {
            public override ChannelOption Option { get; }
            readonly T value;

            public ChannelOptionValue(ChannelOption<T> option, T value)
            {
                this.Option = option;
                this.value = value;
            }

            public override bool Set(IChannelConfiguration config) => config.SetOption(this.Option, this.value);

            public override string ToString() => this.value.ToString();
        }

        abstract class AttributeValue
        {
            public abstract void Set(IAttributeMap map);
        }

        sealed class AttributeValue<T> : AttributeValue
           where T : class
        {
            readonly AttributeKey<T> key;
            readonly T value;

            public AttributeValue(AttributeKey<T> key, T value)
            {
                this.key = key;
                this.value = value;
            }

            public override void Set(IAttributeMap config) => config.GetAttribute(this.key).Set(this.value);
        }

    }
}
