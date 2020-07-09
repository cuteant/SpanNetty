// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;

    sealed class StateActionScheduledTask : ScheduledTask
    {
        readonly Action<object> _action;

        public StateActionScheduledTask(AbstractScheduledEventExecutor executor, Action<object> action, object state, long deadlineNanos)
            : base(executor, deadlineNanos, executor.NewPromise(state))
        {
            _action = action;
        }

        public StateActionScheduledTask(AbstractScheduledEventExecutor executor, Action<object> action, object state, long deadlineNanos, long periodNanos)
            : base(executor, deadlineNanos, periodNanos, executor.NewPromise(state))
        {
            _action = action;
        }

        protected override void Execute() => _action(Completion.AsyncState);
    }
}