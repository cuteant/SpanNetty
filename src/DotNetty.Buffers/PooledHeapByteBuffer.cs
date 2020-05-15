// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Internal;

    sealed partial class PooledHeapByteBuffer : PooledByteBuffer<byte[]>
    {
        static readonly ThreadLocalPool<PooledHeapByteBuffer> Recycler = new ThreadLocalPool<PooledHeapByteBuffer>(handle => new PooledHeapByteBuffer(handle, 0));

        internal static PooledHeapByteBuffer NewInstance(int maxCapacity)
        {
            PooledHeapByteBuffer buf = Recycler.Take();
            buf.Reuse(maxCapacity);
            return buf;
        }

        internal PooledHeapByteBuffer(ThreadLocalPool.Handle recyclerHandle, int maxCapacity)
            : base(recyclerHandle, maxCapacity)
        {
        }

        public sealed override bool IsDirect => false;

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
            if (dst is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dst); }
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
            if (dst is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dst); }
            this.CheckDstIndex(index, length, dstIndex, dst.Length);
            PlatformDependent.CopyMemory(this.Memory, this.Idx(index), dst, dstIndex, length);
            return this;
        }

        public sealed override IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            if (destination is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.destination); }
            this.CheckIndex(index, length);
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
            destination.Write(new ReadOnlySpan<byte>(this.Memory, this.Idx(index), length));
#else
            destination.Write(this.Memory, this.Idx(index), length);
#endif
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
            if (src is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.src); }
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

        public sealed override Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            if (src is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.src); }
            this.CheckIndex(index, length);

            int readTotal = 0;
            int read;
            int offset = this.Idx(index);
            do
            {
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
                read = src.Read(new Span<byte>(this.Memory, offset + readTotal, length - readTotal));
#else
                read = src.Read(this.Memory, offset + readTotal, length - readTotal);
#endif
                readTotal += read;
            }
            while (read > 0 && readTotal < length);

            return Task.FromResult(readTotal);
        }

        public sealed override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            if (src is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.src); }
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

        public sealed override bool IsSingleIoBuffer => true;

        public sealed override int IoBufferCount => 1;

        public sealed override ArraySegment<byte> GetIoBuffer(int index, int length)
        {
            this.CheckIndex(index, length);
            index = index + this.Offset;
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
            return ref this.Memory[this.Offset];
        }

        public sealed override IntPtr AddressOfPinnedMemory() => IntPtr.Zero;

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