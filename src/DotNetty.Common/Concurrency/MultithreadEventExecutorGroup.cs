namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Abstract base class for <see cref="IEventExecutorGroup"/> implementations that handles their tasks with multiple threads at
    /// the same time.
    /// </summary>
    public abstract class MultithreadEventExecutorGroup<TExecutorGroup, TEventExecutor> : AbstractEventExecutorGroup<TEventExecutor>
        where TExecutorGroup : MultithreadEventExecutorGroup<TExecutorGroup, TEventExecutor>
        where TEventExecutor : class, IEventExecutor
    {
        private readonly TEventExecutor[] _children;
        private readonly IEventExecutorChooser<TEventExecutor> _chooser;

        /// <inheritdoc />
        public override bool IsShutdown => _children.All(eventLoop => eventLoop.IsShutdown);

        /// <inheritdoc />
        public override bool IsTerminated => _children.All(eventLoop => eventLoop.IsTerminated);

        /// <inheritdoc />
        public override bool IsShuttingDown => _children.All(eventLoop => eventLoop.IsShuttingDown);

        /// <inheritdoc />
        public override Task TerminationCompletion { get; }

        /// <inheritdoc />
        public override IEnumerable<IEventExecutor> Items => _children;

        /// <inheritdoc />
        public override IReadOnlyList<TEventExecutor> GetItems() => _children;

        /// <inheritdoc />
        public override TEventExecutor GetNext() => _chooser.GetNext();

        /// <summary>Creates a new instance of <see cref="MultithreadEventExecutorGroup{T1, T2}"/>.</summary>
        protected MultithreadEventExecutorGroup(int eventExecutorCount, Func<TExecutorGroup, TEventExecutor> eventExecutorFactory)
            : this(eventExecutorCount, DefaultEventExecutorChooserFactory<TEventExecutor>.Instance, eventExecutorFactory)
        {
        }

        /// <summary>Creates a new instance of <see cref="MultithreadEventExecutorGroup{T1, T2}"/>.</summary>
        protected MultithreadEventExecutorGroup(int nThreads,
            IEventExecutorChooserFactory<TEventExecutor> chooserFactory,
            Func<TExecutorGroup, TEventExecutor> eventExecutorFactory)
        {
            if (nThreads <= 0) { ThrowHelper.ThrowArgumentException_Positive(nThreads, ExceptionArgument.nThreads); }
            if (chooserFactory is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.chooserFactory); }
            if (eventExecutorFactory is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.eventExecutorFactory); }

            _children = new TEventExecutor[nThreads];
            var terminationTasks = new Task[nThreads];
            for (int i = 0; i < nThreads; i++)
            {
                TEventExecutor eventLoop = null;
                bool success = false;
                try
                {
                    eventLoop = eventExecutorFactory((TExecutorGroup)this);
                    success = true;
                }
                catch (Exception ex)
                {
                    ThrowHelper.ThrowInvalidOperationException_FailedToCreateAChildEventLoop(ex);
                }
                finally
                {
                    if (!success)
                    {
                        Task.WhenAll(_children
                                .Take(i)
                                .Select(loop => loop.ShutdownGracefullyAsync()))
                            .Wait();
                    }
                }

                _children[i] = eventLoop;
                terminationTasks[i] = eventLoop.TerminationCompletion;
            }
            TerminationCompletion = Task.WhenAll(terminationTasks);

            _chooser = chooserFactory.NewChooser(_children);
        }

        /// <inheritdoc cref="IEventExecutorGroup.ShutdownGracefullyAsync(TimeSpan, TimeSpan)" />
        public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        {
            for (int i = 0; i < _children.Length; i++)
            {
                _ = _children[i].ShutdownGracefullyAsync(quietPeriod, timeout);
            }
            return TerminationCompletion;
        }

        public override bool WaitTermination(TimeSpan timeout)
        {
            PreciseTimeSpan deadline = PreciseTimeSpan.Deadline(timeout);

            for (int i = 0; i < _children.Length; i++)
            {
                var executor = _children[i];
                for (; ; )
                {
                    PreciseTimeSpan timeLeft = deadline - PreciseTimeSpan.FromStart;
                    if (timeLeft <= PreciseTimeSpan.Zero) { goto LoopEnd; }

                    if (executor.WaitTermination(timeLeft.ToTimeSpan())) { break; }
                }
            }
        LoopEnd:
            return IsTerminated;
        }
    }
}