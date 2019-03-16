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
        public abstract ReadOnlyMemory<byte> GetReadableMemory(int index, int count);

        public virtual ReadOnlySpan<byte> GetReadableSpan() => this.GetReadableSpan(this.readerIndex, this.ReadableBytes);
        public abstract ReadOnlySpan<byte> GetReadableSpan(int index, int count);

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
            CheckAndResizeBuffer(sizeHintt);
            return GetMemory(this.writerIndex, this.WritableBytes);
        }

        public abstract Memory<byte> GetMemory(int index, int count);

        public virtual Span<byte> Free => this.GetSpan(this.writerIndex, this.WritableBytes);

        public virtual Span<byte> GetSpan(int sizeHintt = 0)
        {
            CheckAndResizeBuffer(sizeHintt);
            return GetSpan(this.writerIndex, this.WritableBytes);
        }

        public abstract Span<byte> GetSpan(int index, int count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckAndResizeBuffer(int sizeHint)
        {
            //if (sizeHint < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.sizeHint);
            //if (sizeHint == 0)
            if ((uint)(sizeHint - 1) > c_maxBufferSize)
            {
                sizeHint = c_minimumBufferSize;
            }

            int availableSpace = this.WritableBytes;

            if (sizeHint > availableSpace)
            {
                var capacity = this.Capacity;
                int growBy = Math.Max(sizeHint, capacity);
                int newSize = checked(capacity + growBy);

                AdjustCapacity(newSize);
            }
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