namespace DotNetty.Common.Concurrency
{
    using DotNetty.Common.Internal;

    /// <summary>
    /// Factory used to create <see cref="IQueue{T}"/> instances that will be used to store tasks for an <see cref="IEventExecutor"/>.
    /// 
    /// <para>Generally speaking the returned <see cref="IQueue{T}"/> MUST be thread-safe and depending on the <see cref="IEventExecutor"/>
    /// implementation must be of type <see cref="IBlockingQueue{T}"/>.</para>
    /// </summary>
    public interface IEventExecutorTaskQueueFactory
    {
        /// <summary>
        /// Returns a new <see cref="IQueue{T}"/> to use.
        /// </summary>
        /// <param name="maxCapacity">the maximum amount of elements that can be stored in the <see cref="IQueue{T}"/> at a given point
        /// in time.</param>
        /// <returns>the new queue.</returns>
        IQueue<IRunnable> NewTaskQueue(int maxCapacity);
    }
}
