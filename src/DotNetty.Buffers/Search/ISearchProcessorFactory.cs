namespace DotNetty.Buffers
{
    public interface ISearchProcessorFactory
    {
        /// <summary>
        /// Returns a new <see cref="ISearchProcessor"/>.
        /// </summary>
        ISearchProcessor NewSearchProcessor();
    }
}