
namespace DotNetty.Buffers
{
    using System;

    public sealed class ReadOnlyBufferException : Exception
    {
        public ReadOnlyBufferException() { }

        public ReadOnlyBufferException(string message) : base(message) { }

        public ReadOnlyBufferException(string message, Exception innerException) : base(message, innerException) { }
    }
}
