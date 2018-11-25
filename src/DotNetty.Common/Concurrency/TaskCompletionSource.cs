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
        private int uncancellable = Constants.False;

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
#if NET40
            var result = false;
            FakeSynchronizationContext.Execute(() => result = this.tcs.TrySetResult(0));
            return result;
#else
            return this.tcs.TrySetResult(0);
#endif
        }

        public virtual void Complete()
        {
#if NET40
            FakeSynchronizationContext.Execute(() => this.tcs.SetResult(0));
#else
            this.tcs.SetResult(0);
#endif
        }
        public virtual void SetCanceled()
        {
            if (Constants.True == Volatile.Read(ref this.uncancellable)) { return; }
//#if NET40
//            FakeSynchronizationContext.Execute(() => this.tcs.SetCanceled());
//#else
            this.tcs.SetCanceled();
//#endif
        }

        public virtual void SetException(Exception exception)
        {
            if (exception is AggregateException aggregateException)
            {
                this.SetException(aggregateException.InnerExceptions);
                return;
            }
//#if NET40
//            FakeSynchronizationContext.Execute(() => this.tcs.SetException(exception));
//#else
            this.tcs.SetException(exception);
//#endif
        }

        public virtual void SetException(IEnumerable<Exception> exceptions)
        {
//#if NET40
//            FakeSynchronizationContext.Execute(() => this.tcs.SetException(exceptions));
//#else
            this.tcs.SetException(exceptions);
//#endif
        }

        public virtual bool TrySetCanceled()
        {
            if (Constants.True == Volatile.Read(ref this.uncancellable)) { return false; }
//#if NET40
//            var result = false;
//            FakeSynchronizationContext.Execute(() => result = this.tcs.TrySetCanceled());
//            return result;
//#else
            return this.tcs.TrySetCanceled();
//#endif
        }

        public virtual bool TrySetException(Exception exception)
        {
            if (exception is AggregateException aggregateException)
            {
                return this.TrySetException(aggregateException.InnerExceptions);
            }
//#if NET40
//            var result = false;
//            FakeSynchronizationContext.Execute(() => result = this.tcs.TrySetException(exception));
//            return result;
//#else
            return this.tcs.TrySetException(exception);
//#endif
        }

        public virtual bool TrySetException(IEnumerable<Exception> exceptions)
        {
//#if NET40
//            var result = false;
//            FakeSynchronizationContext.Execute(() => result = this.tcs.TrySetException(exceptions));
//            return result;
//#else
            return this.tcs.TrySetException(exceptions);
//#endif
        }

        public bool SetUncancellable()
        {
            if (Constants.False == Interlocked.CompareExchange(ref this.uncancellable, Constants.True, Constants.False))
            {
                return true;
            }
            return !this.IsCompleted;
        }

        public override string ToString() => "TaskCompletionSource[status: " + this.Task.Status.ToString() + "]";

        public IPromise Unvoid() => this;
    }

#if NET40
    /// <summary>FakeSynchronizationContext: http://stackoverflow.com/questions/21845495/synchronous-or-asynchronous-continuation-upon-taskcompletionsource-trysetresult </summary>
    public sealed class FakeSynchronizationContext : SynchronizationContext
    {
        private static readonly ThreadLocal<FakeSynchronizationContext> s_context =
            new ThreadLocal<FakeSynchronizationContext>(() => new FakeSynchronizationContext());

        private FakeSynchronizationContext() { }

        public static FakeSynchronizationContext Instance { get { return s_context.Value; } }

        public static void Execute(Action action)
        {
            var savedContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(FakeSynchronizationContext.Instance);
            try
            {
                action();
            }
            catch { }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(savedContext);
            }
        }

        // SynchronizationContext methods

        public override SynchronizationContext CreateCopy()
        {
            return this;
        }

        public override void OperationStarted()
        {
            throw new NotImplementedException("OperationStarted");
        }

        public override void OperationCompleted()
        {
            throw new NotImplementedException("OperationCompleted");
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            throw new NotImplementedException("Post");
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            throw new NotImplementedException("Send");
        }
    }
#endif
}