namespace DotNetty.Common.Concurrency
{
    public interface IRejectedExecutionHandler
    {
        /// <summary>
        /// Called when someone tried to add a task to <see cref="SingleThreadEventExecutor"/> but this failed due capacity
        /// </summary>
        /// <param name="task"></param>
        /// <param name="executor"></param>
        void Rejected(IRunnable task, SingleThreadEventExecutor executor);
    }
}
