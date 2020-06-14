namespace DotNetty.Handlers.Logging
{
    using System.Text;
    using DotNetty.Buffers;

    /// <summary>
    /// Used to control the format and verbosity of logging for <see cref="IByteBuffer"/>s and <see cref="IByteBufferHolder"/>s.
    /// </summary>
    public enum ByteBufferFormat
    {
        /// <summary>
        /// <see cref="IByteBuffer"/>s will be logged in a simple format, with no hex dump included.
        /// </summary>
        Simple,

        /// <summary>
        /// <see cref="IByteBuffer"/>s will be logged using <see cref="ByteBufferUtil.AppendPrettyHexDump(StringBuilder, IByteBuffer)"/>.
        /// </summary>
        HexDump
    }
}