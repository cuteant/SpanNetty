// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;
using DotNetty.Common.Internal;
using Xunit;

namespace DotNetty.Common.Tests.Internal
{
    public static partial class SpanTests
    {
        [Theory]
        [InlineData("a", "a", 'a', 0)]
        [InlineData("ab", "a", 'a', 0)]
        [InlineData("aab", "a", 'a', 0)]
        [InlineData("acab", "a", 'a', 0)]
        [InlineData("acab", "c", 'c', 1)]
        [InlineData("abcdefghijklmnopqrstuvwxyz", "lo", 'l', 11)]
        [InlineData("abcdefghijklmnopqrstuvwxyz", "ol", 'l', 11)]
        [InlineData("abcdefghijklmnopqrstuvwxyz", "ll", 'l', 11)]
        [InlineData("abcdefghijklmnopqrstuvwxyz", "lmr", 'l', 11)]
        [InlineData("abcdefghijklmnopqrstuvwxyz", "rml", 'l', 11)]
        [InlineData("abcdefghijklmnopqrstuvwxyz", "mlr", 'l', 11)]
        [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz", "lmr", 'l', 11)]
        [InlineData("aaaaaaaaaaalmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz", "lmr", 'l', 11)]
        [InlineData("aaaaaaaaaaacmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz", "lmr", 'm', 12)]
        [InlineData("aaaaaaaaaaarmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz", "lmr", 'r', 11)]
        [InlineData("/localhost:5000/PATH/%2FPATH2/ HTTP/1.1", " %?", '%', 21)]
        [InlineData("/localhost:5000/PATH/%2FPATH2/?key=value HTTP/1.1", " %?", '%', 21)]
        [InlineData("/localhost:5000/PATH/PATH2/?key=value HTTP/1.1", " %?", '?', 27)]
        [InlineData("/localhost:5000/PATH/PATH2/ HTTP/1.1", " %?", ' ', 27)]
        public static void IndexOfAnyStrings_Byte(string raw, string search, char expectResult, int expectIndex)
        {
            byte[] buffers = Encoding.UTF8.GetBytes(raw);
            ref byte bStart = ref buffers[0];
            char[] searchFor = search.ToCharArray();
            byte[] searchForBytes = Encoding.UTF8.GetBytes(searchFor);

            var index = SpanHelpers.IndexOfAny(ref bStart, buffers.Length, ref searchForBytes[0], searchForBytes.Length);
            if (searchFor.Length == 1)
            {
                Assert.Equal(index, SpanHelpers.IndexOf(ref bStart, (byte)searchFor[0], buffers.Length));
            }
            else if (searchFor.Length == 2)
            {
                Assert.Equal(index, SpanHelpers.IndexOfAny(ref bStart, (byte)searchFor[0], (byte)searchFor[1], buffers.Length));
            }
            else if (searchFor.Length == 3)
            {
                Assert.Equal(index, SpanHelpers.IndexOfAny(ref bStart, (byte)searchFor[0], (byte)searchFor[1], (byte)searchFor[2], buffers.Length));
            }

            var found = buffers[index];
            Assert.Equal((byte)expectResult, found);
            Assert.Equal(expectIndex, index);
        }

        [Fact]
        public static void ZeroLengthIndexOfTwo_Byte()
        {
            Span<byte> span = new Span<byte>(EmptyArray<byte>.Instance);
            ref byte bStart = ref MemoryMarshal.GetReference(span);

            Assert.Equal(-1, SpanHelpers.IndexOfAny(ref bStart, 0, 0, span.Length));
            byte[] searchForBytes = new byte[2];
            Assert.Equal(-1, SpanHelpers.IndexOfAny(ref bStart, span.Length, ref searchForBytes[0], searchForBytes.Length));
        }

        [Fact]
        public static void DefaultFilledIndexOfTwo_Byte()
        {
            Random rnd = new Random(42);

            for (int length = 0; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                Span<byte> span = new Span<byte>(a);
                ref byte bStart = ref MemoryMarshal.GetReference(span);

                byte[] targets = { default, 99 };

                for (int i = 0; i < length; i++)
                {
                    int index = rnd.Next(0, 2) == 0 ? 0 : 1;
                    byte target0 = targets[index];
                    byte target1 = targets[(index + 1) % 2];

                    Assert.Equal(0, SpanHelpers.IndexOfAny(ref bStart, target0, target1, span.Length));
                    byte[] searchForBytes = new[] { target0, target1 };
                    Assert.Equal(0, SpanHelpers.IndexOfAny(ref bStart, span.Length, ref searchForBytes[0], searchForBytes.Length));
                }
            }
        }

        [Fact]
        public static void TestMatchTwo_Byte()
        {
            for (int length = 0; length < byte.MaxValue; length++)
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
                    byte target0 = a[targetIndex];
                    byte target1 = 0;

                    Assert.Equal(targetIndex, SpanHelpers.IndexOfAny(ref bStart, target0, target1, span.Length));
                    byte[] searchForBytes = new[] { target0, target1 };
                    Assert.Equal(targetIndex, SpanHelpers.IndexOfAny(ref bStart, span.Length, ref searchForBytes[0], searchForBytes.Length));
                }

                for (int targetIndex = 0; targetIndex < length - 1; targetIndex++)
                {
                    byte target0 = a[targetIndex];
                    byte target1 = a[targetIndex + 1];

                    Assert.Equal(targetIndex, SpanHelpers.IndexOfAny(ref bStart, target0, target1, span.Length));
                    byte[] searchForBytes = new[] { target0, target1 };
                    Assert.Equal(targetIndex, SpanHelpers.IndexOfAny(ref bStart, span.Length, ref searchForBytes[0], searchForBytes.Length));
                }

                for (int targetIndex = 0; targetIndex < length - 1; targetIndex++)
                {
                    byte target0 = 0;
                    byte target1 = a[targetIndex + 1];

                    Assert.Equal(targetIndex + 1, SpanHelpers.IndexOfAny(ref bStart, target0, target1, span.Length));
                    byte[] searchForBytes = new[] { target0, target1 };
                    Assert.Equal(targetIndex + 1, SpanHelpers.IndexOfAny(ref bStart, span.Length, ref searchForBytes[0], searchForBytes.Length));
                }
            }
        }

        [Fact]
        public static void TestNoMatchTwo_Byte()
        {
            var rnd = new Random(42);
            for (int length = 0; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                byte target0 = (byte)rnd.Next(1, 256);
                byte target1 = (byte)rnd.Next(1, 256);
                Span<byte> span = new Span<byte>(a);
                ref byte bStart = ref MemoryMarshal.GetReference(span);

                Assert.Equal(-1, SpanHelpers.IndexOfAny(ref bStart, target0, target1, span.Length));
                byte[] searchForBytes = new[] { target0, target1 };
                Assert.Equal(-1, SpanHelpers.IndexOfAny(ref bStart, span.Length, ref searchForBytes[0], searchForBytes.Length));
            }
        }

        [Fact]
        public static void TestMultipleMatchTwo_Byte()
        {
            for (int length = 3; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                for (int i = 0; i < length; i++)
                {
                    byte val = (byte)(i + 1);
                    a[i] = val == 200 ? (byte)201 : val;
                }

                a[length - 1] = 200;
                a[length - 2] = 200;
                a[length - 3] = 200;

                Span<byte> span = new Span<byte>(a);
                ref byte bStart = ref MemoryMarshal.GetReference(span);

                Assert.Equal(length - 3, SpanHelpers.IndexOfAny(ref bStart, 200, 200, span.Length));
                byte[] searchForBytes = new byte[] { 200, 200 };
                Assert.Equal(length - 3, SpanHelpers.IndexOfAny(ref bStart, span.Length, ref searchForBytes[0], searchForBytes.Length));
            }
        }

        [Fact]
        public static void MakeSureNoChecksGoOutOfRangeTwo_Byte()
        {
            for (int length = 1; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length + 2];
                a[0] = 99;
                a[length + 1] = 98;
                Span<byte> span = new Span<byte>(a, 1, length - 1);
                ref byte bStart = ref MemoryMarshal.GetReference(span);

                Assert.Equal(-1, SpanHelpers.IndexOfAny(ref bStart, 99, 98, span.Length));
                byte[] searchForBytes = new byte[] { 99, 98 };
                Assert.Equal(-1, SpanHelpers.IndexOfAny(ref bStart, span.Length, ref searchForBytes[0], searchForBytes.Length));
            }

            for (int length = 1; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length + 2];
                a[0] = 99;
                a[length + 1] = 99;
                Span<byte> span = new Span<byte>(a, 1, length - 1);
                ref byte bStart = ref MemoryMarshal.GetReference(span);

                Assert.Equal(-1, SpanHelpers.IndexOfAny(ref bStart, 99, 99, span.Length));
                byte[] searchForBytes = new byte[] { 99, 99 };
                Assert.Equal(-1, SpanHelpers.IndexOfAny(ref bStart, span.Length, ref searchForBytes[0], searchForBytes.Length));
            }
        }

        [Fact]
        public static void ZeroLengthIndexOfThree_Byte()
        {
            Span<byte> span = new Span<byte>(EmptyArray<byte>.Instance);
            ref byte bStart = ref MemoryMarshal.GetReference(span);

            Assert.Equal(-1, SpanHelpers.IndexOfAny(ref bStart, 0, 0, 0, span.Length));
            byte[] searchForBytes = new byte[3];
            Assert.Equal(-1, SpanHelpers.IndexOfAny(ref bStart, span.Length, ref searchForBytes[0], searchForBytes.Length));
        }

        [Fact]
        public static void DefaultFilledIndexOfThree_Byte()
        {
            Random rnd = new Random(42);

            for (int length = 0; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                Span<byte> span = new Span<byte>(a);
                ref byte bStart = ref MemoryMarshal.GetReference(span);

                byte[] targets = { default, 99, 98 };

                for (int i = 0; i < length; i++)
                {
                    int index = rnd.Next(0, 3);
                    byte target0 = targets[index];
                    byte target1 = targets[(index + 1) % 2];
                    byte target2 = targets[(index + 1) % 3];

                    Assert.Equal(0, SpanHelpers.IndexOfAny(ref bStart, target0, target1, target2, span.Length));
                    byte[] searchForBytes = new[] { target0, target1, target2 };
                    Assert.Equal(0, SpanHelpers.IndexOfAny(ref bStart, span.Length, ref searchForBytes[0], searchForBytes.Length));
                }
            }
        }

        [Fact]
        public static void TestMatchThree_Byte()
        {
            for (int length = 0; length < byte.MaxValue; length++)
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
                    byte target0 = a[targetIndex];
                    byte target1 = 0;
                    byte target2 = 0;

                    Assert.Equal(targetIndex, SpanHelpers.IndexOfAny(ref bStart, target0, target1, target2, span.Length));
                    byte[] searchForBytes = new[] { target0, target1, target2 };
                    Assert.Equal(targetIndex, SpanHelpers.IndexOfAny(ref bStart, span.Length, ref searchForBytes[0], searchForBytes.Length));
                }

                for (int targetIndex = 0; targetIndex < length - 2; targetIndex++)
                {
                    byte target0 = a[targetIndex];
                    byte target1 = a[targetIndex + 1];
                    byte target2 = a[targetIndex + 2];

                    Assert.Equal(targetIndex, SpanHelpers.IndexOfAny(ref bStart, target0, target1, target2, span.Length));
                    byte[] searchForBytes = new[] { target0, target1, target2 };
                    Assert.Equal(targetIndex, SpanHelpers.IndexOfAny(ref bStart, span.Length, ref searchForBytes[0], searchForBytes.Length));
                }

                for (int targetIndex = 0; targetIndex < length - 2; targetIndex++)
                {
                    byte target0 = 0;
                    byte target1 = 0;
                    byte target2 = a[targetIndex + 2];

                    Assert.Equal(targetIndex + 2, SpanHelpers.IndexOfAny(ref bStart, target0, target1, target2, span.Length));
                    byte[] searchForBytes = new[] { target0, target1, target2 };
                    Assert.Equal(targetIndex + 2, SpanHelpers.IndexOfAny(ref bStart, span.Length, ref searchForBytes[0], searchForBytes.Length));
                }
            }
        }

        [Fact]
        public static void TestNoMatchThree_Byte()
        {
            var rnd = new Random(42);
            for (int length = 0; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                byte target0 = (byte)rnd.Next(1, 256);
                byte target1 = (byte)rnd.Next(1, 256);
                byte target2 = (byte)rnd.Next(1, 256);
                Span<byte> span = new Span<byte>(a);
                ref byte bStart = ref MemoryMarshal.GetReference(span);

                Assert.Equal(-1, SpanHelpers.IndexOfAny(ref bStart, target0, target1, target2, span.Length));
                byte[] searchForBytes = new[] { target0, target1, target2 };
                Assert.Equal(-1, SpanHelpers.IndexOfAny(ref bStart, span.Length, ref searchForBytes[0], searchForBytes.Length));
            }
        }

        [Fact]
        public static void TestMultipleMatchThree_Byte()
        {
            for (int length = 4; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                for (int i = 0; i < length; i++)
                {
                    byte val = (byte)(i + 1);
                    a[i] = val == 200 ? (byte)201 : val;
                }

                a[length - 1] = 200;
                a[length - 2] = 200;
                a[length - 3] = 200;
                a[length - 4] = 200;

                Span<byte> span = new Span<byte>(a);
                ref byte bStart = ref MemoryMarshal.GetReference(span);

                Assert.Equal(length - 4, SpanHelpers.IndexOfAny(ref bStart, 200, 200, 200, span.Length));
                byte[] searchForBytes = new byte[] { 200, 200, 200 };
                Assert.Equal(length - 4, SpanHelpers.IndexOfAny(ref bStart, span.Length, ref searchForBytes[0], searchForBytes.Length));
            }
        }

        [Fact]
        public static void MakeSureNoChecksGoOutOfRangeThree_Byte()
        {
            for (int length = 1; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length + 2];
                a[0] = 99;
                a[length + 1] = 98;
                Span<byte> span = new Span<byte>(a, 1, length - 1);
                ref byte bStart = ref MemoryMarshal.GetReference(span);

                Assert.Equal(-1, SpanHelpers.IndexOfAny(ref bStart, 99, 98, 99, span.Length));
                byte[] searchForBytes = new byte[] { 99, 98, 99 };
                Assert.Equal(-1, SpanHelpers.IndexOfAny(ref bStart, span.Length, ref searchForBytes[0], searchForBytes.Length));
            }

            for (int length = 1; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length + 2];
                a[0] = 99;
                a[length + 1] = 99;
                Span<byte> span = new Span<byte>(a, 1, length - 1);
                ref byte bStart = ref MemoryMarshal.GetReference(span);

                Assert.Equal(-1, SpanHelpers.IndexOfAny(ref bStart, 99, 99, 99, span.Length));
                byte[] searchForBytes = new byte[] { 99, 99, 99 };
                Assert.Equal(-1, SpanHelpers.IndexOfAny(ref bStart, span.Length, ref searchForBytes[0], searchForBytes.Length));
            }
        }

        [Fact]
        public static void ZeroLengthIndexOfMany_Byte()
        {
            Span<byte> span = new Span<byte>(EmptyArray<byte>.Instance);
            ref byte bStart = ref MemoryMarshal.GetReference(span);

            var searchForBytes = new byte[] { 0, 0, 0, 0 };
            int idx = SpanHelpers.IndexOfAny(ref bStart, span.Length, ref searchForBytes[0], searchForBytes.Length);
            Assert.Equal(-1, idx);

            var values = new Span<byte>(new byte[] { });
            idx = SpanHelpers.IndexOfAny(ref bStart, span.Length, ref MemoryMarshal.GetReference(values), values.Length);
            Assert.Equal(-1, idx);
        }

        [Fact]
        public static void DefaultFilledIndexOfMany_Byte()
        {
            for (int length = 0; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                Span<byte> span = new Span<byte>(a);
                ref byte bStart = ref MemoryMarshal.GetReference(span);

                var values = new byte[] { default, 99, 98, 0 };

                for (int i = 0; i < length; i++)
                {
                    int idx = SpanHelpers.IndexOfAny(ref bStart, span.Length, ref values[0], values.Length);
                    Assert.Equal(0, idx);
                }
            }
        }

        [Fact]
        public static void TestMatchMany_Byte()
        {
            for (int length = 0; length < byte.MaxValue; length++)
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
                    var values = new byte[] { a[targetIndex], 0, 0, 0 };
                    int idx = SpanHelpers.IndexOfAny(ref bStart, span.Length, ref values[0], values.Length);
                    Assert.Equal(targetIndex, idx);
                }

                for (int targetIndex = 0; targetIndex < length - 3; targetIndex++)
                {
                    var values = new byte[] { a[targetIndex], a[targetIndex + 1], a[targetIndex + 2], a[targetIndex + 3] };
                    int idx = SpanHelpers.IndexOfAny(ref bStart, span.Length, ref values[0], values.Length);
                    Assert.Equal(targetIndex, idx);
                }

                for (int targetIndex = 0; targetIndex < length - 3; targetIndex++)
                {
                    var values = new byte[] { 0, 0, 0, a[targetIndex + 3] };
                    int idx = SpanHelpers.IndexOfAny(ref bStart, span.Length, ref values[0], values.Length);
                    Assert.Equal(targetIndex + 3, idx);
                }
            }
        }

        [Fact]
        public static void TestMatchValuesLargerMany_Byte()
        {
            var rnd = new Random(42);
            for (int length = 2; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                int expectedIndex = length / 2;
                for (int i = 0; i < length; i++)
                {
                    if (i == expectedIndex)
                    {
                        continue;
                    }
                    a[i] = 255;
                }
                ref byte bStart = ref a[0];

                byte[] targets = new byte[length * 2];
                for (int i = 0; i < targets.Length; i++)
                {
                    if (i == length + 1)
                    {
                        continue;
                    }
                    targets[i] = (byte)rnd.Next(1, 255);
                }

                int idx = SpanHelpers.IndexOfAny(ref bStart, a.Length, ref targets[0], targets.Length);
                Assert.Equal(expectedIndex, idx);
            }
        }

        [Fact]
        public static void TestNoMatchMany_Byte()
        {
            var rnd = new Random(42);
            for (int length = 1; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                byte[] targets = new byte[length];
                for (int i = 0; i < targets.Length; i++)
                {
                    targets[i] = (byte)rnd.Next(1, 256);
                }
                ref byte bStart = ref a[0];

                int idx = SpanHelpers.IndexOfAny(ref bStart, a.Length, ref targets[0], targets.Length);
                Assert.Equal(-1, idx);
            }
        }

        [Fact]
        public static void TestNoMatchValuesLargerMany_Byte()
        {
            var rnd = new Random(42);
            for (int length = 1; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                byte[] targets = new byte[length * 2];
                for (int i = 0; i < targets.Length; i++)
                {
                    targets[i] = (byte)rnd.Next(1, 256);
                }
                ref byte bStart = ref a[0];

                int idx = SpanHelpers.IndexOfAny(ref bStart, a.Length, ref targets[0], targets.Length);
                Assert.Equal(-1, idx);
            }
        }

        [Fact]
        public static void TestMultipleMatchMany_Byte()
        {
            for (int length = 5; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                for (int i = 0; i < length; i++)
                {
                    byte val = (byte)(i + 1);
                    a[i] = val == 200 ? (byte)201 : val;
                }

                a[length - 1] = 200;
                a[length - 2] = 200;
                a[length - 3] = 200;
                a[length - 4] = 200;
                a[length - 5] = 200;

                var values = new byte[] { 200, 200, 200, 200, 200, 200, 200, 200, 200 };
                int idx = SpanHelpers.IndexOfAny(ref a[0], a.Length, ref values[0], values.Length);
                Assert.Equal(length - 5, idx);
            }
        }

        [Fact]
        public static void MakeSureNoChecksGoOutOfRangeMany_Byte()
        {
            for (int length = 1; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length + 2];
                a[0] = 99;
                a[length + 1] = 98;
                Span<byte> span = new Span<byte>(a, 1, length - 1);
                ref byte bStart = ref MemoryMarshal.GetReference(span);
                var values = new byte[] { 99, 98, 99, 98, 99, 98 };
                int index = SpanHelpers.IndexOfAny(ref bStart, span.Length, ref values[0], values.Length);
                Assert.Equal(-1, index);
            }

            for (int length = 1; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length + 2];
                a[0] = 99;
                a[length + 1] = 99;
                Span<byte> span = new Span<byte>(a, 1, length - 1);
                ref byte bStart = ref MemoryMarshal.GetReference(span);
                var values = new byte[] { 99, 99, 99, 99, 99, 99 };
                int index = SpanHelpers.IndexOfAny(ref bStart, span.Length, ref values[0], values.Length);
                Assert.Equal(-1, index);
            }
        }
    }
}
