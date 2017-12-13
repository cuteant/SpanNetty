using CuteAnt.Buffers;
using DotNetty.Common;
using DotNetty.Common.Internal;

namespace DotNetty.Buffers
{
    abstract class BufferManagerByteBuffer : AbstractReferenceCountedByteBuffer
    {
        readonly ThreadLocalPool.Handle recyclerHandle;

        protected internal byte[] Memory;
        internal int MaxLength;
        PooledByteBufferAllocator _allocator;
        BufferManager _bufferMannager;

        protected BufferManagerByteBuffer(ThreadLocalPool.Handle recyclerHandle, int maxCapacity)
            : base(maxCapacity)
        {
            this.recyclerHandle = recyclerHandle;
        }

        /// <summary>Method must be called before reuse this {@link PooledByteBufAllocator}.</summary>
        /// <param name="bufferManager"></param>
        /// <param name="initialCapacity"></param>
        /// <param name="maxCapacity"></param>
        internal void Reuse(BufferManager bufferManager, int initialCapacity, int maxCapacity)
        {
            _bufferMannager = bufferManager;
            Memory = bufferManager.TakeBuffer(initialCapacity);

            this.SetMaxCapacity(maxCapacity);
            this.SetReferenceCount(1);
            this.SetIndex0(0, 0);
            this.DiscardMarks();
        }

        public override int Capacity => Memory.Length;

        protected virtual byte[] AllocateArray(int initialCapacity) => _bufferMannager.TakeBuffer(initialCapacity);

        protected void FreeArray(byte[] bytes) => _bufferMannager.ReturnBuffer(bytes);

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

        public sealed override IByteBuffer RetainedDuplicate() => PooledDuplicatedByteBuffer.NewInstance(this, this, this.ReaderIndex, this.WriterIndex);

        public sealed override IByteBuffer RetainedSlice()
        {
            int index = this.ReaderIndex;
            return this.RetainedSlice(index, this.WriterIndex - index);
        }

        public sealed override IByteBuffer RetainedSlice(int index, int length) => PooledSlicedByteBuffer.NewInstance(this, this, index, length);

        protected internal sealed override void Deallocate()
        {
            if (_bufferMannager != null & Memory != null)
            {
                _bufferMannager.ReturnBuffer(Memory);
                _bufferMannager = null;
                Memory = null;
                this.Recycle();
            }
        }

        void Recycle() => this.recyclerHandle.Release(this);
    }
}