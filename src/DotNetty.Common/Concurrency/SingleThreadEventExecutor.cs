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
    public class SingleThreadEventExecutor : AbstractScheduledEventExecutor, IOrderedEventExecutor
    {
        private const int ST_NOT_STARTED = 1;
        private const int ST_STARTED = 2;
        private const int ST_SHUTTING_DOWN = 3;
        private const int ST_SHUTDOWN = 4;
        private const int ST_TERMINATED = 5;
        private const string DefaultWorkerThreadName = "SingleThreadEventExecutor worker";

        private static readonly IRunnable s_wakeupTask = new NoOpRunnable();

        private static readonly IInternalLogger Logger =
            InternalLoggerFactory.GetInstance<SingleThreadEventExecutor>();

        private readonly XParameterizedThreadStart _loopAction;
        private readonly Action _loopCoreAciton;

        private readonly IQueue<IRunnable> _taskQueue;
        private readonly Thread _thread;
        private int v_executionState = ST_NOT_STARTED;
        private readonly PreciseTimeSpan _preciseBreakoutInterval;
        private PreciseTimeSpan _lastExecutionTime;
        private readonly ManualResetEventSlim _emptyEvent = new ManualResetEventSlim(false, 1);
        private readonly TaskScheduler _scheduler;
        private readonly IPromise _terminationCompletionSource;
        private PreciseTimeSpan _gracefulShutdownStartTime;
        private PreciseTimeSpan _gracefulShutdownQuietPeriod;
        private PreciseTimeSpan _gracefulShutdownTimeout;
        private readonly ISet<Action> _shutdownHooks = new HashSet<Action>();
        private long v_progress;

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
            _loopAction = Loop;
            _loopCoreAciton = LoopCore;

            _terminationCompletionSource = NewPromise();
            _taskQueue = taskQueue;
            _preciseBreakoutInterval = PreciseTimeSpan.FromTimeSpan(breakoutInterval);
            _scheduler = new ExecutorTaskScheduler(this);
            _thread = new Thread(_loopAction);
            if (string.IsNullOrEmpty(threadName))
            {
                _thread.Name = DefaultWorkerThreadName;
            }
            else
            {
                _thread.Name = threadName;
            }
            _thread.Start();
        }

        /// <summary>
        ///     Task Scheduler that will post work to this executor's queue.
        /// </summary>
        public TaskScheduler Scheduler => _scheduler;

        /// <summary>
        ///     Allows to track whether executor is progressing through its backlog. Useful for diagnosing / mitigating stalls due to blocking calls in conjunction with IsBacklogEmpty property.
        /// </summary>
        public long Progress => Volatile.Read(ref v_progress);

        /// <summary>
        ///     Indicates whether executor's backlog is empty. Useful for diagnosing / mitigating stalls due to blocking calls in conjunction with Progress property.
        /// </summary>
        public bool IsBacklogEmpty => _taskQueue.IsEmpty;

        /// <summary>
        ///     Gets length of backlog of tasks queued for immediate execution.
        /// </summary>
        public int BacklogLength => _taskQueue.Count;

        void Loop(object s)
        {
            SetCurrentExecutor(this);

            Task.Factory.StartNew(_loopCoreAciton, CancellationToken.None, TaskCreationOptions.None, _scheduler);
        }

        void LoopCore()
        {
            try
            {
                Interlocked.CompareExchange(ref v_executionState, ST_STARTED, ST_NOT_STARTED);
                while (!ConfirmShutdown())
                {
                    RunAllTasks(_preciseBreakoutInterval);
                }
                CleanupAndTerminate(true);
            }
            catch (Exception ex)
            {
                Logger.ExecutionLoopFailed(_thread, ex);
                Interlocked.Exchange(ref v_executionState, ST_TERMINATED);
                _terminationCompletionSource.TrySetException(ex);
            }
        }

        /// <inheritdoc cref="IEventExecutor"/>
        public override bool IsShuttingDown => Volatile.Read(ref v_executionState) >= ST_SHUTTING_DOWN;

        /// <inheritdoc cref="IEventExecutor"/>
        public override Task TerminationCompletion => _terminationCompletionSource.Task;

        /// <inheritdoc cref="IEventExecutor"/>
        public override bool IsShutdown => Volatile.Read(ref v_executionState) >= ST_SHUTDOWN;

        /// <inheritdoc cref="IEventExecutor"/>
        public override bool IsTerminated => Volatile.Read(ref v_executionState) == ST_TERMINATED;

        /// <inheritdoc cref="IEventExecutor"/>
        public override bool IsInEventLoop(Thread t) => _thread == t;

        /// <inheritdoc cref="IEventExecutor"/>
        public override void Execute(IRunnable task)
        {
            _taskQueue.TryEnqueue(task);

            if (!InEventLoop)
            {
                _emptyEvent.Set();
            }
        }

        protected override IEnumerable<IEventExecutor> GetItems() => new[] { this };

        protected void WakeUp(bool inEventLoop)
        {
            if (!inEventLoop || (Volatile.Read(ref v_executionState) == ST_SHUTTING_DOWN))
            {
                Execute(s_wakeupTask);
            }
        }

        /// <summary>
        /// Adds an <see cref="Action"/> which will be executed on shutdown of this instance.
        /// </summary>
        /// <param name="action">The <see cref="Action"/> to run on shutdown.</param>
        public void AddShutdownHook(Action action)
        {
            if (InEventLoop)
            {
                _shutdownHooks.Add(action);
            }
            else
            {
                Execute(AddShutdownHookAction, _shutdownHooks, action);
            }
        }

        static readonly Action<object, object> AddShutdownHookAction = OnAddShutdownHook;
        static void OnAddShutdownHook(object s, object a)
        {
            ((ISet<Action>)s).Add((Action)a);
        }

        /// <summary>
        /// Removes a previously added <see cref="Action"/> from the collection of <see cref="Action"/>s which will be
        /// executed on shutdown of this instance.
        /// </summary>
        /// <param name="action">The <see cref="Action"/> to remove.</param>
        public void RemoveShutdownHook(Action action)
        {
            if (InEventLoop)
            {
                _shutdownHooks.Remove(action);
            }
            else
            {
                Execute(RemoveShutdownHookAction, _shutdownHooks, action);
            }
        }

        static readonly Action<object, object> RemoveShutdownHookAction = OnRemoveShutdownHook;
        static void OnRemoveShutdownHook(object s, object a)
        {
            ((ISet<Action>)s).Remove((Action)a);
        }

        bool RunShutdownHooks()
        {
            bool ran = false;

            // Note shutdown hooks can add / remove shutdown hooks.
            while ((uint)_shutdownHooks.Count > 0u)
            {
                var copy = _shutdownHooks.ToArray();
                _shutdownHooks.Clear();

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
                _lastExecutionTime = PreciseTimeSpan.FromStart;
            }

            return ran;
        }


        /// <inheritdoc cref="IEventExecutor"/>
        public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        {
            if (quietPeriod < TimeSpan.Zero) { ThrowHelper.ThrowArgumentException_MustBeGreaterThanOrEquelToZero(quietPeriod); }
            if (timeout < quietPeriod) { ThrowHelper.ThrowArgumentException_MustBeGreaterThanQuietPeriod(timeout, quietPeriod); }

            if (IsShuttingDown)
            {
                return TerminationCompletion;
            }

            bool inEventLoop = InEventLoop;
            bool wakeup;
            int thisState = Volatile.Read(ref v_executionState);
            int oldState;
            do
            {
                if (IsShuttingDown)
                {
                    return TerminationCompletion;
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
                thisState = Interlocked.CompareExchange(ref v_executionState, newState, oldState);
            } while (thisState != oldState);
            _gracefulShutdownQuietPeriod = PreciseTimeSpan.FromTimeSpan(quietPeriod);
            _gracefulShutdownTimeout = PreciseTimeSpan.FromTimeSpan(timeout);

            // TODO: revisit
            //if (ensureThreadStarted(oldState))
            //{
            //    return terminationFuture;
            //}

            if (wakeup)
            {
                WakeUp(inEventLoop);
            }

            return TerminationCompletion;
        }

        protected bool ConfirmShutdown()
        {
            if (!IsShuttingDown)
            {
                return false;
            }

            Debug.Assert(InEventLoop, "must be invoked from an event loop");

            CancelScheduledTasks();

            if (_gracefulShutdownStartTime == PreciseTimeSpan.Zero)
            {
                _gracefulShutdownStartTime = PreciseTimeSpan.FromStart;
            }

            if (RunAllTasks() || RunShutdownHooks())
            {
                if (IsShutdown)
                {
                    // Executor shut down - no new tasks anymore.
                    return true;
                }

                // There were tasks in the queue. Wait a little bit more until no tasks are queued for the quiet period or
                // terminate if the quiet period is 0.
                // See https://github.com/netty/netty/issues/4241
                if (_gracefulShutdownQuietPeriod == PreciseTimeSpan.Zero)
                {
                    return true;
                }
                WakeUp(true);
                return false;
            }

            PreciseTimeSpan nanoTime = PreciseTimeSpan.FromStart;

            if (IsShutdown || (nanoTime - _gracefulShutdownStartTime > _gracefulShutdownTimeout))
            {
                return true;
            }

            if (nanoTime - _lastExecutionTime <= _gracefulShutdownQuietPeriod)
            {
                // Check if any tasks were added to the queue every 100ms.
                // TODO: Change the behavior of takeTask() so that it returns on timeout.
                // todo: ???
                WakeUp(true);
                Thread.Sleep(100);

                return false;
            }

            // No tasks were added for last quiet period - hopefully safe to shut down.
            // (Hopefully because we really cannot make a guarantee that there will be no execute() calls by a user.)
            return true;
        }

        protected void CleanupAndTerminate(bool success)
        {
            var thisState = Volatile.Read(ref v_executionState);
            int oldState;
            do
            {
                oldState = thisState;

                if (oldState >= ST_SHUTTING_DOWN) { break; }

                thisState = Interlocked.CompareExchange(ref v_executionState, ST_SHUTTING_DOWN, oldState);
            } while (thisState != oldState);

            // Check if confirmShutdown() was called at the end of the loop.
            if (success && (_gracefulShutdownStartTime == PreciseTimeSpan.Zero))
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
                    if (ConfirmShutdown())
                    {
                        break;
                    }
                }
            }
            finally
            {
                try
                {
                    Cleanup();
                }
                finally
                {
                    Interlocked.Exchange(ref v_executionState, ST_TERMINATED);
                    if (!_taskQueue.IsEmpty)
                    {
                        Logger.AnEventExecutorTerminatedWithNonEmptyTaskQueue(_taskQueue.Count);
                    }

                    //firstRun = true;
                    _terminationCompletionSource.Complete();
                }
            }
        }

        protected virtual void Cleanup()
        {
            // NOOP
        }

        protected bool RunAllTasks()
        {
            bool fetchedAll;
            bool ranAtLeastOne;
            do
            {
                fetchedAll = FetchFromScheduledTaskQueue();
                IRunnable task = PollTask();
                if (task is null)
                {
                    return false;
                }

                while (true)
                {
                    Volatile.Write(ref v_progress, v_progress + 1); // volatile write is enough as this is the only thread ever writing
                    SafeExecute(task);
                    task = PollTask();
                    if (task is null)
                    {
                        ranAtLeastOne = true;
                        break;
                    }
                }
            } while (!fetchedAll);  // keep on processing until we fetched all scheduled tasks.

            if (ranAtLeastOne)
            {
                _lastExecutionTime = PreciseTimeSpan.FromStart;
            }
            return true;
        }

        bool RunAllTasks(PreciseTimeSpan timeout)
        {
            FetchFromScheduledTaskQueue();
            IRunnable task = PollTask();
            if (task is null)
            {
                AfterRunningAllTasks();
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

                task = PollTask();
                if (task is null)
                {
                    executionTime = PreciseTimeSpan.FromStart;
                    break;
                }
            }

            AfterRunningAllTasks();
            _lastExecutionTime = executionTime;
            return true;
        }

        /// <summary>
        /// Invoked before returning from <see cref="RunAllTasks()"/> and <see cref="RunAllTasks(PreciseTimeSpan)"/>.
        /// </summary>
        protected virtual void AfterRunningAllTasks() { }

        bool FetchFromScheduledTaskQueue()
        {
            if (ScheduledTaskQueue.IsEmpty) { return true; }

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

        IRunnable PollTask()
        {
            Debug.Assert(InEventLoop);

            if (!_taskQueue.TryDequeue(out IRunnable task))
            {
                _emptyEvent.Reset();
                if (!_taskQueue.TryDequeue(out task) && !IsShuttingDown) // revisit queue as producer might have put a task in meanwhile
                {
                    if (ScheduledTaskQueue.TryPeek(out IScheduledRunnable nextScheduledTask))
                    {
                        PreciseTimeSpan wakeupTimeout = nextScheduledTask.Deadline - PreciseTimeSpan.FromStart;
                        if (wakeupTimeout.Ticks > 0L)
                        {
                            double timeout = wakeupTimeout.ToTimeSpan().TotalMilliseconds;
                            _emptyEvent.Wait((int)Math.Min(timeout, int.MaxValue - 1));
                        }
                    }
                    else
                    {
                        _emptyEvent.Wait();
                        _taskQueue.TryDequeue(out task);
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