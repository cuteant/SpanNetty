// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    sealed class RunnableScheduledTask : ScheduledTask
    {
        readonly IRunnable _action;

        public RunnableScheduledTask(AbstractScheduledEventExecutor executor, IRunnable action, in PreciseTimeSpan deadline)
            : base(executor, deadline, executor.NewPromise())
        {
            _action = action;
        }

        protected override void Execute() => _action.Run();
    }
}