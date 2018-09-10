// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Bootstrapping
{
    using System.Runtime.CompilerServices;
    using System.Threading;
    using DotNetty.Transport.Channels;

    partial class ServerBootstrap
    {
        private IEventLoopGroup InternalChildGroup
        {
            [MethodImpl(InlineMethod.Value)]
            get => Volatile.Read(ref _childGroup);
            set => Interlocked.Exchange(ref _childGroup, value);
        }

        private IChannelHandler InternalChildHandler
        {
            [MethodImpl(InlineMethod.Value)]
            get => Volatile.Read(ref _childHandler);
            set => Interlocked.Exchange(ref _childHandler, value);
        }
    }
}