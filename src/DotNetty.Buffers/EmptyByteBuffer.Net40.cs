#if NET40
namespace DotNetty.Buffers
{
    using System;

    partial class EmptyByteBuffer
    {
        public ref byte GetPinnableMemoryOffsetAddress(int elementOffset)
        {
            throw new NotSupportedException();
        }
    }
}
#endif