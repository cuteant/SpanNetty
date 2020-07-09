namespace DotNetty.Common.Concurrency
{
    /// <summary>
    /// Factory that creates new <see cref="IEventExecutorChooser{TEventExecutor}"/>s.
    /// </summary>
    public interface IEventExecutorChooserFactory<TEventExecutor>
        where TEventExecutor : class, IEventExecutor
    {
        /// <summary>
        /// Returns a new <see cref="IEventExecutorChooser{TEventExecutor}"/>.
        /// </summary>
        /// <param name="executors"></param>
        /// <returns></returns>
        IEventExecutorChooser<TEventExecutor> NewChooser(TEventExecutor[] executors);
    }
}