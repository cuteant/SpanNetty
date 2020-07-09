// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    sealed class RunnableScheduledTask : ScheduledTask
    {
        readonly IRunnable _action;

        public RunnableScheduledTask(AbstractScheduledEventExecutor executor, IRunnable action, long deadlineNanos)
            : base(executor, deadlineNanos, executor.NewPromise())
        {
            _action = action;
        }

        public RunnableScheduledTask(AbstractScheduledEventExecutor executor, IRunnable action, long deadlineNanos, long periodNanos)
            : base(executor, deadlineNanos, periodNanos, executor.NewPromise())
        {
            _action = action;
        }

        protected override void Execute() => _action.Run();
    }
}