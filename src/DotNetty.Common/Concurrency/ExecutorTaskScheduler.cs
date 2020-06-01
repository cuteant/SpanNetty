// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public sealed class ExecutorTaskScheduler : TaskScheduler
    {
        private readonly IEventExecutor _executor;
        private bool _started;

        public ExecutorTaskScheduler(IEventExecutor executor)
        {
            _executor = executor;
        }

        protected override void QueueTask(Task task)
        {
            if (_started)
            {
                _executor.Execute(new TaskQueueNode(this, task));
            }
            else
            {
                // hack: enables this executor to be seen as default on Executor's worker thread.
                // This is a special case for SingleThreadEventExecutor.Loop initiated task.
                _started = true;
                TryExecuteTask(task);
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (taskWasPreviouslyQueued || !_executor.InEventLoop)
            {
                return false;
            }

            return TryExecuteTask(task);
        }

        protected override IEnumerable<Task> GetScheduledTasks() => null;

        protected override bool TryDequeue(Task task) => false;

        sealed class TaskQueueNode : IRunnable
        {
            readonly ExecutorTaskScheduler _scheduler;
            readonly Task _task;

            public TaskQueueNode(ExecutorTaskScheduler scheduler, Task task)
            {
                _scheduler = scheduler;
                _task = task;
            }

            public void Run() => _scheduler.TryExecuteTask(_task);
        }
    }
}