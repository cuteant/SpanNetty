// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Internal.Logging;
    using Thread = XThread;

    /// <summary>
    ///     Abstract base class for <see cref="IEventExecutor" /> implementations
    /// </summary>
    public abstract class AbstractEventExecutor : AbstractExecutorService, IEventExecutor
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<AbstractEventExecutor>();

        static readonly TimeSpan DefaultShutdownQuietPeriod = TimeSpan.FromSeconds(2);
        static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(15);

        /// <summary>Creates an instance of <see cref="AbstractEventExecutor"/>.</summary>
        protected AbstractEventExecutor()
            : this(null)
        {
        }

        /// <summary>Creates an instance of <see cref="AbstractEventExecutor"/>.</summary>
        protected AbstractEventExecutor(IEventExecutorGroup parent)
        {
            Parent = parent;
        }

        /// <inheritdoc />
        public abstract bool IsShuttingDown { get; }

        /// <inheritdoc cref="IEventExecutorGroup.TerminationCompletion"/>
        public abstract Task TerminationCompletion { get; }

        /// <inheritdoc cref="IEventExecutorGroup.GetNext()"/>
        public IEventExecutor GetNext() => this;

        /// <inheritdoc />
        public IEventExecutorGroup Parent { get; }

        /// <inheritdoc />
        public bool InEventLoop => IsInEventLoop(Thread.CurrentThread);

        /// <inheritdoc />
        public IEnumerable<IEventExecutor> Items => GetItems();

        protected abstract IEnumerable<IEventExecutor> GetItems();

        /// <inheritdoc />
        public abstract bool IsInEventLoop(Thread thread);

        /// <inheritdoc />
        public virtual IScheduledTask Schedule(IRunnable action, TimeSpan delay)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public virtual IScheduledTask Schedule(Action action, TimeSpan delay)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public virtual IScheduledTask Schedule(Action<object> action, object state, TimeSpan delay)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public virtual IScheduledTask Schedule(Action<object, object> action, object context, object state, TimeSpan delay)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public virtual Task ScheduleAsync(Action action, TimeSpan delay) =>
            ScheduleAsync(action, delay, CancellationToken.None);

        /// <inheritdoc />
        public virtual Task ScheduleAsync(Action<object> action, object state, TimeSpan delay, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public virtual Task ScheduleAsync(Action<object> action, object state, TimeSpan delay) =>
            ScheduleAsync(action, state, delay, CancellationToken.None);

        /// <inheritdoc />
        public virtual Task ScheduleAsync(Action action, TimeSpan delay, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public virtual Task ScheduleAsync(Action<object, object> action, object context, object state, TimeSpan delay) =>
            ScheduleAsync(action, context, state, delay, CancellationToken.None);

        /// <inheritdoc />
        public virtual Task ScheduleAsync(
            Action<object, object> action,
            object context,
            object state,
            TimeSpan delay,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public Task ShutdownGracefullyAsync() => ShutdownGracefullyAsync(DefaultShutdownQuietPeriod, DefaultShutdownTimeout);

        /// <inheritdoc />
        public abstract Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout);

        public IPromise NewPromise() => new TaskCompletionSource();

        public IPromise NewPromise(object state) => new TaskCompletionSource(state);

        /// <inheritdoc />
        protected void SetCurrentExecutor(IEventExecutor executor) => ExecutionEnvironment.SetCurrentExecutor(executor);

        /// <summary>
        /// Try to execute the given <see cref="IRunnable"/> and just log if it throws a <see cref="Exception"/>.
        /// </summary>
        /// <param name="task"></param>
        protected static void SafeExecute(IRunnable task)
        {
            try
            {
                task.Run();
            }
            catch (Exception ex)
            {
                Logger.ATaskRaisedAnException(task, ex);
            }
        }

        /// <summary>
        /// Like <see cref="AbstractExecutorService.Execute(IRunnable)"/> but does not guarantee the task will be run until either
        /// a non-lazy task is executed or the executor is shut down.
        /// 
        /// <para>This is equivalent to submitting a <see cref="ILazyRunnable"/> to
        /// <see cref="AbstractExecutorService.Execute(IRunnable)"/> but for an arbitrary <see cref="IRunnable"/>.</para>
        /// </summary>
        /// <remarks>The default implementation just delegates to <see cref="AbstractExecutorService.Execute(IRunnable)"/>.</remarks>
        /// <param name="task"></param>
        public virtual void LazyExecute(IRunnable task)
        {
            Execute(task);
        }
    }
}