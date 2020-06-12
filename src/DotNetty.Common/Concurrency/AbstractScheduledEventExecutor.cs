// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// Abstract base class for <see cref="IEventExecutor" />s that need to support scheduling.
    /// </summary>
    public abstract class AbstractScheduledEventExecutor : AbstractEventExecutor
    {
        protected static readonly IRunnable WakeupTask;

        static AbstractScheduledEventExecutor()
        {
            WakeupTask = new NoOpRunnable();
        }

        protected internal readonly IPriorityQueue<IScheduledRunnable> ScheduledTaskQueue;
        private long _nextTaskId;

        protected AbstractScheduledEventExecutor()
            : this(null)
        {
        }

        protected AbstractScheduledEventExecutor(IEventExecutorGroup parent)
            : base(parent)
        {
            ScheduledTaskQueue = new PriorityQueue<IScheduledRunnable>();
        }

        protected static PreciseTimeSpan GetNanos() => PreciseTimeSpan.FromStart;

        protected static bool IsNullOrEmpty<T>(IPriorityQueue<T> taskQueue)
            where T : class, IPriorityQueueNode<T>
        {
            return taskQueue is null || 0u >= (uint)taskQueue.Count;
        }

        /// <summary>
        /// Cancel all scheduled tasks
        /// This method MUST be called only when <see cref="IEventExecutor.InEventLoop" /> is <c>true</c>.
        /// </summary>
        protected virtual void CancelScheduledTasks()
        {
            Debug.Assert(InEventLoop);
            IPriorityQueue<IScheduledRunnable> scheduledTaskQueue = ScheduledTaskQueue;
            if (IsNullOrEmpty(scheduledTaskQueue))
            {
                return;
            }

            IScheduledRunnable[] tasks = scheduledTaskQueue.ToArray();
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i].Cancel();
            }

            ScheduledTaskQueue.Clear();
        }

        internal protected IScheduledRunnable PollScheduledTask() => PollScheduledTask(GetNanos());

        protected IScheduledRunnable PollScheduledTask(in PreciseTimeSpan nanoTime)
        {
            Debug.Assert(InEventLoop);

            if (!ScheduledTaskQueue.TryPeek(out IScheduledRunnable scheduledTask))
            {
                return null;
            }

            if (scheduledTask.Deadline <= nanoTime)
            {
                ScheduledTaskQueue.TryDequeue(out var _);
                scheduledTask.SetConsumed();
                return scheduledTask;
            }
            return null;
        }

        protected PreciseTimeSpan NextScheduledTaskNanos()
        {
            IScheduledRunnable nextScheduledRunnable = PeekScheduledTask();
            return nextScheduledRunnable?.Deadline ?? PreciseTimeSpan.MinusOne;
        }

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        protected IScheduledRunnable PeekScheduledTask()
        {
            //IPriorityQueue<IScheduledRunnable> scheduledTaskQueue = ScheduledTaskQueue;
            //return !IsNullOrEmpty(scheduledTaskQueue) && scheduledTaskQueue.TryPeek(out IScheduledRunnable task) ? task : null;
            return ScheduledTaskQueue.TryPeek(out IScheduledRunnable task) ? task : null;
        }

        protected bool HasScheduledTasks()
        {
            return ScheduledTaskQueue.TryPeek(out IScheduledRunnable scheduledTask) && scheduledTask.Deadline <= PreciseTimeSpan.FromStart;
        }

        public override IScheduledTask Schedule(IRunnable action, TimeSpan delay)
        {
            if (action is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.action); }

            return Schedule(new RunnableScheduledTask(this, action, PreciseTimeSpan.Deadline(delay)));
        }

        public override IScheduledTask Schedule(Action action, TimeSpan delay)
        {
            if (action is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.action); }

            return Schedule(new ActionScheduledTask(this, action, PreciseTimeSpan.Deadline(delay)));
        }

        public override IScheduledTask Schedule(Action<object> action, object state, TimeSpan delay)
        {
            if (action is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.action); }

            return Schedule(new StateActionScheduledTask(this, action, state, PreciseTimeSpan.Deadline(delay)));
        }

        public override IScheduledTask Schedule(Action<object, object> action, object context, object state, TimeSpan delay)
        {
            if (action is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.action); }

            return Schedule(new StateActionWithContextScheduledTask(this, action, context, state, PreciseTimeSpan.Deadline(delay)));
        }

        public override Task ScheduleAsync(Action action, TimeSpan delay, CancellationToken cancellationToken)
        {
            if (action is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.action); }

            if (cancellationToken.IsCancellationRequested)
            {
                return TaskUtil.Cancelled;
            }

            if (!cancellationToken.CanBeCanceled)
            {
                return Schedule(action, delay).Completion;
            }

            return Schedule(new ActionScheduledAsyncTask(this, action, PreciseTimeSpan.Deadline(delay), cancellationToken)).Completion;
        }

        public override Task ScheduleAsync(Action<object> action, object state, TimeSpan delay, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return TaskUtil.Cancelled;
            }

            if (!cancellationToken.CanBeCanceled)
            {
                return Schedule(action, state, delay).Completion;
            }

            return Schedule(new StateActionScheduledAsyncTask(this, action, state, PreciseTimeSpan.Deadline(delay), cancellationToken)).Completion;
        }

        public override Task ScheduleAsync(Action<object, object> action, object context, object state, TimeSpan delay, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return TaskUtil.Cancelled;
            }

            if (!cancellationToken.CanBeCanceled)
            {
                return Schedule(action, context, state, delay).Completion;
            }

            return Schedule(new StateActionWithContextScheduledAsyncTask(this, action, context, state, PreciseTimeSpan.Deadline(delay), cancellationToken)).Completion;
        }

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        protected internal void ScheduleFromEventLoop(IScheduledRunnable task)
        {
            // nextTaskId a long and so there is no chance it will overflow back to 0
            ScheduledTaskQueue.TryEnqueue(task.SetId(++_nextTaskId));
        }

        protected virtual IScheduledRunnable Schedule(IScheduledRunnable task)
        {
            if (InEventLoop)
            {
                ScheduleFromEventLoop(task);
            }
            else
            {
                var deadline = task.Deadline;
                // task will add itself to scheduled task queue when run if not expired
                if (BeforeScheduledTaskSubmitted(deadline))
                {
                    Execute(task);
                }
                else
                {
                    LazyExecute(task);
                    // Second hook after scheduling to facilitate race-avoidance
                    if (AfterScheduledTaskSubmitted(deadline))
                    {
                        Execute(WakeupTask);
                    }
                }
            }
            return task;
        }

        internal void RemoveScheduled(IScheduledRunnable task)
        {
            if (InEventLoop)
            {
                ScheduledTaskQueue.TryRemove(task);
            }
            else
            {
                // task will remove itself from scheduled task queue when it runs
                LazyExecute(task);
            }
        }

        /// <summary>
        /// Called from arbitrary non-<see cref="IEventExecutor"/> threads prior to scheduled task submission.
        /// Returns <c>true</c> if the <see cref="IEventExecutor"/> thread should be woken immediately to
        /// process the scheduled task (if not already awake).
        /// 
        /// <para>If <c>false</c> is returned, <see cref="AfterScheduledTaskSubmitted(in PreciseTimeSpan)"/> will be called with
        /// the same value <i>after</i> the scheduled task is enqueued, providing another opportunity
        /// to wake the <see cref="IEventExecutor"/> thread if required.</para>
        /// </summary>
        /// <param name="deadline">deadline of the to-be-scheduled task
        /// relative to <see cref="AbstractScheduledEventExecutor.GetNanos()"/></param>
        /// <returns><c>true</c> if the <see cref="IEventExecutor"/> thread should be woken, <c>false</c> otherwise</returns>
        protected virtual bool BeforeScheduledTaskSubmitted(in PreciseTimeSpan deadline)
        {
            return true;
        }

        /// <summary>
        /// See <see cref="BeforeScheduledTaskSubmitted(in PreciseTimeSpan)"/>. Called only after that method returns false.
        /// </summary>
        /// <param name="deadline">relative to <see cref="AbstractScheduledEventExecutor.GetNanos()"/></param>
        /// <returns><c>true</c> if the <see cref="IEventExecutor"/> thread should be woken, <c>false</c> otherwise</returns>
        protected virtual bool AfterScheduledTaskSubmitted(in PreciseTimeSpan deadline)
        {
            return true;
        }

        sealed class NoOpRunnable : IRunnable
        {
            public void Run()
            {
            }
        }
    }
}