// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.ComponentModel;
    using DotNetty.Common.Utilities;

    public interface IScheduledRunnable : IRunnable, IScheduledTask, IComparable<IScheduledRunnable>, IPriorityQueueNode<IScheduledRunnable>
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        IScheduledRunnable SetId(long id);

        [EditorBrowsable(EditorBrowsableState.Never)]
        void SetConsumed();
    }
}