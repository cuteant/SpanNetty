// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial class PooledSlicedByteBuffer
    {
        public sealed override ReadOnlyMemory<byte> GetReadableMemory(int index, int count)
        {
            CheckIndex0(index, count);
            return Unwrap().GetReadableMemory(Idx(index), count);
        }
        protected internal sealed override ReadOnlyMemory<byte> _GetReadableMemory(int index, int count)
        {
            return UnwrapCore()._GetReadableMemory(Idx(index), count);
        }

        public sealed override ReadOnlySpan<byte> GetReadableSpan(int index, int count)
        {
            CheckIndex0(index, count);
            return Unwrap().GetReadableSpan(Idx(index), count);
        }
        protected internal sealed override ReadOnlySpan<byte> _GetReadableSpan(int index, int count)
        {
            return UnwrapCore()._GetReadableSpan(Idx(index), count);
        }

        public sealed override ReadOnlySequence<byte> GetSequence(int index, int count)
        {
            CheckIndex0(index, count);
            return Unwrap().GetSequence(Idx(index), count);
        }

        protected internal sealed override ReadOnlySequence<byte> _GetSequence(int index, int count)
        {
            return UnwrapCore()._GetSequence(Idx(index), count);
        }

        public sealed override Memory<byte> GetMemory(int index, int count)
        {
            CheckIndex0(index, count);
            return Unwrap().GetMemory(Idx(index), count);
        }
        protected internal sealed override Memory<byte> _GetMemory(int index, int count)
        {
            return UnwrapCore()._GetMemory(Idx(index), count);
        }

        public sealed override Span<byte> GetSpan(int index, int count)
        {
            CheckIndex0(index, count);
            return Unwrap().GetSpan(Idx(index), count);
        }
        protected internal sealed override Span<byte> _GetSpan(int index, int count)
        {
            return UnwrapCore()._GetSpan(Idx(index), count);
        }

        public sealed override int GetBytes(int index, Memory<byte> destination)
        {
            CheckIndex0(index, destination.Length);
            return Unwrap().GetBytes(Idx(index), destination);
        }

        public sealed override int GetBytes(int index, Span<byte> destination)
        {
            CheckIndex0(index, destination.Length);
            return Unwrap().GetBytes(Idx(index), destination);
        }

        public sealed override IByteBuffer SetBytes(int index, in ReadOnlyMemory<byte> src)
        {
            CheckIndex0(index, src.Length);
            _ = Unwrap().SetBytes(Idx(index), src);
            return this;
        }

        public sealed override IByteBuffer SetBytes(int index, in ReadOnlySpan<byte> src)
        {
            CheckIndex0(index, src.Length);
            _ = Unwrap().SetBytes(Idx(index), src);
            return this;
        }
    }
}
