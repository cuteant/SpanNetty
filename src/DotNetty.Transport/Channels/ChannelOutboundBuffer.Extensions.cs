// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;

    partial class ChannelOutboundBuffer
    {
        static readonly Action<object> FireChannelWritabilityChangedAction = OnFireChannelWritabilityChanged;

        static void OnFireChannelWritabilityChanged(object p)
        {
            ((IChannelPipeline)p).FireChannelWritabilityChanged();
        }
    }
}