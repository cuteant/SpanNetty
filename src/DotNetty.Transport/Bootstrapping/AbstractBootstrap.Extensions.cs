// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Bootstrapping
{
    using System;
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Channels;

    partial class AbstractBootstrap<TBootstrap, TChannel>
    {
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
    }
}