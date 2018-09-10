namespace DotNetty.Common.Concurrency
{
    /// <summary>Marker interface for <see cref="IEventExecutor"/>s that will process all submitted tasks in an ordered / serial fashion.</summary>
    public interface IOrderedEventExecutor : IEventExecutor
    {
    }
}
