// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial class EmptyByteBuffer
    {
        public void AdvanceReader(int count) { }

        public ReadOnlyMemory<byte> UnreadMemory => ReadOnlyMemory<byte>.Empty;
        public ReadOnlyMemory<byte> GetReadableMemory(int index, int count) => ReadOnlyMemory<byte>.Empty;

        public ReadOnlySpan<byte> UnreadSpan => ReadOnlySpan<byte>.Empty;
        public ReadOnlySpan<byte> GetReadableSpan(int index, int count) => ReadOnlySpan<byte>.Empty;

        public ReadOnlySequence<byte> UnreadSequence => ReadOnlySequence<byte>.Empty;
        public ReadOnlySequence<byte> GetSequence(int index, int count) => ReadOnlySequence<byte>.Empty;

        public Memory<byte> FreeMemory => Memory<byte>.Empty;
        public Memory<byte> GetMemory(int sizeHintt = 0) => Memory<byte>.Empty;
        public Memory<byte> GetMemory(int index, int count) => Memory<byte>.Empty;

        public void Advance(int count) { }

        public Span<byte> FreeSpan => Span<byte>.Empty;
        public Span<byte> GetSpan(int sizeHintt = 0) => Span<byte>.Empty;
        public Span<byte> GetSpan(int index, int count) => Span<byte>.Empty;

        public int GetBytes(int index, Span<byte> destination) => 0;
        public int GetBytes(int index, Memory<byte> destination) => 0;

        public int ReadBytes(Span<byte> destination) => 0;
        public int ReadBytes(Memory<byte> destination) => 0;

        public IByteBuffer SetBytes(int index, in ReadOnlySpan<byte> src) => this.CheckIndex(index, src.Length);
        public IByteBuffer SetBytes(int index, in ReadOnlyMemory<byte> src) => this.CheckIndex(index, src.Length);

        public IByteBuffer WriteBytes(in ReadOnlySpan<byte> src) => this.CheckLength(src.Length);
        public IByteBuffer WriteBytes(in ReadOnlyMemory<byte> src) => this.CheckLength(src.Length);

        public int FindIndex(int index, int count, Predicate<byte> match)
        {
            this.CheckIndex(index, count);
            return IndexNotFound;
        }

        public int FindLastIndex(int index, int count, Predicate<byte> match)
        {
            this.CheckIndex(index, count);
            return IndexNotFound;
        }

        public int IndexOf(int fromIndex, int toIndex, byte value)
        {
            this.CheckIndex(fromIndex, toIndex - fromIndex);
            return IndexNotFound;
        }

        public int IndexOf(int fromIndex, int toIndex, in ReadOnlySpan<byte> values)
        {
            this.CheckIndex(fromIndex, toIndex - fromIndex);
            return IndexNotFound;
        }

        public int IndexOfAny(int fromIndex, int toIndex, byte value0, byte value1)
        {
            this.CheckIndex(fromIndex, toIndex - fromIndex);
            return IndexNotFound;
        }

        public int IndexOfAny(int fromIndex, int toIndex, byte value0, byte value1, byte value2)
        {
            this.CheckIndex(fromIndex, toIndex - fromIndex);
            return IndexNotFound;
        }

        public int IndexOfAny(int fromIndex, int toIndex, in ReadOnlySpan<byte> values)
        {
            this.CheckIndex(fromIndex, toIndex - fromIndex);
            return IndexNotFound;
        }
    }
}
