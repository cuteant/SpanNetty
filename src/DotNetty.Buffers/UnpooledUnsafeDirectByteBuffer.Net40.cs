#if NET40
namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Internal;

    partial class UnpooledUnsafeDirectByteBuffer
    {
        public override bool HasMemoryAddress => false;

        public override ref byte GetPinnableMemoryAddress() => throw new NotSupportedException();

        protected internal override short _GetShort(int index) => HeapByteBufferUtil.GetShort(this.buffer, index);

        protected internal override short _GetShortLE(int index) => HeapByteBufferUtil.GetShortLE(this.buffer, index);

        protected internal override int _GetUnsignedMedium(int index) => HeapByteBufferUtil.GetUnsignedMedium(this.buffer, index);

        protected internal override int _GetUnsignedMediumLE(int index) => HeapByteBufferUtil.GetUnsignedMediumLE(this.buffer, index);

        protected internal override int _GetInt(int index) => HeapByteBufferUtil.GetInt(this.buffer, index);

        protected internal override int _GetIntLE(int index) => HeapByteBufferUtil.GetIntLE(this.buffer, index);

        protected internal override long _GetLong(int index) => HeapByteBufferUtil.GetLong(this.buffer, index);

        protected internal override long _GetLongLE(int index) => HeapByteBufferUtil.GetLongLE(this.buffer, index);

        public override IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            this.CheckDstIndex(index, length, dstIndex, dst.Capacity);
            if (dst.HasArray)
            {
                this.GetBytes(index, dst.Array, dst.ArrayOffset + dstIndex, length);
            }
            else
            {
                dst.SetBytes(dstIndex, this.buffer, index, length);
            }

            return this;
        }

        public override IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            this.CheckDstIndex(index, length, dstIndex, dst.Length);
            PlatformDependent.CopyMemory(this.buffer, index, dst, dstIndex, length);
            return this;
        }

        public override IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            this.CheckIndex(index, length);
            destination.Write(this.Array, this.ArrayOffset + index, length);
            return this;
        }

        protected internal override void _SetShort(int index, int value) => HeapByteBufferUtil.SetShort(this.buffer, index, value);

        protected internal override void _SetShortLE(int index, int value) => HeapByteBufferUtil.SetShortLE(this.buffer, index, value);

        protected internal override void _SetMedium(int index, int value) => HeapByteBufferUtil.SetMedium(this.buffer, index, value);

        protected internal override void _SetMediumLE(int index, int value) => HeapByteBufferUtil.SetMediumLE(this.buffer, index, value);

        protected internal override void _SetInt(int index, int value) => HeapByteBufferUtil.SetInt(this.buffer, index, value);

        protected internal override void _SetIntLE(int index, int value) => HeapByteBufferUtil.SetIntLE(this.buffer, index, value);

        protected internal override void _SetLong(int index, long value) => HeapByteBufferUtil.SetLong(this.buffer, index, value);

        protected internal override void _SetLongLE(int index, long value) => HeapByteBufferUtil.SetLongLE(this.buffer, index, value);

        public override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            this.CheckSrcIndex(index, length, srcIndex, src.Capacity);
            if (src.HasArray)
            {
                this.SetBytes(index, src.Array, src.ArrayOffset + srcIndex, length);
            }
            else
            {
                src.GetBytes(srcIndex, this.buffer, index, length);
            }
            return this;
        }

        public override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            this.CheckSrcIndex(index, length, srcIndex, src.Length);
            PlatformDependent.CopyMemory(src, srcIndex, this.buffer, index, length);
            return this;
        }

        public override async Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            this.EnsureAccessible();
            int readTotal = 0;
            int read;
            int offset = this.ArrayOffset + index;
            do
            {
                read = await src.ReadAsync(this.Array, offset + readTotal, length - readTotal, cancellationToken);
                readTotal += read;
            }
            while (read > 0 && readTotal < length);

            return readTotal;
        }

        public override IByteBuffer SetZero(int index, int length)
        {
            this.CheckIndex(index, length);
            PlatformDependent.Clear(this.buffer, index, length);
            return this;
        }

        public override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            var copiedArray = new byte[length];
            PlatformDependent.CopyMemory(this.buffer, index, copiedArray, 0, length);

            return new UnpooledHeapByteBuffer(this.Allocator, copiedArray, this.MaxCapacity);
        }

        public override IByteBuffer WriteZero(int length)
        {
            if (length == 0) { return this; }

            this.EnsureWritable(length);
            int wIndex = this.WriterIndex;
            this.CheckIndex0(wIndex, length);
            PlatformDependent.Clear(this.buffer, wIndex, length);
            this.SetWriterIndex(wIndex + length);

            return this;
        }
    }
}
#endif
