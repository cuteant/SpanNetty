// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    sealed partial class ArrayPooledSlicedByteBuffer : AbstractArrayPooledDerivedByteBuffer
    {
        static readonly ThreadLocalPool<ArrayPooledSlicedByteBuffer> Recycler = new ThreadLocalPool<ArrayPooledSlicedByteBuffer>(handle => new ArrayPooledSlicedByteBuffer(handle));

        internal static ArrayPooledSlicedByteBuffer NewInstance(AbstractByteBuffer unwrapped, IByteBuffer wrapped, int index, int length)
        {
            AbstractUnpooledSlicedByteBuffer.CheckSliceOutOfBounds(index, length, unwrapped);
            return NewInstance0(unwrapped, wrapped, index, length);
        }

        static ArrayPooledSlicedByteBuffer NewInstance0(AbstractByteBuffer unwrapped, IByteBuffer wrapped, int adjustment, int length)
        {
            ArrayPooledSlicedByteBuffer slice = Recycler.Take();
            slice.Init<ArrayPooledSlicedByteBuffer>(unwrapped, wrapped, 0, length, length);
            slice.DiscardMarks();
            slice.adjustment = adjustment;

            return slice;
        }

        int adjustment;

        ArrayPooledSlicedByteBuffer(ThreadLocalPool.Handle handle)
            : base(handle)
        {
        }

        public sealed override int Capacity => this.MaxCapacity;

        public sealed override IByteBuffer AdjustCapacity(int newCapacity) => throw new NotSupportedException("sliced buffer");

        public sealed override int ArrayOffset => this.Idx(this.Unwrap().ArrayOffset);

        public sealed override ref byte GetPinnableMemoryAddress() => ref Unsafe.Add(ref this.Unwrap().GetPinnableMemoryAddress(), this.adjustment);

        public sealed override IntPtr AddressOfPinnedMemory()
        {
            IntPtr ptr = this.Unwrap().AddressOfPinnedMemory();
            if (ptr == IntPtr.Zero)
            {
                return ptr;
            }
            return ptr + this.adjustment;
        }

        public sealed override ArraySegment<byte> GetIoBuffer(int index, int length)
        {
            this.CheckIndex0(index, length);
            return this.Unwrap().GetIoBuffer(this.Idx(index), length);
        }

        public sealed override ArraySegment<byte>[] GetIoBuffers(int index, int length)
        {
            this.CheckIndex0(index, length);
            return this.Unwrap().GetIoBuffers(this.Idx(index), length);
        }

        public sealed override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex0(index, length);
            return this.Unwrap().Copy(this.Idx(index), length);
        }

        public sealed override IByteBuffer Slice(int index, int length)
        {
            this.CheckIndex0(index, length);
            return base.Slice(this.Idx(index), length);
        }

        public sealed override IByteBuffer RetainedSlice(int index, int length)
        {
            this.CheckIndex0(index, length);
            return NewInstance0(this.UnwrapCore(), this, this.Idx(index), length);
        }

        public sealed override IByteBuffer Duplicate() => this.Duplicate0().SetIndex(this.Idx(this.ReaderIndex), this.Idx(this.WriterIndex));

        public sealed override IByteBuffer RetainedDuplicate() => ArrayPooledDuplicatedByteBuffer.NewInstance(this.UnwrapCore(), this, this.Idx(this.ReaderIndex), this.Idx(this.WriterIndex));

        public sealed override byte GetByte(int index)
        {
            this.CheckIndex0(index, 1);
            return this.Unwrap().GetByte(this.Idx(index));
        }

        protected internal sealed override byte _GetByte(int index) => this.UnwrapCore()._GetByte(this.Idx(index));

        public sealed override short GetShort(int index)
        {
            this.CheckIndex0(index, 2);
            return this.Unwrap().GetShort(this.Idx(index));
        }

        protected internal sealed override short _GetShort(int index) => this.UnwrapCore()._GetShort(this.Idx(index));

        public sealed override short GetShortLE(int index)
        {
            this.CheckIndex0(index, 2);
            return this.Unwrap().GetShortLE(this.Idx(index));
        }

        protected internal sealed override short _GetShortLE(int index) => this.UnwrapCore()._GetShortLE(this.Idx(index));

        public sealed override int GetUnsignedMedium(int index)
        {
            this.CheckIndex0(index, 3);
            return this.Unwrap().GetUnsignedMedium(this.Idx(index));
        }

        protected internal sealed override int _GetUnsignedMedium(int index) => this.UnwrapCore()._GetUnsignedMedium(this.Idx(index));

        public sealed override int GetUnsignedMediumLE(int index)
        {
            this.CheckIndex0(index, 3);
            return this.Unwrap().GetUnsignedMediumLE(this.Idx(index));
        }

        protected internal sealed override int _GetUnsignedMediumLE(int index) => this.UnwrapCore()._GetUnsignedMediumLE(this.Idx(index));

        public sealed override int GetInt(int index)
        {
            this.CheckIndex0(index, 4);
            return this.Unwrap().GetInt(this.Idx(index));
        }

        protected internal sealed override int _GetInt(int index) => this.UnwrapCore()._GetInt(this.Idx(index));

        public sealed override int GetIntLE(int index)
        {
            this.CheckIndex0(index, 4);
            return this.Unwrap().GetIntLE(this.Idx(index));
        }

        protected internal sealed override int _GetIntLE(int index) => this.UnwrapCore()._GetIntLE(this.Idx(index));

        public sealed override long GetLong(int index)
        {
            this.CheckIndex0(index, 8);
            return this.Unwrap().GetLong(this.Idx(index));
        }

        protected internal sealed override long _GetLong(int index) => this.UnwrapCore()._GetLong(this.Idx(index));

        public sealed override long GetLongLE(int index)
        {
            this.CheckIndex0(index, 8);
            return this.Unwrap().GetLongLE(this.Idx(index));
        }

        protected internal sealed override long _GetLongLE(int index) => this.UnwrapCore()._GetLongLE(this.Idx(index));

        public sealed override IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            this.CheckIndex0(index, length);
            this.Unwrap().GetBytes(this.Idx(index), dst, dstIndex, length);
            return this;
        }

        public sealed override IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            this.CheckIndex0(index, length);
            this.Unwrap().GetBytes(this.Idx(index), dst, dstIndex, length);
            return this;
        }

        public sealed override IByteBuffer SetByte(int index, int value)
        {
            this.CheckIndex0(index, 1);
            this.Unwrap().SetByte(this.Idx(index), value);
            return this;
        }

        protected internal sealed override void _SetByte(int index, int value) => this.UnwrapCore()._SetByte(this.Idx(index), value);

        public sealed override IByteBuffer SetShort(int index, int value)
        {
            this.CheckIndex0(index, 2);
            this.Unwrap().SetShort(this.Idx(index), value);
            return this;
        }

        protected internal sealed override void _SetShort(int index, int value) => this.UnwrapCore()._SetShort(this.Idx(index), value);

        public sealed override IByteBuffer SetShortLE(int index, int value)
        {
            this.CheckIndex0(index, 2);
            this.Unwrap().SetShortLE(this.Idx(index), value);
            return this;
        }

        protected internal sealed override void _SetShortLE(int index, int value) => this.UnwrapCore()._SetShortLE(this.Idx(index), value);

        public sealed override IByteBuffer SetMedium(int index, int value)
        {
            this.CheckIndex0(index, 3);
            this.Unwrap().SetMedium(this.Idx(index), value);
            return this;
        }

        protected internal sealed override void _SetMedium(int index, int value) => this.UnwrapCore()._SetMedium(this.Idx(index), value);

        public sealed override IByteBuffer SetMediumLE(int index, int value)
        {
            this.CheckIndex0(index, 3);
            this.Unwrap().SetMediumLE(this.Idx(index), value);
            return this;
        }

        protected internal sealed override void _SetMediumLE(int index, int value) => this.UnwrapCore()._SetMediumLE(this.Idx(index), value);

        public sealed override IByteBuffer SetInt(int index, int value)
        {
            this.CheckIndex0(index, 4);
            this.Unwrap().SetInt(this.Idx(index), value);
            return this;
        }

        protected internal sealed override void _SetInt(int index, int value) => this.UnwrapCore()._SetInt(this.Idx(index), value);

        public sealed override IByteBuffer SetIntLE(int index, int value)
        {
            this.CheckIndex0(index, 4);
            this.Unwrap().SetIntLE(this.Idx(index), value);
            return this;
        }

        protected internal sealed override void _SetIntLE(int index, int value) => this.UnwrapCore()._SetIntLE(this.Idx(index), value);

        public sealed override IByteBuffer SetLong(int index, long value)
        {
            this.CheckIndex0(index, 8);
            this.Unwrap().SetLong(this.Idx(index), value);
            return this;
        }

        protected internal sealed override void _SetLong(int index, long value) => this.UnwrapCore()._SetLong(this.Idx(index), value);

        public sealed override IByteBuffer SetLongLE(int index, long value)
        {
            this.CheckIndex0(index, 8);
            this.Unwrap().SetLongLE(this.Idx(index), value);
            return this;
        }

        protected internal sealed override void _SetLongLE(int index, long value) => this.UnwrapCore()._SetLongLE(this.Idx(index), value);

        public sealed override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            this.CheckIndex0(index, length);
            this.Unwrap().SetBytes(this.Idx(index), src, srcIndex, length);
            return this;
        }

        public sealed override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            this.CheckIndex0(index, length);
            this.Unwrap().SetBytes(this.Idx(index), src, srcIndex, length);
            return this;
        }

        public sealed override IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            this.CheckIndex0(index, length);
            return this.Unwrap().GetBytes(this.Idx(index), destination, length);
        }

        public sealed override Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            this.CheckIndex0(index, length);
            return this.Unwrap().SetBytesAsync(this.Idx(index), src, length, cancellationToken);
        }

        public sealed override int ForEachByte(int index, int length, IByteProcessor processor)
        {
            this.CheckIndex0(index, length);
            int ret = this.Unwrap().ForEachByte(this.Idx(index), length, processor);
            if (ret < this.adjustment)
            {
                return IndexNotFound;
            }
            return ret - this.adjustment;
        }

        public sealed override int ForEachByteDesc(int index, int length, IByteProcessor processor)
        {
            this.CheckIndex0(index, length);
            int ret = this.Unwrap().ForEachByteDesc(this.Idx(index), length, processor);
            if (ret < this.adjustment)
            {
                return IndexNotFound;
            }
            return ret - this.adjustment;
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        int Idx(int index) => index + this.adjustment;
    }
}
