namespace DotNetty.Buffers
{
    public interface IMultiSearchProcessorFactory
    {
        /// <summary>
        /// Returns a new <see cref="IMultiSearchProcessor"/>.
        /// </summary>
        IMultiSearchProcessor NewSearchProcessor();
    }
}