// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Threading;

    sealed class StateActionWithContextScheduledAsyncTask : ScheduledAsyncTask
    {
        readonly Action<object, object> _action;
        readonly object _context;

        public StateActionWithContextScheduledAsyncTask(AbstractScheduledEventExecutor executor, Action<object, object> action, object context, object state,
            long deadlineNanos, CancellationToken cancellationToken)
            : base(executor, deadlineNanos, executor.NewPromise(state), cancellationToken)
        {
            _action = action;
            _context = context;
        }

        public StateActionWithContextScheduledAsyncTask(AbstractScheduledEventExecutor executor, Action<object, object> action, object context, object state,
            long deadlineNanos, long periodNanos, CancellationToken cancellationToken)
            : base(executor, deadlineNanos, periodNanos, executor.NewPromise(state), cancellationToken)
        {
            _action = action;
            _context = context;
        }

        protected override void Execute() => _action(_context, Completion.AsyncState);
    }
}