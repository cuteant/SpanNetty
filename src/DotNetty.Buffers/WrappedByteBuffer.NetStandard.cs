// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial class WrappedByteBuffer
    {
        public virtual ReadOnlyMemory<byte> GetReadableMemory() => this.Buf.GetReadableMemory();
        public virtual ReadOnlyMemory<byte> GetReadableMemory(int index, int count) => this.Buf.GetReadableMemory(index, count);

        public virtual ReadOnlySpan<byte> GetReadableSpan() => this.Buf.GetReadableSpan();
        public virtual ReadOnlySpan<byte> GetReadableSpan(int index, int count) => this.Buf.GetReadableSpan(index, count);

        public virtual ReadOnlySequence<byte> GetSequence() => this.Buf.GetSequence();
        public virtual ReadOnlySequence<byte> GetSequence(int index, int count) => this.Buf.GetSequence(index, count);

        public void Advance(int count) => this.Buf.Advance(count);

        public virtual Memory<byte> FreeMemory => this.Buf.FreeMemory;
        public virtual Memory<byte> GetMemory(int sizeHintt = 0) => this.Buf.GetMemory(sizeHintt);
        public virtual Memory<byte> GetMemory(int index, int count) => this.Buf.GetMemory(index, count);

        public virtual Span<byte> Free => this.Buf.Free;
        public virtual Span<byte> GetSpan(int sizeHintt = 0) => this.Buf.GetSpan(sizeHintt);
        public virtual Span<byte> GetSpan(int index, int count) => this.Buf.GetSpan(index, count);
    }
}
#endif