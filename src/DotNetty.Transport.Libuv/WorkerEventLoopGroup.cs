// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ForCanBeConvertedToForeach

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    public sealed class WorkerEventLoopGroup : AbstractEventExecutorGroup, IEventLoopGroup
    {
        static readonly int DefaultEventLoopThreadCount = Environment.ProcessorCount;
        static readonly TimeSpan StartTimeout = TimeSpan.FromMilliseconds(500);

        readonly WorkerEventLoop[] _eventLoops;
        readonly DispatcherEventLoop _dispatcherLoop;
        int _requestId;

        public override bool IsShutdown => _eventLoops.All(eventLoop => eventLoop.IsShutdown);

        public override bool IsTerminated => _eventLoops.All(eventLoop => eventLoop.IsTerminated);

        public override bool IsShuttingDown => _eventLoops.All(eventLoop => eventLoop.IsShuttingDown);

        public override Task TerminationCompletion { get; }

        public WorkerEventLoopGroup(DispatcherEventLoopGroup eventLoopGroup)
            : this(eventLoopGroup, DefaultEventLoopThreadCount)
        {
        }

        public WorkerEventLoopGroup(DispatcherEventLoopGroup eventLoopGroup, int eventLoopCount)
        {
            if (eventLoopGroup is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.eventLoopGroup); }

            _dispatcherLoop = eventLoopGroup.Dispatcher;
            PipeName = _dispatcherLoop.PipeName;

            // Wait until the pipe is listening to connect
            _dispatcherLoop.WaitForLoopRun(StartTimeout);

            _eventLoops = new WorkerEventLoop[eventLoopCount];
            var terminationTasks = new Task[eventLoopCount];
            for (int i = 0; i < eventLoopCount; i++)
            {
                WorkerEventLoop eventLoop = null;
                bool success = false;
                try
                {
                    eventLoop = new WorkerEventLoop(this);
                    success = eventLoop.ConnectTask.Wait(StartTimeout);
                    if (!success)
                    {
                        ThrowHelper.ThrowTimeoutException(PipeName);
                    }
                }
                catch (Exception ex)
                {
                    ThrowHelper.ThrowInvalidOperationException_CreateChild(ex);
                }
                finally
                {
                    if (!success)
                    {
                        Task.WhenAll(_eventLoops.Take(i).Select(loop => loop.ShutdownGracefullyAsync())).Wait();
                    }
                }

                _eventLoops[i] = eventLoop;
                terminationTasks[i] = eventLoop.TerminationCompletion;
            }

            TerminationCompletion = Task.WhenAll(terminationTasks);
        }

        internal string PipeName { get; }

        IEnumerable<IEventLoop> IEventLoopGroup.Items => _eventLoops;

        internal void Accept(NativeHandle handle)
        {
            Debug.Assert(_dispatcherLoop is object);
            _dispatcherLoop.Accept(handle);
        }

        IEventLoop IEventLoopGroup.GetNext() => (IEventLoop)GetNext();

        public override IEventExecutor GetNext()
        {
            int id = Interlocked.Increment(ref _requestId);
            return _eventLoops[Math.Abs(id % _eventLoops.Length)];
        }

        public Task RegisterAsync(IChannel channel)
        {
            var nativeChannel = channel as INativeChannel;
            if (nativeChannel is null)
            {
                ThrowHelper.ThrowArgumentException_RegChannel();
            }

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
            foreach (WorkerEventLoop eventLoop in _eventLoops)
            {
                eventLoop.ShutdownGracefullyAsync(quietPeriod, timeout);
            }
            return TerminationCompletion;
        }

        protected override IEnumerable<IEventExecutor> GetItems() => _eventLoops;
    }
}