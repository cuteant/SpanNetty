
namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// A promise combiner monitors the outcome of a number of discrete futures, then notifies a final, aggregate promise
    /// when all of the combined futures are finished. The aggregate promise will succeed if and only if all of the combined
    /// futures succeed. If any of the combined futures fail, the aggregate promise will fail. The cause failure for the
    /// aggregate promise will be the failure for one of the failed combined futures; if more than one of the combined
    /// futures fails, exactly which cause of failure will be assigned to the aggregate promise is undefined.
    /// 
    /// <para>Callers may populate a promise combiner with any number of futures to be combined via the
    /// {@link PromiseCombiner#add(Future)} and {@link PromiseCombiner#addAll(Future[])} methods. When all futures to be
    /// combined have been added, callers must provide an aggregate promise to be notified when all combined promises have
    /// finished via the {@link PromiseCombiner#finish(Promise)} method.</para>
    /// </summary>
    public sealed class PromiseCombiner
    {
        private int expectedCount;
        private int doneCount;
        private int doneAdding;
        private IPromise aggregatePromise;
        private ReadOnlyCollection<Exception> causes;

        public void Add(IPromise promise)
        {
            this.Add(promise.Task);
        }

        public void Add(Task future)
        {
            if (SharedConstants.False < (uint)Volatile.Read(ref this.doneAdding))
            {
                ThrowHelper.ThrowInvalidOperationException_AddingPromisesIsNotAllowedAfterFinishedAdding();
            }

            Interlocked.Increment(ref this.expectedCount);

            if (future.IsCompleted)
            {
                OperationComplete(future, this);
            }
            else
            {
                future.ContinueWith(OperationCompleteAction, this, TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        public void AddAll(params IPromise[] promises)
        {
            if (promises is null || 0u >= (uint)promises.Length) { return; }

            for (int i = 0; i < promises.Length; i++)
            {
                this.Add(promises[i].Task);
            }
        }

        public void AddAll(params Task[] futures)
        {
            if (futures is null || 0u >= (uint)futures.Length) { return; }

            for (int i = 0; i < futures.Length; i++)
            {
                this.Add(futures[i]);
            }
        }

        public void Finish(IPromise aggregatePromise)
        {
            if (aggregatePromise is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.aggregatePromise);
            }
            if (SharedConstants.False < (uint)Volatile.Read(ref this.doneAdding))
            {
                ThrowHelper.ThrowInvalidOperationException_AlreadyFinished();
            }
            Interlocked.Exchange(ref this.doneAdding, SharedConstants.True);
            this.aggregatePromise = aggregatePromise;
            if (Volatile.Read(ref this.doneCount) == Volatile.Read(ref this.expectedCount))
            {
                this.TryPromise();
            }
        }

        bool TryPromise()
        {
            var excs = Volatile.Read(ref this.causes);
            return (excs is null) ? this.aggregatePromise.TryComplete() : this.aggregatePromise.TrySetException(excs);
        }

        static readonly Action<Task, object> OperationCompleteAction = OperationComplete;
        static void OperationComplete(Task future, object state)
        {
            var self = (PromiseCombiner)state;
            Interlocked.Increment(ref self.doneCount);
            if (!future.IsSuccess() && Volatile.Read(ref self.causes) is null)
            {
                Interlocked.Exchange(ref self.causes, future.Exception.InnerExceptions);
            }
            if (Volatile.Read(ref self.doneCount) == Volatile.Read(ref self.expectedCount) && SharedConstants.False < (uint)Volatile.Read(ref self.doneAdding))
            {
                self.TryPromise();
            }
        }
    }
}
