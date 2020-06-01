// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Internal;

    unsafe partial class UnpooledUnsafeDirectByteBuffer : AbstractReferenceCountedByteBuffer
    {
        readonly IByteBufferAllocator allocator;

        int capacity;
        bool doNotFree;
        byte[] buffer;

        public UnpooledUnsafeDirectByteBuffer(IByteBufferAllocator alloc, int initialCapacity, int maxCapacity)
            : base(maxCapacity)
        {
            if (alloc is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.alloc); }
            if (initialCapacity < 0) { ThrowHelper.ThrowArgumentException_PositiveOrZero(initialCapacity, ExceptionArgument.initialCapacity); }
            if (maxCapacity < 0) { ThrowHelper.ThrowArgumentException_PositiveOrZero(maxCapacity, ExceptionArgument.maxCapacity); }

            if (initialCapacity > maxCapacity)
            {
                ThrowHelper.ThrowArgumentException_InitialCapacity(initialCapacity, maxCapacity);
            }

            this.allocator = alloc;
            this.SetByteBuffer(this.NewArray(initialCapacity), false);
        }

        protected UnpooledUnsafeDirectByteBuffer(IByteBufferAllocator alloc, byte[] initialBuffer, int maxCapacity, bool doFree)
            : base(maxCapacity)
        {
            if (alloc is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.alloc); }
            if (initialBuffer is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.initialBuffer); }

            int initialCapacity = initialBuffer.Length;
            if (initialCapacity > maxCapacity)
            {
                ThrowHelper.ThrowArgumentException_InitialCapacity(initialCapacity, maxCapacity);
            }

            this.allocator = alloc;
            this.doNotFree = !doFree;
            this.SetByteBuffer(initialBuffer, false);
        }

        protected virtual byte[] AllocateDirect(int initialCapacity) => this.NewArray(initialCapacity);

        protected byte[] NewArray(int initialCapacity) => new byte[initialCapacity];

        protected virtual void FreeDirect(byte[] array)
        {
            // NOOP rely on GC.
        }

        void SetByteBuffer(byte[] array, bool tryFree)
        {
            if (tryFree)
            {
                byte[] oldBuffer = this.buffer;
                if (oldBuffer is object)
                {
                    if (this.doNotFree)
                    {
                        this.doNotFree = false;
                    }
                    else
                    {
                        this.FreeDirect(oldBuffer);
                    }
                }
            }
            this.buffer = array;
            this.capacity = array.Length;
        }

        public sealed override bool IsDirect => true;

        public sealed override int Capacity => this.capacity;

        public sealed override IByteBuffer AdjustCapacity(int newCapacity)
        {
            this.CheckNewCapacity(newCapacity);

            uint unewCapacity = (uint)newCapacity;
            uint oldCapacity = (uint)this.capacity;
            if (oldCapacity == unewCapacity)
            {
                return this;
            }
            int bytesToCopy;
            if (unewCapacity > oldCapacity)
            {
                bytesToCopy = this.capacity;
            }
            else
            {
                this.TrimIndicesToCapacity(newCapacity);
                bytesToCopy = newCapacity;
            }
            byte[] oldBuffer = this.buffer;
            byte[] newBuffer = this.AllocateDirect(newCapacity);
            PlatformDependent.CopyMemory(oldBuffer, 0, newBuffer, 0, bytesToCopy);
            this.SetByteBuffer(newBuffer, true);
            return this;
        }

        public sealed override IByteBufferAllocator Allocator => this.allocator;

        public sealed override bool HasArray => true;

        public sealed override byte[] Array
        {
            get
            {
                this.EnsureAccessible();
                return this.buffer;
            }
        }

        public sealed override int ArrayOffset => 0;

        public sealed override bool IsSingleIoBuffer => true;

        public sealed override int IoBufferCount => 1;

        public sealed override ArraySegment<byte> GetIoBuffer(int index, int length)
        {
            this.CheckIndex(index, length);
            return new ArraySegment<byte>(this.buffer, index, length);
        }

        public sealed override ArraySegment<byte>[] GetIoBuffers(int index, int length) => new[] { this.GetIoBuffer(index, length) };

        protected internal sealed override void Deallocate()
        {
            byte[] buf = this.buffer;
            if (buf is null)
            {
                return;
            }

            this.buffer = null;

            if (!this.doNotFree)
            {
                this.FreeDirect(buf);
            }
        }

        public sealed override IByteBuffer Unwrap() => null;

        protected internal sealed override byte _GetByte(int index) => this.buffer[index];

        protected internal sealed override void _SetByte(int index, int value) => this.buffer[index] = unchecked((byte)value);

        public sealed override IntPtr AddressOfPinnedMemory() => IntPtr.Zero;

        public sealed override bool HasMemoryAddress => true;

        public sealed override ref byte GetPinnableMemoryAddress()
        {
            this.EnsureAccessible();
            return ref this.buffer[0];
        }

        protected internal sealed override short _GetShort(int index)
        {
            fixed (byte* addr = &this.Addr(index))
                return UnsafeByteBufferUtil.GetShort(addr);
        }

        protected internal sealed override short _GetShortLE(int index)
        {
            fixed (byte* addr = &this.Addr(index))
                return UnsafeByteBufferUtil.GetShortLE(addr);
        }

        protected internal sealed override int _GetUnsignedMedium(int index)
        {
            fixed (byte* addr = &this.Addr(index))
                return UnsafeByteBufferUtil.GetUnsignedMedium(addr);
        }

        protected internal sealed override int _GetUnsignedMediumLE(int index)
        {
            fixed (byte* addr = &this.Addr(index))
                return UnsafeByteBufferUtil.GetUnsignedMediumLE(addr);
        }

        protected internal sealed override int _GetInt(int index)
        {
            fixed (byte* addr = &this.Addr(index))
                return UnsafeByteBufferUtil.GetInt(addr);
        }

        protected internal sealed override int _GetIntLE(int index)
        {
            fixed (byte* addr = &this.Addr(index))
                return UnsafeByteBufferUtil.GetIntLE(addr);
        }

        protected internal sealed override long _GetLong(int index)
        {
            fixed (byte* addr = &this.Addr(index))
                return UnsafeByteBufferUtil.GetLong(addr);
        }

        protected internal sealed override long _GetLongLE(int index)
        {
            fixed (byte* addr = &this.Addr(index))
                return UnsafeByteBufferUtil.GetLongLE(addr);
        }

        public sealed override IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            if (dst is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dst); }
            this.CheckDstIndex(index, length, dstIndex, dst.Capacity);
            fixed (byte* addr = &this.Addr(index))
            {
                UnsafeByteBufferUtil.GetBytes(this, addr, index, dst, dstIndex, length);
                return this;
            }
        }

        public sealed override IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            if (dst is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dst); }
            this.CheckDstIndex(index, length, dstIndex, dst.Length);
            fixed (byte* addr = &this.Addr(index))
            {
                UnsafeByteBufferUtil.GetBytes(addr, dst, dstIndex, length);
                return this;
            }
        }

        protected internal sealed override void _SetShort(int index, int value)
        {
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.SetShort(addr, value);
        }

        protected internal sealed override void _SetShortLE(int index, int value)
        {
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.SetShortLE(addr, value);
        }

        protected internal sealed override void _SetMedium(int index, int value)
        {
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.SetMedium(addr, value);
        }

        protected internal sealed override void _SetMediumLE(int index, int value)
        {
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.SetMediumLE(addr, value);
        }

        protected internal sealed override void _SetInt(int index, int value)
        {
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.SetInt(addr, value);
        }

        protected internal sealed override void _SetIntLE(int index, int value)
        {
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.SetIntLE(addr, value);
        }

        protected internal sealed override void _SetLong(int index, long value)
        {
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.SetLong(addr, value);
        }

        protected internal sealed override void _SetLongLE(int index, long value)
        {
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.SetLongLE(addr, value);
        }

        public sealed override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            if (src is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.src); }
            this.CheckSrcIndex(index, length, srcIndex, src.Capacity);
            fixed (byte* addr = &this.Addr(index))
            {
                UnsafeByteBufferUtil.SetBytes(this, addr, index, src, srcIndex, length);
                return this;
            }
        }

        public sealed override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            if (src is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.src); }
            this.CheckSrcIndex(index, length, srcIndex, src.Length);
            if (length != 0)
            {
                fixed (byte* addr = &this.Addr(index))
                {
                    UnsafeByteBufferUtil.SetBytes(addr, src, srcIndex, length);
                    return this;
                }
            }
            return this;
        }

        public sealed override IByteBuffer GetBytes(int index, Stream output, int length)
        {
            if (output is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.output); }
            this.CheckIndex(index, length);
            //fixed (byte* addr = &this.Addr(index))
            //{
            //    UnsafeByteBufferUtil.GetBytes(this, addr, index, output, length);
            //    return this;
            //}
            // UnsafeByteBufferUtil.GetBytes 多一遍内存拷贝，最终还是调用 stream.write，没啥必要
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
            output.Write(new ReadOnlySpan<byte>(this.buffer, index, length));
#else
            output.Write(this.buffer, index, length);
#endif
            return this;
        }

        public sealed override Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            if (src is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.src); }
            this.CheckIndex(index, length);
            //int read;
            //fixed (byte* addr = &this.Addr(index))
            //{
            //    read = UnsafeByteBufferUtil.SetBytes(this, addr, index, src, length);

            //    // See https://github.com/Azure/DotNetty/issues/436
            //    return Task.FromResult(read);
            //}
            int readTotal = 0;
            int read;
            do
            {
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
                read = src.Read(new Span<byte>(this.buffer, index + readTotal, length - readTotal));
#else
                read = src.Read(this.buffer, index + readTotal, length - readTotal);
#endif
                readTotal += read;
            }
            while (read > 0 && readTotal < length);

            return Task.FromResult(readTotal);
        }

        public sealed override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            fixed (byte* addr = &this.Addr(index))
                return UnsafeByteBufferUtil.Copy(this, addr, index, length);
        }

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        ref byte Addr(int index) => ref this.buffer[index];

        public sealed override IByteBuffer SetZero(int index, int length)
        {
            this.CheckIndex(index, length);
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.SetZero(addr, length);
            return this;
        }

        public sealed override IByteBuffer WriteZero(int length)
        {
            if (0u >= (uint)length) { return this; }

            this.EnsureWritable(length);
            int wIndex = this.WriterIndex;
            this.CheckIndex0(wIndex, length);
            fixed (byte* addr = &this.Addr(wIndex))
                UnsafeByteBufferUtil.SetZero(addr, length);
            this.SetWriterIndex(wIndex + length);

            return this;
        }
    }
}