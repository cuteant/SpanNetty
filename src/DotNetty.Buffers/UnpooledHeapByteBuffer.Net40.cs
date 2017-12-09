#if NET40
namespace DotNetty.Buffers
{
    partial class UnpooledHeapByteBuffer
    {
        public override ref byte GetPinnableMemoryOffsetAddress(int elementOffset)
        {
            this.EnsureAccessible();
            return ref this.array[elementOffset];
        }
    }
}
#endif
