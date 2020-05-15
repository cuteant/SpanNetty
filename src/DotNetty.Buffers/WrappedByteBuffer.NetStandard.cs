// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial class WrappedByteBuffer
    {
        public void AdvanceReader(int count) => this.Buf.AdvanceReader(count);

        public virtual ReadOnlyMemory<byte> UnreadMemory => this.Buf.FreeMemory;
        public virtual ReadOnlyMemory<byte> GetReadableMemory(int index, int count) => this.Buf.GetReadableMemory(index, count);

        public virtual ReadOnlySpan<byte> UnreadSpan => this.Buf.UnreadSpan;
        public virtual ReadOnlySpan<byte> GetReadableSpan(int index, int count) => this.Buf.GetReadableSpan(index, count);

        public virtual ReadOnlySequence<byte> UnreadSequence => this.Buf.UnreadSequence;
        public virtual ReadOnlySequence<byte> GetSequence(int index, int count) => this.Buf.GetSequence(index, count);

        public void Advance(int count) => this.Buf.Advance(count);

        public virtual Memory<byte> FreeMemory => this.Buf.FreeMemory;
        public virtual Memory<byte> GetMemory(int sizeHintt = 0) => this.Buf.GetMemory(sizeHintt);
        public virtual Memory<byte> GetMemory(int index, int count) => this.Buf.GetMemory(index, count);

        public virtual Span<byte> FreeSpan => this.Buf.FreeSpan;
        public virtual Span<byte> GetSpan(int sizeHintt = 0) => this.Buf.GetSpan(sizeHintt);
        public virtual Span<byte> GetSpan(int index, int count) => this.Buf.GetSpan(index, count);

        public virtual int GetBytes(int index, Span<byte> destination) => this.Buf.GetBytes(index, destination);
        public virtual int GetBytes(int index, Memory<byte> destination) => this.Buf.GetBytes(index, destination);

        public virtual int ReadBytes(Span<byte> destination) => this.Buf.ReadBytes(destination);
        public virtual int ReadBytes(Memory<byte> destination) => this.Buf.ReadBytes(destination);

        public virtual IByteBuffer SetBytes(int index, in ReadOnlySpan<byte> src) => this.Buf.SetBytes(index, src);
        public virtual IByteBuffer SetBytes(int index, in ReadOnlyMemory<byte> src) => this.Buf.SetBytes(index, src);

        public virtual IByteBuffer WriteBytes(in ReadOnlySpan<byte> src) => this.Buf.WriteBytes(src);
        public virtual IByteBuffer WriteBytes(in ReadOnlyMemory<byte> src) => this.Buf.WriteBytes(src);

        public virtual int FindIndex(int index, int count, Predicate<byte> match)
        {
            return this.Buf.FindIndex(index, count, match);
        }

        public virtual int FindLastIndex(int index, int count, Predicate<byte> match)
        {
            return this.Buf.FindLastIndex(index, count, match);
        }

        public virtual int IndexOf(int fromIndex, int toIndex, byte value) => this.Buf.IndexOf(fromIndex, toIndex, value);

        public virtual int IndexOf(int fromIndex, int toIndex, in ReadOnlySpan<byte> values) => this.Buf.IndexOf(fromIndex, toIndex, values);

        public virtual int IndexOfAny(int fromIndex, int toIndex, byte value0, byte value1)
        {
            return this.Buf.IndexOfAny(fromIndex, toIndex, value0, value1);
        }

        public virtual int IndexOfAny(int fromIndex, int toIndex, byte value0, byte value1, byte value2)
        {
            return this.Buf.IndexOfAny(fromIndex, toIndex, value0, value1, value2);
        }

        public virtual int IndexOfAny(int fromIndex, int toIndex, in ReadOnlySpan<byte> values)
        {
            return this.Buf.IndexOfAny(fromIndex, toIndex, values);
        }
    }
}
