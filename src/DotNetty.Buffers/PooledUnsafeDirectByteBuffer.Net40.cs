#if NET40
namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Internal;

    sealed class PooledUnsafeDirectByteBuffer : PooledByteBuffer<byte[]>
    {
        static readonly ThreadLocalPool<PooledUnsafeDirectByteBuffer> Recycler = new ThreadLocalPool<PooledUnsafeDirectByteBuffer>(handle => new PooledUnsafeDirectByteBuffer(handle, 0));

        internal static PooledUnsafeDirectByteBuffer NewInstance(int maxCapacity)
        {
            PooledUnsafeDirectByteBuffer buf = Recycler.Take();
            buf.Reuse(maxCapacity);
            return buf;
        }

        PooledUnsafeDirectByteBuffer(ThreadLocalPool.Handle recyclerHandle, int maxCapacity)
            : base(recyclerHandle, maxCapacity)
        {
        }

        internal sealed override void Init(PoolChunk<byte[]> chunk, long handle, int offset, int length, int maxLength,
            PoolThreadCache<byte[]> cache)
        {
            base.Init(chunk, handle, offset, length, maxLength, cache);
        }

        internal sealed override void InitUnpooled(PoolChunk<byte[]> chunk, int length)
        {
            base.InitUnpooled(chunk, length);
        }

        public sealed override bool IsDirect => true;

        public sealed override bool IsSingleIoBuffer => true;

        public sealed override int IoBufferCount => 1;

        public sealed override ArraySegment<byte> GetIoBuffer(int index, int length)
        {
            this.CheckIndex(index, length);
            index = this.Idx(index);
            return new ArraySegment<byte>(this.Memory, index, length);
        }

        public sealed override ArraySegment<byte>[] GetIoBuffers(int index, int length) => new[] { this.GetIoBuffer(index, length) };

        public sealed override bool HasArray => true;

        public sealed override byte[] Array
        {
            get
            {
                this.EnsureAccessible();
                return this.Memory;
            }
        }

        public sealed override int ArrayOffset => this.Offset;

        public sealed override bool HasMemoryAddress => false;

        public sealed override ref byte GetPinnableMemoryAddress() => throw new NotSupportedException();

        public sealed override IntPtr AddressOfPinnedMemory() => IntPtr.Zero;

        protected internal sealed override byte _GetByte(int index) => HeapByteBufferUtil.GetByte(this.Memory, this.Idx(index));

        protected internal sealed override short _GetShort(int index) => HeapByteBufferUtil.GetShort(this.Memory, this.Idx(index));

        protected internal sealed override short _GetShortLE(int index) => HeapByteBufferUtil.GetShortLE(this.Memory, this.Idx(index));

        protected internal sealed override int _GetUnsignedMedium(int index) => HeapByteBufferUtil.GetUnsignedMedium(this.Memory, this.Idx(index));

        protected internal sealed override int _GetUnsignedMediumLE(int index) => HeapByteBufferUtil.GetUnsignedMediumLE(this.Memory, this.Idx(index));

        protected internal sealed override int _GetInt(int index) => HeapByteBufferUtil.GetInt(this.Memory, this.Idx(index));

        protected internal sealed override int _GetIntLE(int index) => HeapByteBufferUtil.GetIntLE(this.Memory, this.Idx(index));

        protected internal sealed override long _GetLong(int index) => HeapByteBufferUtil.GetLong(this.Memory, this.Idx(index));

        protected internal sealed override long _GetLongLE(int index) => HeapByteBufferUtil.GetLongLE(this.Memory, this.Idx(index));

        public sealed override IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            this.CheckDstIndex(index, length, dstIndex, dst.Capacity);
            if (dst.HasArray)
            {
                this.GetBytes(index, dst.Array, dst.ArrayOffset + dstIndex, length);
            }
            else
            {
                dst.SetBytes(dstIndex, this.Memory, this.Idx(index), length);
            }
            return this;
        }

        public sealed override IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            this.CheckDstIndex(index, length, dstIndex, dst.Length);
            PlatformDependent.CopyMemory(this.Memory, this.Idx(index), dst, dstIndex, length);
            return this;
        }

        public sealed override IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            this.CheckIndex(index, length);
            destination.Write(this.Memory, this.Idx(index), length);
            return this;
        }

        protected internal sealed override void _SetByte(int index, int value) => HeapByteBufferUtil.SetByte(this.Memory, this.Idx(index), value);

        protected internal sealed override void _SetShort(int index, int value) => HeapByteBufferUtil.SetShort(this.Memory, this.Idx(index), value);

        protected internal sealed override void _SetShortLE(int index, int value) => HeapByteBufferUtil.SetShortLE(this.Memory, this.Idx(index), value);

        protected internal sealed override void _SetMedium(int index, int value) => HeapByteBufferUtil.SetMedium(this.Memory, this.Idx(index), value);

        protected internal sealed override void _SetMediumLE(int index, int value) => HeapByteBufferUtil.SetMediumLE(this.Memory, this.Idx(index), value);

        protected internal sealed override void _SetInt(int index, int value) => HeapByteBufferUtil.SetInt(this.Memory, this.Idx(index), value);

        protected internal sealed override void _SetIntLE(int index, int value) => HeapByteBufferUtil.SetIntLE(this.Memory, this.Idx(index), value);

        protected internal sealed override void _SetLong(int index, long value) => HeapByteBufferUtil.SetLong(this.Memory, this.Idx(index), value);

        protected internal sealed override void _SetLongLE(int index, long value) => HeapByteBufferUtil.SetLongLE(this.Memory, this.Idx(index), value);

        public sealed override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            this.CheckSrcIndex(index, length, srcIndex, src.Capacity);
            if (src.HasArray)
            {
                this.SetBytes(index, src.Array, src.ArrayOffset + srcIndex, length);
            }
            else
            {
                src.GetBytes(srcIndex, this.Memory, this.Idx(index), length);
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
            PlatformDependent.CopyMemory(src, srcIndex, this.Memory, this.Idx(index), length);
            return this;
        }

        public sealed override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            IByteBuffer copy = this.Allocator.HeapBuffer(length, this.MaxCapacity);
            copy.WriteBytes(this.Memory, this.Idx(index), length);
            return copy;
        }


        public sealed override IByteBuffer SetZero(int index, int length)
        {
            this.CheckIndex(index, length);
            PlatformDependent.Clear(this.Memory, this.Idx(index), length);
            return this;
        }

        public sealed override IByteBuffer WriteZero(int length)
        {
            if (0u >= (uint)length) { return this; }

            this.EnsureWritable(length);
            int wIndex = this.WriterIndex;
            this.CheckIndex0(wIndex, length);
            PlatformDependent.Clear(this.Memory, this.Idx(wIndex), length);
            this.SetWriterIndex(wIndex + length);

            return this;
        }
    }
}
#endif
