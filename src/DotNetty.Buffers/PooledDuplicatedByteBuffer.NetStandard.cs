// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial class PooledDuplicatedByteBuffer
    {
        protected internal override ReadOnlyMemory<byte> _GetReadableMemory(int index, int count) => this.UnwrapCore()._GetReadableMemory(index, count);

        protected internal override ReadOnlySpan<byte> _GetReadableSpan(int index, int count) => this.UnwrapCore()._GetReadableSpan(index, count);

        public override ReadOnlySequence<byte> GetSequence(int index, int count) => this.Unwrap().GetSequence(index, count);

        protected internal override Memory<byte> _GetMemory(int index, int count) => this.UnwrapCore()._GetMemory(index, count);

        protected internal override Span<byte> _GetSpan(int index, int count) => this.UnwrapCore()._GetSpan(index, count);

        public override int GetBytes(int index, Memory<byte> destination) => this.Unwrap().GetBytes(index, destination);

        public override int GetBytes(int index, Span<byte> destination) => this.Unwrap().GetBytes(index, destination);

        public override IByteBuffer SetBytes(int index, ReadOnlyMemory<byte> src) { this.Unwrap().SetBytes(index, src); return this; }

        public override IByteBuffer SetBytes(int index, ReadOnlySpan<byte> src) { this.Unwrap().SetBytes(index, src); return this; }
    }
}
#endif