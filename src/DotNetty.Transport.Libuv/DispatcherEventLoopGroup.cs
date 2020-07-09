// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;

    public sealed class DispatcherEventLoopGroup : AbstractEventExecutorGroup<DispatcherEventLoop>, IEventLoopGroup
    {
        private readonly DispatcherEventLoop _dispatcherEventLoop;
        private readonly DispatcherEventLoop[] _eventLoops;

        public DispatcherEventLoopGroup()
        {
            _dispatcherEventLoop = new DispatcherEventLoop(this);
            _eventLoops = new[] { _dispatcherEventLoop };
        }

        public override bool IsShutdown => _dispatcherEventLoop.IsShutdown;

        public override bool IsTerminated => _dispatcherEventLoop.IsTerminated;

        public override bool IsShuttingDown => _dispatcherEventLoop.IsShuttingDown;

        public override Task TerminationCompletion => _dispatcherEventLoop.TerminationCompletion;

        internal DispatcherEventLoop Dispatcher => _dispatcherEventLoop;

        public override IEnumerable<IEventExecutor> Items => _eventLoops;

        IEnumerable<IEventLoop> IEventLoopGroup.Items => _eventLoops;

        public override IReadOnlyList<DispatcherEventLoop> GetItems() => _eventLoops;

        IEventLoop IEventLoopGroup.GetNext() => _dispatcherEventLoop;

        public override DispatcherEventLoop GetNext() => _dispatcherEventLoop;

        public Task RegisterAsync(IChannel channel) => _dispatcherEventLoop.RegisterAsync(channel);

        public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        {
            _ = _dispatcherEventLoop.ShutdownGracefullyAsync(quietPeriod, timeout);
            return TerminationCompletion;
        }

        public override bool WaitTermination(TimeSpan timeout)
        {
            return _dispatcherEventLoop.TerminationCompletion.Wait(timeout);
        }
    }
}