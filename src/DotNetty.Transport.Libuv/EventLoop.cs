// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;

    public sealed class EventLoop : LoopExecutor
    {
        internal EventLoop(IEventLoopGroup parent)
            : this(parent, DefaultThreadFactory<EventLoop>.Instance, RejectedExecutionHandlers.Reject(), DefaultBreakoutInterval)
        {
        }

        internal EventLoop(IEventLoopGroup parent, IThreadFactory threadFactory, IRejectedExecutionHandler rejectedHandler, TimeSpan breakoutInterval)
            : base(parent, threadFactory, rejectedHandler, breakoutInterval)
        {
            Start();
        }
    }
}