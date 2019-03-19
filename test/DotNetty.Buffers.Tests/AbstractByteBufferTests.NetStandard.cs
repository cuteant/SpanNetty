// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !TEST40
namespace DotNetty.Buffers.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using DotNetty.Common.Utilities;
    using Xunit;

    partial class AbstractByteBufferTests
    {
        [Fact]
        public void GetByteBufferState_Span()
        {
            var value = new Span<byte>(new byte[4]);
            value[0] = 1;
            value[1] = 2;
            value[2] = 3;
            value[3] = 4;
            var self = this.buffer.SetBytes(0, value);
            var dst = new Span<byte>(new byte[4]);
            this.buffer.GetBytes(1, dst.Slice(1, 2));

            Assert.Equal(0, dst[0]);
            Assert.Equal(2, dst[1]);
            Assert.Equal(3, dst[2]);
            Assert.Equal(0, dst[3]);
        }

        [Fact]
        public void GetByteBufferState_Memory()
        {
            var value = new byte[4];
            value[0] = 1;
            value[1] = 2;
            value[2] = 3;
            value[3] = 4;
            var self = this.buffer.SetBytes(0, new Memory<byte>(value));
            var dst = new Memory<byte>(new byte[4]);
            this.buffer.GetBytes(1, dst.Slice(1, 2));
            var dstSpan = dst.Span;
            Assert.Equal(0, dstSpan[0]);
            Assert.Equal(2, dstSpan[1]);
            Assert.Equal(3, dstSpan[2]);
            Assert.Equal(0, dstSpan[3]);
        }

        [Fact]
        public void GetDirectByteBufferBoundaryCheck_Span() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetBytes(-1, Span<byte>.Empty));

        [Fact]
        public void GetDirectByteBufferBoundaryCheck_Memory() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetBytes(-1, Memory<byte>.Empty));

        [Fact]
        public void ByteArrayTransfer_Span()
        {
            var raw = new byte[BlockSize * 2];
            var value = new Span<byte>(raw);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(raw);
                this.buffer.SetBytes(i, value.Slice(this.random.Next(BlockSize), BlockSize));
            }

            this.random = new Random(this.seed);
            var expectedRaw = new byte[BlockSize * 2];
            var expectedValue = new Span<byte>(expectedRaw);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(expectedRaw);
                int valueOffset = this.random.Next(BlockSize);
                this.buffer.GetBytes(i, value.Slice(valueOffset, BlockSize));
                for (int j = valueOffset; j < valueOffset + BlockSize; j++)
                {
                    Assert.Equal(expectedValue[j], value[j]);
                }
            }
        }
    }
}
#endif