// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using DotNetty.Common.Utilities;

namespace DotNetty.Buffers.Tests
{
    public sealed class BufferManagerDirectByteBufferTests : AbstractBufferManagerByteBufferTests
    {
        protected override IByteBuffer NewBuffer(int length, int maxCapacity) => BufferManagerByteBufferAllocator.Default.DirectBuffer(length, maxCapacity);

        protected override void SetCharSequenceNoExpand(Encoding encoding)
        {
            var array = new byte[1];
            var buf = BufferManagerUnsafeDirectByteBuffer.NewInstance(BufferManagerUtil.Allocator, BufferManagerUtil.DefaultBufferPool, array, array.Length, array.Length);
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
