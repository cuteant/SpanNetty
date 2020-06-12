// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial class PooledDuplicatedByteBuffer
    {
        protected internal sealed override ReadOnlyMemory<byte> _GetReadableMemory(int index, int count) => UnwrapCore()._GetReadableMemory(index, count);

        protected internal sealed override ReadOnlySpan<byte> _GetReadableSpan(int index, int count) => UnwrapCore()._GetReadableSpan(index, count);

        protected internal sealed override ReadOnlySequence<byte> _GetSequence(int index, int count) => UnwrapCore()._GetSequence(index, count);

        protected internal sealed override Memory<byte> _GetMemory(int index, int count) => UnwrapCore()._GetMemory(index, count);

        protected internal sealed override Span<byte> _GetSpan(int index, int count) => UnwrapCore()._GetSpan(index, count);

        public sealed override int GetBytes(int index, Memory<byte> destination) => Unwrap().GetBytes(index, destination);

        public sealed override int GetBytes(int index, Span<byte> destination) => Unwrap().GetBytes(index, destination);

        public sealed override IByteBuffer SetBytes(int index, in ReadOnlyMemory<byte> src) { Unwrap().SetBytes(index, src); return this; }

        public sealed override IByteBuffer SetBytes(int index, in ReadOnlySpan<byte> src) { Unwrap().SetBytes(index, src); return this; }
    }
}
