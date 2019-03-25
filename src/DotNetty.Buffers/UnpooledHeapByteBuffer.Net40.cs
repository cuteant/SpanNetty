#if NET40
namespace DotNetty.Buffers
{
    partial class UnpooledHeapByteBuffer
    {
        public sealed override bool HasMemoryAddress => false;

        public sealed override ref byte GetPinnableMemoryAddress() => throw new System.NotSupportedException();
    }
}
#endif
