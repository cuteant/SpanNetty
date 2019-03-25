#if NET40
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Common;
using DotNetty.Common.Internal;

namespace DotNetty.Buffers
{
    partial class ArrayPooledUnsafeDirectByteBuffer : ArrayPooledByteBuffer
    {
        protected internal sealed override short _GetShort(int index) => HeapByteBufferUtil.GetShort(this.Memory, index);

        protected internal sealed override short _GetShortLE(int index) => HeapByteBufferUtil.GetShortLE(this.Memory, index);

        protected internal sealed override int _GetUnsignedMedium(int index) => HeapByteBufferUtil.GetUnsignedMedium(this.Memory, index);

        protected internal sealed override int _GetUnsignedMediumLE(int index) => HeapByteBufferUtil.GetUnsignedMediumLE(this.Memory, index);

        protected internal sealed override int _GetInt(int index) => HeapByteBufferUtil.GetInt(this.Memory, index);

        protected internal sealed override int _GetIntLE(int index) => HeapByteBufferUtil.GetIntLE(this.Memory, index);

        protected internal sealed override long _GetLong(int index) => HeapByteBufferUtil.GetLong(this.Memory, index);

        protected internal sealed override long _GetLongLE(int index) => HeapByteBufferUtil.GetLongLE(this.Memory, index);

        public sealed override IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            this.CheckDstIndex(index, length, dstIndex, dst.Capacity);
            if (dst.HasArray)
            {
                this.GetBytes(index, dst.Array, dst.ArrayOffset + dstIndex, length);
            }
            else
            {
                dst.SetBytes(dstIndex, this.Memory, index, length);
            }
            return this;
        }

        public sealed override IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            this.CheckDstIndex(index, length, dstIndex, dst.Length);
            PlatformDependent.CopyMemory(this.Memory, index, dst, dstIndex, length);
            return this;
        }

        public sealed override IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            this.CheckIndex(index, length);
            destination.Write(this.Memory, index, length);
            return this;
        }

        protected internal sealed override void _SetShort(int index, int value) => HeapByteBufferUtil.SetShort(this.Memory, index, value);

        protected internal sealed override void _SetShortLE(int index, int value) => HeapByteBufferUtil.SetShortLE(this.Memory, index, value);

        protected internal sealed override void _SetMedium(int index, int value) => HeapByteBufferUtil.SetMedium(this.Memory, index, value);

        protected internal sealed override void _SetMediumLE(int index, int value) => HeapByteBufferUtil.SetMediumLE(this.Memory, index, value);

        protected internal sealed override void _SetInt(int index, int value) => HeapByteBufferUtil.SetInt(this.Memory, index, value);

        protected internal sealed override void _SetIntLE(int index, int value) => HeapByteBufferUtil.SetIntLE(this.Memory, index, value);

        protected internal sealed override void _SetLong(int index, long value) => HeapByteBufferUtil.SetLong(this.Memory, index, value);

        protected internal sealed override void _SetLongLE(int index, long value) => HeapByteBufferUtil.SetLongLE(this.Memory, index, value);

        public sealed override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            this.CheckSrcIndex(index, length, srcIndex, src.Capacity);
            if (src.HasArray)
            {
                this.SetBytes(index, src.Array, src.ArrayOffset + srcIndex, length);
            }
            else
            {
                src.GetBytes(srcIndex, this.Memory, index, length);
            }
            return this;
        }

        public sealed override async Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
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

        public sealed override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            this.CheckSrcIndex(index, length, srcIndex, src.Length);
            PlatformDependent.CopyMemory(src, srcIndex, this.Memory, index, length);
            return this;
        }

        public sealed override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            IByteBuffer copy = this.Allocator.HeapBuffer(length, this.MaxCapacity);
            copy.WriteBytes(this.Memory, index, length);
            return copy;
        }


        public sealed override IByteBuffer SetZero(int index, int length)
        {
            this.CheckIndex(index, length);
            PlatformDependent.Clear(this.Memory, index, length);
            return this;
        }

        public sealed override IByteBuffer WriteZero(int length)
        {
            if (0u >= (uint)length) { return this; }

            this.EnsureWritable(length);
            int wIndex = this.WriterIndex;
            this.CheckIndex0(wIndex, length);
            PlatformDependent.Clear(this.Memory, wIndex, length);
            this.SetWriterIndex(wIndex + length);

            return this;
        }
    }
}
#endif
