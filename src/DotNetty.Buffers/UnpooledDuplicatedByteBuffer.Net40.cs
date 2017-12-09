#if NET40
namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;

    partial class UnpooledDuplicatedByteBuffer
    {
        public override ref byte GetPinnableMemoryOffsetAddress(int elementOffset)
        {
            return ref this.Unwrap().GetPinnableMemoryOffsetAddress(elementOffset);
        }
    }
}
#endif
