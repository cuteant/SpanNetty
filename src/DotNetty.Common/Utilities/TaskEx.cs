// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;

    public static class TaskUtil
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance(typeof(TaskUtil));

        public static readonly Task<int> Zero = CuteAnt.AsyncEx.TaskConstants.Int32Zero;

        public static readonly Task Completed = CuteAnt.AsyncEx.TaskConstants.Completed;

        public static readonly Task<int> Cancelled = CreateCancelledTask();

        public static readonly Task<bool> True = CuteAnt.AsyncEx.TaskConstants.BooleanTrue;

        public static readonly Task<bool> False = CuteAnt.AsyncEx.TaskConstants.BooleanFalse;

        static Task<int> CreateCancelledTask()
        {
            var tcs = new TaskCompletionSource<int>();
            tcs.SetCanceled();
            return tcs.Task;
        }

        public static Task FromException(Exception exception)
        {
            //var tcs = new TaskCompletionSource();
            //tcs.TrySetException(exception);
            return CuteAnt.AsyncEx.AsyncUtils.FromException(exception); //tcs.Task;
        }

        public static Task<T> FromException<T>(Exception exception)
        {
            //var tcs = new TaskCompletionSource<T>();
            //tcs.TrySetException(exception);
            return CuteAnt.AsyncEx.AsyncUtils.FromException<T>(exception); //tcs.Task;
        }

        static readonly Action<Task, object> LinkOutcomeContinuationAction = LinkOutcomeContinuation;
        private static void LinkOutcomeContinuation(Task t, object tcs)
        {
            switch (t.Status)
            {
                case TaskStatus.RanToCompletion:
                    ((IPromise)tcs).TryComplete();
                    break;
                case TaskStatus.Canceled:
                    ((IPromise)tcs).TrySetCanceled();
                    break;
                case TaskStatus.Faulted:
                    ((IPromise)tcs).TrySetException(t.Exception.InnerExceptions);
                    break;
                default:
                    ThrowHelper.ThrowArgumentOutOfRangeException(); break;
            }
        }

        public static void LinkOutcome(this Task task, IPromise promise)
        {
            switch (task.Status)
            {
                case TaskStatus.RanToCompletion:
                    promise.TryComplete();
                    break;
                case TaskStatus.Canceled:
                    promise.TrySetCanceled();
                    break;
                case TaskStatus.Faulted:
                    promise.TrySetException(task.Exception.InnerExceptions);
                    break;
                default:
#if !NET40
                    task.ContinueWith(
                        LinkOutcomeContinuationAction,
                        promise,
                        TaskContinuationOptions.ExecuteSynchronously);
#else
                    Action<Task> continuationAction = completed => LinkOutcomeContinuation(completed, promise);
                    task.ContinueWith(
                        continuationAction,
                        TaskContinuationOptions.ExecuteSynchronously);
#endif
                    break;
            }
        }

        static readonly Action<Task, object> CascadeToContinuationAction = CascadeToContinuation;
        private static void CascadeToContinuation(Task t, object s)
        {
            var wrapped = (Tuple<IPromise, IInternalLogger>)s;
            switch (t.Status)
            {
                case TaskStatus.RanToCompletion:
                    wrapped.Item1.TryComplete(wrapped.Item2);
                    break;
                case TaskStatus.Canceled:
                    wrapped.Item1.TrySetCanceled(wrapped.Item2);
                    break;
                case TaskStatus.Faulted:
                    wrapped.Item1.TrySetException(t.Exception, wrapped.Item2);
                    break;
                default:
                    ThrowHelper.ThrowArgumentOutOfRangeException(); break;
            }
        }

        public static void CascadeTo(this Task task, IPromise promise, IInternalLogger logger = null)
        {
            logger = logger ?? Logger;
            var internalLogger = !promise.IsVoid ? logger : null;

            switch (task.Status)
            {
                case TaskStatus.RanToCompletion:
                    promise.TryComplete(internalLogger);
                    break;
                case TaskStatus.Canceled:
                    promise.TrySetCanceled(internalLogger);
                    break;
                case TaskStatus.Faulted:
                    promise.TrySetException(task.Exception, internalLogger);
                    break;
                default:
#if !NET40
                    task.ContinueWith(
                        CascadeToContinuationAction,
                        Tuple.Create(promise, internalLogger),
                        TaskContinuationOptions.ExecuteSynchronously);
#else
                    Action<Task> continuationAction = completed => CascadeToContinuation(completed, Tuple.Create(promise, internalLogger));
                    task.ContinueWith(
                        continuationAction,
                        TaskContinuationOptions.ExecuteSynchronously);
#endif
                    break;
            }
        }

        static class LinkOutcomeActionHost<T>
        {
            public static readonly Action<Task<T>, object> Action =
                (t, tcs) =>
                {
                    switch (t.Status)
                    {
                        case TaskStatus.RanToCompletion:
                            ((TaskCompletionSource<T>)tcs).TrySetResult(t.Result);
                            break;
                        case TaskStatus.Canceled:
                            ((TaskCompletionSource<T>)tcs).TrySetCanceled();
                            break;
                        case TaskStatus.Faulted:
                            ((TaskCompletionSource<T>)tcs).TryUnwrap(t.Exception);
                            break;
                        default:
                            ThrowHelper.ThrowArgumentOutOfRangeException(); break;
                    }
                };
        }

        public static void LinkOutcome<T>(this Task<T> task, TaskCompletionSource<T> taskCompletionSource)
        {
            switch (task.Status)
            {
                case TaskStatus.RanToCompletion:
                    taskCompletionSource.TrySetResult(task.Result);
                    break;
                case TaskStatus.Canceled:
                    taskCompletionSource.TrySetCanceled();
                    break;
                case TaskStatus.Faulted:
                    taskCompletionSource.TryUnwrap(task.Exception);
                    break;
                default:
#if !NET40
                    task.ContinueWith(LinkOutcomeActionHost<T>.Action, taskCompletionSource, TaskContinuationOptions.ExecuteSynchronously);
#else
                    Action<Task<T>> continuationAction = completed => LinkOutcomeActionHost<T>.Action(completed, taskCompletionSource);
                    task.ContinueWith(continuationAction, TaskContinuationOptions.ExecuteSynchronously);
#endif
                    break;
            }
        }

        public static void TryUnwrap<T>(this TaskCompletionSource<T> completionSource, Exception exception)
        {
            if (exception is AggregateException aggregateException)
            {
                completionSource.TrySetException(aggregateException.InnerExceptions);
            }
            else
            {
                completionSource.TrySetException(exception);
            }
        }

        public static Exception Unwrap(this Exception exception)
        {
            if (exception is AggregateException aggregateException)
            {
                return aggregateException.InnerException;
            }

            return exception;
        }

        private static readonly Action<Task> IgnoreTaskContinuation = t => { var ignored = t.Exception; };

        public static bool IsSuccess(this Task task) => task.IsCompleted && !task.IsFaulted && !task.IsCanceled;

        /// <summary>Observes and ignores a potential exception on a given Task.
        /// If a Task fails and throws an exception which is never observed, it will be caught by the .NET finalizer thread.
        /// This function awaits the given task and if the exception is thrown, it observes this exception and simply ignores it.
        /// This will prevent the escalation of this exception to the .NET finalizer thread.</summary>
        /// <param name="task">The task to be ignored.</param>
        public static void Ignore(this Task task)
        {
            if (task.IsCompleted)
            {
                var ignored = task.Exception;
            }
            else
            {
                task.ContinueWith(
                    IgnoreTaskContinuation,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }

        public static async Task<bool> WaitAsync(Task task, TimeSpan timeout)
        {
#if NET40
            return await TaskEx.WhenAny(task, TaskEx.Delay(timeout)).ConfigureAwait(false) == task;
#else
            return await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false) == task;
#endif
        }
    }
}