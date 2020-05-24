// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using Xunit;

    public abstract class AbstractPooledByteBufferTests : AbstractByteBufferTests
    {
        protected abstract IByteBuffer Alloc(int length, int maxCapacity);

        protected override IByteBuffer NewBuffer(int length, int maxCapacity)
        {
            IByteBuffer buffer = this.Alloc(length, maxCapacity);

            // Testing if the writerIndex and readerIndex are correct when allocate and also after we reset the mark.
            Assert.Equal(0, buffer.WriterIndex);
            Assert.Equal(0, buffer.ReaderIndex);
            buffer.ResetReaderIndex();
            buffer.ResetWriterIndex();
            Assert.Equal(0, buffer.WriterIndex);
            Assert.Equal(0, buffer.ReaderIndex);

            return buffer;
        }

        [Fact]
        public void EnsureWritableWithEnoughSpaceShouldNotThrow()
        {
            IByteBuffer buf = this.NewBuffer(1, 10);
            buf.EnsureWritable(3);
            Assert.True(buf.WritableBytes >= 3);
            buf.Release();
        }

        [Fact]
        public void EnsureWritableWithNotEnoughSpaceShouldThrow()
        {
            IByteBuffer buf = this.NewBuffer(1, 10);
            Assert.Throws<IndexOutOfRangeException>(() => buf.EnsureWritable(11));
            buf.Release();
        }

        [Fact]
        public override void MaxFastWritableBytes()
        {
            IByteBuffer buffer = this.NewBuffer(150, 500).SetWriterIndex(100);
            Assert.Equal(50, buffer.WritableBytes);
            Assert.Equal(150, buffer.Capacity);
            Assert.Equal(500, buffer.MaxCapacity);
            Assert.Equal(400, buffer.MaxWritableBytes);

            int chunkSize = PooledByteBuf(buffer).MaxLength;
            Assert.True(chunkSize >= 150);
            int remainingInAlloc = Math.Min(chunkSize - 100, 400);
            Assert.Equal(remainingInAlloc, buffer.MaxFastWritableBytes);

            // write up to max, chunk alloc should not change (same handle)
            long handleBefore = PooledByteBuf(buffer).Handle;
            buffer.WriteBytes(new byte[remainingInAlloc]);
            Assert.Equal(handleBefore, PooledByteBuf(buffer).Handle);

            Assert.Equal(0, buffer.MaxFastWritableBytes);
            // writing one more should trigger a reallocation (new handle)
            buffer.WriteByte(7);
            Assert.NotEqual(handleBefore, PooledByteBuf(buffer).Handle);

            // should not exceed maxCapacity even if chunk alloc does
            buffer.AdjustCapacity(500);
            Assert.Equal(500 - buffer.WriterIndex, buffer.MaxFastWritableBytes);
            buffer.Release();
        }

        private static IPooledByteBuffer PooledByteBuf(IByteBuffer buffer)
        {
            // might need to unwrap if swapped (LE) and/or leak-aware-wrapped
            while (!(buffer is IPooledByteBuffer))
            {
                buffer = buffer.Unwrap();
            }
            return (IPooledByteBuffer)buffer;
        }

        [Fact]
        public void EnsureWritableDoesntGrowTooMuch()
        {
            var buffer = this.NewBuffer(150, 500).SetWriterIndex(100);

            Assert.Equal(50, buffer.WritableBytes);
            int fastWritable = buffer.MaxFastWritableBytes;
            Assert.True(fastWritable > 50);

            long handleBefore = PooledByteBuf(buffer).Handle;

            // capacity expansion should not cause reallocation
            // (should grow precisely the specified amount)
            buffer.EnsureWritable(fastWritable);
            Assert.Equal(handleBefore, PooledByteBuf(buffer).Handle);
            Assert.Equal(100 + fastWritable, buffer.Capacity);
            Assert.Equal(buffer.WritableBytes, buffer.MaxFastWritableBytes);
            buffer.Release();
        }
    }
}