// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ForCanBeConvertedToForeach

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    public sealed class EventLoopGroup : AbstractEventExecutorGroup, IEventLoopGroup
    {
        static readonly int DefaultEventLoopCount = Environment.ProcessorCount;
        readonly EventLoop[] _eventLoops;
        int _requestId;

        public override bool IsShutdown => _eventLoops.All(eventLoop => eventLoop.IsShutdown);

        public override bool IsTerminated => _eventLoops.All(eventLoop => eventLoop.IsTerminated);

        public override bool IsShuttingDown => _eventLoops.All(eventLoop => eventLoop.IsShuttingDown);

        public override Task TerminationCompletion { get; }

        public new IEnumerable<IEventLoop> Items => _eventLoops;

        public EventLoopGroup()
            : this(DefaultEventLoopCount)
        {
        }

        public EventLoopGroup(int eventLoopCount)
        {
            _eventLoops = new EventLoop[eventLoopCount];
            var terminationTasks = new Task[eventLoopCount];
            for (int i = 0; i < eventLoopCount; i++)
            {
                EventLoop eventLoop = null;
                bool success = false;
                try
                {
                    eventLoop = new EventLoop(this, $"{nameof(EventLoopGroup)}-{i}");
                    success = true;
                }
                catch (Exception ex)
                {
                    ThrowHelper.ThrowInvalidOperationException_FailedToCreateChildEventLoop(ex);
                }
                finally
                {
                    if (!success)
                    {
                        Task.WhenAll(
                                _eventLoops
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

        public override IEventExecutor GetNext()
        {
            // Attempt to select event loop based on thread first
            int threadId = XThread.CurrentThread.Id;
            int i;
            for (i = 0; i < _eventLoops.Length; i++)
            {
                if (_eventLoops[i].LoopThreadId == threadId)
                {
                    return _eventLoops[i];
                }
            }

            // Default select, this means libuv handles not created yet,
            // the chosen loop will be used to create handles from.
            i = Interlocked.Increment(ref _requestId);
            return _eventLoops[Math.Abs(i % _eventLoops.Length)];
        }

        IEventLoop IEventLoopGroup.GetNext() => (IEventLoop)GetNext();

        public Task RegisterAsync(IChannel channel)
        {
            var nativeChannel = channel as INativeChannel;
            if (nativeChannel is null)
            {
                ThrowHelper.ThrowArgumentException_RegChannel();
            }

            // The handle loop must be the same as the loop of the
            // handle was created from.
            NativeHandle handle = nativeChannel.GetHandle();
            IntPtr loopHandle = handle.LoopHandle();
            for (int i = 0; i < _eventLoops.Length; i++)
            {
                if (_eventLoops[i].UnsafeLoop.Handle == loopHandle)
                {
                    return _eventLoops[i].RegisterAsync(nativeChannel);
                }
            }

            return ThrowHelper.ThrowInvalidOperationException(loopHandle);
        }

        public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        {
            foreach (EventLoop eventLoop in _eventLoops)
            {
                eventLoop.ShutdownGracefullyAsync(quietPeriod, timeout);
            }
            return TerminationCompletion;
        }

        protected override IEnumerable<IEventExecutor> GetItems() => _eventLoops;
    }
}