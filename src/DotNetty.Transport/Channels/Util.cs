// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;

    public static class Util
    {
        static readonly IInternalLogger Log = InternalLoggerFactory.GetInstance<IChannel>();

        /// <summary>
        /// Marks the specified <see cref="IPromise"/> as success. If the
        /// <see cref="IPromise"/> is done already, logs a message.
        /// </summary>
        /// <param name="promise">The <see cref="IPromise"/> to complete.</param>
        /// <param name="logger">The <see cref="IInternalLogger"/> to use to log a failure message.</param>
        public static void SafeSetSuccess(IPromise promise, IInternalLogger logger)
        {
            if (!promise.IsVoid && !promise.TryComplete() && logger.WarnEnabled)
            {
                var err = promise.Task.Exception?.InnerException;
                if (err is null)
                {
                    logger.FailedToMarkAPromiseAsSuccess(promise);
                }
                else
                {
                    logger.FailedToMarkAPromiseAsSuccessFailed(promise, err);
                }
            }
        }

        /// <summary>
        /// Marks the specified <see cref="IPromise"/> as failure. If the
        /// <see cref="IPromise"/> is done already, log a message.
        /// </summary>
        /// <param name="promise">The <see cref="IPromise"/> to complete.</param>
        /// <param name="cause">The <see cref="Exception"/> to fail the <see cref="IPromise"/> with.</param>
        /// <param name="logger">The <see cref="IInternalLogger"/> to use to log a failure message.</param>
        public static void SafeSetFailure(IPromise promise, Exception cause, IInternalLogger logger)
        {
            if (!promise.IsVoid && !promise.TrySetException(cause) && logger.WarnEnabled)
            {
                var err = promise.Task.Exception?.InnerException;
                if (err is null)
                {
                    logger.FailedToMarkAPromiseAsFailure(promise, cause);
                }
                else
                {
                    logger.FailedToMarkAPromiseAsFailureFailed(promise, cause, err);
                }
            }
        }

        public static void CloseSafe(this IChannel channel)
        {
            CompleteChannelCloseTaskSafely(channel, channel.CloseAsync());
        }

        //public static void CloseSafe(this IChannelUnsafe u)
        //{
        //    CompleteChannelCloseTaskSafely(u, u.CloseAsync());
        //}

        internal static async void CompleteChannelCloseTaskSafely(object channelObject, Task closeTask)
        {
            try
            {
                await closeTask;
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex) 
            {
                if (Log.DebugEnabled)
                {
                    Log.FailedToCloseChannelCleanly(channelObject, ex);
                }
            }
        }
    }
}