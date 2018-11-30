#if NET40
namespace DotNetty.Buffers
{
    partial class UnpooledSlicedByteBuffer
    {
        public override ref byte GetPinnableMemoryAddress() => throw new System.NotSupportedException();
    }
}
#endif