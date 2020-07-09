namespace DotNetty.Transport.Channels
{
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;

    /// <summary>
    /// Default <see cref="SingleThreadEventLoopBase"/> implementation which just execute all submitted task in a serial fashion.
    /// </summary>
    public class DefaultEventLoop : SingleThreadEventLoopBase
    {
        public DefaultEventLoop()
            : this(null)
        {
        }

        public DefaultEventLoop(IEventLoopGroup parent)
            : this(parent, DefaultMaxPendingTasks)
        {
        }

        public DefaultEventLoop(IEventLoopGroup parent, int maxPendingTasks)
            : this(parent, RejectedExecutionHandlers.Reject(), maxPendingTasks)
        {
        }

        public DefaultEventLoop(IEventLoopGroup parent, IEventLoopTaskQueueFactory queueFactory)
            : this(parent, RejectedExecutionHandlers.Reject(), queueFactory)
        {
        }

        public DefaultEventLoop(IEventLoopGroup parent, IRejectedExecutionHandler rejectedHandler)
            : this(parent, rejectedHandler, queueFactory: null)
        {
        }

        public DefaultEventLoop(IEventLoopGroup parent, IRejectedExecutionHandler rejectedHandler, int maxPendingTasks)
            : this(parent, DefaultThreadFactory<DefaultEventLoop>.Instance, rejectedHandler, maxPendingTasks)
        {
        }

        public DefaultEventLoop(IEventLoopGroup parent, IRejectedExecutionHandler rejectedHandler, IEventLoopTaskQueueFactory queueFactory)
            : this(parent, DefaultThreadFactory<DefaultEventLoop>.Instance, rejectedHandler, queueFactory)
        {
        }

        public DefaultEventLoop(IEventLoopGroup parent, IThreadFactory threadFactory, int maxPendingTasks)
            : this(parent, threadFactory, RejectedExecutionHandlers.Reject(), maxPendingTasks)
        {
        }

        public DefaultEventLoop(IEventLoopGroup parent, IThreadFactory threadFactory, IEventLoopTaskQueueFactory queueFactory)
            : this(parent, threadFactory, RejectedExecutionHandlers.Reject(), queueFactory)
        {
        }

#if DEBUG
        public DefaultEventLoop(IEventLoopGroup parent, IThreadFactory threadFactory, IRejectedExecutionHandler rejectedHandler, int maxPendingTasks)
            : base(parent, threadFactory, true, NewBlockingTaskQueue(maxPendingTasks), NewBlockingTaskQueue(maxPendingTasks), rejectedHandler)
        {
            Start();
        }

        public DefaultEventLoop(IEventLoopGroup parent, IThreadFactory threadFactory, IRejectedExecutionHandler rejectedHandler, IEventLoopTaskQueueFactory queueFactory)
            : base(parent, threadFactory, true, NewBlockingTaskQueue(queueFactory), NewBlockingTaskQueue(queueFactory), rejectedHandler)
        {
            Start();
        }
#else
        public DefaultEventLoop(IEventLoopGroup parent, IThreadFactory threadFactory, IRejectedExecutionHandler rejectedHandler, int maxPendingTasks)
            : base(parent, threadFactory, true, NewBlockingTaskQueue(maxPendingTasks), rejectedHandler)
        {
            Start();
        }

        public DefaultEventLoop(IEventLoopGroup parent, IThreadFactory threadFactory, IRejectedExecutionHandler rejectedHandler, IEventLoopTaskQueueFactory queueFactory)
            : base(parent, threadFactory, true, NewBlockingTaskQueue(queueFactory), rejectedHandler)
        {
            Start();
        }
#endif

        private static IQueue<IRunnable> NewBlockingTaskQueue(IEventLoopTaskQueueFactory queueFactory)
        {
            if (queueFactory is null)
            {
                return NewBlockingTaskQueue(DefaultMaxPendingTasks);
            }
            return queueFactory.NewTaskQueue(DefaultMaxPendingTasks);
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