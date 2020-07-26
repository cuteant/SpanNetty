namespace DotNetty.Transport.Channels
{
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    public interface IDuplexChannel : IChannel
    {
        /// <summary>
        /// Returns <c>true</c> if and only if the remote peer shut down its output so that no more
        /// data is received from this channel.
        /// </summary>
        bool IsInputShutdown { get; }

        Task ShutdownInputAsync();

        /// <summary>
        /// Will shutdown the input and notify <see cref="IPromise"/>.
        /// </summary>
        Task ShutdownInputAsync(IPromise promise);

        bool IsOutputShutdown { get; }

        Task ShutdownOutputAsync();

        Task ShutdownOutputAsync(IPromise promise);

        bool IsShutdown { get; }

        Task ShutdownAsync();

        Task ShutdownAsync(IPromise promise);
    }
}