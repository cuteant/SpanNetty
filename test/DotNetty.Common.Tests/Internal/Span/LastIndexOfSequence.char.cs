// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using CuteAnt;
using DotNetty.Common.Internal;
using Xunit;

namespace DotNetty.Common.Tests.Internal
{
    public static partial class SpanTests
    {
        [Fact]
        public static void LastIndexOfSequenceMatchAtStart_Char()
        {
            Span<char> span = new Span<char>(new char[] { '5', '1', '7', '2', '3', '7', '7', '4', '5', '7', '7', '7', '8', '6', '6', '7', '7', '8', '9' });
            Span<char> value = new Span<char>(new char[] { '5', '1', '7' });
            int index = SpanHelpers.LastIndexOf(ref MemoryMarshal.GetReference(span), span.Length, ref MemoryMarshal.GetReference(value), value.Length);
            Assert.Equal(0, index);
        }

        [Fact]
        public static void LastIndexOfSequenceMultipleMatch_Char()
        {
            Span<char> span = new Span<char>(new char[] { '1', '2', '3', '1', '2', '3', '1', '2', '3', '1' });
            Span<char> value = new Span<char>(new char[] { '2', '3' });
            int index = SpanHelpers.LastIndexOf(ref MemoryMarshal.GetReference(span), span.Length, ref MemoryMarshal.GetReference(value), value.Length);
            Assert.Equal(7, index);
        }

        [Fact]
        public static void LastIndexOfSequenceRestart_Char()
        {
            Span<char> span = new Span<char>(new char[] { '5', '1', '7', '2', '3', '7', '7', '4', '5', '7', '7', '7', '8', '6', '6', '7', '7', '6', '9', '7', '0', '1' });
            Span<char> value = new Span<char>(new char[] { '7', '7', '8' });
            int index = SpanHelpers.LastIndexOf(ref MemoryMarshal.GetReference(span), span.Length, ref MemoryMarshal.GetReference(value), value.Length);
            Assert.Equal(10, index);
        }

        [Fact]
        public static void LastIndexOfSequenceNoMatch_Char()
        {
            Span<char> span = new Span<char>(new char[] { '0', '1', '7', '2', '3', '7', '7', '4', '5', '7', '7', '7', '8', '6', '6', '7', '7', '8', '9' });
            Span<char> value = new Span<char>(new char[] { '7', '7', '8', 'X' });
            int index = SpanHelpers.LastIndexOf(ref MemoryMarshal.GetReference(span), span.Length, ref MemoryMarshal.GetReference(value), value.Length);
            Assert.Equal(-1, index);
        }

        [Fact]
        public static void LastIndexOfSequenceNotEvenAHeadMatch_Char()
        {
            Span<char> span = new Span<char>(new char[] { '0', '1', '7', '2', '3', '7', '7', '4', '5', '7', '7', '7', '8', '6', '6', '7', '7', '8', '9' });
            Span<char> value = new Span<char>(new char[] { 'X', '7', '8', '9' });
            int index = SpanHelpers.LastIndexOf(ref MemoryMarshal.GetReference(span), span.Length, ref MemoryMarshal.GetReference(value), value.Length);
            Assert.Equal(-1, index);
        }

        [Fact]
        public static void LastIndexOfSequenceMatchAtVeryEnd_Char()
        {
            Span<char> span = new Span<char>(new char[] { '0', '1', '2', '3', '4', '5' });
            Span<char> value = new Span<char>(new char[] { '3', '4', '5' });
            int index = SpanHelpers.LastIndexOf(ref MemoryMarshal.GetReference(span), span.Length, ref MemoryMarshal.GetReference(value), value.Length);
            Assert.Equal(3, index);
        }

        [Fact]
        public static void LastIndexOfSequenceJustPastVeryEnd_Char()
        {
            Span<char> span = new Span<char>(new char[] { '0', '1', '2', '3', '4', '5' }, 0, 5);
            Span<char> value = new Span<char>(new char[] { '3', '4', '5' });
            int index = SpanHelpers.LastIndexOf(ref MemoryMarshal.GetReference(span), span.Length, ref MemoryMarshal.GetReference(value), value.Length);
            Assert.Equal(-1, index);
        }

        [Fact]
        public static void LastIndexOfSequenceZeroLengthValue_Char()
        {
            // A zero-length value is always "found" at the start of the span.
            Span<char> span = new Span<char>(new char[] { '0', '1', '7', '2', '3', '7', '7', '4', '5', '7', '7', '7', '8', '6', '6', '7', '7', '8', '9' });
            Span<char> value = new Span<char>(EmptyArray<char>.Instance);
            int index = SpanHelpers.LastIndexOf(ref MemoryMarshal.GetReference(span), span.Length, ref MemoryMarshal.GetReference(value), value.Length);
            Assert.Equal(0, index);
        }

        [Fact]
        public static void LastIndexOfSequenceZeroLengthSpan_Char()
        {
            Span<char> span = new Span<char>(EmptyArray<char>.Instance);
            Span<char> value = new Span<char>(new char[] { '1', '2', '3' });
            int index = SpanHelpers.LastIndexOf(ref MemoryMarshal.GetReference(span), span.Length, ref MemoryMarshal.GetReference(value), value.Length);
            Assert.Equal(-1, index);
        }

        [Fact]
        public static void LastIndexOfSequenceLengthOneValue_Char()
        {
            // A zero-length value is always "found" at the start of the span.
            Span<char> span = new Span<char>(new char[] { '0', '1', '2', '3', '4', '5' });
            Span<char> value = new Span<char>(new char[] { '2' });
            int index = SpanHelpers.LastIndexOf(ref MemoryMarshal.GetReference(span), span.Length, ref MemoryMarshal.GetReference(value), value.Length);
            Assert.Equal(2, index);
        }

        [Fact]
        public static void LastIndexOfSequenceLengthOneValueAtVeryEnd_Char()
        {
            // A zero-length value is always "found" at the start of the span.
            Span<char> span = new Span<char>(new char[] { '0', '1', '2', '3', '4', '5' });
            Span<char> value = new Span<char>(new char[] { '5' });
            int index = SpanHelpers.LastIndexOf(ref MemoryMarshal.GetReference(span), span.Length, ref MemoryMarshal.GetReference(value), value.Length);
            Assert.Equal(5, index);
        }

        [Fact]
        public static void LastIndexOfSequenceLengthOneValueMultipleTimes_Char()
        {
            // A zero-length value is always "found" at the start of the span.
            Span<char> span = new Span<char>(new char[] { '0', '1', '5', '3', '4', '5' });
            Span<char> value = new Span<char>(new char[] { '5' });
            int index = SpanHelpers.LastIndexOf(ref MemoryMarshal.GetReference(span), span.Length, ref MemoryMarshal.GetReference(value), value.Length);
            Assert.Equal(5, index);
        }

        [Fact]
        public static void LastIndexOfSequenceLengthOneValueJustPasttVeryEnd_Char()
        {
            // A zero-length value is always "found" at the start of the span.
            Span<char> span = new Span<char>(new char[] { '0', '1', '2', '3', '4', '5' }, 0, 5);
            Span<char> value = new Span<char>(new char[] { '5' });
            int index = SpanHelpers.LastIndexOf(ref MemoryMarshal.GetReference(span), span.Length, ref MemoryMarshal.GetReference(value), value.Length);
            Assert.Equal(-1, index);
        }
    }
}
