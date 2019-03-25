// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;

    sealed unsafe partial class PooledUnsafeDirectByteBuffer : PooledByteBuffer<byte[]>
    {
        static readonly ThreadLocalPool<PooledUnsafeDirectByteBuffer> Recycler = new ThreadLocalPool<PooledUnsafeDirectByteBuffer>(handle => new PooledUnsafeDirectByteBuffer(handle, 0));

        byte* memoryAddress;

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
            this.InitMemoryAddress();
        }

        internal sealed override void InitUnpooled(PoolChunk<byte[]> chunk, int length)
        {
            base.InitUnpooled(chunk, length);
            this.InitMemoryAddress();
        }

        void InitMemoryAddress()
        {
            this.memoryAddress = (byte*)Unsafe.Add<byte>(this.Origin.ToPointer(), this.Offset);
        }

        public sealed override bool IsDirect => true;

        protected internal sealed override byte _GetByte(int index) => *(this.memoryAddress + index);

        protected internal sealed override short _GetShort(int index) => UnsafeByteBufferUtil.GetShort(this.Addr(index));

        protected internal sealed override short _GetShortLE(int index) => UnsafeByteBufferUtil.GetShortLE(this.Addr(index));

        protected internal sealed override int _GetUnsignedMedium(int index) => UnsafeByteBufferUtil.GetUnsignedMedium(this.Addr(index));

        protected internal sealed override int _GetUnsignedMediumLE(int index) => UnsafeByteBufferUtil.GetUnsignedMediumLE(this.Addr(index));

        protected internal sealed override int _GetInt(int index) => UnsafeByteBufferUtil.GetInt(this.Addr(index));

        protected internal sealed override int _GetIntLE(int index) => UnsafeByteBufferUtil.GetIntLE(this.Addr(index));

        protected internal sealed override long _GetLong(int index) => UnsafeByteBufferUtil.GetLong(this.Addr(index));

        protected internal sealed override long _GetLongLE(int index) => UnsafeByteBufferUtil.GetLongLE(this.Addr(index));

        public sealed override IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            if (null == dst) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dst); }
            this.CheckDstIndex(index, length, dstIndex, dst.Capacity);
            UnsafeByteBufferUtil.GetBytes(this, this.Addr(index), index, dst, dstIndex, length);
            return this;
        }

        public sealed override IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            if (null == dst) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dst); }
            this.CheckDstIndex(index, length, dstIndex, dst.Length);
            UnsafeByteBufferUtil.GetBytes(this.Addr(index), dst, dstIndex, length);
            return this;
        }

        public sealed override IByteBuffer GetBytes(int index, Stream output, int length)
        {
            if (null == output) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.output); }
            this.CheckIndex(index, length);
            //UnsafeByteBufferUtil.GetBytes(this, this.Addr(index), index, output, length);
            // UnsafeByteBufferUtil.GetBytes 多一遍内存拷贝，最终还是调用 stream.write，没啥必要
#if NETCOREAPP
            output.Write(this._GetReadableSpan(index, length));
#else
            output.Write(this.Memory, this.Idx(index), length);
#endif
            return this;
        }

        protected internal sealed override void _SetByte(int index, int value) => *(this.memoryAddress + index) = unchecked((byte)value);

        protected internal sealed override void _SetShort(int index, int value) => UnsafeByteBufferUtil.SetShort(this.Addr(index), value);

        protected internal sealed override void _SetShortLE(int index, int value) => UnsafeByteBufferUtil.SetShortLE(this.Addr(index), value);

        protected internal sealed override void _SetMedium(int index, int value) => UnsafeByteBufferUtil.SetMedium(this.Addr(index), value);

        protected internal sealed override void _SetMediumLE(int index, int value) => UnsafeByteBufferUtil.SetMediumLE(this.Addr(index), value);

        protected internal sealed override void _SetInt(int index, int value) => UnsafeByteBufferUtil.SetInt(this.Addr(index), value);

        protected internal sealed override void _SetIntLE(int index, int value) => UnsafeByteBufferUtil.SetIntLE(this.Addr(index), value);

        protected internal sealed override void _SetLong(int index, long value) => UnsafeByteBufferUtil.SetLong(this.Addr(index), value);

        protected internal sealed override void _SetLongLE(int index, long value) => UnsafeByteBufferUtil.SetLongLE(this.Addr(index), value);

        public sealed override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            if (null == src) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.src); }
            this.CheckSrcIndex(index, length, srcIndex, src.Capacity);
            UnsafeByteBufferUtil.SetBytes(this, this.Addr(index), index, src, srcIndex, length);
            return this;
        }

        public sealed override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            if (null == src) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.src); }
            this.CheckSrcIndex(index, length, srcIndex, src.Length);
            UnsafeByteBufferUtil.SetBytes(this.Addr(index), src, srcIndex, length);
            return this;
        }

        public sealed override Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            if (null == src) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.src); }
            this.CheckIndex(index, length);
            //int read = UnsafeByteBufferUtil.SetBytes(this, this.Addr(index), index, src, length);
            //return Task.FromResult(read);
            int readTotal = 0;
            int read;
#if !NETCOREAPP
            int offset = this.Idx(index);
#endif
            do
            {
#if NETCOREAPP
                read = src.Read(this._GetSpan(index + readTotal, length - readTotal));
#else
                read = src.Read(this.Memory, offset + readTotal, length - readTotal);
#endif
                readTotal += read;
            }
            while (read > 0 && readTotal < length);

#if NET40
            return TaskEx.FromResult(readTotal);
#else
            return Task.FromResult(readTotal);
#endif
        }

        public sealed override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            return UnsafeByteBufferUtil.Copy(this, this.Addr(index), index, length);
        }

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

        public sealed override bool HasMemoryAddress => true;

        public sealed override ref byte GetPinnableMemoryAddress()
        {
            this.EnsureAccessible();
            return ref Unsafe.AsRef<byte>(this.memoryAddress);
        }

        public sealed override IntPtr AddressOfPinnedMemory() => (IntPtr)this.memoryAddress;

        [MethodImpl(InlineMethod.Value)]
        byte* Addr(int index) => this.memoryAddress + index;

        public sealed override IByteBuffer SetZero(int index, int length)
        {
            this.CheckIndex(index, length);
            UnsafeByteBufferUtil.SetZero(this.Addr(index), length);
            return this;
        }

        public sealed override IByteBuffer WriteZero(int length)
        {
            if (0u >= (uint)length) { return this; }

            this.EnsureWritable(length);
            int wIndex = this.WriterIndex;
            this.CheckIndex0(wIndex, length);
            UnsafeByteBufferUtil.SetZero(this.Addr(wIndex), length);
            this.SetWriterIndex(wIndex + length);

            return this;
        }
    }
}
#endif
