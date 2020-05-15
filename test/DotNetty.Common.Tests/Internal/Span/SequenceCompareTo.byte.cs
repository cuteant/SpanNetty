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
        public static void ZeroLengthSequenceCompareTo_Byte()
        {
            byte[] a = new byte[3];

            Span<byte> first = new Span<byte>(a, 1, 0);
            Span<byte> second = new Span<byte>(a, 2, 0);
            int result = SpanHelpers.SequenceCompareTo(ref MemoryMarshal.GetReference(first), first.Length, ref MemoryMarshal.GetReference(second), second.Length);
            Assert.Equal(0, result);
        }

        [Fact]
        public static void SameSpanSequenceCompareTo_Byte()
        {
            byte[] a = { 4, 5, 6 };
            Span<byte> span = new Span<byte>(a);
            ref byte bStart = ref MemoryMarshal.GetReference(span);
            int result = SpanHelpers.SequenceCompareTo(ref bStart, span.Length, ref bStart, span.Length);
            Assert.Equal(0, result);
        }

        [Fact]
        public static void SequenceCompareToArrayImplicit_Byte()
        {
            byte[] a = { 4, 5, 6 };
            Span<byte> first = new Span<byte>(a, 0, 3);
            int result = SpanHelpers.SequenceCompareTo(ref MemoryMarshal.GetReference(first), first.Length, ref a[0], a.Length);
            Assert.Equal(0, result);
        }

        [Fact]
        public static void SequenceCompareToArraySegmentImplicit_Byte()
        {
            byte[] src = { 1, 2, 3 };
            byte[] dst = { 5, 1, 2, 3, 10 };
            var segment = new ArraySegment<byte>(dst, 1, 3);

            Span<byte> first = new Span<byte>(src, 0, 3);
            Span<byte> second = segment;
            int result = SpanHelpers.SequenceCompareTo(ref MemoryMarshal.GetReference(first), first.Length, ref MemoryMarshal.GetReference(second), second.Length);
            Assert.Equal(0, result);
        }

        [Fact]
        public static void LengthMismatchSequenceCompareTo_Byte()
        {
            byte[] a = { 4, 5, 6 };
            Span<byte> first = new Span<byte>(a, 0, 2);
            Span<byte> second = new Span<byte>(a, 0, 3);
            int result = SpanHelpers.SequenceCompareTo(ref MemoryMarshal.GetReference(first), first.Length, ref MemoryMarshal.GetReference(second), second.Length);
            Assert.True(result < 0);

            result = SpanHelpers.SequenceCompareTo(ref MemoryMarshal.GetReference(second), second.Length, ref MemoryMarshal.GetReference(first), first.Length);
            Assert.True(result > 0);

            // one sequence is empty
            first = new Span<byte>(a, 1, 0);

            result = SpanHelpers.SequenceCompareTo(ref MemoryMarshal.GetReference(first), first.Length, ref MemoryMarshal.GetReference(second), second.Length);
            Assert.True(result < 0);

            result = SpanHelpers.SequenceCompareTo(ref MemoryMarshal.GetReference(second), second.Length, ref MemoryMarshal.GetReference(first), first.Length);
            Assert.True(result > 0);
        }

        [Fact]
        public static void SequenceCompareToEqual_Byte()
        {
            for (int length = 1; length < 128; length++)
            {
                var first = new byte[length];
                var second = new byte[length];
                for (int i = 0; i < length; i++)
                {
                    first[i] = second[i] = (byte)(i + 1);
                }

                var firstSpan = new Span<byte>(first);
                var secondSpan = new Span<byte>(second);

                Assert.Equal(0, SpanHelpers.SequenceCompareTo(ref MemoryMarshal.GetReference(firstSpan), firstSpan.Length, ref MemoryMarshal.GetReference(secondSpan), secondSpan.Length));
                Assert.Equal(0, SpanHelpers.SequenceCompareTo(ref MemoryMarshal.GetReference(secondSpan), secondSpan.Length, ref MemoryMarshal.GetReference(firstSpan), firstSpan.Length));
            }
        }

        [Fact]
        public static void SequenceCompareToWithSingleMismatch_Byte()
        {
            for (int length = 1; length < 128; length++)
            {
                for (int mismatchIndex = 0; mismatchIndex < length; mismatchIndex++)
                {
                    byte[] first = new byte[length];
                    byte[] second = new byte[length];
                    for (int i = 0; i < length; i++)
                    {
                        first[i] = second[i] = (byte)(i + 1);
                    }

                    second[mismatchIndex] = (byte)(second[mismatchIndex] + 1);

                    Span<byte> firstSpan = new Span<byte>(first);
                    ReadOnlySpan<byte> secondSpan = new ReadOnlySpan<byte>(second);
                    int result = SpanHelpers.SequenceCompareTo(ref MemoryMarshal.GetReference(firstSpan), firstSpan.Length, ref MemoryMarshal.GetReference(secondSpan), secondSpan.Length);
                    Assert.True(result < 0);

                    result = SpanHelpers.SequenceCompareTo(ref MemoryMarshal.GetReference(secondSpan), secondSpan.Length, ref MemoryMarshal.GetReference(firstSpan), firstSpan.Length);
                    Assert.True(result > 0);
                }
            }
        }

        [Fact]
        public static void SequenceCompareToNoMatch_Byte()
        {
            for (int length = 1; length < 128; length++)
            {
                byte[] first = new byte[length];
                byte[] second = new byte[length];

                for (int i = 0; i < length; i++)
                {
                    first[i] = (byte)(i + 1);
                    second[i] = (byte)(byte.MaxValue - i);
                }

                Span<byte> firstSpan = new Span<byte>(first);
                ReadOnlySpan<byte> secondSpan = new ReadOnlySpan<byte>(second);
                int result = SpanHelpers.SequenceCompareTo(ref MemoryMarshal.GetReference(firstSpan), firstSpan.Length, ref MemoryMarshal.GetReference(secondSpan), secondSpan.Length);
                Assert.True(result < 0);

                result = SpanHelpers.SequenceCompareTo(ref MemoryMarshal.GetReference(secondSpan), secondSpan.Length, ref MemoryMarshal.GetReference(firstSpan), firstSpan.Length);
                Assert.True(result > 0);
            }
        }

        [Fact]
        public static void MakeSureNoSequenceCompareToChecksGoOutOfRange_Byte()
        {
            for (int length = 0; length < 100; length++)
            {
                byte[] first = new byte[length + 2];
                first[0] = 99;
                first[length + 1] = 99;
                byte[] second = new byte[length + 2];
                second[0] = 100;
                second[length + 1] = 100;
                Span<byte> span1 = new Span<byte>(first, 1, length);
                ReadOnlySpan<byte> span2 = new ReadOnlySpan<byte>(second, 1, length);
                int result = SpanHelpers.SequenceCompareTo(ref MemoryMarshal.GetReference(span1), span1.Length, ref MemoryMarshal.GetReference(span2), span2.Length);
                Assert.Equal(0, result);
            }
        }
    }
}
