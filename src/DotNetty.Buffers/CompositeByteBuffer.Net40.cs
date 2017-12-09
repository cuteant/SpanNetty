#if NET40
namespace DotNetty.Buffers
{
    using System;

    partial class CompositeByteBuffer
    {
        public override ref byte GetPinnableMemoryOffsetAddress(int elementOffset)
        {
            switch (this.components.Count)
            {
                case 1:
                    return ref this.components[0].Buffer.GetPinnableMemoryOffsetAddress(elementOffset);
                default:
                    throw new NotSupportedException();
            }
        }
    }
}
#endif
