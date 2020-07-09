namespace DotNetty.Common.Concurrency
{
    /// <summary>
    /// Chooses the next <typeparamref name="TEventExecutor"/> to use.
    /// </summary>
    public interface IEventExecutorChooser<TEventExecutor>
        where TEventExecutor : class, IEventExecutor
    {
        /// <summary>
        /// Returns the new <typeparamref name="TEventExecutor"/> to use.
        /// </summary>
        TEventExecutor GetNext();
    }
}