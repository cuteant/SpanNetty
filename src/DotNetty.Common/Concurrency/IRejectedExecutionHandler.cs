namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;

    public interface IRejectedExecutionHandler
    {
        /// <summary>
        /// Called when someone tried to add a task to <see cref="SingleThreadEventExecutor"/> but this failed due capacity
        /// restrictions.
        /// </summary>
        /// <param name="task"></param>
        /// <param name="executor"></param>
        void Rejected(IRunnable task, SingleThreadEventExecutor executor);
    }

    sealed class DefaultRejectedExecutionHandler : IRejectedExecutionHandler
    {
        public static readonly DefaultRejectedExecutionHandler Instance = new DefaultRejectedExecutionHandler();

        private DefaultRejectedExecutionHandler() { }

        public void Rejected(IRunnable task, SingleThreadEventExecutor executor)
        {
            ThrowHelper.ThrowRejectedExecutionException();
        }
    }

    sealed class FixedBackoffRejectedExecutionHandler : IRejectedExecutionHandler
    {
        private readonly int _retries;
        private readonly TimeSpan _delay;

        public FixedBackoffRejectedExecutionHandler(int retries, TimeSpan delay)
        {
            if (retries <= 0) { ThrowHelper.ThrowArgumentException_Positive(retries, ExceptionArgument.retries); }
            if (delay <= TimeSpan.Zero) { ThrowHelper.ArgumentOutOfRangeException_Positive(delay, ExceptionArgument.delay); }

            _retries = retries;
            _delay = delay;
        }

        public void Rejected(IRunnable task, SingleThreadEventExecutor executor)
        {
            if (!executor.InEventLoop)
            {
                for (int i = 0; i < _retries; i++)
                {
                    // Try to wake up the executor so it will empty its task queue.
                    executor.WakeUp(false);

                    Thread.Sleep(_delay);
                    if (executor.OfferTask(task)) { return; }
                }
            }
            // Either we tried to add the task from within the EventLoop or we was not able to add it even with backoff.
            ThrowHelper.ThrowRejectedExecutionException();
        }
    }

    sealed class ExponentialBackoffRejectedExecutionHandler : IRejectedExecutionHandler
    {
        private readonly SafeRandom _random;
        private readonly int _retries;
        private readonly TimeSpan _minDelay;
        private readonly TimeSpan _maxDelay;
        private readonly TimeSpan _step;

        public ExponentialBackoffRejectedExecutionHandler(int retries, TimeSpan minDelay, TimeSpan maxDelay, TimeSpan step)
        {
            if (retries <= 0) { ThrowHelper.ThrowArgumentException_Positive(retries, ExceptionArgument.retries); }
            if (minDelay <= TimeSpan.Zero) { ThrowHelper.ArgumentOutOfRangeException_Positive(minDelay, ExceptionArgument.minDelay); }
            if (maxDelay <= TimeSpan.Zero) { ThrowHelper.ArgumentOutOfRangeException_Positive(maxDelay, ExceptionArgument.maxDelay); }
            if (step <= TimeSpan.Zero) { ThrowHelper.ArgumentOutOfRangeException_Positive(step, ExceptionArgument.step); }
            if (minDelay >= maxDelay) { ThrowHelper.ArgumentOutOfRangeException_Invalid_minValue(minDelay); }

            _retries = retries;
            _minDelay = minDelay;
            _maxDelay = maxDelay;
            _step = step;
            _random = new SafeRandom();
        }

        public void Rejected(IRunnable task, SingleThreadEventExecutor executor)
        {
            if (!executor.InEventLoop)
            {
                for (int i = 0; i < _retries; i++)
                {
                    // Try to wake up the executor so it will empty its task queue.
                    executor.WakeUp(false);

                    Thread.Sleep(Next(i));
                    if (executor.OfferTask(task)) { return; }
                }
            }
            // Either we tried to add the task from within the EventLoop or we was not able to add it even with backoff.
            ThrowHelper.ThrowRejectedExecutionException();
        }

        public TimeSpan Next(int attempt)
        {
            TimeSpan currMax;
            try
            {
                long multiple = checked(1 << attempt);
                currMax = _minDelay + _step.Multiply(multiple); // may throw OverflowException
                if (currMax <= TimeSpan.Zero) { ThrowHelper.ThrowOverflowException(); }
            }
            catch (OverflowException)
            {
                currMax = _maxDelay;
            }
            currMax = TimeUtil.Min(currMax, _maxDelay);

            if (_minDelay >= currMax) { ThrowHelper.ArgumentOutOfRangeException_Invalid_minValue(_minDelay, currMax); }
            return _random.NextTimeSpan(_minDelay, currMax);
        }
    }
}
