// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Internal;

    partial class UnpooledHeapByteBuffer : AbstractReferenceCountedByteBuffer
    {
        readonly IByteBufferAllocator allocator;
        byte[] array;

        protected internal UnpooledHeapByteBuffer(IByteBufferAllocator alloc, int initialCapacity, int maxCapacity)
            : base(maxCapacity)
        {
            if (alloc is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.alloc); }
            if (initialCapacity > maxCapacity) { ThrowHelper.ThrowArgumentException_InitialCapacityMaxCapacity(initialCapacity, maxCapacity); }

            this.allocator = alloc;
            this.SetArray(this.NewArray(initialCapacity));
            this.SetIndex0(0, 0);
        }

        protected internal UnpooledHeapByteBuffer(IByteBufferAllocator alloc, byte[] initialArray, int maxCapacity)
            : base(maxCapacity)
        {
            if (alloc is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.alloc); }
            if (initialArray is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.initialArray); }

            if (initialArray.Length > maxCapacity)
            {
                ThrowHelper.ThrowArgumentException_InitialCapacity(initialArray.Length, maxCapacity);
            }

            this.allocator = alloc;
            this.SetArray(initialArray);
            this.SetIndex0(0, initialArray.Length);
        }

        protected virtual byte[] AllocateArray(int initialCapacity) => this.NewArray(initialCapacity);

        protected byte[] NewArray(int initialCapacity) => new byte[initialCapacity];

        protected virtual void FreeArray(byte[] bytes)
        {
            // NOOP
        }

        protected void SetArray(byte[] initialArray) => this.array = initialArray;

        public sealed override IByteBufferAllocator Allocator => this.allocator;

        public sealed override bool IsDirect => false;

        public sealed override int Capacity
        {
            [System.Runtime.CompilerServices.MethodImpl(InlineMethod.AggressiveInlining)]
            get
            {
                return this.array.Length;
            }
        }

        public sealed override IByteBuffer AdjustCapacity(int newCapacity)
        {
            this.CheckNewCapacity(newCapacity);

            uint unewCapacity = (uint)newCapacity;
            byte[] oldArray = this.array;
            uint oldCapacity = (uint)oldArray.Length;
            if (oldCapacity == unewCapacity)
            {
                return this;
            }

            int bytesToCopy;
            if (unewCapacity > oldCapacity)
            {
                bytesToCopy = oldArray.Length;
            }
            else
            {
                this.TrimIndicesToCapacity(newCapacity);
                bytesToCopy = newCapacity;
            }
            byte[] newArray = this.AllocateArray(newCapacity);
            PlatformDependent.CopyMemory(oldArray, 0, newArray, 0, bytesToCopy);
            this.SetArray(newArray);
            this.FreeArray(oldArray);
            return this;
        }

        public sealed override bool HasArray => true;

        public sealed override byte[] Array
        {
            get
            {
                this.EnsureAccessible();
                return this.array;
            }
        }

        public sealed override int ArrayOffset => 0;

        public sealed override bool HasMemoryAddress => true;

        public sealed override ref byte GetPinnableMemoryAddress()
        {
            this.EnsureAccessible();
            return ref this.array[0];
        }

        public sealed override IntPtr AddressOfPinnedMemory() => IntPtr.Zero;

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
                dst.SetBytes(dstIndex, this.array, index, length);
            }

            return this;
        }

        public sealed override IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            if (dst is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dst); }
            this.CheckDstIndex(index, length, dstIndex, dst.Length);
            PlatformDependent.CopyMemory(this.array, index, dst, dstIndex, length);
            return this;
        }

        public sealed override IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            if (destination is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.destination); }
            this.CheckIndex(index, length);
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
            destination.Write(new ReadOnlySpan<byte>(this.array, index, length));
#else
            destination.Write(this.array, index, length);
#endif
            return this;
        }

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
                src.GetBytes(srcIndex, this.array, index, length);
            }
            return this;
        }

        public sealed override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            if (src is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.src); }
            this.CheckSrcIndex(index, length, srcIndex, src.Length);
            PlatformDependent.CopyMemory(src, srcIndex, this.array, index, length);
            return this;
        }

        public sealed override Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            if (src is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.src); }
            this.CheckIndex(index, length);

            int readTotal = 0;
            int read;
            do
            {
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
                read = src.Read(new Span<byte>(this.array, index + readTotal, length - readTotal));
#else
                read = src.Read(this.array, index + readTotal, length - readTotal);
#endif
                readTotal += read;
            }
            while (read > 0 && readTotal < length);

            return Task.FromResult(readTotal);
        }

        public sealed override bool IsSingleIoBuffer => true;

        public sealed override int IoBufferCount => 1;

        public sealed override ArraySegment<byte> GetIoBuffer(int index, int length)
        {
            this.CheckIndex(index, length);
            return new ArraySegment<byte>(this.array, index, length);
        }

        public sealed override ArraySegment<byte>[] GetIoBuffers(int index, int length) => new[] { this.GetIoBuffer(index, length) };

        //public sealed override byte GetByte(int index)
        //{
        //    this.EnsureAccessible();
        //    return this._GetByte(index);
        //}

        protected internal sealed override byte _GetByte(int index) => HeapByteBufferUtil.GetByte(this.array, index);

        public sealed override IByteBuffer SetZero(int index, int length)
        {
            this.CheckIndex(index, length);
            PlatformDependent.Clear(this.array, index, length);
            return this;
        }

        //public sealed override short GetShort(int index)
        //{
        //    this.EnsureAccessible();
        //    return this._GetShort(index);
        //}

        protected internal sealed override short _GetShort(int index) => HeapByteBufferUtil.GetShort(this.array, index);

        //public sealed override short GetShortLE(int index)
        //{
        //    this.EnsureAccessible();
        //    return this._GetShortLE(index);
        //}

        protected internal sealed override short _GetShortLE(int index) => HeapByteBufferUtil.GetShortLE(this.array, index);

        //public override int GetUnsignedMedium(int index)
        //{
        //    this.EnsureAccessible();
        //    return this._GetUnsignedMedium(index);
        //}

        protected internal sealed override int _GetUnsignedMedium(int index) => HeapByteBufferUtil.GetUnsignedMedium(this.array, index);

        //public sealed override int GetUnsignedMediumLE(int index)
        //{
        //    this.EnsureAccessible();
        //    return this._GetUnsignedMediumLE(index);
        //}

        protected internal sealed override int _GetUnsignedMediumLE(int index) => HeapByteBufferUtil.GetUnsignedMediumLE(this.array, index);

        //public sealed override int GetInt(int index)
        //{
        //    this.EnsureAccessible();
        //    return this._GetInt(index);
        //}

        protected internal sealed override int _GetInt(int index) => HeapByteBufferUtil.GetInt(this.array, index);

        //public sealed override int GetIntLE(int index)
        //{
        //    this.EnsureAccessible();
        //    return this._GetIntLE(index);
        //}

        protected internal sealed override int _GetIntLE(int index) => HeapByteBufferUtil.GetIntLE(this.array, index);

        //public sealed override long GetLong(int index)
        //{
        //    this.EnsureAccessible();
        //    return this._GetLong(index);
        //}

        protected internal sealed override long _GetLong(int index) => HeapByteBufferUtil.GetLong(this.array, index);

        //public sealed override long GetLongLE(int index)
        //{
        //    this.EnsureAccessible();
        //    return this._GetLongLE(index);
        //}

        protected internal sealed override long _GetLongLE(int index) => HeapByteBufferUtil.GetLongLE(this.array, index);

        //public sealed override IByteBuffer SetByte(int index, int value)
        //{
        //    this.EnsureAccessible();
        //    this._SetByte(index, value);
        //    return this;
        //}

        protected internal sealed override void _SetByte(int index, int value) => HeapByteBufferUtil.SetByte(this.array, index, value);

        //public sealed override IByteBuffer SetShort(int index, int value)
        //{
        //    this.EnsureAccessible();
        //    this._SetShort(index, value);
        //    return this;
        //}

        protected internal sealed override void _SetShort(int index, int value) => HeapByteBufferUtil.SetShort(this.array, index, value);

        //public sealed override IByteBuffer SetShortLE(int index, int value)
        //{
        //    this.EnsureAccessible();
        //    this._SetShortLE(index, value);
        //    return this;
        //}

        protected internal sealed override void _SetShortLE(int index, int value) => HeapByteBufferUtil.SetShortLE(this.array, index, value);

        //public sealed override IByteBuffer SetMedium(int index, int value)
        //{
        //    this.EnsureAccessible();
        //    this._SetMedium(index, value);
        //    return this;
        //}

        protected internal sealed override void _SetMedium(int index, int value) => HeapByteBufferUtil.SetMedium(this.array, index, value);

        //public sealed override IByteBuffer SetMediumLE(int index, int value)
        //{
        //    this.EnsureAccessible();
        //    this._SetMediumLE(index, value);
        //    return this;
        //}

        protected internal sealed override void _SetMediumLE(int index, int value) => HeapByteBufferUtil.SetMediumLE(this.array, index, value);

        //public sealed override IByteBuffer SetInt(int index, int value)
        //{
        //    this.EnsureAccessible();
        //    this._SetInt(index, value);
        //    return this;
        //}

        protected internal sealed override void _SetInt(int index, int value) => HeapByteBufferUtil.SetInt(this.array, index, value);

        //public sealed override IByteBuffer SetIntLE(int index, int value)
        //{
        //    this.EnsureAccessible();
        //    this._SetIntLE(index, value);
        //    return this;
        //}

        protected internal sealed override void _SetIntLE(int index, int value) => HeapByteBufferUtil.SetIntLE(this.array, index, value);

        //public sealed override IByteBuffer SetLong(int index, long value)
        //{
        //    this.EnsureAccessible();
        //    this._SetLong(index, value);
        //    return this;
        //}

        protected internal sealed override void _SetLong(int index, long value) => HeapByteBufferUtil.SetLong(this.array, index, value);

        //public sealed override IByteBuffer SetLongLE(int index, long value)
        //{
        //    this.EnsureAccessible();
        //    this._SetLongLE(index, value);
        //    return this;
        //}

        protected internal sealed override void _SetLongLE(int index, long value) => HeapByteBufferUtil.SetLongLE(this.array, index, value);

        public sealed override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            return this.allocator.HeapBuffer(length, this.MaxCapacity).WriteBytes(this.array, index, length);
        }

        protected internal sealed override void Deallocate()
        {
            this.FreeArray(this.array);
            this.array = EmptyArrays.EmptyBytes;
        }

        public sealed override IByteBuffer Unwrap() => null;

        public sealed override IByteBuffer WriteZero(int length)
        {
            if (0u >= (uint)length) { return this; }

            this.EnsureWritable(length);
            int wIndex = this.WriterIndex;
            this.CheckIndex0(wIndex, length);
            PlatformDependent.Clear(this.array, wIndex, length);
            this.SetWriterIndex(wIndex + length);

            return this;
        }
    }
}