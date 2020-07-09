// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System.Threading;

    abstract class ScheduledAsyncTask : ScheduledTask
    {
        private readonly CancellationToken _cancellationToken;
        private CancellationTokenRegistration _cancellationTokenRegistration;

        protected ScheduledAsyncTask(AbstractScheduledEventExecutor executor, long deadlineNanos, IPromise promise, CancellationToken cancellationToken)
            : base(executor, deadlineNanos, promise)
        {
            _cancellationToken = cancellationToken;
            _cancellationTokenRegistration = cancellationToken.Register(s => ((ScheduledAsyncTask)s).Cancel(), this);
        }

        protected ScheduledAsyncTask(AbstractScheduledEventExecutor executor, long deadlineNanos, long periodNanos, IPromise promise, CancellationToken cancellationToken)
            : base(executor, deadlineNanos, periodNanos, promise)
        {
            _cancellationToken = cancellationToken;
            _cancellationTokenRegistration = cancellationToken.Register(s => ((ScheduledAsyncTask)s).Cancel(), this);
        }

        public override void Run()
        {
            _cancellationTokenRegistration.Dispose();
            if (_cancellationToken.IsCancellationRequested)
            {
                _ = Promise.TrySetCanceled();
            }
            else
            {
                base.Run();
            }
        }
    }
}