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
        private const int c_minimumGrowthSize = 256;

        public virtual void AdvanceReader(int count)
        {
            if (count < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count);

            if (0u >= (uint)count) { return; }

            var readerIdx = this.readerIndex + count;
            var writerIdx = this.writerIndex;
            if (CheckBounds && readerIdx > writerIdx)
            {
                ThrowHelper.ThrowIndexOutOfRangeException_ReaderIndex(readerIdx, writerIdx);
            }
            this.readerIndex = readerIdx;
        }

        public virtual ReadOnlyMemory<byte> UnreadMemory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                this.EnsureAccessible();
                return this._GetReadableMemory(this.readerIndex, this.ReadableBytes);
            }
        }

        public virtual ReadOnlyMemory<byte> GetReadableMemory(int index, int count)
        {
            this.CheckIndex(index, count);
            return _GetReadableMemory(index, count);
        }
        protected internal abstract ReadOnlyMemory<byte> _GetReadableMemory(int index, int count);


        public virtual ReadOnlySpan<byte> UnreadSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                this.EnsureAccessible();
                return this._GetReadableSpan(this.readerIndex, this.ReadableBytes);
            }
        }

        public virtual ReadOnlySpan<byte> GetReadableSpan(int index, int count)
        {
            this.CheckIndex(index, count);
            return _GetReadableSpan(index, count);
        }
        protected internal abstract ReadOnlySpan<byte> _GetReadableSpan(int index, int count);


        public virtual ReadOnlySequence<byte> UnreadSequence
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                this.EnsureAccessible();
                return this._GetSequence(this.readerIndex, this.ReadableBytes);
            }
        }

        public virtual ReadOnlySequence<byte> GetSequence(int index, int count)
        {
            this.CheckIndex(index, count);
            return _GetSequence(index, count);
        }
        protected internal abstract ReadOnlySequence<byte> _GetSequence(int index, int count);


        public virtual void Advance(int count)
        {
            if (count < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count);

            if (0u >= (uint)count) { return; }

            var capacity = this.Capacity;
            var writerIdx = this.writerIndex + count;
            if (CheckBounds && writerIdx > capacity) { ThrowHelper.ThrowInvalidOperationException(capacity); }

            this.writerIndex = writerIdx;
        }


        public virtual Memory<byte> FreeMemory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                this.EnsureAccessible();
                return this._GetMemory(this.writerIndex, this.WritableBytes);
            }
        }

        public virtual Memory<byte> GetMemory(int sizeHintt = 0)
        {
            this.EnsureAccessible();
            var writerIdx = this.writerIndex;
            this.EnsureWritable0(writerIdx, sizeHintt);
            return this._GetMemory(writerIdx, this.WritableBytes);
        }

        public virtual Memory<byte> GetMemory(int index, int count)
        {
            this.CheckIndex(index, count);
            return this._GetMemory(index, count);
        }
        protected internal abstract Memory<byte> _GetMemory(int index, int count);


        public virtual Span<byte> FreeSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                this.EnsureAccessible();
                return this._GetSpan(this.writerIndex, this.WritableBytes);
            }
        }

        public virtual Span<byte> GetSpan(int sizeHintt = 0)
        {
            this.EnsureAccessible();
            var writerIdx = this.writerIndex;
            this.EnsureWritable0(writerIdx, sizeHintt);
            return this._GetSpan(writerIdx, this.WritableBytes);
        }

        public virtual Span<byte> GetSpan(int index, int count)
        {
            this.CheckIndex(index, count);
            return this._GetSpan(index, count);
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


        public virtual IByteBuffer SetBytes(int index, in ReadOnlySpan<byte> src)
        {
            this.CheckIndex(index);
            if (src.IsEmpty) { return this; }

            var length = src.Length;
            this.EnsureWritable0(index, length);
            var selfSpan = this._GetSpan(index, length);
            src.CopyTo(selfSpan);
            return this;
        }
        public virtual IByteBuffer SetBytes(int index, in ReadOnlyMemory<byte> src)
        {
            this.CheckIndex(index);
            if (src.IsEmpty) { return this; }

            var length = src.Length;
            this.EnsureWritable0(index, length);
            var selfMemory = this._GetMemory(index, length);
            src.CopyTo(selfMemory);
            return this;
        }

        public virtual IByteBuffer WriteBytes(in ReadOnlySpan<byte> src)
        {
            var writerIdx = this.writerIndex;
            this.SetBytes(writerIdx, src);
            this.writerIndex = writerIdx + src.Length;
            return this;
        }
        public virtual IByteBuffer WriteBytes(in ReadOnlyMemory<byte> src)
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
            if ((uint)(sizeHint - 1) > SharedConstants.TooBigOrNegative)
            {
                sizeHint = c_minimumGrowthSize;
            }

            if ((uint)sizeHint <= (uint)(this.Capacity - writerIdx)) { return; }

            this.EnsureWritableInternal(writerIdx, sizeHint);
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

                    if (segment is object)
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

                if (first is null)
                {
                    first = segment = new ReadOnlyBufferSegment();
                }

                return new ReadOnlySequence<byte>(first, 0, segment, segment.Memory.Length);
            }
        }


        internal protected virtual int ForEachByteAsc0(int index, int count, IByteProcessor processor)
        {
            var span = this.GetReadableSpan(index, count);

            var result = SpanHelpers.ForEachByte(ref MemoryMarshal.GetReference(span), processor, span.Length);

            return (uint)result < NIndexNotFound ? index + result : result;
        }

        internal protected virtual int ForEachByteDesc0(int index, int count, IByteProcessor processor)
        {
            var span = this.GetReadableSpan(index, count);

            var result = SpanHelpers.ForEachByteDesc(ref MemoryMarshal.GetReference(span), processor, span.Length);

            return (uint)result < NIndexNotFound ? index + result : result;
        }


        public virtual int FindIndex(int index, int length, Predicate<byte> match)
        {
            if (0u >= (uint)this.Capacity) { return IndexNotFound; }

            return this.FindIndex0(index, length, match);
        }
        internal protected virtual int FindIndex0(int index, int count, Predicate<byte> match)
        {
            var span = this.GetReadableSpan(index, count);

            var result = SpanHelpers.FindIndex(ref MemoryMarshal.GetReference(span), match, span.Length);

            return (uint)result < NIndexNotFound ? index + result : result;
        }

        public virtual int FindLastIndex(int index, int count, Predicate<byte> match)
        {
            if (0u >= (uint)this.Capacity) { return IndexNotFound; }

            return this.FindLastIndex0(index, count, match);
        }
        internal protected virtual int FindLastIndex0(int index, int count, Predicate<byte> match)
        {
            var span = this.GetReadableSpan(index, count);

            var result = SpanHelpers.FindLastIndex(ref MemoryMarshal.GetReference(span), match, span.Length);

            return (uint)result < NIndexNotFound ? index + result : result;
        }


        public virtual int IndexOf(int fromIndex, int toIndex, byte value)
        {
            if (fromIndex <= toIndex)
            {
                fromIndex = Math.Max(fromIndex, 0);
                if (fromIndex >= toIndex || 0u >= (uint)this.Capacity) { return IndexNotFound; }

                return this.IndexOf0(fromIndex, toIndex - fromIndex, value);
            }
            else
            {
                int capacity = this.Capacity;
                fromIndex = Math.Min(fromIndex, capacity);
                if (fromIndex < 0 || 0u >= (uint)capacity) { return IndexNotFound; }

                return this.LastIndexOf0(toIndex, fromIndex - toIndex, value);
            }
        }

        internal protected virtual int IndexOf0(int index, int count, byte value)
        {
            var span = this.GetReadableSpan(index, count);
            var result = span.IndexOf(value);
            return (uint)result < NIndexNotFound ? index + result : result;
        }

        internal protected virtual int LastIndexOf0(int index, int count, byte value)
        {
            var span = this.GetReadableSpan(index, count);
            var result = span.LastIndexOf(value);
            return (uint)result < NIndexNotFound ? index + result : result;
        }

        public virtual int IndexOf(int fromIndex, int toIndex, in ReadOnlySpan<byte> values)
        {
            if (fromIndex <= toIndex)
            {
                fromIndex = Math.Max(fromIndex, 0);
                if (fromIndex >= toIndex || 0u >= (uint)this.Capacity) { return IndexNotFound; }

                return this.IndexOf0(fromIndex, toIndex - fromIndex, values);
            }
            else
            {
                int capacity = this.Capacity;
                fromIndex = Math.Min(fromIndex, capacity);
                if (fromIndex < 0 || 0u >= (uint)capacity) { return IndexNotFound; }

                return this.LastIndexOf0(toIndex, fromIndex - toIndex, values);
            }
        }

        internal protected virtual int IndexOf0(int index, int count, in ReadOnlySpan<byte> values)
        {
            var span = this.GetReadableSpan(index, count);
            var result = span.IndexOf(values);
            return (uint)result < NIndexNotFound ? index + result : result;
        }

        internal protected virtual int LastIndexOf0(int index, int count, in ReadOnlySpan<byte> values)
        {
            var span = this.GetReadableSpan(index, count);
            var result = span.LastIndexOf(values);
            return (uint)result < NIndexNotFound ? index + result : result;
        }

        public virtual int IndexOfAny(int fromIndex, int toIndex, byte value0, byte value1)
        {
            if (fromIndex <= toIndex)
            {
                fromIndex = Math.Max(fromIndex, 0);
                if (fromIndex >= toIndex || 0u >= (uint)this.Capacity) { return IndexNotFound; }

                return this.IndexOfAny0(fromIndex, toIndex - fromIndex, value0, value1);
            }
            else
            {
                int capacity = this.Capacity;
                fromIndex = Math.Min(fromIndex, capacity);
                if (fromIndex < 0 || 0u >= (uint)capacity) { return IndexNotFound; }

                return this.LastIndexOfAny0(toIndex, fromIndex - toIndex, value0, value1);
            }
        }

        internal protected virtual int IndexOfAny0(int index, int count, byte value0, byte value1)
        {
            var span = this.GetReadableSpan(index, count);
            var result = span.IndexOfAny(value0, value1);
            return (uint)result < NIndexNotFound ? index + result : result;
        }

        internal protected virtual int LastIndexOfAny0(int index, int count, byte value0, byte value1)
        {
            var span = this.GetReadableSpan(index, count);
            var result = span.LastIndexOfAny(value0, value1);
            return (uint)result < NIndexNotFound ? index + result : result;
        }

        public virtual int IndexOfAny(int fromIndex, int toIndex, byte value0, byte value1, byte value2)
        {
            if (fromIndex <= toIndex)
            {
                fromIndex = Math.Max(fromIndex, 0);
                if (fromIndex >= toIndex || 0u >= (uint)this.Capacity) { return IndexNotFound; }

                return this.IndexOfAny0(fromIndex, toIndex - fromIndex, value0, value1, value2);
            }
            else
            {
                int capacity = this.Capacity;
                fromIndex = Math.Min(fromIndex, capacity);
                if (fromIndex < 0 || 0u >= (uint)capacity) { return IndexNotFound; }

                return this.LastIndexOfAny0(toIndex, fromIndex - toIndex, value0, value1, value2);
            }
        }

        internal protected virtual int IndexOfAny0(int index, int count, byte value0, byte value1, byte value2)
        {
            var span = this.GetReadableSpan(index, count);
            var result = span.IndexOfAny(value0, value1, value2);
            return (uint)result < NIndexNotFound ? index + result : result;
        }

        internal protected virtual int LastIndexOfAny0(int index, int count, byte value0, byte value1, byte value2)
        {
            var span = this.GetReadableSpan(index, count);
            var result = span.LastIndexOfAny(value0, value1, value2);
            return (uint)result < NIndexNotFound ? index + result : result;
        }

        public virtual int IndexOfAny(int fromIndex, int toIndex, in ReadOnlySpan<byte> values)
        {
            if (fromIndex <= toIndex)
            {
                fromIndex = Math.Max(fromIndex, 0);
                if (fromIndex >= toIndex || 0u >= (uint)this.Capacity) { return IndexNotFound; }

                return this.IndexOfAny0(fromIndex, toIndex - fromIndex, values);
            }
            else
            {
                int capacity = this.Capacity;
                fromIndex = Math.Min(fromIndex, capacity);
                if (fromIndex < 0 || 0u >= (uint)capacity) { return IndexNotFound; }

                return this.LastIndexOfAny0(toIndex, fromIndex - toIndex, values);
            }
        }

        internal protected virtual int IndexOfAny0(int index, int count, in ReadOnlySpan<byte> values)
        {
            var span = this.GetReadableSpan(index, count);
            var result = span.IndexOfAny(values);
            return (uint)result < NIndexNotFound ? index + result : result;
        }

        internal protected virtual int LastIndexOfAny0(int index, int count, in ReadOnlySpan<byte> values)
        {
            var span = this.GetReadableSpan(index, count);
            var result = span.LastIndexOfAny(values);
            return (uint)result < NIndexNotFound ? index + result : result;
        }
    }
}
