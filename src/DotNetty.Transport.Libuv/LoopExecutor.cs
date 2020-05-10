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
        const int DefaultBreakoutTime = 100; //ms
        static readonly TimeSpan DefaultBreakoutInterval = TimeSpan.FromMilliseconds(DefaultBreakoutTime);

        protected static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<LoopExecutor>();

        const int NotStartedState = 1;
        const int StartedState = 2;
        const int ShuttingDownState = 3;
        const int ShutdownState = 4;
        const int TerminatedState = 5;

        readonly ThreadLocalPool<WriteRequest> writeRequestPool = new ThreadLocalPool<WriteRequest>(handle => new WriteRequest(handle));
        readonly long preciseBreakoutInterval;
        readonly IQueue<IRunnable> taskQueue;
        readonly XThread thread;
        readonly TaskScheduler scheduler;
        readonly ManualResetEventSlim loopRunStart;
        readonly IPromise terminationCompletionSource;
        readonly Loop loop;
        readonly Async asyncHandle;
        readonly Timer timerHandle;

        int executionState = NotStartedState;

        long lastExecutionTime;
        long gracefulShutdownStartTime;
        long gracefulShutdownQuietPeriod;
        long gracefulShutdownTimeout;

        // Flag to indicate whether async handle should be used to wake up 
        // the loop, only accessed when InEventLoop is true
        int wakeUp = SharedConstants.True;

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
            this.preciseBreakoutInterval = (long)breakoutInterval.TotalMilliseconds;
            this.terminationCompletionSource = this.NewPromise();
            this.taskQueue = PlatformDependent.NewMpscQueue<IRunnable>();
            this.scheduler = new ExecutorTaskScheduler(this);

            this.loop = new Loop();
            this.asyncHandle = new Async(this.loop, OnCallbackAction, this);
            this.timerHandle = new Timer(this.loop, OnCallbackAction, this);
            string name = $"{this.GetType().Name}:{this.loop.Handle}";
            if (!string.IsNullOrEmpty(threadName))
            {
                name = $"{name}({threadName})";
            }
            this.thread = new XThread(RunAction) { Name = name };
            this.loopRunStart = new ManualResetEventSlim(false, 1);
        }

        internal ThreadLocalPool<WriteRequest> WriteRequestPool => this.writeRequestPool;

        protected void Start()
        {
            var currState = Volatile.Read(ref this.executionState);
            if (currState > NotStartedState)
            {
                ThrowHelper.ThrowInvalidOperationException_ExecutionState(currState);
            }
            this.thread.Start(this);
        }

        internal Loop UnsafeLoop => this.loop;

        internal int LoopThreadId => this.thread.Id;

        static readonly XParameterizedThreadStart RunAction = s => Run(s);
        static void Run(object state)
        {
            var loopExecutor = (LoopExecutor)state;
            loopExecutor.SetCurrentExecutor(loopExecutor);

            Task.Factory.StartNew(
                executor => ((LoopExecutor)executor).StartLoop(), state,
                CancellationToken.None,
                TaskCreationOptions.AttachedToParent,
                loopExecutor.scheduler);
        }

        static readonly Action<object> OnCallbackAction = s => OnCallback(s);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void OnCallback(object state) => ((LoopExecutor)state).OnCallback();

        void OnCallback()
        {
            if (this.IsShuttingDown)
            {
                this.ShuttingDown();
            }
            else
            {
                this.RunAllTasks(this.preciseBreakoutInterval);
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

        internal void WaitForLoopRun(TimeSpan timeout) => this.loopRunStart.Wait(timeout);

        void StartLoop()
        {
            IntPtr handle = this.loop.Handle;
            try
            {
                this.UpdateLastExecutionTime();
                this.Initialize();
                var oldState = Interlocked.CompareExchange(ref this.executionState, StartedState, NotStartedState);
                if (oldState != NotStartedState)
                {
                    ThrowHelper.ThrowInvalidOperationException_ExecutionState0(oldState);
                }
                this.loopRunStart.Set();
                this.loop.Run(uv_run_mode.UV_RUN_DEFAULT);
            }
            catch (Exception ex)
            {
                this.loopRunStart.Set();
                Logger.LoopRunDefaultError(this.thread, handle, ex);
                this.terminationCompletionSource.TrySetException(ex);
            }
            finally
            {
                if (Logger.InfoEnabled) Logger.LoopThreadFinished(this.thread, handle);
                this.CleanupAndTerminate();
            }
        }

        void StopLoop()
        {
            try
            {
                // Drop out from the loop so that it can be safely disposed,
                // other active handles will be closed by loop.Close()
                this.timerHandle.Stop();
                this.loop.Stop();
            }
            catch (Exception ex)
            {
                Logger.ShuttingDownLoopError(ex);
            }
        }

        void ShuttingDown()
        {
            Debug.Assert(this.InEventLoop);

            this.CancelScheduledTasks();

            if (0ul >= (ulong)this.gracefulShutdownStartTime)
            {
                this.gracefulShutdownStartTime = this.GetLoopTime();
            }

            bool runTask;
            do
            {
                runTask = this.RunAllTasks();

                // Terminate if the quiet period is 0.
                if (0ul >= (ulong)this.gracefulShutdownQuietPeriod)
                {
                    this.StopLoop();
                    return;
                }
            }
            while (runTask);

            long nanoTime = this.GetLoopTime();

            // Shutdown timed out
            if (nanoTime - this.gracefulShutdownStartTime <= this.gracefulShutdownTimeout
                && nanoTime - this.lastExecutionTime <= this.gracefulShutdownQuietPeriod)
            {
                // Wait for quiet period passed
                this.timerHandle.Start(DefaultBreakoutTime, 0); // 100ms
            }
            else
            {
                // No tasks were added for last quiet period
                this.StopLoop();
            }
        }

        void CleanupAndTerminate()
        {
            try
            {
                this.Cleanup();
            }
            finally
            {
                Interlocked.Exchange(ref this.executionState, TerminatedState);
                if (this.taskQueue.NonEmpty && Logger.WarnEnabled)
                {
                    Logger.TerminatedWithNonEmptyTaskQueue(this.taskQueue.Count);
                }
                this.terminationCompletionSource.TryComplete();
            }
        }

        void Cleanup()
        {
            IntPtr handle = this.loop.Handle;

            try
            {
                this.Release();
            }
            catch (Exception ex)
            {
                if (Logger.WarnEnabled) Logger.LoopReleaseError(this.thread, handle, ex);
            }

            SafeDispose(this.timerHandle);
            SafeDispose(this.asyncHandle);
            SafeDispose(this.loop);
            if (Logger.InfoEnabled) Logger.LoopDisposed(this.thread, handle);
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

        void UpdateLastExecutionTime() => this.lastExecutionTime = this.GetLoopTime();

        long GetLoopTime()
        {
            this.loop.UpdateTime();
            return this.loop.Now;
        }

        void RunAllTasks(long timeout)
        {
            this.FetchFromScheduledTaskQueue();
            IRunnable task = this.PollTask();
            if (task is null)
            {
                this.AfterRunningAllTasks();
                return;
            }

            long start = this.GetLoopTime();
            long runTasks = 0;
            long executionTime;
            Interlocked.Exchange(ref this.wakeUp, SharedConstants.False);
            while (true)
            {
                SafeExecute(task);

                runTasks++;

                // Check timeout every 64 tasks because nanoTime() is relatively expensive.
                // XXX: Hard-coded value - will make it configurable if it is really a problem.
                if (0ul >= (ulong)(runTasks & 0x3F))
                {
                    executionTime = this.GetLoopTime();
                    if ((executionTime - start) >= timeout)
                    {
                        break;
                    }
                }

                task = this.PollTask();
                if (task is null)
                {
                    executionTime = this.GetLoopTime();
                    break;
                }
            }
            Interlocked.Exchange(ref this.wakeUp, SharedConstants.True);

            this.AfterRunningAllTasks();
            this.lastExecutionTime = executionTime;
        }

        void AfterRunningAllTasks()
        {
            if (this.IsShuttingDown)
            {
                // Immediate shutdown
                this.WakeUp(true);
                return;
            }

            long nextTimeout = DefaultBreakoutTime;
            if (this.taskQueue.NonEmpty)
            {
                this.timerHandle.Start(nextTimeout, 0);
            }
            else
            {
                if (this.ScheduledTaskQueue.TryPeek(out IScheduledRunnable nextScheduledTask))
                {
                    PreciseTimeSpan wakeUpTimeout = nextScheduledTask.Deadline - PreciseTimeSpan.FromStart;
                    if (wakeUpTimeout.Ticks > 0)
                    {
                        nextTimeout = (long)wakeUpTimeout.ToTimeSpan().TotalMilliseconds;
                    }
                    this.timerHandle.Start(nextTimeout, 0);
                }
            }
        }

        bool FetchFromScheduledTaskQueue()
        {
            PreciseTimeSpan nanoTime = PreciseTimeSpan.FromStart;
            IScheduledRunnable scheduledTask = this.PollScheduledTask(nanoTime);
            while (scheduledTask is object)
            {
                if (!this.taskQueue.TryEnqueue(scheduledTask))
                {
                    // No space left in the task queue add it back to the scheduledTaskQueue so we pick it up again.
                    this.ScheduledTaskQueue.TryEnqueue(scheduledTask);
                    return false;
                }
                scheduledTask = this.PollScheduledTask(nanoTime);
            }
            return true;
        }

        IRunnable PollTask() => PollTaskFrom(this.taskQueue);

        bool RunAllTasks()
        {
            bool fetchedAll;
            bool ranAtLeastOne = false;
            do
            {
                fetchedAll = this.FetchFromScheduledTaskQueue();
                if (RunAllTasksFrom(this.taskQueue))
                {
                    ranAtLeastOne = true;
                }
            }
            while (!fetchedAll); // keep on processing until we fetched all scheduled tasks.
            if (ranAtLeastOne)
            {
                this.lastExecutionTime = this.GetLoopTime();
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

        public override Task TerminationCompletion => this.terminationCompletionSource.Task;

        public override bool IsShuttingDown => Volatile.Read(ref this.executionState) >= ShuttingDownState;

        public override bool IsShutdown => Volatile.Read(ref this.executionState) >= ShutdownState;

        public override bool IsTerminated => Volatile.Read(ref this.executionState) == TerminatedState;

        public override bool IsInEventLoop(XThread t) => this.thread == t;

        void WakeUp(bool inEventLoop)
        {
            // If the executor is not in the event loop, wake up the loop by async handle immediately.
            //
            // If the executor is in the event loop and in the middle of RunAllTasks, no need to 
            // wake up the loop again because this is normally called by the current running task.
            if (!inEventLoop || SharedConstants.True == Volatile.Read(ref this.wakeUp))
            {
                this.asyncHandle.Send();
            }
        }

        protected override IScheduledRunnable Schedule(IScheduledRunnable task)
        {
            if (this.InEventLoop)
            {
                this.ScheduledTaskQueue.TryEnqueue(task);
                //this.WakeUp(true);
                if (SharedConstants.True == Volatile.Read(ref this.wakeUp))
                {
                    this.asyncHandle.Send();
                }
            }
            else
            {
                this.Execute(EnqueueRunnableAction, this, task);
            }
            return task;
        }

        public override void Execute(IRunnable task)
        {
            if (task is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.task); }

            this.AddTask(task);
            bool inEventLoop = this.InEventLoop;
            if (!inEventLoop && this.IsShutdown)
            {
                ThrowHelper.ThrowRejectedExecutionException_Terminated();
            }
            this.WakeUp(inEventLoop);
        }

        void AddTask(IRunnable task)
        {
            if (this.IsShutdown)
            {
                ThrowHelper.ThrowRejectedExecutionException_Shutdown();
            }
            if (!this.taskQueue.TryEnqueue(task))
            {
                ThrowHelper.ThrowRejectedExecutionException_Queue();
            }
        }

        static void Reject(string message) => throw new RejectedExecutionException(message);

        public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        {
            if (quietPeriod < TimeSpan.Zero) { ThrowHelper.ThrowArgumentException(); }
            if (timeout < quietPeriod) { ThrowHelper.ThrowArgumentException(); }

            if (this.IsShuttingDown)
            {
                return this.TerminationCompletion;
            }

            // In case of Shutdown called before the loop run
            this.loopRunStart.Wait();

            bool inEventLoop = this.InEventLoop;
            bool wakeUpLoop;
            int prevState = Volatile.Read(ref this.executionState);
            int oldState;
            do
            {
                if (this.IsShuttingDown)
                {
                    return this.TerminationCompletion;
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
                prevState = Interlocked.CompareExchange(ref this.executionState, newState, prevState);
            } while (prevState != oldState);

            this.gracefulShutdownQuietPeriod = (long)quietPeriod.TotalMilliseconds;
            this.gracefulShutdownTimeout = (long)timeout.TotalMilliseconds;

            if (oldState == NotStartedState)
            {
                // If the loop is not yet running (e.g. Initialize failed) close all 
                // handles directly because wake up callback will not be executed. 
                this.CleanupAndTerminate();
            }
            else
            {
                if (wakeUpLoop)
                {
                    this.WakeUp(inEventLoop);
                }
            }

            return this.TerminationCompletion;
        }

        protected override IEnumerable<IEventExecutor> GetItems() => new[] { this };
    }
}
