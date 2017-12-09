#if NET40
namespace DotNetty.Buffers
{
    using System;

    partial class AbstractByteBuffer
    {
        public virtual ref byte GetPinnableMemoryOffsetAddress(int elementOffset)
        {
            throw new NotSupportedException();
        }
    }
}
#endif