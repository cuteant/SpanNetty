// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    /// <summary>
    /// <see cref="IEventLoopGroup"/> backed by a set of <see cref="SingleThreadEventLoop"/> instances.
    /// </summary>
    public sealed class MultithreadEventLoopGroup : AbstractEventExecutorGroup, IEventLoopGroup
    {
        private static readonly int DefaultEventLoopThreadCount = Environment.ProcessorCount * 2;
        private static readonly Func<IEventLoopGroup, IEventLoop> DefaultEventLoopFactory = group => new SingleThreadEventLoop(group);

        private readonly IEventLoop[] _eventLoops;
        private int _requestId;

        public override bool IsShutdown => _eventLoops.All(eventLoop => eventLoop.IsShutdown);

        public override bool IsTerminated => _eventLoops.All(eventLoop => eventLoop.IsTerminated);

        public override bool IsShuttingDown => _eventLoops.All(eventLoop => eventLoop.IsShuttingDown);

        /// <inheritdoc />
        public override Task TerminationCompletion { get; }

        /// <inheritdoc />
        protected override IEnumerable<IEventExecutor> GetItems() => _eventLoops;

        /// <inheritdoc />
        public new IEnumerable<IEventLoop> Items => _eventLoops;

        /// <summary>Creates a new instance of <see cref="MultithreadEventLoopGroup"/>.</summary>
        public MultithreadEventLoopGroup()
            : this(DefaultEventLoopFactory, DefaultEventLoopThreadCount)
        {
        }

        /// <summary>Creates a new instance of <see cref="MultithreadEventLoopGroup"/>.</summary>
        public MultithreadEventLoopGroup(int eventLoopCount)
            : this(DefaultEventLoopFactory, eventLoopCount)
        {
        }

        /// <summary>Creates a new instance of <see cref="MultithreadEventLoopGroup"/>.</summary>
        public MultithreadEventLoopGroup(Func<IEventLoopGroup, IEventLoop> eventLoopFactory)
            : this(eventLoopFactory, DefaultEventLoopThreadCount)
        {
        }

        /// <summary>Creates a new instance of <see cref="MultithreadEventLoopGroup"/>.</summary>
        public MultithreadEventLoopGroup(Func<IEventLoopGroup, IEventLoop> eventLoopFactory, int eventLoopCount)
        {
            if (eventLoopFactory is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.eventLoopFactory); }
            if (eventLoopCount <= 0) { ThrowHelper.ThrowArgumentException_Positive(eventLoopCount, ExceptionArgument.eventLoopCount); }

            _eventLoops = new IEventLoop[eventLoopCount];
            var terminationTasks = new Task[eventLoopCount];
            for (int i = 0; i < eventLoopCount; i++)
            {
                IEventLoop eventLoop = null;
                bool success = false;
                try
                {
                    eventLoop = eventLoopFactory(this);
                    success = true;
                }
                catch (Exception ex)
                {
                    ThrowHelper.ThrowInvalidOperationException(ex);
                }
                finally
                {
                    if (!success)
                    {
                        Task.WhenAll(_eventLoops
                                .Take(i)
                                .Select(loop => loop.ShutdownGracefullyAsync()))
                            .Wait();
                    }
                }

                _eventLoops[i] = eventLoop;
                terminationTasks[i] = eventLoop.TerminationCompletion;
            }
            TerminationCompletion = Task.WhenAll(terminationTasks);
        }

        /// <inheritdoc />
        IEventLoop IEventLoopGroup.GetNext() => (IEventLoop)GetNext();

        /// <inheritdoc />
        public override IEventExecutor GetNext()
        {
            int id = Interlocked.Increment(ref _requestId);
            return _eventLoops[Math.Abs(id % _eventLoops.Length)];
        }

        public Task RegisterAsync(IChannel channel) => ((IEventLoop)GetNext()).RegisterAsync(channel);

        /// <inheritdoc cref="IEventExecutorGroup.ShutdownGracefullyAsync(TimeSpan, TimeSpan)" />
        public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        {
            for (int i = 0; i < _eventLoops.Length; i++)
            {
                _ = _eventLoops[i].ShutdownGracefullyAsync(quietPeriod, timeout);
            }
            return TerminationCompletion;
        }
    }
}