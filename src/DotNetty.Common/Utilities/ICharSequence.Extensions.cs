// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;

    partial interface ICharSequence : IEquatable<ICharSequence>
    {
    }

#if !NET40
    public interface IHasAsciiSpan
    {
        int Count { get; }

        ReadOnlySpan<byte> AsciiSpan { get; }
    }

    public interface IHasUtf16Span
    {
        int Count { get; }

        ReadOnlySpan<char> Utf16Span { get; }
    }

    public interface IHasUtf8Span
    {
        int Count { get; }

        ReadOnlySpan<byte> Utf8Span { get; }
    }
#endif
}
