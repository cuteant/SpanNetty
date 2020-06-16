namespace DotNetty.Buffers
{
    /// <summary>
    /// Interface for <see cref="ISearchProcessor"/> that implements simultaneous search for multiple strings.
    /// </summary>
    public interface IMultiSearchProcessor : ISearchProcessor
    {
        /// <summary>
        /// Return the index of found search string (if any, or -1 if none) at current position of this <see cref="IMultiSearchProcessor"/>
        /// </summary>
        int GetFoundNeedleId();
    }
}