#if NET40
namespace DotNetty.Buffers
{
    using System;

    partial interface IByteBuffer
    {
        ref byte GetPinnableMemoryOffsetAddress(int elementOffset);
    }
}
#endif