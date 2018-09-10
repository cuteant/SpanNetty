// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Bootstrapping
{
    using System;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    partial class AbstractBootstrap<TBootstrap, TChannel>
    {
        static readonly Action<object> BindlocalAddressAction = OnBindlocalAddress;

        private IEventLoopGroup InternalGroup
        {
            [MethodImpl(InlineMethod.Value)]
            get => Volatile.Read(ref _group);
            set => Interlocked.Exchange(ref _group, value);
        }

        private Func<TChannel> InternalChannelFactory
        {
            [MethodImpl(InlineMethod.Value)]
            get => Volatile.Read(ref _channelFactory);
            set => Interlocked.Exchange(ref _channelFactory, value);
        }

        private EndPoint InternalLocalAddress
        {
            [MethodImpl(InlineMethod.Value)]
            get => Volatile.Read(ref _localAddress);
            set => Interlocked.Exchange(ref _localAddress, value);
        }

        private IChannelHandler InternalHandler
        {
            [MethodImpl(InlineMethod.Value)]
            get => Volatile.Read(ref _handler);
            set => Interlocked.Exchange(ref _handler, value);
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

        private static void OnBindlocalAddress(object state)
        {
            var wrapped = (Tuple<IChannel, EndPoint, TaskCompletionSource>)state;
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
    }
}