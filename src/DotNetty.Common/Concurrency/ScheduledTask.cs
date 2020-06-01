// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;

    abstract class ScheduledTask : IScheduledRunnable
    {
        private const int CancellationProhibited = 1;
        private const int CancellationRequested = 1 << 1;

        protected readonly IPromise Promise;
        protected readonly AbstractScheduledEventExecutor Executor;
        private int v_cancellationState;
        private int _queueIndex = PriorityQueue<IScheduledRunnable>.IndexNotInQueue;

        protected ScheduledTask(AbstractScheduledEventExecutor executor, in PreciseTimeSpan deadline, IPromise promise)
        {
            Executor = executor;
            Promise = promise;
            Deadline = deadline;
        }

        public PreciseTimeSpan Deadline { get; }

        public bool Cancel()
        {
            if (!AtomicCancellationStateUpdate(CancellationRequested, CancellationProhibited))
            {
                return false;
            }

            bool canceled = Promise.TrySetCanceled();
            if (canceled)
            {
                Executor.RemoveScheduled(this);
            }
            return canceled;
        }

        public Task Completion => Promise.Task;

        public TaskAwaiter GetAwaiter() => Completion.GetAwaiter();

        int IComparable<IScheduledRunnable>.CompareTo(IScheduledRunnable other)
        {
            if (other is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.other); }

            return Deadline.CompareTo(other.Deadline);
        }

        public virtual void Run()
        {
            if (TrySetUncancelable())
            {
                try
                {
                    Execute();
                    Promise.TryComplete();
                }
                catch (Exception ex)
                {
                    // todo: check for fatal
                    Promise.TrySetException(ex);
                }
            }
        }

        protected abstract void Execute();

        bool TrySetUncancelable() => AtomicCancellationStateUpdate(CancellationProhibited, CancellationRequested);

        bool AtomicCancellationStateUpdate(int newBits, int illegalBits)
        {
            int cancellationState = Volatile.Read(ref v_cancellationState);
            int oldCancellationState;
            do
            {
                oldCancellationState = cancellationState;
                if ((cancellationState & illegalBits) != 0)
                {
                    return false;
                }
                cancellationState = Interlocked.CompareExchange(ref v_cancellationState, cancellationState | newBits, cancellationState);
            }
            while (cancellationState != oldCancellationState);

            return true;
        }

        public int GetPriorityQueueIndex(IPriorityQueue<IScheduledRunnable> queue) => _queueIndex;

        public void SetPriorityQueueIndex(IPriorityQueue<IScheduledRunnable> queue, int i) => _queueIndex = i;
    }
}