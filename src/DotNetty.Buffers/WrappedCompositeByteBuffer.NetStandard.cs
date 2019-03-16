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

        public override ReadOnlySpan<byte> GetReadableSpan(int index, int count) => this.wrapped.GetReadableSpan(index, count);

        public override ReadOnlySequence<byte> GetSequence(int index, int count) => this.wrapped.GetSequence(index, count);

        public sealed override void Advance(int count) => this.wrapped.Advance(count);

        public override Memory<byte> GetMemory(int index, int count) => this.wrapped.GetMemory(index, count);

        public override Span<byte> GetSpan(int index, int count) => this.wrapped.GetSpan(index, count);
    }
}
#endif