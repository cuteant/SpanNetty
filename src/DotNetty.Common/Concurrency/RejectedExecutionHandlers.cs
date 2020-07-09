namespace DotNetty.Common.Concurrency
{
    using System;

    /// <summary>
    /// Expose helper methods which create different <see cref="IRejectedExecutionHandler"/>s.
    /// </summary>
    public sealed class RejectedExecutionHandlers
    {
        /// <summary>
        /// Returns a <see cref="IRejectedExecutionHandler"/> that will always just throw a <see cref="RejectedExecutionException"/>.
        /// </summary>
        public static IRejectedExecutionHandler Reject()
        {
            return DefaultRejectedExecutionHandler.Instance;
        }

        /// <summary>
        /// Tries to backoff when the task can not be added due restrictions for an configured amount of time. This
        /// </summary>
        /// <param name="retries"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        public static IRejectedExecutionHandler Backoff(int retries, TimeSpan delay)
        {
            return new FixedBackoffRejectedExecutionHandler(retries, delay);
        }

        public static IRejectedExecutionHandler Backoff(int retries, TimeSpan minDelay, TimeSpan maxDelay, TimeSpan step)
        {
            return new ExponentialBackoffRejectedExecutionHandler(retries, minDelay, maxDelay, step);
        }
    }
}
