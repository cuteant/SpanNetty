// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial class WrappedCompositeByteBuffer
    {
        public override ReadOnlyMemory<byte> GetReadableMemory(int index, int count) => this.wrapped.GetReadableMemory(index, count);
        protected internal override ReadOnlyMemory<byte> _GetReadableMemory(int index, int count) => this.wrapped._GetReadableMemory(index, count);

        public override ReadOnlySpan<byte> GetReadableSpan(int index, int count) => this.wrapped.GetReadableSpan(index, count);
        protected internal override ReadOnlySpan<byte> _GetReadableSpan(int index, int count) => this.wrapped._GetReadableSpan(index, count);

        public override ReadOnlySequence<byte> GetSequence(int index, int count) => this.wrapped.GetSequence(index, count);

        public sealed override void Advance(int count) => this.wrapped.Advance(count);

        public override Memory<byte> FreeMemory => this.wrapped.FreeMemory;
        public override Memory<byte> GetMemory(int sizeHintt = 0) => this.wrapped.GetMemory(sizeHintt);
        public override Memory<byte> GetMemory(int index, int count) => this.wrapped.GetMemory(index, count);
        protected internal override Memory<byte> _GetMemory(int index, int count) => this.wrapped._GetMemory(index, count);

        public override Span<byte> Free => this.wrapped.Free;
        public override Span<byte> GetSpan(int sizeHintt = 0) => this.wrapped.GetSpan(sizeHintt);
        public override Span<byte> GetSpan(int index, int count) => this.wrapped.GetSpan(index, count);
        protected internal override Span<byte> _GetSpan(int index, int count) => this.wrapped._GetSpan(index, count);

        public override int GetBytes(int index, Span<byte> destination) => this.wrapped.GetBytes(index, destination);
        public override int GetBytes(int index, Memory<byte> destination) => this.wrapped.GetBytes(index, destination);

        public override int ReadBytes(Span<byte> destination) => this.wrapped.ReadBytes(destination);
        public override int ReadBytes(Memory<byte> destination) => this.wrapped.ReadBytes(destination);

        public override IByteBuffer SetBytes(int index, ReadOnlySpan<byte> src) { this.wrapped.SetBytes(index, src); return this; }
        public override IByteBuffer SetBytes(int index, ReadOnlyMemory<byte> src) { this.wrapped.SetBytes(index, src); return this; }

        public override IByteBuffer WriteBytes(ReadOnlySpan<byte> src) { this.wrapped.WriteBytes(src); return this; }
        public override IByteBuffer WriteBytes(ReadOnlyMemory<byte> src) { this.wrapped.WriteBytes(src); return this; }

        public override int FindIndex(int index, int count, Predicate<byte> match)
        {
            return this.wrapped.FindIndex(index, count, match);
        }

        public override int FindLastIndex(int index, int count, Predicate<byte> match)
        {
            return this.wrapped.FindLastIndex(index, count, match);
        }

        public override int IndexOf(int fromIndex, int toIndex, byte value) => this.wrapped.IndexOf(fromIndex, toIndex, value);

        public override int IndexOf(int fromIndex, int toIndex, ReadOnlySpan<byte> values) => this.wrapped.IndexOf(fromIndex, toIndex, values);

        public override int IndexOfAny(int fromIndex, int toIndex, byte value0, byte value1)
        {
            return this.wrapped.IndexOfAny(fromIndex, toIndex, value0, value1);
        }

        public override int IndexOfAny(int fromIndex, int toIndex, byte value0, byte value1, byte value2)
        {
            return this.wrapped.IndexOfAny(fromIndex, toIndex, value0, value1, value2);
        }

        public override int IndexOfAny(int fromIndex, int toIndex, ReadOnlySpan<byte> values)
        {
            return this.wrapped.IndexOfAny(fromIndex, toIndex, values);
        }
    }
}
#endif