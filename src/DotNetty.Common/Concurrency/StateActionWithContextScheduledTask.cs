// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;

    sealed class StateActionWithContextScheduledTask : ScheduledTask
    {
        readonly Action<object, object> _action;
        readonly object _context;

        public StateActionWithContextScheduledTask(AbstractScheduledEventExecutor executor, Action<object, object> action, object context, object state,
            in PreciseTimeSpan deadline)
            : base(executor, deadline, executor.NewPromise(state))
        {
            _action = action;
            _context = context;
        }

        protected override void Execute() => _action(_context, Completion.AsyncState);
    }
}