
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
            if (Constants.True == Volatile.Read(ref this.doneAdding))
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
#if NET40
                future.ContinueWith(t => OperationComplete(t, this), TaskContinuationOptions.ExecuteSynchronously);
#else
                future.ContinueWith(OperationCompleteAction, this, TaskContinuationOptions.ExecuteSynchronously);
#endif
            }
        }

        public void AddAll(params IPromise[] promises)
        {
            foreach (var promise in promises)
            {
                this.Add(promise.Task);
            }
        }

        public void AddAll(params Task[] futures)
        {
            foreach (var future in futures)
            {
                this.Add(future);
            }
        }

        public void Finish(IPromise aggregatePromise)
        {
            if (null == aggregatePromise)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.aggregatePromise);
            }
            if (Constants.True == Volatile.Read(ref this.doneAdding))
            {
                ThrowHelper.ThrowInvalidOperationException_AlreadyFinished();
            }
            Interlocked.Exchange(ref this.doneAdding, Constants.True);
            this.aggregatePromise = aggregatePromise;
            if (Volatile.Read(ref this.doneCount) == Volatile.Read(ref this.expectedCount))
            {
                this.TryPromise();
            }
        }

        bool TryPromise()
        {
            var excs = Volatile.Read(ref this.causes);
            return (excs == null) ? this.aggregatePromise.TryComplete() : this.aggregatePromise.TrySetException(excs);
        }

        static readonly Action<Task, object> OperationCompleteAction = OperationComplete;
        static void OperationComplete(Task future, object state)
        {
            var self = (PromiseCombiner)state;
            Interlocked.Increment(ref self.doneCount);
            if (!future.IsSuccess() && Volatile.Read(ref self.causes) == null)
            {
                Interlocked.Exchange(ref self.causes, future.Exception.InnerExceptions);
            }
            if (Volatile.Read(ref self.doneCount) == Volatile.Read(ref self.expectedCount) && Constants.True == Volatile.Read(ref self.doneAdding))
            {
                self.TryPromise();
            }
        }
    }
}
