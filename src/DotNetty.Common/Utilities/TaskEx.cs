// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    public static class TaskUtil
    {
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

        static readonly Action<Task, object> LinkOutcomeContinuationAction = (t, tcs) =>
        {
            switch (t.Status)
            {
                case TaskStatus.RanToCompletion:
                    ((TaskCompletionSource)tcs).TryComplete();
                    break;
                case TaskStatus.Canceled:
                    ((TaskCompletionSource)tcs).TrySetCanceled();
                    break;
                case TaskStatus.Faulted:
                    ((TaskCompletionSource)tcs).TryUnwrap(t.Exception);
                    break;
                default:
                    ThrowHelper.ThrowArgumentOutOfRangeException(); break;
            }
        };

        public static void LinkOutcome(this Task task, TaskCompletionSource taskCompletionSource)
        {
            switch (task.Status)
            {
                case TaskStatus.RanToCompletion:
                    taskCompletionSource.TryComplete();
                    break;
                case TaskStatus.Canceled:
                    taskCompletionSource.TrySetCanceled();
                    break;
                case TaskStatus.Faulted:
                    taskCompletionSource.TryUnwrap(task.Exception);
                    break;
                default:
#if !NET40
                    task.ContinueWith(
                        LinkOutcomeContinuationAction,
                        taskCompletionSource,
                        TaskContinuationOptions.ExecuteSynchronously);
#else
                    Action<Task> continuationAction = completed => LinkOutcomeContinuationAction(completed, taskCompletionSource);
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
    }
}