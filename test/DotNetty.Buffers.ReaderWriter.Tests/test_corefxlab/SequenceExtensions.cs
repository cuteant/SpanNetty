// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Sequences;

namespace DotNetty.Buffers
{
    public static class Sequence
    {
        public static ReadOnlySpan<byte> ToSpan(this ReadOnlySequence<byte> sequence)
        {
            SequencePosition position = sequence.Start;
            ResizableArray<byte> array = new ResizableArray<byte>(1024);
            while (sequence.TryGet(ref position, out ReadOnlyMemory<byte> buffer))
            {
                array.AddAll(buffer.Span);
            }
            return array.Span;
        }
    }
}
