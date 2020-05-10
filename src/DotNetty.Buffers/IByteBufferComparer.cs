using System.Collections.Generic;

namespace DotNetty.Buffers
{
    public sealed class ByteBufferComparer : IEqualityComparer<IByteBuffer>
    {
        public static readonly IEqualityComparer<IByteBuffer> Default = new ByteBufferComparer();

        private ByteBufferComparer() { }

        public bool Equals(IByteBuffer x, IByteBuffer y)
        {
            if (x is null) { return false; }

            return x.Equals(y);
        }

        public int GetHashCode(IByteBuffer obj)
        {
            //if (obj is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.obj); }

            return ByteBufferUtil.HashCode(obj);
        }
    }
}
