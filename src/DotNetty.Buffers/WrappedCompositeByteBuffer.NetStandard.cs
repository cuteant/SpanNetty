// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial class WrappedCompositeByteBuffer
    {
        public override ReadOnlyMemory<byte> GetReadableMemory() => this.wrapped.GetReadableMemory();
        public override ReadOnlyMemory<byte> GetReadableMemory(int index, int count) => this.wrapped.GetReadableMemory(index, count);
        protected internal override ReadOnlyMemory<byte> _GetReadableMemory(int index, int count) => this.wrapped._GetReadableMemory(index, count);

        public override ReadOnlySpan<byte> GetReadableSpan() => this.wrapped.GetReadableSpan();
        public override ReadOnlySpan<byte> GetReadableSpan(int index, int count) => this.wrapped.GetReadableSpan(index, count);
        protected internal override ReadOnlySpan<byte> _GetReadableSpan(int index, int count) => this.wrapped._GetReadableSpan(index, count);

        public override ReadOnlySequence<byte> GetSequence() => this.wrapped.GetSequence();
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

        public override IByteBuffer SetBytes(int index, ReadOnlySpan<byte> src) => this.wrapped.SetBytes(index, src);
        public override IByteBuffer SetBytes(int index, ReadOnlyMemory<byte> src) => this.wrapped.SetBytes(index, src);

        public override IByteBuffer WriteBytes(ReadOnlySpan<byte> src) => this.wrapped.WriteBytes(src);
        public override IByteBuffer WriteBytes(ReadOnlyMemory<byte> src) => this.wrapped.WriteBytes(src);
    }
}
#endif