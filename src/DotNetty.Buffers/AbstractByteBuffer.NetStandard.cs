#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;

    partial class AbstractByteBuffer : IBufferWriter<byte>
    {
        private const int c_minimumBufferSize = 256;
        private const uint c_maxBufferSize = int.MaxValue;

        public virtual ReadOnlyMemory<byte> GetReadableMemory(int index, int count)
        {
            this.CheckIndex(index, count);
            return _GetReadableMemory(index, count);
        }
        protected internal abstract ReadOnlyMemory<byte> _GetReadableMemory(int index, int count);


        public virtual ReadOnlySpan<byte> GetReadableSpan(int index, int count)
        {
            this.CheckIndex(index, count);
            return _GetReadableSpan(index, count);
        }
        protected internal abstract ReadOnlySpan<byte> _GetReadableSpan(int index, int count);


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


        internal protected virtual int ForEachByteAsc0(int index, int count, IByteProcessor processor)
        {
            var span = this.GetReadableSpan(index, count);

            var result = PlatformDependent.ForEachByte(ref MemoryMarshal.GetReference(span), processor, span.Length);

            return result >= 0 ? index + result : result;
        }

        internal protected virtual int ForEachByteDesc0(int index, int count, IByteProcessor processor)
        {
            var span = this.GetReadableSpan(index, count);

            var result = PlatformDependent.ForEachByteDesc(ref MemoryMarshal.GetReference(span), processor, span.Length);

            return result >= 0 ? index + result : result;
        }


        public virtual int FindIndex(int index, int length, Predicate<byte> match)
        {
            if (0u >= (uint)this.Capacity) { return -1; }

            return this.FindIndex0(index, length, match);
        }
        internal protected virtual int FindIndex0(int index, int count, Predicate<byte> match)
        {
            var span = this.GetReadableSpan(index, count);

            var result = PlatformDependent.FindIndex(ref MemoryMarshal.GetReference(span), match, span.Length);

            return result >= 0 ? index + result : result;
        }

        public virtual int FindLastIndex(int index, int count, Predicate<byte> match)
        {
            if (0u >= (uint)this.Capacity) { return -1; }

            return this.FindLastIndex0(index, count, match);
        }
        internal protected virtual int FindLastIndex0(int index, int count, Predicate<byte> match)
        {
            var span = this.GetReadableSpan(index, count);

            var result = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span), match, span.Length);

            return result >= 0 ? index + result : result;
        }


        public virtual int IndexOf(int fromIndex, int toIndex, byte value)
        {
            if (fromIndex <= toIndex)
            {
                fromIndex = Math.Max(fromIndex, 0);
                if (fromIndex >= toIndex || 0u >= (uint)this.Capacity) { return -1; }

                return IndexOf0(fromIndex, toIndex - fromIndex, value);
            }
            else
            {
                int capacity = this.Capacity;
                fromIndex = Math.Min(fromIndex, capacity);
                if (fromIndex < 0 || 0u >= (uint)capacity) { return -1; }

                return LastIndexOf0(toIndex, fromIndex - toIndex, value);
            }
        }

        internal protected virtual int IndexOf0(int index, int count, byte value)
        {
            var span = this.GetReadableSpan(index, count);
            var result = span.IndexOf(value);
            return result >= 0 ? index + result : result;
        }

        internal protected virtual int LastIndexOf0(int index, int count, byte value)
        {
            var span = this.GetReadableSpan(index, count);
            var result = span.LastIndexOf(value);
            return result >= 0 ? index + result : result;
        }

        public virtual int IndexOf(int fromIndex, int toIndex, ReadOnlySpan<byte> values)
        {
            if (fromIndex <= toIndex)
            {
                fromIndex = Math.Max(fromIndex, 0);
                if (fromIndex >= toIndex || 0u >= (uint)this.Capacity) { return -1; }

                return IndexOf0(fromIndex, toIndex - fromIndex, values);
            }
            else
            {
                int capacity = this.Capacity;
                fromIndex = Math.Min(fromIndex, capacity);
                if (fromIndex < 0 || 0u >= (uint)capacity) { return -1; }

                return LastIndexOf0(toIndex, fromIndex - toIndex, values);
            }
        }

        internal protected virtual int IndexOf0(int index, int count, ReadOnlySpan<byte> values)
        {
            var span = this.GetReadableSpan(index, count);
            var result = span.IndexOf(values);
            return result >= 0 ? index + result : result;
        }

        internal protected virtual int LastIndexOf0(int index, int count, ReadOnlySpan<byte> values)
        {
            var span = this.GetReadableSpan(index, count);
            var result = span.LastIndexOf(values);
            return result >= 0 ? index + result : result;
        }

        public virtual int IndexOfAny(int fromIndex, int toIndex, byte value0, byte value1)
        {
            if (fromIndex <= toIndex)
            {
                fromIndex = Math.Max(fromIndex, 0);
                if (fromIndex >= toIndex || 0u >= (uint)this.Capacity) { return -1; }

                return IndexOfAny0(fromIndex, toIndex - fromIndex, value0, value1);
            }
            else
            {
                int capacity = this.Capacity;
                fromIndex = Math.Min(fromIndex, capacity);
                if (fromIndex < 0 || 0u >= (uint)capacity) { return -1; }

                return LastIndexOfAny0(toIndex, fromIndex - toIndex, value0, value1);
            }
        }

        internal protected virtual int IndexOfAny0(int index, int count, byte value0, byte value1)
        {
            var span = this.GetReadableSpan(index, count);
            var result = span.IndexOfAny(value0, value1);
            return result >= 0 ? index + result : result;
        }

        internal protected virtual int LastIndexOfAny0(int index, int count, byte value0, byte value1)
        {
            var span = this.GetReadableSpan(index, count);
            var result = span.LastIndexOfAny(value0, value1);
            return result >= 0 ? index + result : result;
        }

        public virtual int IndexOfAny(int fromIndex, int toIndex, byte value0, byte value1, byte value2)
        {
            if (fromIndex <= toIndex)
            {
                fromIndex = Math.Max(fromIndex, 0);
                if (fromIndex >= toIndex || 0u >= (uint)this.Capacity) { return -1; }

                return IndexOfAny0(fromIndex, toIndex - fromIndex, value0, value1, value2);
            }
            else
            {
                int capacity = this.Capacity;
                fromIndex = Math.Min(fromIndex, capacity);
                if (fromIndex < 0 || 0u >= (uint)capacity) { return -1; }

                return LastIndexOfAny0(toIndex, fromIndex - toIndex, value0, value1, value2);
            }
        }

        internal protected virtual int IndexOfAny0(int index, int count, byte value0, byte value1, byte value2)
        {
            var span = this.GetReadableSpan(index, count);
            var result = span.IndexOfAny(value0, value1, value2);
            return result >= 0 ? index + result : result;
        }

        internal protected virtual int LastIndexOfAny0(int index, int count, byte value0, byte value1, byte value2)
        {
            var span = this.GetReadableSpan(index, count);
            var result = span.LastIndexOfAny(value0, value1, value2);
            return result >= 0 ? index + result : result;
        }

        public virtual int IndexOfAny(int fromIndex, int toIndex, ReadOnlySpan<byte> values)
        {
            if (fromIndex <= toIndex)
            {
                fromIndex = Math.Max(fromIndex, 0);
                if (fromIndex >= toIndex || 0u >= (uint)this.Capacity) { return -1; }

                return IndexOfAny0(fromIndex, toIndex - fromIndex, values);
            }
            else
            {
                int capacity = this.Capacity;
                fromIndex = Math.Min(fromIndex, capacity);
                if (fromIndex < 0 || 0u >= (uint)capacity) { return -1; }

                return LastIndexOfAny0(toIndex, fromIndex - toIndex, values);
            }
        }

        internal protected virtual int IndexOfAny0(int index, int count, ReadOnlySpan<byte> values)
        {
            var span = this.GetReadableSpan(index, count);
            var result = span.IndexOfAny(values);
            return result >= 0 ? index + result : result;
        }

        internal protected virtual int LastIndexOfAny0(int index, int count, ReadOnlySpan<byte> values)
        {
            var span = this.GetReadableSpan(index, count);
            var result = span.LastIndexOfAny(values);
            return result >= 0 ? index + result : result;
        }
    }
}
#endif