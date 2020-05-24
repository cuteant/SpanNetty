// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    internal interface IPooledByteBuffer
    {
        long Handle { get; }
        int MaxLength { get; }
    }

    abstract class PooledByteBuffer<T> : AbstractReferenceCountedByteBuffer, IPooledByteBuffer
    {
        readonly ThreadLocalPool.Handle recyclerHandle;

        protected internal PoolChunk<T> Chunk;
        protected internal long Handle;
        protected internal T Memory;
        protected internal int Offset;
        protected internal int Length;
        protected internal IntPtr Origin;
        internal int MaxLength;
        internal PoolThreadCache<T> Cache;
        PooledByteBufferAllocator allocator;

        protected PooledByteBuffer(ThreadLocalPool.Handle recyclerHandle, int maxCapacity)
            : base(maxCapacity)
        {
            this.recyclerHandle = recyclerHandle;
        }

        internal virtual void Init(PoolChunk<T> chunk, long handle, int offset, int length, int maxLength, PoolThreadCache<T> cache) =>
            this.Init0(chunk, handle, offset, length, maxLength, cache);

        internal virtual void InitUnpooled(PoolChunk<T> chunk, int length) => this.Init0(chunk, 0, 0, length, length, null);

        unsafe void Init0(PoolChunk<T> chunk, long handle, int offset, int length, int maxLength, PoolThreadCache<T> cache)
        {
            Debug.Assert(handle >= 0);
            Debug.Assert(chunk is object);

            this.Chunk = chunk;
            this.Memory = chunk.Memory;
            this.allocator = chunk.Arena.Parent;
            this.Origin = chunk.NativePointer;
            this.Cache = cache;
            this.Handle = handle;
            this.Offset = offset;
            this.Length = length;
            this.MaxLength = maxLength;
        }

        long IPooledByteBuffer.Handle => this.Handle;
        int IPooledByteBuffer.MaxLength => this.MaxLength;

        /**
          * Method must be called before reuse this {@link PooledByteBufAllocator}
          */
        internal void Reuse(int maxCapacity)
        {
            this.SetMaxCapacity(maxCapacity);
            this.ResetReferenceCount();
            this.SetIndex0(0, 0);
            this.DiscardMarks();
        }

        public override int Capacity
        {
            [MethodImpl(InlineMethod.AggressiveInlining)]
            get => this.Length;
        }

        public override int MaxFastWritableBytes => Math.Min(this.MaxLength, this.MaxCapacity) - this.WriterIndex;

        public sealed override IByteBuffer AdjustCapacity(int newCapacity)
        {
            uint uLength = (uint)this.Length;
            uint unewCapacity = (uint)newCapacity;
            if (unewCapacity == uLength)
            {
                this.EnsureAccessible();
                return this;
            }

            this.CheckNewCapacity(newCapacity);

            if (!this.Chunk.Unpooled)
            {
                uint uMaxLength = (uint)this.MaxLength;
                // If the request capacity does not require reallocation, just update the length of the memory.
                if (unewCapacity > uLength)
                {
                    if (unewCapacity <= uMaxLength)
                    {
                        this.Length = newCapacity;
                        return this;
                    }
                }
                else if (unewCapacity > (uint)this.MaxLength.RightUShift(1)
                    && (uMaxLength > 512u || unewCapacity > uMaxLength - 16u))
                {
                    // here newCapacity < length
                    this.Length = newCapacity;
                    this.SetIndex(Math.Min(this.ReaderIndex, newCapacity), Math.Min(this.WriterIndex, newCapacity));
                    return this;
                }
            }

            // Reallocation required.
            this.Chunk.Arena.Reallocate(this, newCapacity, true);
            return this;
        }

        public sealed override IByteBufferAllocator Allocator => this.allocator;

        public sealed override IByteBuffer Unwrap() => null;

        public sealed override IByteBuffer RetainedDuplicate() => PooledDuplicatedByteBuffer.NewInstance(this, this, this.ReaderIndex, this.WriterIndex);

        public sealed override IByteBuffer RetainedSlice()
        {
            int index = this.ReaderIndex;
            return this.RetainedSlice(index, this.WriterIndex - index);
        }

        public sealed override IByteBuffer RetainedSlice(int index, int length) => PooledSlicedByteBuffer.NewInstance(this, this, index, length);

        protected internal sealed override void Deallocate()
        {
            if (this.Handle >= 0)
            {
                long handle = this.Handle;
                this.Handle = -1;
                this.Origin = IntPtr.Zero;
                this.Memory = default;
                this.Chunk.Arena.Free(this.Chunk, handle, this.MaxLength, this.Cache);
                this.Chunk = null;
                this.Recycle();
            }
        }

        void Recycle() => this.recyclerHandle.Release(this);

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        protected int Idx(int index) => this.Offset + index;
    }
}