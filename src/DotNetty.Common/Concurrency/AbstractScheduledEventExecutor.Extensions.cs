// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;

    partial class AbstractScheduledEventExecutor
    {
        protected static readonly Action<object, object> EnqueueRunnableAction = OnEnqueueRunnable;
        static readonly Action<object, object> RemoveRunnableAction = OnRemoveRunnable;

        static void OnEnqueueRunnable(object e, object t)
        {
            ((AbstractScheduledEventExecutor)e).ScheduledTaskQueue.TryEnqueue((IScheduledRunnable)t);
        }

        static void OnRemoveRunnable(object e, object t)
        {
            ((AbstractScheduledEventExecutor)e).ScheduledTaskQueue.TryRemove((IScheduledRunnable)t);
        }
    }
}