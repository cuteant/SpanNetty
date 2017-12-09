#if NET40
namespace DotNetty.Buffers
{
    partial class WrappedByteBuffer
    {
        public ref byte GetPinnableMemoryOffsetAddress(int elementOffset)
        {
            return ref this.Buf.GetPinnableMemoryOffsetAddress(elementOffset);
        }
    }
}
#endif