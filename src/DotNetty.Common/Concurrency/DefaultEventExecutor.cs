namespace DotNetty.Common.Concurrency
{
    using DotNetty.Common.Internal;

    /// <summary>
    /// Default <see cref="SingleThreadEventExecutor"/> implementation which just execute all submitted task in a serial fashion.
    /// </summary>
    public sealed class DefaultEventExecutor : SingleThreadEventExecutor
    {
        public DefaultEventExecutor()
            : this(DefaultMaxPendingExecutorTasks)
        {
        }

        public DefaultEventExecutor(int maxPendingTasks)
            : this(RejectedExecutionHandlers.Reject(), maxPendingTasks)
        {
        }

        public DefaultEventExecutor(IRejectedExecutionHandler rejectedHandler)
            : this(rejectedHandler, DefaultMaxPendingExecutorTasks)
        {
        }

        public DefaultEventExecutor(IEventExecutorTaskQueueFactory queueFactory)
            : this(RejectedExecutionHandlers.Reject(), queueFactory)
        {
        }

        public DefaultEventExecutor(IRejectedExecutionHandler rejectedHandler, int maxPendingTasks)
            : this(null, DefaultThreadFactory<DefaultEventExecutor>.Instance, rejectedHandler, maxPendingTasks)
        {
        }

        public DefaultEventExecutor(IRejectedExecutionHandler rejectedHandler, IEventExecutorTaskQueueFactory queueFactory)
            : this(null, DefaultThreadFactory<DefaultEventExecutor>.Instance, rejectedHandler, queueFactory)
        {
        }


        public DefaultEventExecutor(IThreadFactory threadFactory)
            : this(threadFactory, DefaultMaxPendingExecutorTasks)
        {
        }

        public DefaultEventExecutor(IThreadFactory threadFactory, int maxPendingTasks)
            : this(threadFactory, RejectedExecutionHandlers.Reject(), maxPendingTasks)
        {
        }

        public DefaultEventExecutor(IThreadFactory threadFactory, IEventExecutorTaskQueueFactory queueFactory)
            : this(threadFactory, RejectedExecutionHandlers.Reject(), queueFactory)
        {
        }

        public DefaultEventExecutor(IThreadFactory threadFactory, IRejectedExecutionHandler rejectedHandler)
            : this(threadFactory, rejectedHandler, DefaultMaxPendingExecutorTasks)
        {
        }

        public DefaultEventExecutor(IThreadFactory threadFactory, IRejectedExecutionHandler rejectedHandler, int maxPendingTasks)
            : this(null, threadFactory, rejectedHandler, maxPendingTasks)
        {
        }

        public DefaultEventExecutor(IThreadFactory threadFactory, IRejectedExecutionHandler rejectedHandler, IEventExecutorTaskQueueFactory queueFactory)
            : this(null, threadFactory, rejectedHandler, queueFactory)
        {
        }


        public DefaultEventExecutor(IEventExecutorGroup parent)
            : this(parent, DefaultMaxPendingExecutorTasks)
        {
        }

        public DefaultEventExecutor(IEventExecutorGroup parent, int maxPendingTasks)
            : this(parent, RejectedExecutionHandlers.Reject(), maxPendingTasks)
        {
        }

        public DefaultEventExecutor(IEventExecutorGroup parent, IEventExecutorTaskQueueFactory queueFactory)
            : this(parent, RejectedExecutionHandlers.Reject(), queueFactory)
        {
        }

        public DefaultEventExecutor(IEventExecutorGroup parent, IRejectedExecutionHandler rejectedHandler)
            : this(parent, rejectedHandler, queueFactory: null)
        {
        }

        public DefaultEventExecutor(IEventExecutorGroup parent, IRejectedExecutionHandler rejectedHandler, int maxPendingTasks)
            : this(parent, DefaultThreadFactory<DefaultEventExecutor>.Instance, rejectedHandler, maxPendingTasks)
        {
        }

        public DefaultEventExecutor(IEventExecutorGroup parent, IRejectedExecutionHandler rejectedHandler, IEventExecutorTaskQueueFactory queueFactory)
            : this(parent, DefaultThreadFactory<DefaultEventExecutor>.Instance, rejectedHandler, queueFactory)
        {
        }

        public DefaultEventExecutor(IEventExecutorGroup parent, IThreadFactory threadFactory, int maxPendingTasks)
            : this(parent, threadFactory, RejectedExecutionHandlers.Reject(), maxPendingTasks)
        {
        }

        public DefaultEventExecutor(IEventExecutorGroup parent, IThreadFactory threadFactory, IEventExecutorTaskQueueFactory queueFactory)
            : this(parent, threadFactory, RejectedExecutionHandlers.Reject(), queueFactory)
        {
        }

        public DefaultEventExecutor(IEventExecutorGroup parent, IThreadFactory threadFactory, IRejectedExecutionHandler rejectedHandler, int maxPendingTasks)
            : base(parent, threadFactory, true, NewBlockingTaskQueue(maxPendingTasks), rejectedHandler)
        {
            Start();
        }

        public DefaultEventExecutor(IEventExecutorGroup parent, IThreadFactory threadFactory, IRejectedExecutionHandler rejectedHandler, IEventExecutorTaskQueueFactory queueFactory)
            : base(parent, threadFactory, true, NewBlockingTaskQueue(queueFactory), rejectedHandler)
        {
            Start();
        }

        private static IQueue<IRunnable> NewBlockingTaskQueue(IEventExecutorTaskQueueFactory queueFactory)
        {
            if (queueFactory is null)
            {
                return NewBlockingTaskQueue(DefaultMaxPendingExecutorTasks);
            }
            return queueFactory.NewTaskQueue(DefaultMaxPendingExecutorTasks);
        }

        protected override void Run()
        {
            do
            {
                IRunnable task = TakeTask();
                if (task is object)
                {
                    task.Run();
                    UpdateLastExecutionTime();
                }
            } while (!ConfirmShutdown());
        }
    }
}