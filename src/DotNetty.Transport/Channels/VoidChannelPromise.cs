// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using DotNetty.Common.Concurrency;

    public sealed class VoidChannelPromise : IPromise
    {
        static readonly Exception Error;

        private readonly IChannel channel;
        // Will be null if we should not propagate exceptions through the pipeline on failure case.
        private readonly bool fireException;

        static VoidChannelPromise()
        {
            Error = new InvalidOperationException("No operations are allowed on void promise");
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="channel">channel the <see cref="IChannel"/> associated with this future</param>
        /// <param name="fireException"></param>
        public VoidChannelPromise(IChannel channel, bool fireException)
        {
            if (channel is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.channel); }
            this.channel = channel;
            this.fireException = fireException;
            this.Task = TaskUtil.FromException(Error);
        }

        public Task Task { get; }

        public bool IsVoid => true;

        public bool IsSuccess => false;

        public bool IsCompleted => false;

        public bool IsFaulted => false;

        public bool IsCanceled => false;

        public bool TryComplete() => false;

        public void Complete() { }

        public bool TrySetException(Exception exception)
        {
            if (!this.fireException) { return false; }
            if (exception is AggregateException aggregateException)
            {
                return this.TrySetException(aggregateException.InnerExceptions);
            }
            this.FireException0(exception);
            return false;
        }

        public bool TrySetException(IEnumerable<Exception> exceptions)
        {
            if (!this.fireException) { return false; }
            foreach (var item in exceptions)
            {
                this.FireException0(item);
            }
            return false;
        }

        public void SetException(Exception exception)
        {
            if (!this.fireException) { return; }
            if (exception is AggregateException aggregateException)
            {
                this.SetException(aggregateException.InnerExceptions);
                return;
            }
            this.FireException0(exception);
        }

        public void SetException(IEnumerable<Exception> exceptions)
        {
            if (!this.fireException) { return; }
            foreach (var item in exceptions)
            {
                this.FireException0(item);
            }
        }

        public bool TrySetCanceled() => false;

        public void SetCanceled() { }

        public bool SetUncancellable() => true;

        public IPromise Unvoid()
        {
            var promise = new TaskCompletionSource();
            if (this.fireException)
            {
#if NET40
                void fireExceptionOnFailure(Task t)
                {
                    if (t.IsFaulted)// && this.channel.Registered)
                    {
                        this.channel.Pipeline.FireExceptionCaught(t.Exception.InnerException);
                    }
                }
                promise.Task.ContinueWith(fireExceptionOnFailure, TaskContinuationOptions.ExecuteSynchronously);
#else
                promise.Task.ContinueWith(FireExceptionOnFailureAction, this.channel, TaskContinuationOptions.ExecuteSynchronously);
#endif

            }
            return promise;
        }

        private static readonly Action<Task, object> FireExceptionOnFailureAction = FireExceptionOnFailure;
        private static void FireExceptionOnFailure(Task t, object s)
        {
            var ch = (IChannel)s;
            if (t.IsFaulted)// && ch.Registered)
            {
                ch.Pipeline.FireExceptionCaught(t.Exception.InnerException);
            }
        }

        private void FireException0(Exception cause)
        {
            // Only fire the exception if the channel is open and registered
            // if not the pipeline is not setup and so it would hit the tail
            // of the pipeline.
            // See https://github.com/netty/netty/issues/1517
            if (channel.Registered)
            {
                channel.Pipeline.FireExceptionCaught(cause);
            }
        }

        public override string ToString() => "VoidPromise";
    }
}