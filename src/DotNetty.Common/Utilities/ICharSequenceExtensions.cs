// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System.Runtime.InteropServices;
    using DotNetty.Common.Internal;


    public static class ICharSequenceExtensions
    {
        public static bool Contains(this ICharSequence sequence, char c)
        {
            switch (sequence)
            {
                case null:
                    return false;

                case IHasAsciiSpan hasAscii:
                    if ((uint)c > AsciiString.uMaxCharValue) { return false; }
                    var asciiSpan = hasAscii.AsciiSpan;
                    return SpanHelpers.Contains(ref MemoryMarshal.GetReference(asciiSpan), (byte)c, asciiSpan.Length);

                case IHasUtf16Span hasUtf16:
                    var utf16Span = hasUtf16.Utf16Span;
                    return SpanHelpers.Contains(ref MemoryMarshal.GetReference(utf16Span), c, utf16Span.Length);

                default:
                    int length = sequence.Count;
                    for (int i = 0; i < length; i++)
                    {
                        if (sequence[i] == c)
                        {
                            return true;
                        }
                    }
                    return false;
            }
        }
    }
}