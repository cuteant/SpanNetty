// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial class WrappedByteBuffer
    {
        public void AdvanceReader(int count) => Buf.AdvanceReader(count);

        public virtual ReadOnlyMemory<byte> UnreadMemory => Buf.FreeMemory;
        public virtual ReadOnlyMemory<byte> GetReadableMemory(int index, int count) => Buf.GetReadableMemory(index, count);

        public virtual ReadOnlySpan<byte> UnreadSpan => Buf.UnreadSpan;
        public virtual ReadOnlySpan<byte> GetReadableSpan(int index, int count) => Buf.GetReadableSpan(index, count);

        public virtual ReadOnlySequence<byte> UnreadSequence => Buf.UnreadSequence;
        public virtual ReadOnlySequence<byte> GetSequence(int index, int count) => Buf.GetSequence(index, count);

        public void Advance(int count) => Buf.Advance(count);

        public virtual Memory<byte> FreeMemory => Buf.FreeMemory;
        public virtual Memory<byte> GetMemory(int sizeHintt = 0) => Buf.GetMemory(sizeHintt);
        public virtual Memory<byte> GetMemory(int index, int count) => Buf.GetMemory(index, count);

        public virtual Span<byte> FreeSpan => Buf.FreeSpan;
        public virtual Span<byte> GetSpan(int sizeHintt = 0) => Buf.GetSpan(sizeHintt);
        public virtual Span<byte> GetSpan(int index, int count) => Buf.GetSpan(index, count);

        public virtual int GetBytes(int index, Span<byte> destination) => Buf.GetBytes(index, destination);
        public virtual int GetBytes(int index, Memory<byte> destination) => Buf.GetBytes(index, destination);

        public virtual int ReadBytes(Span<byte> destination) => Buf.ReadBytes(destination);
        public virtual int ReadBytes(Memory<byte> destination) => Buf.ReadBytes(destination);

        public virtual IByteBuffer SetBytes(int index, in ReadOnlySpan<byte> src) => Buf.SetBytes(index, src);
        public virtual IByteBuffer SetBytes(int index, in ReadOnlyMemory<byte> src) => Buf.SetBytes(index, src);

        public virtual IByteBuffer WriteBytes(in ReadOnlySpan<byte> src) => Buf.WriteBytes(src);
        public virtual IByteBuffer WriteBytes(in ReadOnlyMemory<byte> src) => Buf.WriteBytes(src);

        public virtual int FindIndex(int index, int count, Predicate<byte> match)
        {
            return Buf.FindIndex(index, count, match);
        }

        public virtual int FindLastIndex(int index, int count, Predicate<byte> match)
        {
            return Buf.FindLastIndex(index, count, match);
        }

        public virtual int IndexOf(int fromIndex, int toIndex, byte value) => Buf.IndexOf(fromIndex, toIndex, value);

        public virtual int IndexOf(int fromIndex, int toIndex, in ReadOnlySpan<byte> values) => Buf.IndexOf(fromIndex, toIndex, values);

        public virtual int IndexOfAny(int fromIndex, int toIndex, byte value0, byte value1)
        {
            return Buf.IndexOfAny(fromIndex, toIndex, value0, value1);
        }

        public virtual int IndexOfAny(int fromIndex, int toIndex, byte value0, byte value1, byte value2)
        {
            return Buf.IndexOfAny(fromIndex, toIndex, value0, value1, value2);
        }

        public virtual int IndexOfAny(int fromIndex, int toIndex, in ReadOnlySpan<byte> values)
        {
            return Buf.IndexOfAny(fromIndex, toIndex, values);
        }
    }
}
