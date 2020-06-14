// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System.Threading;

    abstract class ScheduledAsyncTask : ScheduledTask
    {
        private readonly CancellationToken _cancellationToken;
        private CancellationTokenRegistration _cancellationTokenRegistration;

        protected ScheduledAsyncTask(AbstractScheduledEventExecutor executor, in PreciseTimeSpan deadline, IPromise promise, CancellationToken cancellationToken)
            : base(executor, deadline, promise)
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