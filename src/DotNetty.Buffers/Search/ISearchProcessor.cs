namespace DotNetty.Buffers
{
    using DotNetty.Common.Utilities;

    /// <summary>
    /// Interface for <see cref="IByteProcessor"/> that implements string search.
    /// </summary>
    public interface ISearchProcessor : IByteProcessor
    {
        /// <summary>
        /// Resets the state of <see cref="ISearchProcessor"/>.
        /// </summary>
        void Reset();
    }
}