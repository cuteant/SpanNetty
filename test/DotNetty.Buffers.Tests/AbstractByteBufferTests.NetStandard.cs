// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        public void ReadBytes_Memory()
        {
            const int capacity = 8;
            const int dataLen = capacity + 8;
            IByteBuffer buf = this.NewBuffer(capacity);
            if (buf.MaxCapacity < dataLen)
            {
                //For SlicedByteBuffer
                _ = buf.Release();
                buf = this.NewBuffer(dataLen);
            }
            //For SlicedByteBuffer
            buf.SetWriterIndex(0);

            var input = new byte[dataLen + 4];
            this.random.NextBytes(input);
            buf.WriteBytes(input.AsMemory(0, dataLen));
            Assert.True(buf.WriterIndex == dataLen);

            var output = new byte[dataLen + 4];
            Array.Copy(input, dataLen, output, dataLen, 4);

            Assert.True(buf.ReadBytes(output.AsMemory(0, 4)) == 4);
            Assert.True(buf.ReaderIndex == 4);
            Assert.True(buf.Slice(4, 4).ReadBytes(output.AsMemory(4)) == 4);
            Assert.True(buf.Slice(8, 8).ReadBytes(output.AsMemory(8, 4)) == 4);
            buf.SkipBytes(8);
            Assert.True(buf.ReadBytes(output.AsMemory(12)) == 4);
            Assert.True(buf.ReaderIndex == dataLen);
            Assert.Equal(input, output);
            Assert.True(buf.Release());
        }

        [Fact]
        public void ReadBytes_Span()
        {
            const int capacity = 8;
            const int dataLen = capacity + 8;
            IByteBuffer buf = this.NewBuffer(capacity);
            if (buf.MaxCapacity < dataLen)
            {
                //For SlicedByteBuffer
                _ = buf.Release();
                buf = this.NewBuffer(dataLen);
            }
            //For SlicedByteBuffer
            buf.SetWriterIndex(0);

            var input = new byte[dataLen + 4];
            this.random.NextBytes(input);
            buf.WriteBytes(input.AsSpan(0, dataLen));
            Assert.True(buf.WriterIndex == dataLen);

            var output = new byte[dataLen + 4];
            Array.Copy(input, dataLen, output, dataLen, 4);

            Assert.True(buf.ReadBytes(output.AsSpan(0, 4)) == 4);
            Assert.True(buf.ReaderIndex == 4);
            Assert.True(buf.Slice(4, 4).ReadBytes(output.AsSpan(4)) == 4);
            Assert.True(buf.Slice(8, 8).ReadBytes(output.AsSpan(8, 4)) == 4);
            buf.SkipBytes(8);
            Assert.True(buf.ReadBytes(output.AsSpan(12)) == 4);
            Assert.True(buf.ReaderIndex == dataLen);
            Assert.Equal(input, output);
            Assert.True(buf.Release());
        }

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

        [Fact]
        public void IndexOfAny()
        {
            this.buffer.Clear();
            this.buffer.WriteByte(1);
            this.buffer.WriteByte(2);
            this.buffer.WriteByte(3);
            this.buffer.WriteByte(2);
            this.buffer.WriteByte(1);

            Assert.Equal(-1, this.buffer.IndexOfAny(1, 4, 1, 1));
            Assert.Equal(-1, this.buffer.IndexOfAny(4, 1, 1, 1));
            Assert.Equal(1, this.buffer.IndexOfAny(1, 4, 2, 3));
            Assert.Equal(3, this.buffer.IndexOfAny(4, 1, 2, 3));
        }

        [Fact]
        public void FindIndex()
        {
            this.buffer.Clear();
            for (int i = 0; i < Capacity; i++)
            {
                this.buffer.WriteByte(i + 1);
            }

            int lastIndex = 0;
            this.buffer.SetIndex(Capacity / 4, Capacity * 3 / 4);
            int i1 = Capacity / 4;
            Assert.Equal(-1,
                this.buffer.FindIndex(
                    value =>
                    {
                        Assert.Equal(value, (byte)(i1 + 1));
                        Volatile.Write(ref lastIndex, i1);
                        i1++;
                        return false;
                    }));

            Assert.Equal(Capacity * 3 / 4 - 1, Volatile.Read(ref lastIndex));
        }

        [Fact]
        public void FindIndex2()
        {
            this.buffer.Clear();
            for (int i = 0; i < Capacity; i++)
            {
                this.buffer.WriteByte(i + 1);
            }

            int stop = Capacity / 2;
            int i1 = Capacity / 3;
            Assert.Equal(stop, this.buffer.FindIndex(Capacity / 3, Capacity / 3, value =>
            {
                Assert.Equal((byte)(i1 + 1), value);
                if (i1 == stop)
                {
                    return true;
                }

                i1++;
                return false;
            }));
        }

        [Fact]
        public void FindLastIndex()
        {
            this.buffer.Clear();
            for (int i = 0; i < Capacity; i++)
            {
                this.buffer.WriteByte(i + 1);
            }

            int lastIndex = 0;
            int i1 = Capacity * 3 / 4 - 1;
            Assert.Equal(-1, this.buffer.FindLastIndex(Capacity / 4, Capacity * 2 / 4, value =>
            {
                Assert.Equal((byte)(i1 + 1), value);
                Volatile.Write(ref lastIndex, i1);
                i1--;
                return false;
            }));

            Assert.Equal(Capacity / 4, Volatile.Read(ref lastIndex));
        }
    }
}
