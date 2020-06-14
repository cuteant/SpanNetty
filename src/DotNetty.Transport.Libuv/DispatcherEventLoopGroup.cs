// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;

    public sealed class DispatcherEventLoopGroup : AbstractEventExecutorGroup, IEventLoopGroup
    {
        readonly DispatcherEventLoop _dispatcherEventLoop;

        public DispatcherEventLoopGroup()
        {
            _dispatcherEventLoop = new DispatcherEventLoop(this);
        }

        public override bool IsShutdown => _dispatcherEventLoop.IsShutdown;

        public override bool IsTerminated => _dispatcherEventLoop.IsTerminated;

        public override bool IsShuttingDown => _dispatcherEventLoop.IsShuttingDown;

        public override Task TerminationCompletion => _dispatcherEventLoop.TerminationCompletion;

        internal DispatcherEventLoop Dispatcher => _dispatcherEventLoop;

        protected override IEnumerable<IEventExecutor> GetItems() => new[] { _dispatcherEventLoop };

        public new IEnumerable<IEventLoop> Items => new[] { _dispatcherEventLoop };

        IEventLoop IEventLoopGroup.GetNext() => (IEventLoop)GetNext();

        public override IEventExecutor GetNext() => _dispatcherEventLoop;

        public Task RegisterAsync(IChannel channel) => ((IEventLoop)GetNext()).RegisterAsync(channel);

        public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        {
            _ = _dispatcherEventLoop.ShutdownGracefullyAsync(quietPeriod, timeout);
            return TerminationCompletion;
        }
    }
}