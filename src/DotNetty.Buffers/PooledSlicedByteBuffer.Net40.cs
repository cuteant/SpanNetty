#if NET40
namespace DotNetty.Buffers
{
    partial class PooledSlicedByteBuffer
    {
        public override ref byte GetPinnableMemoryAddress() => throw new System.NotSupportedException();
    }
}
#endif