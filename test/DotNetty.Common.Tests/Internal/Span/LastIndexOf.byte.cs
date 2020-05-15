// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using CuteAnt;
using DotNetty.Common.Internal;
using Xunit;

namespace DotNetty.Common.Tests.Internal
{
    public static partial class SpanTests
    {
        [Fact]
        public static void ZeroLengthLastIndexOf_Byte()
        {
            Span<byte> span = new Span<byte>(EmptyArray<byte>.Instance);
            ref byte bStart = ref MemoryMarshal.GetReference(span);
            int idx = SpanHelpers.LastIndexOf(ref bStart, 0, span.Length);
            Assert.Equal(-1, idx);
        }

        [Fact]
        public static void DefaultFilledLastIndexOf_Byte()
        {
            for (int length = 0; length <= byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                Span<byte> span = new Span<byte>(a);
                ref byte bStart = ref MemoryMarshal.GetReference(span);

                for (int i = 0; i < length; i++)
                {
                    byte target0 = default;
                    int idx = SpanHelpers.LastIndexOf(ref bStart, target0, span.Length);
                    Assert.Equal(length - 1, idx);
                }
            }
        }

        [Fact]
        public static void TestMatchLastIndexOf_Byte()
        {
            for (int length = 0; length <= byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                for (int i = 0; i < length; i++)
                {
                    a[i] = (byte)(i + 1);
                }
                Span<byte> span = new Span<byte>(a);
                ref byte bStart = ref MemoryMarshal.GetReference(span);

                for (int targetIndex = 0; targetIndex < length; targetIndex++)
                {
                    byte target = a[targetIndex];
                    int idx = SpanHelpers.LastIndexOf(ref bStart, target, span.Length);
                    Assert.Equal(targetIndex, idx);
                }
            }
        }

        [Fact]
        public static void TestNoMatchLastIndexOf_Byte()
        {
            var rnd = new Random(42);
            for (int length = 0; length <= byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                byte target = (byte)rnd.Next(0, 256);
                for (int i = 0; i < length; i++)
                {
                    byte val = (byte)(i + 1);
                    a[i] = val == target ? (byte)(target + 1) : val;
                }
                Span<byte> span = new Span<byte>(a);
                ref byte bStart = ref MemoryMarshal.GetReference(span);

                int idx = SpanHelpers.LastIndexOf(ref bStart, target, span.Length);
                Assert.Equal(-1, idx);
            }
        }

        [Fact]
        public static void TestAllignmentNoMatchLastIndexOf_Byte()
        {
            byte[] array = new byte[4 * Vector<byte>.Count];
            for (var i = 0; i < Vector<byte>.Count; i++)
            {
                var span = new Span<byte>(array, i, 3 * Vector<byte>.Count);
                ref byte bStart = ref MemoryMarshal.GetReference(span);
                int idx = SpanHelpers.LastIndexOf(ref bStart, 5, span.Length);
                Assert.Equal(-1, idx);

                span = new Span<byte>(array, i, 3 * Vector<byte>.Count - 3);
                bStart = ref MemoryMarshal.GetReference(span);
                idx = SpanHelpers.LastIndexOf(ref bStart, 5, span.Length);
                Assert.Equal(-1, idx);
            }
        }

        [Fact]
        public static void TestAllignmentMatchLastIndexOf_Byte()
        {
            byte[] array = new byte[4 * Vector<byte>.Count];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = 5;
            }
            for (var i = 0; i < Vector<byte>.Count; i++)
            {
                var span = new Span<byte>(array, i, 3 * Vector<byte>.Count);
                ref byte bStart = ref MemoryMarshal.GetReference(span);
                int idx = SpanHelpers.LastIndexOf(ref bStart, 5, span.Length);
                Assert.Equal(span.Length - 1, idx);

                span = new Span<byte>(array, i, 3 * Vector<byte>.Count - 3);
                bStart = ref MemoryMarshal.GetReference(span);
                idx = SpanHelpers.LastIndexOf(ref bStart, 5, span.Length);
                Assert.Equal(span.Length - 1, idx);
            }
        }

        [Fact]
        public static void TestMultipleMatchLastIndexOf_Byte()
        {
            for (int length = 2; length <= byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                for (int i = 0; i < length; i++)
                {
                    byte val = (byte)(i + 1);
                    a[i] = val == 200 ? (byte)201 : val;
                }

                a[length - 1] = 200;
                a[length - 2] = 200;

                Span<byte> span = new Span<byte>(a);
                ref byte bStart = ref MemoryMarshal.GetReference(span);
                int idx = SpanHelpers.LastIndexOf(ref bStart, 200, span.Length);
                Assert.Equal(length - 1, idx);
            }
        }

        [Fact]
        public static void MakeSureNoChecksGoOutOfRangeLastIndexOf_Byte()
        {
            for (int length = 0; length <= byte.MaxValue; length++)
            {
                byte[] a = new byte[length + 2];
                a[0] = 99;
                a[length + 1] = 99;
                Span<byte> span = new Span<byte>(a, 1, length);
                ref byte bStart = ref MemoryMarshal.GetReference(span);
                int index = SpanHelpers.LastIndexOf(ref bStart, 99, span.Length);
                Assert.Equal(-1, index);
            }
        }
    }
}
