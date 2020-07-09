// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System.Threading;

    sealed class RunnableScheduledAsyncTask : ScheduledAsyncTask
    {
        readonly IRunnable _action;

        public RunnableScheduledAsyncTask(AbstractScheduledEventExecutor executor, IRunnable action, long deadlineNanos, CancellationToken cancellationToken)
            : base(executor, deadlineNanos, executor.NewPromise(), cancellationToken)
        {
            _action = action;
        }

        public RunnableScheduledAsyncTask(AbstractScheduledEventExecutor executor, IRunnable action, long deadlineNanos, long periodNanos, CancellationToken cancellationToken)
            : base(executor, deadlineNanos, periodNanos, executor.NewPromise(), cancellationToken)
        {
            _action = action;
        }

        protected override void Execute() => _action.Run();
    }
}