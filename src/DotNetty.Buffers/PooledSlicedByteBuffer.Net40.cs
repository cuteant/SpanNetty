#if NET40
namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;
    using static AbstractUnpooledSlicedByteBuffer;

    partial class PooledSlicedByteBuffer
    {
        public override ref byte GetPinnableMemoryAddress() => ref this.Unwrap().GetPinnableMemoryOffsetAddress(this.adjustment);

        public override ref byte GetPinnableMemoryOffsetAddress(int elementOffset)
        {
            return ref this.Unwrap().GetPinnableMemoryOffsetAddress(elementOffset);
        }
    }
}
#endif