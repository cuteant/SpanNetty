#if NET40
namespace DotNetty.Buffers
{
    partial class PooledHeapByteBuffer
    {
        public override ref byte GetPinnableMemoryOffsetAddress(int elementOffset)
        {
            this.EnsureAccessible();
            return ref this.Memory[this.Offset + elementOffset];
        }
    }
}
#endif
