// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetty.Common.Concurrency
{
    public class TaskCompletionSource : IPromise
    {
        private readonly TaskCompletionSource<int> tcs;
        private int uncancellable = SharedConstants.False;

        public TaskCompletionSource()
        {
            this.tcs = new TaskCompletionSource<int>();
        }

        public TaskCompletionSource(object state)
        {
            this.tcs = new TaskCompletionSource<int>(state);
        }

        public Task Task => this.tcs.Task;

        public bool IsVoid => false;

        public bool IsSuccess
        {
            get
            {
                var task = this.Task;
                return task.IsCompleted && !task.IsFaulted && !task.IsCanceled;
            }
        }

        public bool IsCompleted => this.Task.IsCompleted;

        public bool IsFaulted => this.Task.IsFaulted;

        public bool IsCanceled => this.Task.IsCanceled;

        public virtual bool TryComplete()
        {
            return this.tcs.TrySetResult(0);
        }

        public virtual void Complete()
        {
            this.tcs.SetResult(0);
        }
        public virtual void SetCanceled()
        {
            if (SharedConstants.True == Volatile.Read(ref this.uncancellable)) { return; }
            this.tcs.SetCanceled();
        }

        public virtual void SetException(Exception exception)
        {
            if (exception is AggregateException aggregateException)
            {
                this.SetException(aggregateException.InnerExceptions);
                return;
            }
            this.tcs.SetException(exception);
        }

        public virtual void SetException(IEnumerable<Exception> exceptions)
        {
            this.tcs.SetException(exceptions);
        }

        public virtual bool TrySetCanceled()
        {
            if (SharedConstants.True == Volatile.Read(ref this.uncancellable)) { return false; }
            return this.tcs.TrySetCanceled();
        }

        public virtual bool TrySetException(Exception exception)
        {
            if (exception is AggregateException aggregateException)
            {
                return this.TrySetException(aggregateException.InnerExceptions);
            }
            return this.tcs.TrySetException(exception);
        }

        public virtual bool TrySetException(IEnumerable<Exception> exceptions)
        {
            return this.tcs.TrySetException(exceptions);
        }

        public bool SetUncancellable()
        {
            if (SharedConstants.False == Interlocked.CompareExchange(ref this.uncancellable, SharedConstants.True, SharedConstants.False))
            {
                return true;
            }
            return !this.IsCompleted;
        }

        public override string ToString() => "TaskCompletionSource[status: " + this.Task.Status.ToString() + "]";

        public IPromise Unvoid() => this;
    }
}