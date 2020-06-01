// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Threading;

    sealed class StateActionScheduledAsyncTask : ScheduledAsyncTask
    {
        readonly Action<object> _action;

        public StateActionScheduledAsyncTask(AbstractScheduledEventExecutor executor, Action<object> action, object state, in PreciseTimeSpan deadline,
            CancellationToken cancellationToken)
            : base(executor, deadline, executor.NewPromise(state), cancellationToken)
        {
            _action = action;
        }

        protected override void Execute() => _action(Completion.AsyncState);
    }
}