// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using Thread = XThread;

    /// <summary>
    /// <see cref="IOrderedEventExecutor"/> backed by a single thread.
    /// </summary>
    public partial class SingleThreadEventExecutor : AbstractScheduledEventExecutor, IOrderedEventExecutor
    {
#pragma warning disable 420 // referencing volatile fields is fine in Interlocked methods

        const int ST_NOT_STARTED = 1;
        const int ST_STARTED = 2;
        const int ST_SHUTTING_DOWN = 3;
        const int ST_SHUTDOWN = 4;
        const int ST_TERMINATED = 5;
        const string DefaultWorkerThreadName = "SingleThreadEventExecutor worker";

        static readonly IRunnable WAKEUP_TASK = new NoOpRunnable();

        static readonly IInternalLogger Logger =
            InternalLoggerFactory.GetInstance<SingleThreadEventExecutor>();

        readonly IQueue<IRunnable> taskQueue;
        readonly Thread thread;
        int executionState = ST_NOT_STARTED;
        readonly PreciseTimeSpan preciseBreakoutInterval;
        PreciseTimeSpan lastExecutionTime;
        readonly ManualResetEventSlim emptyEvent = new ManualResetEventSlim(false, 1);
        readonly TaskScheduler scheduler;
        readonly IPromise terminationCompletionSource;
        PreciseTimeSpan gracefulShutdownStartTime;
        PreciseTimeSpan gracefulShutdownQuietPeriod;
        PreciseTimeSpan gracefulShutdownTimeout;
        readonly ISet<Action> shutdownHooks = new HashSet<Action>();
        long progress;

        /// <summary>Creates a new instance of <see cref="SingleThreadEventExecutor"/>.</summary>
        public SingleThreadEventExecutor(string threadName, TimeSpan breakoutInterval)
            : this(null, threadName, breakoutInterval, new CompatibleConcurrentQueue<IRunnable>())
        {
        }

        /// <summary>Creates a new instance of <see cref="SingleThreadEventExecutor"/>.</summary>
        public SingleThreadEventExecutor(IEventExecutorGroup parent, string threadName, TimeSpan breakoutInterval)
            : this(parent, threadName, breakoutInterval, new CompatibleConcurrentQueue<IRunnable>())
        {
        }

        protected SingleThreadEventExecutor(string threadName, TimeSpan breakoutInterval, IQueue<IRunnable> taskQueue)
            : this(null, threadName, breakoutInterval, taskQueue)
        { }

        protected SingleThreadEventExecutor(IEventExecutorGroup parent, string threadName, TimeSpan breakoutInterval, IQueue<IRunnable> taskQueue)
            : base(parent)
        {
            this.loopAction = this.Loop;
            this.loopCoreAciton = this.LoopCore;

            this.terminationCompletionSource = this.NewPromise();
            this.taskQueue = taskQueue;
            this.preciseBreakoutInterval = PreciseTimeSpan.FromTimeSpan(breakoutInterval);
            this.scheduler = new ExecutorTaskScheduler(this);
            this.thread = new Thread(this.loopAction);
            if (string.IsNullOrEmpty(threadName))
            {
                this.thread.Name = DefaultWorkerThreadName;
            }
            else
            {
                this.thread.Name = threadName;
            }
            this.thread.Start();
        }

        /// <summary>
        ///     Task Scheduler that will post work to this executor's queue.
        /// </summary>
        public TaskScheduler Scheduler => this.scheduler;

        /// <summary>
        ///     Allows to track whether executor is progressing through its backlog. Useful for diagnosing / mitigating stalls due to blocking calls in conjunction with IsBacklogEmpty property.
        /// </summary>
        public long Progress => Volatile.Read(ref this.progress);

        /// <summary>
        ///     Indicates whether executor's backlog is empty. Useful for diagnosing / mitigating stalls due to blocking calls in conjunction with Progress property.
        /// </summary>
        public bool IsBacklogEmpty => this.taskQueue.IsEmpty;

        /// <summary>
        ///     Gets length of backlog of tasks queued for immediate execution.
        /// </summary>
        public int BacklogLength => this.taskQueue.Count;

        readonly XParameterizedThreadStart loopAction;
        void Loop(object s)
        {
            this.SetCurrentExecutor(this);

            Task.Factory.StartNew(this.loopCoreAciton, CancellationToken.None, TaskCreationOptions.None, this.scheduler);
        }

        readonly Action loopCoreAciton;
        void LoopCore()
        {
            try
            {
                Interlocked.CompareExchange(ref this.executionState, ST_STARTED, ST_NOT_STARTED);
                while (!this.ConfirmShutdown())
                {
                    this.RunAllTasks(this.preciseBreakoutInterval);
                }
                this.CleanupAndTerminate(true);
            }
            catch (Exception ex)
            {
                Logger.ExecutionLoopFailed(this.thread, ex);
                Interlocked.Exchange(ref this.executionState, ST_TERMINATED);
                this.terminationCompletionSource.TrySetException(ex);
            }
        }

        /// <inheritdoc cref="IEventExecutor"/>
        public override bool IsShuttingDown => Volatile.Read(ref this.executionState) >= ST_SHUTTING_DOWN;

        /// <inheritdoc cref="IEventExecutor"/>
        public override Task TerminationCompletion => this.terminationCompletionSource.Task;

        /// <inheritdoc cref="IEventExecutor"/>
        public override bool IsShutdown => Volatile.Read(ref this.executionState) >= ST_SHUTDOWN;

        /// <inheritdoc cref="IEventExecutor"/>
        public override bool IsTerminated => Volatile.Read(ref this.executionState) == ST_TERMINATED;

        /// <inheritdoc cref="IEventExecutor"/>
        public override bool IsInEventLoop(Thread t) => this.thread == t;

        /// <inheritdoc cref="IEventExecutor"/>
        public override void Execute(IRunnable task)
        {
            this.taskQueue.TryEnqueue(task);

            if (!this.InEventLoop)
            {
                this.emptyEvent.Set();
            }
        }

        protected override IEnumerable<IEventExecutor> GetItems() => new[] { this };

        protected void WakeUp(bool inEventLoop)
        {
            if (!inEventLoop || (Volatile.Read(ref this.executionState) == ST_SHUTTING_DOWN))
            {
                this.Execute(WAKEUP_TASK);
            }
        }

        /// <summary>
        /// Adds an <see cref="Action"/> which will be executed on shutdown of this instance.
        /// </summary>
        /// <param name="action">The <see cref="Action"/> to run on shutdown.</param>
        public void AddShutdownHook(Action action)
        {
            if (this.InEventLoop)
            {
                this.shutdownHooks.Add(action);
            }
            else
            {
                this.Execute(AddShutdownHookAction, this.shutdownHooks, action);
            }
        }

        /// <summary>
        /// Removes a previously added <see cref="Action"/> from the collection of <see cref="Action"/>s which will be
        /// executed on shutdown of this instance.
        /// </summary>
        /// <param name="action">The <see cref="Action"/> to remove.</param>
        public void RemoveShutdownHook(Action action)
        {
            if (this.InEventLoop)
            {
                this.shutdownHooks.Remove(action);
            }
            else
            {
                this.Execute(RemoveShutdownHookAction, this.shutdownHooks, action);
            }
        }

        bool RunShutdownHooks()
        {
            bool ran = false;

            // Note shutdown hooks can add / remove shutdown hooks.
            while (this.shutdownHooks.Count > 0)
            {
                var copy = this.shutdownHooks.ToArray();
                this.shutdownHooks.Clear();

                for (var i = 0; i < copy.Length; i++)
                {
                    try
                    {
                        copy[i]();
                    }
                    catch (Exception ex)
                    {
                        Logger.ShutdownHookRaisedAnException(ex);
                    }
                    finally
                    {
                        ran = true;
                    }
                }
            }

            if (ran)
            {
                this.lastExecutionTime = PreciseTimeSpan.FromStart;
            }

            return ran;
        }


        /// <inheritdoc cref="IEventExecutor"/>
        public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        {
            if (quietPeriod < TimeSpan.Zero) { ThrowHelper.ThrowArgumentException_MustBeGreaterThanOrEquelToZero(quietPeriod); }
            if (timeout < quietPeriod) { ThrowHelper.ThrowArgumentException_MustBeGreaterThanQuietPeriod(timeout, quietPeriod); }

            if (this.IsShuttingDown)
            {
                return this.TerminationCompletion;
            }

            bool inEventLoop = this.InEventLoop;
            bool wakeup;
            int thisState = Volatile.Read(ref this.executionState);
            int oldState;
            do
            {
                if (this.IsShuttingDown)
                {
                    return this.TerminationCompletion;
                }
                int newState;
                wakeup = true;
                oldState = thisState;
                if (inEventLoop)
                {
                    newState = ST_SHUTTING_DOWN;
                }
                else
                {
                    switch (oldState)
                    {
                        case ST_NOT_STARTED:
                        case ST_STARTED:
                            newState = ST_SHUTTING_DOWN;
                            break;
                        default:
                            newState = oldState;
                            wakeup = false;
                            break;
                    }
                }
                thisState = Interlocked.CompareExchange(ref this.executionState, newState, oldState);
            } while (thisState != oldState);
            this.gracefulShutdownQuietPeriod = PreciseTimeSpan.FromTimeSpan(quietPeriod);
            this.gracefulShutdownTimeout = PreciseTimeSpan.FromTimeSpan(timeout);

            // todo: revisit
            //if (oldState == ST_NOT_STARTED)
            //{
            //    scheduleExecution();
            //}

            if (wakeup)
            {
                this.WakeUp(inEventLoop);
            }

            return this.TerminationCompletion;
        }

        protected bool ConfirmShutdown()
        {
            if (!this.IsShuttingDown)
            {
                return false;
            }

            Debug.Assert(this.InEventLoop, "must be invoked from an event loop");

            this.CancelScheduledTasks();

            if (this.gracefulShutdownStartTime == PreciseTimeSpan.Zero)
            {
                this.gracefulShutdownStartTime = PreciseTimeSpan.FromStart;
            }

            if (this.RunAllTasks() || this.RunShutdownHooks())
            {
                if (this.IsShutdown)
                {
                    // Executor shut down - no new tasks anymore.
                    return true;
                }

                // There were tasks in the queue. Wait a little bit more until no tasks are queued for the quiet period.
                this.WakeUp(true);
                return false;
            }

            PreciseTimeSpan nanoTime = PreciseTimeSpan.FromStart;

            if (this.IsShutdown || (nanoTime - this.gracefulShutdownStartTime > this.gracefulShutdownTimeout))
            {
                return true;
            }

            if (nanoTime - this.lastExecutionTime <= this.gracefulShutdownQuietPeriod)
            {
                // Check if any tasks were added to the queue every 100ms.
                // TODO: Change the behavior of takeTask() so that it returns on timeout.
                // todo: ???
                this.WakeUp(true);
                Thread.Sleep(100);

                return false;
            }

            // No tasks were added for last quiet period - hopefully safe to shut down.
            // (Hopefully because we really cannot make a guarantee that there will be no execute() calls by a user.)
            return true;
        }

        protected void CleanupAndTerminate(bool success)
        {
            var thisState = Volatile.Read(ref this.executionState);
            int oldState;
            do
            {
                oldState = thisState;

                if (oldState >= ST_SHUTTING_DOWN) { break; }

                thisState = Interlocked.CompareExchange(ref this.executionState, ST_SHUTTING_DOWN, oldState);
            } while (thisState != oldState);

            // Check if confirmShutdown() was called at the end of the loop.
            if (success && (this.gracefulShutdownStartTime == PreciseTimeSpan.Zero))
            {
                Logger.BuggyImplementation();
                //$"Buggy {typeof(IEventExecutor).Name} implementation; {typeof(SingleThreadEventExecutor).Name}.ConfirmShutdown() must be called "
                //+ "before run() implementation terminates.");
            }

            try
            {
                // Run all remaining tasks and shutdown hooks.
                while (true)
                {
                    if (this.ConfirmShutdown())
                    {
                        break;
                    }
                }
            }
            finally
            {
                try
                {
                    this.Cleanup();
                }
                finally
                {
                    Interlocked.Exchange(ref this.executionState, ST_TERMINATED);
                    if (!this.taskQueue.IsEmpty)
                    {
                        Logger.AnEventExecutorTerminatedWithNonEmptyTaskQueue(this.taskQueue.Count);
                    }

                    //firstRun = true;
                    this.terminationCompletionSource.Complete();
                }
            }
        }

        protected virtual void Cleanup()
        {
            // NOOP
        }

        protected bool RunAllTasks()
        {
            this.FetchFromScheduledTaskQueue();
            IRunnable task = this.PollTask();
            if (task == null)
            {
                return false;
            }

            while (true)
            {
                Volatile.Write(ref this.progress, this.progress + 1); // volatile write is enough as this is the only thread ever writing
                SafeExecute(task);
                task = this.PollTask();
                if (task == null)
                {
                    this.lastExecutionTime = PreciseTimeSpan.FromStart;
                    return true;
                }
            }
        }

        bool RunAllTasks(PreciseTimeSpan timeout)
        {
            this.FetchFromScheduledTaskQueue();
            IRunnable task = this.PollTask();
            if (task == null)
            {
                return false;
            }

            PreciseTimeSpan deadline = PreciseTimeSpan.Deadline(timeout);
            long runTasks = 0;
            PreciseTimeSpan executionTime;
            while (true)
            {
                SafeExecute(task);

                runTasks++;

                // Check timeout every 64 tasks because nanoTime() is relatively expensive.
                // XXX: Hard-coded value - will make it configurable if it is really a problem.
                if (0ul >= (ulong)(runTasks & 0x3F))
                {
                    executionTime = PreciseTimeSpan.FromStart;
                    if (executionTime >= deadline)
                    {
                        break;
                    }
                }

                task = this.PollTask();
                if (task == null)
                {
                    executionTime = PreciseTimeSpan.FromStart;
                    break;
                }
            }

            this.lastExecutionTime = executionTime;
            return true;
        }

        bool FetchFromScheduledTaskQueue()
        {
            PreciseTimeSpan nanoTime = PreciseTimeSpan.FromStart;
            IScheduledRunnable scheduledTask = this.PollScheduledTask(nanoTime);
            while (scheduledTask != null)
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

        IRunnable PollTask()
        {
            Debug.Assert(this.InEventLoop);

            IRunnable task;
            if (!this.taskQueue.TryDequeue(out task))
            {
                this.emptyEvent.Reset();
                if (!this.taskQueue.TryDequeue(out task) && !this.IsShuttingDown) // revisit queue as producer might have put a task in meanwhile
                {
                    if (this.ScheduledTaskQueue.TryPeek(out IScheduledRunnable nextScheduledTask))
                    {
                        PreciseTimeSpan wakeupTimeout = nextScheduledTask.Deadline - PreciseTimeSpan.FromStart;
                        if (wakeupTimeout.Ticks > 0)
                        {
                            double timeout = wakeupTimeout.ToTimeSpan().TotalMilliseconds;
                            this.emptyEvent.Wait((int)Math.Min(timeout, int.MaxValue - 1));
                        }
                    }
                    else
                    {
                        this.emptyEvent.Wait();
                        this.taskQueue.TryDequeue(out task);
                    }
                }
            }

            return task;
        }

        sealed class NoOpRunnable : IRunnable
        {
            public void Run()
            {
            }
        }
    }
}