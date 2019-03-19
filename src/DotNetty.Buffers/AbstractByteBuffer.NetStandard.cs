#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    partial class AbstractByteBuffer : IBufferWriter<byte>
    {
        private const int c_minimumBufferSize = 256;
        private const uint c_maxBufferSize = int.MaxValue;

        public virtual ReadOnlyMemory<byte> GetReadableMemory() => this.GetReadableMemory(this.readerIndex, this.ReadableBytes);
        public virtual ReadOnlyMemory<byte> GetReadableMemory(int index, int count)
        {
            this.CheckIndex(index, count);
            return _GetReadableMemory(index, count);
        }
        protected internal abstract ReadOnlyMemory<byte> _GetReadableMemory(int index, int count);


        public virtual ReadOnlySpan<byte> GetReadableSpan() => this.GetReadableSpan(this.readerIndex, this.ReadableBytes);
        public virtual ReadOnlySpan<byte> GetReadableSpan(int index, int count)
        {
            this.CheckIndex(index, count);
            return _GetReadableSpan(index, count);
        }
        protected internal abstract ReadOnlySpan<byte> _GetReadableSpan(int index, int count);


        public virtual ReadOnlySequence<byte> GetSequence() => this.GetSequence(this.readerIndex, this.ReadableBytes);
        public abstract ReadOnlySequence<byte> GetSequence(int index, int count);


        public virtual void Advance(int count)
        {
            if (count < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count);

            var capacity = this.Capacity;
            var writerIdx = this.writerIndex;
            if (writerIdx > capacity - count) { ThrowHelper.ThrowInvalidOperationException(capacity); }

            this.writerIndex = writerIdx + count;
        }


        public virtual Memory<byte> FreeMemory => this.GetMemory(this.writerIndex, this.WritableBytes);

        public virtual Memory<byte> GetMemory(int sizeHintt = 0)
        {
            var writerIdx = this.writerIndex;
            EnsureWritable0(writerIdx, sizeHintt);
            return GetMemory(writerIdx, this.WritableBytes);
        }

        public virtual Memory<byte> GetMemory(int index, int count)
        {
            this.CheckIndex(index, count);
            return _GetMemory(index, count);
        }
        protected internal abstract Memory<byte> _GetMemory(int index, int count);


        public virtual Span<byte> Free => this.GetSpan(this.writerIndex, this.WritableBytes);

        public virtual Span<byte> GetSpan(int sizeHintt = 0)
        {
            var writerIdx = this.writerIndex;
            EnsureWritable0(writerIdx, sizeHintt);
            return GetSpan(writerIdx, this.WritableBytes);
        }

        public virtual Span<byte> GetSpan(int index, int count)
        {
            this.CheckIndex(index, count);
            return _GetSpan(index, count);
        }
        protected internal abstract Span<byte> _GetSpan(int index, int count);


        public virtual int GetBytes(int index, Span<byte> destination)
        {
            this.CheckIndex(index);

            var count = Math.Min(this.Capacity - index, destination.Length);
            if (0u >= (uint)count) { return 0; }

            var selfSpan = this._GetReadableSpan(index, count);
            selfSpan.CopyTo(destination);
            return count;
        }
        public virtual int GetBytes(int index, Memory<byte> destination)
        {
            this.CheckIndex(index);

            var count = Math.Min(this.Capacity - index, destination.Length);
            if (0u >= (uint)count) { return 0; }

            var selfMemory = this._GetReadableMemory(index, count);
            selfMemory.CopyTo(destination);
            return count;
        }

        public virtual int ReadBytes(Span<byte> destination)
        {
            var count = this.GetBytes(this.readerIndex, destination);
            if (count > 0) { this.readerIndex += count; }
            return count;
        }
        public virtual int ReadBytes(Memory<byte> destination)
        {
            var count = this.GetBytes(this.readerIndex, destination);
            if (count > 0) { this.readerIndex += count; }
            return count;
        }


        public virtual IByteBuffer SetBytes(int index, ReadOnlySpan<byte> src)
        {
            this.CheckIndex(index);
            if (src.IsEmpty) { return this; }

            var length = src.Length;
            EnsureWritable0(index, length);
            var selfSpan = this._GetSpan(index, length);
            src.CopyTo(selfSpan);
            return this;
        }
        public virtual IByteBuffer SetBytes(int index, ReadOnlyMemory<byte> src)
        {
            this.CheckIndex(index);
            if (src.IsEmpty) { return this; }

            var length = src.Length;
            EnsureWritable0(index, length);
            var selfMemory = this._GetMemory(index, length);
            src.CopyTo(selfMemory);
            return this;
        }

        public virtual IByteBuffer WriteBytes(ReadOnlySpan<byte> src)
        {
            var writerIdx = this.writerIndex;
            this.SetBytes(writerIdx, src);
            this.writerIndex = writerIdx + src.Length;
            return this;
        }
        public virtual IByteBuffer WriteBytes(ReadOnlyMemory<byte> src)
        {
            var writerIdx = this.writerIndex;
            this.SetBytes(writerIdx, src);
            this.writerIndex = writerIdx + src.Length;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void EnsureWritable0(int writerIdx, int sizeHint)
        {
            //this.EnsureAccessible();
            //if (sizeHint < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.sizeHint);
            //if (sizeHint == 0)
            if ((uint)(sizeHint - 1) > c_maxBufferSize)
            {
                sizeHint = c_minimumBufferSize;
            }

            var capacity = this.Capacity;
            int availableSpace = capacity - writerIdx;

            if (sizeHint <= availableSpace) { return; }

            var maxCapacity = this.MaxCapacity;
            if (CheckBounds)
            {
                CheckMinWritableBounds(sizeHint, writerIdx, maxCapacity, this);
            }

            // Normalize the current capacity to the power of 2.
            int newCapacity = this.Allocator.CalculateNewCapacity(writerIdx + sizeHint, maxCapacity);

            // Adjust to the new capacity.
            this.AdjustCapacity(newCapacity);
        }

        protected sealed class ReadOnlyBufferSegment : ReadOnlySequenceSegment<byte>
        {
            public static ReadOnlySequence<byte> Create(IEnumerable<ReadOnlyMemory<byte>> buffers)
            {
                ReadOnlyBufferSegment segment = null;
                ReadOnlyBufferSegment first = null;
                foreach (var buffer in buffers)
                {
                    var newSegment = new ReadOnlyBufferSegment()
                    {
                        Memory = buffer,
                    };

                    if (segment != null)
                    {
                        segment.Next = newSegment;
                        newSegment.RunningIndex = segment.RunningIndex + segment.Memory.Length;
                    }
                    else
                    {
                        first = newSegment;
                    }

                    segment = newSegment;
                }

                if (first == null)
                {
                    first = segment = new ReadOnlyBufferSegment();
                }

                return new ReadOnlySequence<byte>(first, 0, segment, segment.Memory.Length);
            }
        }
    }
}
#endif