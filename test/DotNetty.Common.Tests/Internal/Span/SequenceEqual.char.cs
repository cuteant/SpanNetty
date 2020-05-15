// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using DotNetty.Common.Internal;
using Xunit;

namespace DotNetty.Common.Tests.Internal
{
    public static partial class SpanTests
    {
        [Fact]
        public static void ZeroLengthSequenceEqual_Char()
        {
            char[] a = new char[3];

            Span<char> first = new Span<char>(a, 1, 0);
            Span<char> second = new Span<char>(a, 2, 0);
            bool b = SpanHelpers.SequenceEqual(ref MemoryMarshal.GetReference(first), ref MemoryMarshal.GetReference(second), first.Length);
            Assert.True(b);
        }

        [Fact]
        public static void SameSpanSequenceEqual_Char()
        {
            char[] a = { '4', '5', '6' };
            Span<char> span = new Span<char>(a);
            ref char cStart = ref MemoryMarshal.GetReference(span);
            bool b = SpanHelpers.SequenceEqual(ref cStart, ref cStart, span.Length);
            Assert.True(b);
        }

        [Fact]
        public static void LengthMismatchSequenceEqual_Char()
        {
            char[] a = { '4', '5', '6' };
            Span<char> first = new Span<char>(a, 1, 2);
            Span<char> second = new Span<char>(a, 0, 2);
            bool b = SpanHelpers.SequenceEqual(ref MemoryMarshal.GetReference(first), ref MemoryMarshal.GetReference(second), first.Length);
            Assert.False(b);
        }

        [Fact]
        public static void SequenceEqualNoMatch_Char()
        {
            for (int length = 1; length < 32; length++)
            {
                for (int mismatchIndex = 0; mismatchIndex < length; mismatchIndex++)
                {
                    char[] first = new char[length];
                    char[] second = new char[length];
                    for (int i = 0; i < length; i++)
                    {
                        first[i] = second[i] = (char)(i + 1);
                    }

                    second[mismatchIndex] = (char)(second[mismatchIndex] + 1);

                    Span<char> firstSpan = new Span<char>(first);
                    ReadOnlySpan<char> secondSpan = new ReadOnlySpan<char>(second);
                    bool b = SpanHelpers.SequenceEqual(ref MemoryMarshal.GetReference(firstSpan), ref MemoryMarshal.GetReference(secondSpan), firstSpan.Length);
                    Assert.False(b);
                }
            }
        }

        [Fact]
        public static void MakeSureNoSequenceEqualChecksGoOutOfRange_Char()
        {
            for (int length = 0; length < 100; length++)
            {
                char[] first = new char[length + 2];
                first[0] = '9';
                first[length + 1] = '9';
                char[] second = new char[length + 2];
                second[0] = 'a';
                second[length + 1] = 'a';
                Span<char> span1 = new Span<char>(first, 1, length);
                ReadOnlySpan<char> span2 = new ReadOnlySpan<char>(second, 1, length);
                bool b = SpanHelpers.SequenceEqual(ref MemoryMarshal.GetReference(span1), ref MemoryMarshal.GetReference(span2), span1.Length);
                Assert.True(b);
            }
        }
    }
}
