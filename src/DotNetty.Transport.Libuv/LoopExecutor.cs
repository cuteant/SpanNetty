// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoProperty
#pragma warning disable 420
namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Internal;
    using System.Threading;
    using DotNetty.Common;
    using DotNetty.Transport.Libuv.Native;
    using Timer = Native.Timer;

    class LoopExecutor : AbstractScheduledEventExecutor, IOrderedEventExecutor
    {
        private const int DefaultBreakoutTime = 100; //ms
        private static readonly TimeSpan DefaultBreakoutInterval = TimeSpan.FromMilliseconds(DefaultBreakoutTime);
        private static readonly Func<ThreadLocalPool.Handle, WriteRequest> s_valueFactory = handle => new WriteRequest(handle);

        protected static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<LoopExecutor>();

        private const int NotStartedState = 1;
        private const int StartedState = 2;
        private const int ShuttingDownState = 3;
        private const int ShutdownState = 4;
        private const int TerminatedState = 5;

        private readonly ThreadLocalPool<WriteRequest> _writeRequestPool = new ThreadLocalPool<WriteRequest>(s_valueFactory);
        private readonly long _preciseBreakoutInterval;
        private readonly IQueue<IRunnable> _taskQueue;
        private readonly XThread _thread;
        private readonly TaskScheduler _scheduler;
        private readonly ManualResetEventSlim _loopRunStart;
        private readonly IPromise _terminationCompletionSource;
        private readonly Loop _loop;
        private readonly Async _asyncHandle;
        private readonly Timer _timerHandle;

        private int v_executionState = NotStartedState;

        private long _lastExecutionTime;
        private long _gracefulShutdownStartTime;
        private long _gracefulShutdownQuietPeriod;
        private long _gracefulShutdownTimeout;

        // Flag to indicate whether async handle should be used to wake up 
        // the loop, only accessed when InEventLoop is true
        private int v_wakeUp = SharedConstants.True;

        public LoopExecutor(string threadName)
            : this(null, threadName, DefaultBreakoutInterval)
        {
        }

        public LoopExecutor(IEventLoopGroup parent, string threadName)
            : this(parent, threadName, DefaultBreakoutInterval)
        {
        }

        public LoopExecutor(IEventLoopGroup parent, string threadName, TimeSpan breakoutInterval) : base(parent)
        {
            _preciseBreakoutInterval = (long)breakoutInterval.TotalMilliseconds;
            _terminationCompletionSource = NewPromise();
            _taskQueue = PlatformDependent.NewMpscQueue<IRunnable>();
            _scheduler = new ExecutorTaskScheduler(this);

            _loop = new Loop();
            _asyncHandle = new Async(_loop, OnCallbackAction, this);
            _timerHandle = new Timer(_loop, OnCallbackAction, this);
            string name = $"{GetType().Name}:{_loop.Handle}";
            if (!string.IsNullOrEmpty(threadName))
            {
                name = $"{name}({threadName})";
            }
            _thread = new XThread(RunAction) { Name = name };
            _loopRunStart = new ManualResetEventSlim(false, 1);
        }

        internal ThreadLocalPool<WriteRequest> WriteRequestPool => _writeRequestPool;

        protected void Start()
        {
            var currState = Volatile.Read(ref v_executionState);
            if (currState > NotStartedState)
            {
                ThrowHelper.ThrowInvalidOperationException_ExecutionState(currState);
            }
            _thread.Start(this);
        }

        internal Loop UnsafeLoop => _loop;

        internal int LoopThreadId => _thread.Id;

        static readonly XParameterizedThreadStart RunAction = s => Run(s);
        static void Run(object state)
        {
            var loopExecutor = (LoopExecutor)state;
            loopExecutor.SetCurrentExecutor(loopExecutor);

            Task.Factory.StartNew(
                executor => ((LoopExecutor)executor).StartLoop(), state,
                CancellationToken.None,
                TaskCreationOptions.AttachedToParent,
                loopExecutor._scheduler);
        }

        static readonly Action<object> OnCallbackAction = s => OnCallback(s);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void OnCallback(object state) => ((LoopExecutor)state).OnCallback();

        void OnCallback()
        {
            if (IsShuttingDown)
            {
                ShuttingDown();
            }
            else
            {
                RunAllTasks(_preciseBreakoutInterval);
            }
        }

        /// <summary>
        /// Called before run the loop in the loop thread.
        /// </summary>
        protected virtual void Initialize()
        {
            // NOOP
        }

        /// <summary>
        /// Called before stop the loop in the loop thread.
        /// </summary>
        protected virtual void Release()
        {
            // NOOP
        }

        internal void WaitForLoopRun(TimeSpan timeout) => _loopRunStart.Wait(timeout);

        void StartLoop()
        {
            IntPtr handle = _loop.Handle;
            try
            {
                UpdateLastExecutionTime();
                Initialize();
                var oldState = Interlocked.CompareExchange(ref v_executionState, StartedState, NotStartedState);
                if (oldState != NotStartedState)
                {
                    ThrowHelper.ThrowInvalidOperationException_ExecutionState0(oldState);
                }
                _loopRunStart.Set();
                _loop.Run(uv_run_mode.UV_RUN_DEFAULT);
            }
            catch (Exception ex)
            {
                _loopRunStart.Set();
                Logger.LoopRunDefaultError(_thread, handle, ex);
                _terminationCompletionSource.TrySetException(ex);
            }
            finally
            {
                if (Logger.InfoEnabled) Logger.LoopThreadFinished(_thread, handle);
                CleanupAndTerminate();
            }
        }

        void StopLoop()
        {
            try
            {
                // Drop out from the loop so that it can be safely disposed,
                // other active handles will be closed by loop.Close()
                _timerHandle.Stop();
                _loop.Stop();
            }
            catch (Exception ex)
            {
                Logger.ShuttingDownLoopError(ex);
            }
        }

        void ShuttingDown()
        {
            Debug.Assert(InEventLoop);

            CancelScheduledTasks();

            if (0ul >= (ulong)_gracefulShutdownStartTime)
            {
                _gracefulShutdownStartTime = GetLoopTime();
            }

            bool runTask;
            do
            {
                runTask = RunAllTasks();

                // Terminate if the quiet period is 0.
                if (0ul >= (ulong)_gracefulShutdownQuietPeriod)
                {
                    StopLoop();
                    return;
                }
            }
            while (runTask);

            long nanoTime = GetLoopTime();

            // Shutdown timed out
            if (nanoTime - _gracefulShutdownStartTime <= _gracefulShutdownTimeout
                && nanoTime - _lastExecutionTime <= _gracefulShutdownQuietPeriod)
            {
                // Wait for quiet period passed
                _timerHandle.Start(DefaultBreakoutTime, 0); // 100ms
            }
            else
            {
                // No tasks were added for last quiet period
                StopLoop();
            }
        }

        void CleanupAndTerminate()
        {
            try
            {
                Cleanup();
            }
            finally
            {
                Interlocked.Exchange(ref v_executionState, TerminatedState);
                if (_taskQueue.NonEmpty && Logger.WarnEnabled)
                {
                    Logger.TerminatedWithNonEmptyTaskQueue(_taskQueue.Count);
                }
                _terminationCompletionSource.TryComplete();
            }
        }

        void Cleanup()
        {
            IntPtr handle = _loop.Handle;

            try
            {
                Release();
            }
            catch (Exception ex)
            {
                if (Logger.WarnEnabled) Logger.LoopReleaseError(_thread, handle, ex);
            }

            SafeDispose(_timerHandle);
            SafeDispose(_asyncHandle);
            SafeDispose(_loop);
            if (Logger.InfoEnabled) Logger.LoopDisposed(_thread, handle);
        }

        static void SafeDispose(IDisposable handle)
        {
            try
            {
                if (Logger.InfoEnabled) Logger.LoopDisposing(handle);
                handle.Dispose();
            }
            catch (Exception ex)
            {
                if (Logger.WarnEnabled) Logger.LoopDisposeError(handle, ex);
            }
        }

        void UpdateLastExecutionTime() => _lastExecutionTime = GetLoopTime();

        long GetLoopTime()
        {
            _loop.UpdateTime();
            return _loop.Now;
        }

        void RunAllTasks(long timeout)
        {
            FetchFromScheduledTaskQueue();
            IRunnable task = PollTask();
            if (task is null)
            {
                AfterRunningAllTasks();
                return;
            }

            long start = GetLoopTime();
            long runTasks = 0;
            long executionTime;
            Interlocked.Exchange(ref v_wakeUp, SharedConstants.False);
            while (true)
            {
                SafeExecute(task);

                runTasks++;

                // Check timeout every 64 tasks because nanoTime() is relatively expensive.
                // XXX: Hard-coded value - will make it configurable if it is really a problem.
                if (0ul >= (ulong)(runTasks & 0x3F))
                {
                    executionTime = GetLoopTime();
                    if ((executionTime - start) >= timeout)
                    {
                        break;
                    }
                }

                task = PollTask();
                if (task is null)
                {
                    executionTime = GetLoopTime();
                    break;
                }
            }
            Interlocked.Exchange(ref v_wakeUp, SharedConstants.True);

            AfterRunningAllTasks();
            _lastExecutionTime = executionTime;
        }

        void AfterRunningAllTasks()
        {
            if (IsShuttingDown)
            {
                // Immediate shutdown
                WakeUp(true);
                return;
            }

            long nextTimeout = DefaultBreakoutTime;
            if (_taskQueue.NonEmpty)
            {
                _timerHandle.Start(nextTimeout, 0);
            }
            else
            {
                if (ScheduledTaskQueue.TryPeek(out IScheduledRunnable nextScheduledTask))
                {
                    PreciseTimeSpan wakeUpTimeout = nextScheduledTask.Deadline - PreciseTimeSpan.FromStart;
                    if (wakeUpTimeout.Ticks > 0)
                    {
                        nextTimeout = (long)wakeUpTimeout.ToTimeSpan().TotalMilliseconds;
                    }
                    _timerHandle.Start(nextTimeout, 0);
                }
            }
        }

        bool FetchFromScheduledTaskQueue()
        {
            PreciseTimeSpan nanoTime = PreciseTimeSpan.FromStart;
            IScheduledRunnable scheduledTask = PollScheduledTask(nanoTime);
            while (scheduledTask is object)
            {
                if (!_taskQueue.TryEnqueue(scheduledTask))
                {
                    // No space left in the task queue add it back to the scheduledTaskQueue so we pick it up again.
                    ScheduledTaskQueue.TryEnqueue(scheduledTask);
                    return false;
                }
                scheduledTask = PollScheduledTask(nanoTime);
            }
            return true;
        }

        IRunnable PollTask() => PollTaskFrom(_taskQueue);

        bool RunAllTasks()
        {
            bool fetchedAll;
            bool ranAtLeastOne = false;
            do
            {
                fetchedAll = FetchFromScheduledTaskQueue();
                if (RunAllTasksFrom(_taskQueue))
                {
                    ranAtLeastOne = true;
                }
            }
            while (!fetchedAll); // keep on processing until we fetched all scheduled tasks.
            if (ranAtLeastOne)
            {
                _lastExecutionTime = GetLoopTime();
            }
            return ranAtLeastOne;
        }

        static bool RunAllTasksFrom(IQueue<IRunnable> taskQueue)
        {
            IRunnable task = PollTaskFrom(taskQueue);
            if (task is null)
            {
                return false;
            }
            while (true)
            {
                SafeExecute(task);
                task = PollTaskFrom(taskQueue);
                if (task is null)
                {
                    return true;
                }
            }
        }

        static IRunnable PollTaskFrom(IQueue<IRunnable> taskQueue) =>
            taskQueue.TryDequeue(out IRunnable task) ? task : null;

        public override Task TerminationCompletion => _terminationCompletionSource.Task;

        public override bool IsShuttingDown => Volatile.Read(ref v_executionState) >= ShuttingDownState;

        public override bool IsShutdown => Volatile.Read(ref v_executionState) >= ShutdownState;

        public override bool IsTerminated => Volatile.Read(ref v_executionState) == TerminatedState;

        public override bool IsInEventLoop(XThread t) => _thread == t;

        void WakeUp(bool inEventLoop)
        {
            // If the executor is not in the event loop, wake up the loop by async handle immediately.
            //
            // If the executor is in the event loop and in the middle of RunAllTasks, no need to 
            // wake up the loop again because this is normally called by the current running task.
            if (!inEventLoop || SharedConstants.False < (uint)Volatile.Read(ref v_wakeUp))
            {
                _asyncHandle.Send();
            }
        }

        protected override IScheduledRunnable Schedule(IScheduledRunnable task)
        {
            if (InEventLoop)
            {
                ScheduledTaskQueue.TryEnqueue(task);
                //this.WakeUp(true);
                if (SharedConstants.False < (uint)Volatile.Read(ref v_wakeUp))
                {
                    _asyncHandle.Send();
                }
            }
            else
            {
                Execute(EnqueueRunnableAction, this, task);
            }
            return task;
        }

        public override void Execute(IRunnable task)
        {
            if (task is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.task); }

            AddTask(task);
            bool inEventLoop = InEventLoop;
            if (!inEventLoop && IsShutdown)
            {
                ThrowHelper.ThrowRejectedExecutionException_Terminated();
            }
            WakeUp(inEventLoop);
        }

        void AddTask(IRunnable task)
        {
            if (IsShutdown)
            {
                ThrowHelper.ThrowRejectedExecutionException_Shutdown();
            }
            if (!_taskQueue.TryEnqueue(task))
            {
                ThrowHelper.ThrowRejectedExecutionException_Queue();
            }
        }

        public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        {
            if (quietPeriod < TimeSpan.Zero) { ThrowHelper.ThrowArgumentException(); }
            if (timeout < quietPeriod) { ThrowHelper.ThrowArgumentException(); }

            if (IsShuttingDown)
            {
                return TerminationCompletion;
            }

            // In case of Shutdown called before the loop run
            _loopRunStart.Wait();

            bool inEventLoop = InEventLoop;
            bool wakeUpLoop;
            int prevState = Volatile.Read(ref v_executionState);
            int oldState;
            do
            {
                if (IsShuttingDown)
                {
                    return TerminationCompletion;
                }
                int newState;
                wakeUpLoop = true;
                oldState = prevState;
                if (inEventLoop)
                {
                    newState = ShuttingDownState;
                }
                else
                {
                    switch (prevState)
                    {
                        case NotStartedState:
                        case StartedState:
                            newState = ShuttingDownState;
                            break;
                        default:
                            newState = prevState;
                            wakeUpLoop = false;
                            break;
                    }
                }
                prevState = Interlocked.CompareExchange(ref v_executionState, newState, prevState);
            } while (prevState != oldState);

            _gracefulShutdownQuietPeriod = (long)quietPeriod.TotalMilliseconds;
            _gracefulShutdownTimeout = (long)timeout.TotalMilliseconds;

            if (oldState == NotStartedState)
            {
                // If the loop is not yet running (e.g. Initialize failed) close all 
                // handles directly because wake up callback will not be executed. 
                CleanupAndTerminate();
            }
            else
            {
                if (wakeUpLoop)
                {
                    WakeUp(inEventLoop);
                }
            }

            return TerminationCompletion;
        }

        protected override IEnumerable<IEventExecutor> GetItems() => new[] { this };
    }
}
