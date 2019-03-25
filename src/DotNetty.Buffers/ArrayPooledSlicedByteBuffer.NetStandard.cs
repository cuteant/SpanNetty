// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial class ArrayPooledSlicedByteBuffer
    {
        public sealed override ReadOnlyMemory<byte> GetReadableMemory(int index, int count)
        {
            this.CheckIndex0(index, count);
            return this.Unwrap().GetReadableMemory(this.Idx(index), count);
        }
        protected internal sealed override ReadOnlyMemory<byte> _GetReadableMemory(int index, int count)
        {
            return this.UnwrapCore()._GetReadableMemory(this.Idx(index), count);
        }

        public sealed override ReadOnlySpan<byte> GetReadableSpan(int index, int count)
        {
            this.CheckIndex0(index, count);
            return this.Unwrap().GetReadableSpan(this.Idx(index), count);
        }
        protected internal sealed override ReadOnlySpan<byte> _GetReadableSpan(int index, int count)
        {
            return this.UnwrapCore()._GetReadableSpan(this.Idx(index), count);
        }

        public sealed override ReadOnlySequence<byte> GetSequence(int index, int count)
        {
            this.CheckIndex0(index, count);
            return this.Unwrap().GetSequence(this.Idx(index), count);
        }

        protected internal sealed override ReadOnlySequence<byte> _GetSequence(int index, int count)
        {
            return this.UnwrapCore()._GetSequence(this.Idx(index), count);
        }

        public sealed override Memory<byte> GetMemory(int index, int count)
        {
            this.CheckIndex0(index, count);
            return this.Unwrap().GetMemory(this.Idx(index), count);
        }
        protected internal sealed override Memory<byte> _GetMemory(int index, int count)
        {
            return this.UnwrapCore()._GetMemory(this.Idx(index), count);
        }

        public sealed override Span<byte> GetSpan(int index, int count)
        {
            this.CheckIndex0(index, count);
            return this.Unwrap().GetSpan(this.Idx(index), count);
        }
        protected internal sealed override Span<byte> _GetSpan(int index, int count)
        {
            return this.UnwrapCore()._GetSpan(this.Idx(index), count);
        }

        public sealed override int GetBytes(int index, Memory<byte> destination)
        {
            this.CheckIndex0(index, destination.Length);
            return this.Unwrap().GetBytes(this.Idx(index), destination);
        }

        public sealed override int GetBytes(int index, Span<byte> destination)
        {
            this.CheckIndex0(index, destination.Length);
            return this.Unwrap().GetBytes(this.Idx(index), destination);
        }

        public sealed override IByteBuffer SetBytes(int index, ReadOnlyMemory<byte> src)
        {
            this.CheckIndex0(index, src.Length);
            this.Unwrap().SetBytes(this.Idx(index), src);
            return this;
        }

        public sealed override IByteBuffer SetBytes(int index, ReadOnlySpan<byte> src)
        {
            this.CheckIndex0(index, src.Length);
            this.Unwrap().SetBytes(this.Idx(index), src);
            return this;
        }
    }
}
#endif