#if !NET40

namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial class ReadOnlyByteBuffer
    {
        public override ReadOnlyMemory<byte> GetReadableMemory(int index, int count)
        {
            return this.Unwrap().GetReadableMemory(index, count);
        }

        protected internal override ReadOnlyMemory<byte> _GetReadableMemory(int index, int count)
        {
            return this.Unwrap().GetReadableMemory(index, count);
        }


        public override ReadOnlySpan<byte> GetReadableSpan(int index, int count)
        {
            return this.Unwrap().GetReadableSpan(index, count);
        }

        protected internal override ReadOnlySpan<byte> _GetReadableSpan(int index, int count)
        {
            return this.Unwrap().GetReadableSpan(index, count);
        }


        public override ReadOnlySequence<byte> GetSequence(int index, int count)
        {
            return this.Unwrap().GetSequence(index, count);
        }


        public override int GetBytes(int index, Memory<byte> destination)
        {
            return this.Unwrap().GetBytes(index, destination);
        }

        public override int GetBytes(int index, Span<byte> destination)
        {
            return this.Unwrap().GetBytes(index, destination);
        }

        public override int FindIndex(int index, int count, Predicate<byte> match)
        {
            return this.Unwrap().FindIndex(index, count, match);
        }

        public override int FindLastIndex(int index, int count, Predicate<byte> match)
        {
            return this.Unwrap().FindLastIndex(index, count, match);
        }

        public override int IndexOf(int fromIndex, int toIndex, byte value)
        {
            return this.Unwrap().IndexOf(fromIndex, toIndex, value);
        }

        public override int IndexOf(int fromIndex, int toIndex, ReadOnlySpan<byte> values)
        {
            return this.Unwrap().IndexOf(fromIndex, toIndex, values);
        }

        public override int IndexOfAny(int fromIndex, int toIndex, byte value0, byte value1)
        {
            return this.Unwrap().IndexOfAny(fromIndex, toIndex, value0, value1);
        }

        public override int IndexOfAny(int fromIndex, int toIndex, byte value0, byte value1, byte value2)
        {
            return this.Unwrap().IndexOfAny(fromIndex, toIndex, value0, value1, value2);
        }

        public override int IndexOfAny(int fromIndex, int toIndex, ReadOnlySpan<byte> values)
        {
            return this.Unwrap().IndexOfAny(fromIndex, toIndex, values);
        }

        public override void Advance(int count) => throw new ReadOnlyBufferException();


        public override Memory<byte> FreeMemory => throw new ReadOnlyBufferException();
        public override Memory<byte> GetMemory(int sizeHintt = 0)
        {
            throw new ReadOnlyBufferException();
        }
        public override Memory<byte> GetMemory(int index, int count)
        {
            throw new ReadOnlyBufferException();
        }
        protected internal override Memory<byte> _GetMemory(int index, int count)
        {
            throw new ReadOnlyBufferException();
        }


        public override Span<byte> Free => throw new ReadOnlyBufferException();
        public override Span<byte> GetSpan(int sizeHintt = 0)
        {
            throw new ReadOnlyBufferException();
        }
        public override Span<byte> GetSpan(int index, int count)
        {
            throw new ReadOnlyBufferException();
        }
        protected internal override Span<byte> _GetSpan(int index, int count)
        {
            throw new ReadOnlyBufferException();
        }


        public override IByteBuffer SetBytes(int index, ReadOnlySpan<byte> src)
        {
            throw new ReadOnlyBufferException();
        }
        public override IByteBuffer SetBytes(int index, ReadOnlyMemory<byte> src)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer WriteBytes(ReadOnlySpan<byte> src)
        {
            throw new ReadOnlyBufferException();
        }
        public override IByteBuffer WriteBytes(ReadOnlyMemory<byte> src)
        {
            throw new ReadOnlyBufferException();
        }
    }
}

#endif