#if NET40
namespace DotNetty.Buffers
{
    using System;

    partial class AbstractUnpooledSlicedByteBuffer
    {
        public override ref byte GetPinnableMemoryAddress() => ref this.Unwrap().GetPinnableMemoryOffsetAddress(this.adjustment);

        public override ref byte GetPinnableMemoryOffsetAddress(int elementOffset)
        {
            return ref this.Unwrap().GetPinnableMemoryOffsetAddress(elementOffset);
        }
    }
}
#endif
