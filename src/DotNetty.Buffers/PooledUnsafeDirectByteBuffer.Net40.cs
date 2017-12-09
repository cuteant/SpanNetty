#if NET40
namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Internal;

    // https://www.codeproject.com/articles/1129443/subverting-net-type-safety-with-system-runtime-com

    sealed unsafe class PooledUnsafeDirectByteBuffer : PooledByteBuffer<byte[]>
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

        internal override void Init(PoolChunk<byte[]> chunk, long handle, int offset, int length, int maxLength,
            PoolThreadCache<byte[]> cache)
        {
            base.Init(chunk, handle, offset, length, maxLength, cache);
        }

        internal override void InitUnpooled(PoolChunk<byte[]> chunk, int length)
        {
            base.InitUnpooled(chunk, length);
        }

        public override bool IsDirect => true;

        protected internal override byte _GetByte(int index) => this.Memory[this.Idx(index)];

        protected internal override short _GetShort(int index)
        {
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                return UnsafeByteBufferUtil.GetShort(bytes);
        }

        protected internal override short _GetShortLE(int index)
        {
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                return UnsafeByteBufferUtil.GetShortLE(bytes);
        }

        protected internal override int _GetUnsignedMedium(int index)
        {
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                return UnsafeByteBufferUtil.GetUnsignedMedium(bytes);
        }

        protected internal override int _GetUnsignedMediumLE(int index)
        {
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                return UnsafeByteBufferUtil.GetUnsignedMediumLE(bytes);
        }

        protected internal override int _GetInt(int index)
        {
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                return UnsafeByteBufferUtil.GetInt(bytes);
        }

        protected internal override int _GetIntLE(int index)
        {
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                return UnsafeByteBufferUtil.GetIntLE(bytes);
        }

        protected internal override long _GetLong(int index)
        {
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                return UnsafeByteBufferUtil.GetLong(bytes);
        }

        protected internal override long _GetLongLE(int index)
        {
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                return UnsafeByteBufferUtil.GetLongLE(bytes);
        }

        public override IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            this.CheckIndex(index, length);
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                UnsafeByteBufferUtil.GetBytes(this, bytes, index, dst, dstIndex, length);
            return this;
        }

        public override IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            this.CheckIndex(index, length);
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                UnsafeByteBufferUtil.GetBytes(this, bytes, index, dst, dstIndex, length);
            return this;
        }

        public override IByteBuffer GetBytes(int index, Stream output, int length)
        {
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                UnsafeByteBufferUtil.GetBytes(this, bytes, index, output, length);
            return this;
        }

        protected internal override void _SetByte(int index, int value) => this.Memory[this.Idx(index)] = unchecked((byte)value);

        protected internal override void _SetShort(int index, int value)
        {
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                UnsafeByteBufferUtil.SetShort(bytes, value);
        }

        protected internal override void _SetShortLE(int index, int value)
        {
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                UnsafeByteBufferUtil.SetShortLE(bytes, value);
        }

        protected internal override void _SetMedium(int index, int value)
        {
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                UnsafeByteBufferUtil.SetMedium(bytes, value);
        }

        protected internal override void _SetMediumLE(int index, int value)
        {
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                UnsafeByteBufferUtil.SetMediumLE(bytes, value);
        }

        protected internal override void _SetInt(int index, int value)
        {
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                UnsafeByteBufferUtil.SetInt(bytes, value);
        }

        protected internal override void _SetIntLE(int index, int value)
        {
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                UnsafeByteBufferUtil.SetIntLE(bytes, value);
        }

        protected internal override void _SetLong(int index, long value)
        {
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                UnsafeByteBufferUtil.SetLong(bytes, value);
        }

        protected internal override void _SetLongLE(int index, long value)
        {
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                UnsafeByteBufferUtil.SetLongLE(bytes, value);
        }

        public override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            this.CheckIndex(index, length);
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                UnsafeByteBufferUtil.SetBytes(this, bytes, index, src, srcIndex, length);
            return this;
        }

        public override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            this.CheckIndex(index, length);
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                UnsafeByteBufferUtil.SetBytes(this, bytes, index, src, srcIndex, length);
            return this;
        }

        public override Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            this.CheckIndex(index, length);
            int read;
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                read = UnsafeByteBufferUtil.SetBytes(this, bytes, index, src, length);
            return TaskEx.FromResult(read);
        }

        public override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            fixed (byte* bytes = &this.Memory[this.Idx(index)])
                return UnsafeByteBufferUtil.Copy(this, bytes, index, length);
        }

        public override int IoBufferCount => 1;

        public override ArraySegment<byte> GetIoBuffer(int index, int length)
        {
            this.CheckIndex(index, length);
            index = this.Idx(index);
            return new ArraySegment<byte>(this.Memory, index, length);
        }

        public override ArraySegment<byte>[] GetIoBuffers(int index, int length) => new[] { this.GetIoBuffer(index, length) };

        public override bool HasArray => true;

        public override byte[] Array
        {
            get
            {
                this.EnsureAccessible();
                return this.Memory;
            }
        }

        public override int ArrayOffset => this.Offset;

        public override bool HasMemoryAddress => true;

        public override ref byte GetPinnableMemoryAddress()
        {
            this.EnsureAccessible();
            return ref this.Memory[this.Offset];
        }

        public override ref byte GetPinnableMemoryOffsetAddress(int elementOffset)
        {
            this.EnsureAccessible();
            return ref this.Memory[this.Idx(elementOffset)];
        }

        public override IntPtr AddressOfPinnedMemory() => IntPtr.Zero;

        public override IByteBuffer SetZero(int index, int length)
        {
            this.CheckIndex(index, length);
            PlatformDependent.Clear(this.Memory, this.Offset + index, length);
            return this;
        }

        public override IByteBuffer WriteZero(int length)
        {
            this.EnsureWritable(length);
            int wIndex = this.WriterIndex;
            PlatformDependent.Clear(this.Memory, this.Offset + wIndex, length);
            this.SetWriterIndex(wIndex + length);
            return this;
        }
    }
}
#endif
