// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial class AbstractDerivedByteBuffer
    {
        public override ReadOnlyMemory<byte> GetReadableMemory(int index, int count) => this.Unwrap().GetReadableMemory(index, count);

        public override ReadOnlySpan<byte> GetReadableSpan(int index, int count) => this.Unwrap().GetReadableSpan(index, count);

        public override ReadOnlySequence<byte> GetSequence(int index, int count) => this.Unwrap().GetSequence(index, count);

        public override Memory<byte> GetMemory(int index, int count) => this.Unwrap().GetMemory(index, count);

        public override Span<byte> GetSpan(int index, int count) => this.Unwrap().GetSpan(index, count);
    }
}
#endif