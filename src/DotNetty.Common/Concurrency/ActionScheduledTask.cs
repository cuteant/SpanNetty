// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;

    sealed class ActionScheduledTask : ScheduledTask
    {
        readonly Action _action;

        public ActionScheduledTask(AbstractScheduledEventExecutor executor, Action action, in PreciseTimeSpan deadline)
            : base(executor, deadline, executor.NewPromise())
        {
            _action = action;
        }

        protected override void Execute() => _action();
    }
}