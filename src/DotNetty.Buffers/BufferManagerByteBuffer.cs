using System;
using CuteAnt.Buffers;
using DotNetty.Common;
using DotNetty.Common.Internal;

namespace DotNetty.Buffers
{
    abstract partial class BufferManagerByteBuffer : AbstractReferenceCountedByteBuffer
    {
        readonly ThreadLocalPool.Handle recyclerHandle;

        protected internal byte[] Memory;
        protected BufferManagerByteBufferAllocator _allocator;
        BufferManager _bufferMannager;

        protected BufferManagerByteBuffer(ThreadLocalPool.Handle recyclerHandle, int maxCapacity)
            : base(maxCapacity)
        {
            this.recyclerHandle = recyclerHandle;
        }

        /// <summary>Method must be called before reuse this {@link BufferManagerByteBufAllocator}.</summary>
        /// <param name="allocator"></param>
        /// <param name="initialCapacity"></param>
        /// <param name="maxCapacity"></param>
        /// <param name="bufferManager"></param>
        internal void Reuse(BufferManagerByteBufferAllocator allocator, BufferManager bufferManager, int initialCapacity, int maxCapacity)
        {
            _allocator = allocator;
            _bufferMannager = bufferManager;
            Memory = AllocateArray(initialCapacity);

            this.SetMaxCapacity(maxCapacity);
            this.SetReferenceCount(1);
            this.SetIndex0(0, 0);
            this.DiscardMarks();
        }

        internal void Reuse(BufferManagerByteBufferAllocator allocator, BufferManager bufferManager, byte[] buffer, int length, int maxCapacity)
        {
            _allocator = allocator;
            _bufferMannager = bufferManager;
            Memory = buffer;

            this.SetMaxCapacity(maxCapacity);
            this.SetReferenceCount(1);
            this.SetIndex0(0, length);
            this.DiscardMarks();
        }

        public override int Capacity => Memory.Length;

        protected virtual byte[] AllocateArray(int initialCapacity) => _bufferMannager.TakeBuffer(initialCapacity);

        protected virtual void FreeArray(byte[] bytes)
        {
            try
            {
                _bufferMannager.ReturnBuffer(bytes);
            }
            catch { } // 防止回收非 BufferMannager 的 byte array 抛异常
        }

        protected void SetArray(byte[] initialArray) => this.Memory = initialArray;

        public sealed override IByteBuffer AdjustCapacity(int newCapacity)
        {
            this.CheckNewCapacity(newCapacity);

            int oldCapacity = this.Memory.Length;
            byte[] oldArray = this.Memory;
            if (newCapacity > oldCapacity)
            {
                byte[] newArray = this.AllocateArray(newCapacity);
                PlatformDependent.CopyMemory(this.Memory, 0, newArray, 0, oldCapacity);

                this.SetArray(newArray);
                this.FreeArray(oldArray);
            }
            else if (newCapacity < oldCapacity)
            {
                byte[] newArray = this.AllocateArray(newCapacity);
                int readerIndex = this.ReaderIndex;
                if (readerIndex < newCapacity)
                {
                    int writerIndex = this.WriterIndex;
                    if (writerIndex > newCapacity)
                    {
                        this.SetWriterIndex0(writerIndex = newCapacity);
                    }

                    PlatformDependent.CopyMemory(this.Memory, readerIndex, newArray, 0, writerIndex - readerIndex);
                }
                else
                {
                    this.SetIndex(newCapacity, newCapacity);
                }

                this.SetArray(newArray);
                this.FreeArray(oldArray);
            }
            return this;
        }

        public sealed override IByteBufferAllocator Allocator => this._allocator;

        public sealed override IByteBuffer Unwrap() => null;

        public sealed override IByteBuffer RetainedDuplicate() => BufferManagerDuplicatedByteBuffer.NewInstance(this, this, this.ReaderIndex, this.WriterIndex);

        public sealed override IByteBuffer RetainedSlice()
        {
            int index = this.ReaderIndex;
            return this.RetainedSlice(index, this.WriterIndex - index);
        }

        public sealed override IByteBuffer RetainedSlice(int index, int length) => BufferManagerSlicedByteBuffer.NewInstance(this, this, index, length);

        protected internal sealed override void Deallocate()
        {
            var buffer = Memory;
            if (_bufferMannager != null & buffer != null)
            {
                FreeArray(buffer);

                _bufferMannager = null;
                Memory = null;

                this.Recycle();
            }
        }

        void Recycle() => this.recyclerHandle.Release(this);

        public override int IoBufferCount => 1;

        public override ArraySegment<byte> GetIoBuffer(int index, int length)
        {
            this.CheckIndex(index, length);
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

        public override int ArrayOffset => 0;

        public override bool HasMemoryAddress => true;

        public override ref byte GetPinnableMemoryAddress()
        {
            this.EnsureAccessible();
            return ref this.Memory[0];
        }

        public override IntPtr AddressOfPinnedMemory() => IntPtr.Zero;
    }
}