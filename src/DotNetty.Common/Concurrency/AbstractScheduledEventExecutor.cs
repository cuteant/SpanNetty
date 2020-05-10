// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;

    /// <summary>
    ///     Abstract base class for <see cref="IEventExecutor" />s that need to support scheduling.
    /// </summary>
    public abstract partial class AbstractScheduledEventExecutor : AbstractEventExecutor
    {
        protected readonly IPriorityQueue<IScheduledRunnable> ScheduledTaskQueue = new PriorityQueue<IScheduledRunnable>();

        protected AbstractScheduledEventExecutor()
        {
        }

        protected AbstractScheduledEventExecutor(IEventExecutorGroup parent)
            : base(parent)
        {
        }

        protected static PreciseTimeSpan GetNanos() => PreciseTimeSpan.FromStart;

        protected static bool IsNullOrEmpty<T>(IPriorityQueue<T> taskQueue)
            where T : class, IPriorityQueueNode<T>
        {
            return taskQueue is null || 0u >= (uint)taskQueue.Count;
        }

        /// <summary>
        ///     Cancel all scheduled tasks
        ///     This method MUST be called only when <see cref="IEventExecutor.InEventLoop" /> is <c>true</c>.
        /// </summary>
        protected virtual void CancelScheduledTasks()
        {
            Debug.Assert(this.InEventLoop);
            IPriorityQueue<IScheduledRunnable> scheduledTaskQueue = this.ScheduledTaskQueue;
            if (IsNullOrEmpty(scheduledTaskQueue))
            {
                return;
            }

            IScheduledRunnable[] tasks = scheduledTaskQueue.ToArray();
            foreach (IScheduledRunnable t in tasks)
            {
                t.Cancel();
            }

            this.ScheduledTaskQueue.Clear();
        }

        internal protected IScheduledRunnable PollScheduledTask() => this.PollScheduledTask(GetNanos());

        protected IScheduledRunnable PollScheduledTask(in PreciseTimeSpan nanoTime)
        {
            Debug.Assert(this.InEventLoop);

            if (!this.ScheduledTaskQueue.TryPeek(out IScheduledRunnable scheduledTask))
            {
                return null;
            }

            if (scheduledTask.Deadline <= nanoTime)
            {
                this.ScheduledTaskQueue.TryDequeue(out var _);
                return scheduledTask;
            }
            return null;
        }

        protected PreciseTimeSpan NextScheduledTaskNanos()
        {
            IScheduledRunnable nextScheduledRunnable = this.PeekScheduledTask();
            return nextScheduledRunnable?.Deadline ?? PreciseTimeSpan.MinusOne;
        }

        protected IScheduledRunnable PeekScheduledTask()
        {
            IPriorityQueue<IScheduledRunnable> scheduledTaskQueue = this.ScheduledTaskQueue;
            return !IsNullOrEmpty(scheduledTaskQueue) && scheduledTaskQueue.TryPeek(out IScheduledRunnable task) ? task : null;
        }

        protected bool HasScheduledTasks()
        {
            return this.ScheduledTaskQueue.TryPeek(out IScheduledRunnable scheduledTask) && scheduledTask.Deadline <= PreciseTimeSpan.FromStart;
        }

        public override IScheduledTask Schedule(IRunnable action, TimeSpan delay)
        {
            if (action is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.action); }

            return this.Schedule(new RunnableScheduledTask(this, action, PreciseTimeSpan.Deadline(delay)));
        }

        public override IScheduledTask Schedule(Action action, TimeSpan delay)
        {
            if (action is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.action); }

            return this.Schedule(new ActionScheduledTask(this, action, PreciseTimeSpan.Deadline(delay)));
        }

        public override IScheduledTask Schedule(Action<object> action, object state, TimeSpan delay)
        {
            if (action is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.action); }

            return this.Schedule(new StateActionScheduledTask(this, action, state, PreciseTimeSpan.Deadline(delay)));
        }

        public override IScheduledTask Schedule(Action<object, object> action, object context, object state, TimeSpan delay)
        {
            if (action is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.action); }

            return this.Schedule(new StateActionWithContextScheduledTask(this, action, context, state, PreciseTimeSpan.Deadline(delay)));
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
                return this.Schedule(action, delay).Completion;
            }

            return this.Schedule(new ActionScheduledAsyncTask(this, action, PreciseTimeSpan.Deadline(delay), cancellationToken)).Completion;
        }

        public override Task ScheduleAsync(Action<object> action, object state, TimeSpan delay, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return TaskUtil.Cancelled;
            }

            if (!cancellationToken.CanBeCanceled)
            {
                return this.Schedule(action, state, delay).Completion;
            }

            return this.Schedule(new StateActionScheduledAsyncTask(this, action, state, PreciseTimeSpan.Deadline(delay), cancellationToken)).Completion;
        }

        public override Task ScheduleAsync(Action<object, object> action, object context, object state, TimeSpan delay, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return TaskUtil.Cancelled;
            }

            if (!cancellationToken.CanBeCanceled)
            {
                return this.Schedule(action, context, state, delay).Completion;
            }

            return this.Schedule(new StateActionWithContextScheduledAsyncTask(this, action, context, state, PreciseTimeSpan.Deadline(delay), cancellationToken)).Completion;
        }

        protected virtual IScheduledRunnable Schedule(IScheduledRunnable task)
        {
            if (this.InEventLoop)
            {
                this.ScheduledTaskQueue.TryEnqueue(task);
            }
            else
            {
                this.Execute(EnqueueRunnableAction, this, task);
            }
            return task;
        }

        internal void RemoveScheduled(IScheduledRunnable task)
        {
            if (this.InEventLoop)
            {
                this.ScheduledTaskQueue.TryRemove(task);
            }
            else
            {
                this.Execute(RemoveRunnableAction, this, task);
            }
        }
    }
}