// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial class ArrayPooledSlicedByteBuffer
    {
        public override ReadOnlyMemory<byte> GetReadableMemory(int index, int count)
        {
            this.CheckIndex0(index, count);
            return this.Unwrap().GetReadableMemory(this.Idx(index), count);
        }
        protected internal override ReadOnlyMemory<byte> _GetReadableMemory(int index, int count)
        {
            return this.UnwrapCore()._GetReadableMemory(this.Idx(index), count);
        }

        public override ReadOnlySpan<byte> GetReadableSpan(int index, int count)
        {
            this.CheckIndex0(index, count);
            return this.Unwrap().GetReadableSpan(this.Idx(index), count);
        }
        protected internal override ReadOnlySpan<byte> _GetReadableSpan(int index, int count)
        {
            return this.UnwrapCore()._GetReadableSpan(this.Idx(index), count);
        }

        public override ReadOnlySequence<byte> GetSequence(int index, int count)
        {
            this.CheckIndex0(index, count);
            return this.Unwrap().GetSequence(this.Idx(index), count);
        }

        public override Memory<byte> GetMemory(int index, int count)
        {
            this.CheckIndex0(index, count);
            return this.Unwrap().GetMemory(this.Idx(index), count);
        }
        protected internal override Memory<byte> _GetMemory(int index, int count)
        {
            return this.UnwrapCore()._GetMemory(this.Idx(index), count);
        }

        public override Span<byte> GetSpan(int index, int count)
        {
            this.CheckIndex0(index, count);
            return this.Unwrap().GetSpan(this.Idx(index), count);
        }
        protected internal override Span<byte> _GetSpan(int index, int count)
        {
            return this.UnwrapCore()._GetSpan(this.Idx(index), count);
        }

        public override int GetBytes(int index, Memory<byte> destination)
        {
            this.CheckIndex0(index, destination.Length);
            return this.Unwrap().GetBytes(this.Idx(index), destination);
        }

        public override int GetBytes(int index, Span<byte> destination)
        {
            this.CheckIndex0(index, destination.Length);
            return this.Unwrap().GetBytes(this.Idx(index), destination);
        }

        public override IByteBuffer SetBytes(int index, ReadOnlyMemory<byte> src)
        {
            this.CheckIndex0(index, src.Length);
            this.Unwrap().SetBytes(this.Idx(index), src);
            return this;
        }

        public override IByteBuffer SetBytes(int index, ReadOnlySpan<byte> src)
        {
            this.CheckIndex0(index, src.Length);
            this.Unwrap().SetBytes(this.Idx(index), src);
            return this;
        }
    }
}
#endif