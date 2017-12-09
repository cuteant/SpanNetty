#if NET40
namespace DotNetty.Buffers
{
    partial class UnpooledUnsafeDirectByteBuffer
    {
        public override ref byte GetPinnableMemoryOffsetAddress(int elementOffset)
        {
            this.EnsureAccessible();
            return ref this.buffer[elementOffset];
        }
    }
}
#endif
