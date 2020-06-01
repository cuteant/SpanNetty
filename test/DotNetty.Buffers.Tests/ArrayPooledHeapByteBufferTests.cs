using System.Text;
using DotNetty.Common.Utilities;

namespace DotNetty.Buffers.Tests
{
    public sealed class ArrayPooledHeapByteBufferTests : AbstractArrayPooledByteBufferTests
    {
        protected override IByteBuffer NewBuffer(int length, int maxCapacity) => ArrayPooledByteBufferAllocator.Default.HeapBuffer(length, maxCapacity);

        protected override void SetCharSequenceNoExpand(Encoding encoding)
        {
            var array = new byte[1];
            var buf = ArrayPooledHeapByteBuffer.NewInstance(ArrayPooled.Allocator, ArrayPooled.DefaultArrayPool, array, array.Length, array.Length);
            try
            {
                buf.SetCharSequence(0, new StringCharSequence("AB"), encoding);
            }
            finally
            {
                buf.Release();
            }
        }
    }
}