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
        public static void ZeroLengthIndexOf_Char()
        {
            Span<char> sp = new Span<char>(EmptyArray<char>.Instance);
            ref char cStart = ref MemoryMarshal.GetReference(sp);
            int idx = SpanHelpers.IndexOf(ref cStart, (char)0, sp.Length);
            Assert.Equal(-1, idx);
        }

        [Fact]
        public static void TestMatch_Char()
        {
            for (int length = 0; length < 32; length++)
            {
                char[] a = new char[length];
                for (int i = 0; i < length; i++)
                {
                    a[i] = (char)(i + 1);
                }
                Span<char> span = new Span<char>(a);
                ref char cStart = ref MemoryMarshal.GetReference(span);

                for (int targetIndex = 0; targetIndex < length; targetIndex++)
                {
                    char target = a[targetIndex];
                    int idx = SpanHelpers.IndexOf(ref cStart, target, span.Length);
                    Assert.Equal(targetIndex, idx);
                }
            }
        }

        [Fact]
        public static void TestMultipleMatch_Char()
        {
            for (int length = 2; length < 32; length++)
            {
                char[] a = new char[length];
                for (int i = 0; i < length; i++)
                {
                    a[i] = (char)(i + 1);
                }

                a[length - 1] = (char)200;
                a[length - 2] = (char)200;

                ref char cStart = ref a[0];
                int idx = SpanHelpers.IndexOf(ref cStart, (char)200, a.Length);
                Assert.Equal(length - 2, idx);
            }
        }

        [Fact]
        public static void MakeSureNoChecksGoOutOfRange_Char()
        {
            for (int length = 0; length < 100; length++)
            {
                char[] a = new char[length + 2];
                a[0] = '9';
                a[length + 1] = '9';
                Span<char> span = new Span<char>(a, 1, length);
                ref char cStart = ref MemoryMarshal.GetReference(span);
                int index = SpanHelpers.IndexOf(ref cStart, '9', span.Length);
                Assert.Equal(-1, index);
            }
        }
    }
}
