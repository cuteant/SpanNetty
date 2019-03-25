// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// borrowed from https://github.com/dotnet/corefxlab/tree/master/src/System.Buffers.Primitives/System/Buffers

#if !NET40

namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    public interface IBufferOperation
    {
        OperationStatus Execute(ReadOnlySpan<byte> input, Span<byte> output, out int bytesConsumed, out int bytesWritten);
    }
}

#endif