#if NET40
namespace DotNetty.Buffers
{
    partial class ArrayPooledByteBuffer
    {
        public override ref byte GetPinnableMemoryOffsetAddress(int elementOffset)
        {
            this.EnsureAccessible();
            return ref this.Memory[elementOffset];
        }
    }
}
#endif
