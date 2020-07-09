//// Copyright (c) Microsoft. All rights reserved.
//// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//namespace DotNetty.Transport.Channels
//{
//    using System;
//    using System.Collections.Generic;
//    using System.Threading.Tasks;
//    using DotNetty.Common.Concurrency;

//    /// <summary>
//    /// <see cref="IEventLoopGroup"/> that works as a wrapper for another <see cref="IEventLoopGroup"/> providing affinity on <see cref="GetNext"/> call.
//    /// </summary>
//    public class AffinitizedEventLoopGroup : AbstractEventExecutorGroup, IEventLoopGroup
//    {
//        readonly IEventLoopGroup _innerGroup;

//        public override bool IsShutdown => _innerGroup.IsShutdown;

//        public override bool IsTerminated => _innerGroup.IsTerminated;

//        public override bool IsShuttingDown => _innerGroup.IsShuttingDown;

//        /// <inheritdoc cref="IEventExecutorGroup.TerminationCompletion"/>
//        public override Task TerminationCompletion => _innerGroup.TerminationCompletion;

//        protected override IEnumerable<IEventExecutor> GetItems() => _innerGroup.Items;

//        public new IEnumerable<IEventLoop> Items => ((IEventLoopGroup)_innerGroup).Items;

//        /// <summary>
//        /// Creates a new instance of <see cref="AffinitizedEventLoopGroup"/>.
//        /// </summary>
//        /// <param name="innerGroup"><see cref="IEventLoopGroup"/> serving as an actual provider of <see cref="IEventLoop"/>s.</param>
//        public AffinitizedEventLoopGroup(IEventLoopGroup innerGroup)
//        {
//            _innerGroup = innerGroup;
//        }

//        /// <summary>
//        /// If running in a context of an existing <see cref="IEventLoop"/>, this <see cref="IEventLoop"/> is returned.
//        /// Otherwise, <see cref="IEventLoop"/> is retrieved from underlying <see cref="IEventLoopGroup"/>.
//        /// </summary>
//        public override IEventExecutor GetNext()
//        {
//            if (ExecutionEnvironment.TryGetCurrentExecutor(out var executor))
//            {
//                if (executor is IEventLoop loop && loop.Parent == _innerGroup)
//                {
//                    return loop;
//                }
//            }
//            return _innerGroup.GetNext();
//        }

//        IEventLoop IEventLoopGroup.GetNext() => (IEventLoop)GetNext();

//        public Task RegisterAsync(IChannel channel) => ((IEventLoop)GetNext()).RegisterAsync(channel);

//        /// <inheritdoc cref="IEventExecutorGroup.ShutdownGracefullyAsync(TimeSpan, TimeSpan)"/>
//        public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout) => _innerGroup.ShutdownGracefullyAsync(quietPeriod, timeout);
//    }
//}