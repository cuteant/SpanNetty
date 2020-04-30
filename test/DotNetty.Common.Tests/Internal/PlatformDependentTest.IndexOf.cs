#if !TEST40

namespace DotNetty.Common.Tests.Internal
{
    using System;
    using System.Linq;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using System.Text;
    using CuteAnt;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;
    using Xunit;

    public partial class PlatformDependentTest
    {
        [Fact]
        public void ZeroLengthIndexOf_Byte()
        {
            Span<byte> sp = new Span<byte>(EmptyArray<byte>.Instance);
            //int idx = sp.IndexOf<byte>(0);
            int idx = PlatformDependent.FindIndex(ref MemoryMarshal.GetReference(sp), x => x == 0, sp.Length);
            Assert.Equal(-1, idx);
        }

        [Fact]
        public void DefaultFilledIndexOf_Byte()
        {
            for (int length = 0; length <= byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                Span<byte> span = new Span<byte>(a);

                for (int i = 0; i < length; i++)
                {
                    byte target0 = default;
                    //int idx = span.IndexOf(target0);
                    int idx = PlatformDependent.FindIndex(ref MemoryMarshal.GetReference(span), x => x == target0, span.Length);
                    Assert.Equal(0, idx);
                }
            }
        }

        [Fact]
        public void TestMatch_Byte()
        {
            for (int length = 0; length <= byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                for (int i = 0; i < length; i++)
                {
                    a[i] = (byte)(i + 1);
                }
                Span<byte> span = new Span<byte>(a);

                for (int targetIndex = 0; targetIndex < length; targetIndex++)
                {
                    byte target = a[targetIndex];
                    //int idx = span.IndexOf(target);
                    int idx = PlatformDependent.FindIndex(ref MemoryMarshal.GetReference(span), x => x == target, span.Length);
                    Assert.Equal(targetIndex, idx);
                }
            }
        }

        [Fact]
        public void TestNoMatch_Byte()
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

                //int idx = span.IndexOf(target);
                int idx = PlatformDependent.FindIndex(ref MemoryMarshal.GetReference(span), x => x == target, span.Length);
                Assert.Equal(-1, idx);
            }
        }

        [Fact]
        public void TestAllignmentNoMatch_Byte()
        {
            byte[] array = new byte[4 * Vector<byte>.Count];
            for (var i = 0; i < Vector<byte>.Count; i++)
            {
                var span = new Span<byte>(array, i, 3 * Vector<byte>.Count);
                //int idx = span.IndexOf((byte)'1');
                int idx = PlatformDependent.FindIndex(ref MemoryMarshal.GetReference(span), x => x == (byte)'1', span.Length);
                Assert.Equal(-1, idx);

                span = new Span<byte>(array, i, 3 * Vector<byte>.Count - 3);
                //idx = span.IndexOf((byte)'1');
                idx = PlatformDependent.FindIndex(ref MemoryMarshal.GetReference(span), x => x == (byte)'1', span.Length);
                Assert.Equal(-1, idx);
            }
        }

        [Fact]
        public void TestAllignmentMatch_Byte()
        {
            byte[] array = new byte[4 * Vector<byte>.Count];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = 5;
            }
            for (var i = 0; i < Vector<byte>.Count; i++)
            {
                var span = new Span<byte>(array, i, 3 * Vector<byte>.Count);
                //int idx = span.IndexOf<byte>(5);
                int idx = PlatformDependent.FindIndex(ref MemoryMarshal.GetReference(span), x => x == 5, span.Length);
                Assert.Equal(0, idx);

                span = new Span<byte>(array, i, 3 * Vector<byte>.Count - 3);
                //idx = span.IndexOf<byte>(5);
                idx = PlatformDependent.FindIndex(ref MemoryMarshal.GetReference(span), x => x == 5, span.Length);
                Assert.Equal(0, idx);
            }
        }

        [Fact]
        public void TestMultipleMatch_Byte()
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
                //int idx = span.IndexOf<byte>(200);
                int idx = PlatformDependent.FindIndex(ref MemoryMarshal.GetReference(span), x => x == 200, span.Length);
                Assert.Equal(length - 2, idx);
            }
        }

        [Fact]
        public void MakeSureNoChecksGoOutOfRange_Byte()
        {
            for (int length = 0; length <= byte.MaxValue; length++)
            {
                byte[] a = new byte[length + 2];
                a[0] = 99;
                a[length + 1] = 99;
                Span<byte> span = new Span<byte>(a, 1, length);
                //int index = span.IndexOf<byte>(99);
                int index = PlatformDependent.FindIndex(ref MemoryMarshal.GetReference(span), x => x == 99, span.Length);
                Assert.Equal(-1, index);
            }
        }

        [Fact]
        public void ZeroLengthLastIndexOf_Byte()
        {
            Span<byte> sp = new Span<byte>(EmptyArray<byte>.Instance);
            //int idx = sp.LastIndexOf<byte>(0);
            int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(sp), x => x == 0, sp.Length);
            Assert.Equal(-1, idx);
        }

        [Fact]
        public void DefaultFilledLastIndexOf_Byte()
        {
            for (int length = 0; length <= byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                Span<byte> span = new Span<byte>(a);

                for (int i = 0; i < length; i++)
                {
                    byte target0 = default;
                    //int idx = span.LastIndexOf(target0);
                    int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span), x => x == target0, span.Length);
                    Assert.Equal(length - 1, idx);
                }
            }
        }

        [Fact]
        public void TestMatchLastIndexOf_Byte()
        {
            for (int length = 0; length <= byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                for (int i = 0; i < length; i++)
                {
                    a[i] = (byte)(i + 1);
                }
                Span<byte> span = new Span<byte>(a);

                for (int targetIndex = 0; targetIndex < length; targetIndex++)
                {
                    byte target = a[targetIndex];
                    //int idx = span.LastIndexOf(target);
                    int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span), x => x == target, span.Length);
                    Assert.Equal(targetIndex, idx);
                }
            }
        }

        [Fact]
        public void TestNoMatchLastIndexOf_Byte()
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

                //int idx = span.LastIndexOf(target);
                int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span), x => x == target, span.Length);
                Assert.Equal(-1, idx);
            }
        }

        [Fact]
        public void TestAllignmentNoMatchLastIndexOf_Byte()
        {
            byte[] array = new byte[4 * Vector<byte>.Count];
            for (var i = 0; i < Vector<byte>.Count; i++)
            {
                var span = new Span<byte>(array, i, 3 * Vector<byte>.Count);
                //int idx = span.LastIndexOf<byte>(5);
                int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span), x => x == 5, span.Length);
                Assert.Equal(-1, idx);

                span = new Span<byte>(array, i, 3 * Vector<byte>.Count - 3);
                //idx = span.LastIndexOf<byte>(5);
                idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span), x => x == 5, span.Length);
                Assert.Equal(-1, idx);
            }
        }

        [Fact]
        public void TestAllignmentMatchLastIndexOf_Byte()
        {
            byte[] array = new byte[4 * Vector<byte>.Count];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = 5;
            }
            for (var i = 0; i < Vector<byte>.Count; i++)
            {
                var span = new Span<byte>(array, i, 3 * Vector<byte>.Count);
                //int idx = span.LastIndexOf<byte>(5);
                int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span), x => x == 5, span.Length);
                Assert.Equal(span.Length - 1, idx);

                span = new Span<byte>(array, i, 3 * Vector<byte>.Count - 3);
                //idx = span.LastIndexOf<byte>(5);
                idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span), x => x == 5, span.Length);
                Assert.Equal(span.Length - 1, idx);
            }
        }

        [Fact]
        public void TestMultipleMatchLastIndexOf_Byte()
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
                //int idx = span.LastIndexOf<byte>(200);
                int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span), x => x == 200, span.Length);
                Assert.Equal(length - 1, idx);
            }
        }

        [Fact]
        public void MakeSureNoChecksGoOutOfRangeLastIndexOf_Byte()
        {
            for (int length = 0; length <= byte.MaxValue; length++)
            {
                byte[] a = new byte[length + 2];
                a[0] = 99;
                a[length + 1] = 99;
                Span<byte> span = new Span<byte>(a, 1, length);
                //int index = span.LastIndexOf<byte>(99);
                int index = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span), x => x == 99, span.Length);
                Assert.Equal(-1, index);
            }
        }

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
            var span = new Span<byte>(buffers);
            char[] searchFor = search.ToCharArray();
            byte[] searchForBytes = Encoding.UTF8.GetBytes(searchFor);

            var index = -1;
            if (searchFor.Length == 1)
            {
                //index = span.IndexOf((byte)searchFor[0]);
                index = PlatformDependent.FindIndex(ref MemoryMarshal.GetReference(span),
                    x => x == (byte)searchFor[0],
                    span.Length);
            }
            else if (searchFor.Length == 2)
            {
                //index = span.IndexOfAny((byte)searchFor[0], (byte)searchFor[1]);
                index = PlatformDependent.FindIndex(ref MemoryMarshal.GetReference(span),
                    x => x == (byte)searchFor[0] || x == (byte)searchFor[1],
                    span.Length);
            }
            else if (searchFor.Length == 3)
            {
                //index = span.IndexOfAny((byte)searchFor[0], (byte)searchFor[1], (byte)searchFor[2]);
                index = PlatformDependent.FindIndex(ref MemoryMarshal.GetReference(span),
                    x => x == (byte)searchFor[0] || x == (byte)searchFor[1] || x == (byte)searchFor[2],
                    span.Length);
            }
            else
            {
                //index = span.IndexOfAny(new ReadOnlySpan<byte>(searchForBytes));
                index = PlatformDependent.FindIndex(ref MemoryMarshal.GetReference(span),
                    x => searchForBytes.Contains(x),
                    span.Length);
            }

            var found = span[index];
            Assert.Equal((byte)expectResult, found);
            Assert.Equal(expectIndex, index);
        }

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
        public static void IndexOfAnyStrings_Byte1(string raw, string search, char expectResult, int expectIndex)
        {
            byte[] buffers = Encoding.UTF8.GetBytes(raw);
            var span = new Span<byte>(buffers);
            char[] searchFor = search.ToCharArray();
            byte[] searchForBytes = Encoding.UTF8.GetBytes(searchFor);

            var index = -1;
            if (searchFor.Length == 1)
            {
                //index = span.IndexOf((byte)searchFor[0]);
                index = PlatformDependent.ForEachByte(ref MemoryMarshal.GetReference(span),
                    new ByteProcessor(x => x != (byte)searchFor[0]),
                    span.Length);
            }
            else if (searchFor.Length == 2)
            {
                //index = span.IndexOfAny((byte)searchFor[0], (byte)searchFor[1]);
                index = PlatformDependent.ForEachByte(ref MemoryMarshal.GetReference(span),
                    new ByteProcessor(x => !(x == (byte)searchFor[0] || x == (byte)searchFor[1])),
                    span.Length);
            }
            else if (searchFor.Length == 3)
            {
                //index = span.IndexOfAny((byte)searchFor[0], (byte)searchFor[1], (byte)searchFor[2]);
                index = PlatformDependent.ForEachByte(ref MemoryMarshal.GetReference(span),
                    new ByteProcessor(x => !(x == (byte)searchFor[0] || x == (byte)searchFor[1] || x == (byte)searchFor[2])),
                    span.Length);
            }
            else
            {
                //index = span.IndexOfAny(new ReadOnlySpan<byte>(searchForBytes));
                index = PlatformDependent.ForEachByte(ref MemoryMarshal.GetReference(span),
                    new ByteProcessor(x => !searchForBytes.Contains(x)),
                    span.Length);
            }

            var found = span[index];
            Assert.Equal((byte)expectResult, found);
            Assert.Equal(expectIndex, index);
        }

        [Fact]
        public static void ZeroLengthIndexOfTwo_Byte()
        {
            Span<byte> sp = new Span<byte>(EmptyArray<byte>.Instance);
            int idx = sp.IndexOfAny<byte>(0, 0);
            Assert.Equal(-1, idx);
        }

        [Fact]
        public static void DefaultFilledIndexOfTwo_Byte()
        {
            Random rnd = new Random(42);

            for (int length = 0; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                Span<byte> span = new Span<byte>(a);

                byte[] targets = { default, 99 };

                for (int i = 0; i < length; i++)
                {
                    int index = rnd.Next(0, 2) == 0 ? 0 : 1;
                    byte target0 = targets[index];
                    byte target1 = targets[(index + 1) % 2];
                    int idx = span.IndexOfAny(target0, target1);
                    Assert.Equal(0, idx);
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

                for (int targetIndex = 0; targetIndex < length; targetIndex++)
                {
                    byte target0 = a[targetIndex];
                    byte target1 = 0;
                    int idx = span.IndexOfAny(target0, target1);
                    Assert.Equal(targetIndex, idx);
                }

                for (int targetIndex = 0; targetIndex < length - 1; targetIndex++)
                {
                    byte target0 = a[targetIndex];
                    byte target1 = a[targetIndex + 1];
                    int idx = span.IndexOfAny(target0, target1);
                    Assert.Equal(targetIndex, idx);
                }

                for (int targetIndex = 0; targetIndex < length - 1; targetIndex++)
                {
                    byte target0 = 0;
                    byte target1 = a[targetIndex + 1];
                    int idx = span.IndexOfAny(target0, target1);
                    Assert.Equal(targetIndex + 1, idx);
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

                int idx = span.IndexOfAny(target0, target1);
                Assert.Equal(-1, idx);
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
                int idx = span.IndexOfAny<byte>(200, 200);
                Assert.Equal(length - 3, idx);
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
                int index = span.IndexOfAny<byte>(99, 98);
                Assert.Equal(-1, index);
            }

            for (int length = 1; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length + 2];
                a[0] = 99;
                a[length + 1] = 99;
                Span<byte> span = new Span<byte>(a, 1, length - 1);
                int index = span.IndexOfAny<byte>(99, 99);
                Assert.Equal(-1, index);
            }
        }

        [Fact]
        public static void ZeroLengthIndexOfThree_Byte()
        {
            Span<byte> sp = new Span<byte>(EmptyArray<byte>.Instance);
            int idx = sp.IndexOfAny<byte>(0, 0, 0);
            Assert.Equal(-1, idx);
        }

        [Fact]
        public static void DefaultFilledIndexOfThree_Byte()
        {
            Random rnd = new Random(42);

            for (int length = 0; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                Span<byte> span = new Span<byte>(a);

                byte[] targets = { default, 99, 98 };

                for (int i = 0; i < length; i++)
                {
                    int index = rnd.Next(0, 3);
                    byte target0 = targets[index];
                    byte target1 = targets[(index + 1) % 2];
                    byte target2 = targets[(index + 1) % 3];
                    int idx = span.IndexOfAny(target0, target1, target2);
                    Assert.Equal(0, idx);
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

                for (int targetIndex = 0; targetIndex < length; targetIndex++)
                {
                    byte target0 = a[targetIndex];
                    byte target1 = 0;
                    byte target2 = 0;
                    int idx = span.IndexOfAny(target0, target1, target2);
                    Assert.Equal(targetIndex, idx);
                }

                for (int targetIndex = 0; targetIndex < length - 2; targetIndex++)
                {
                    byte target0 = a[targetIndex];
                    byte target1 = a[targetIndex + 1];
                    byte target2 = a[targetIndex + 2];
                    int idx = span.IndexOfAny(target0, target1, target2);
                    Assert.Equal(targetIndex, idx);
                }

                for (int targetIndex = 0; targetIndex < length - 2; targetIndex++)
                {
                    byte target0 = 0;
                    byte target1 = 0;
                    byte target2 = a[targetIndex + 2];
                    int idx = span.IndexOfAny(target0, target1, target2);
                    Assert.Equal(targetIndex + 2, idx);
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

                int idx = span.IndexOfAny(target0, target1, target2);
                Assert.Equal(-1, idx);
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
                int idx = span.IndexOfAny<byte>(200, 200, 200);
                Assert.Equal(length - 4, idx);
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
                int index = span.IndexOfAny<byte>(99, 98, 99);
                Assert.Equal(-1, index);
            }

            for (int length = 1; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length + 2];
                a[0] = 99;
                a[length + 1] = 99;
                Span<byte> span = new Span<byte>(a, 1, length - 1);
                int index = span.IndexOfAny<byte>(99, 99, 99);
                Assert.Equal(-1, index);
            }
        }

        [Fact]
        public static void ZeroLengthIndexOfMany_Byte()
        {
            Span<byte> sp = new Span<byte>(EmptyArray<byte>.Instance);
            var values = new ReadOnlySpan<byte>(new byte[] { 0, 0, 0, 0 });
            int idx = sp.IndexOfAny(values);
            Assert.Equal(-1, idx);

            values = new ReadOnlySpan<byte>(new byte[] { });
            idx = sp.IndexOfAny(values);
            Assert.Equal(0, idx);
        }

        [Fact]
        public static void DefaultFilledIndexOfMany_Byte()
        {
            for (int length = 0; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                Span<byte> span = new Span<byte>(a);

                var values = new ReadOnlySpan<byte>(new byte[] { default, 99, 98, 0 });

                for (int i = 0; i < length; i++)
                {
                    int idx = span.IndexOfAny(values);
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

                for (int targetIndex = 0; targetIndex < length; targetIndex++)
                {
                    var values = new ReadOnlySpan<byte>(new byte[] { a[targetIndex], 0, 0, 0 });
                    int idx = span.IndexOfAny(values);
                    Assert.Equal(targetIndex, idx);
                }

                for (int targetIndex = 0; targetIndex < length - 3; targetIndex++)
                {
                    var values = new ReadOnlySpan<byte>(new byte[] { a[targetIndex], a[targetIndex + 1], a[targetIndex + 2], a[targetIndex + 3] });
                    int idx = span.IndexOfAny(values);
                    Assert.Equal(targetIndex, idx);
                }

                for (int targetIndex = 0; targetIndex < length - 3; targetIndex++)
                {
                    var values = new ReadOnlySpan<byte>(new byte[] { 0, 0, 0, a[targetIndex + 3] });
                    int idx = span.IndexOfAny(values);
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
                Span<byte> span = new Span<byte>(a);

                byte[] targets = new byte[length * 2];
                for (int i = 0; i < targets.Length; i++)
                {
                    if (i == length + 1)
                    {
                        continue;
                    }
                    targets[i] = (byte)rnd.Next(1, 255);
                }

                var values = new ReadOnlySpan<byte>(targets);
                int idx = span.IndexOfAny(values);
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
                Span<byte> span = new Span<byte>(a);
                var values = new ReadOnlySpan<byte>(targets);

                int idx = span.IndexOfAny(values);
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
                Span<byte> span = new Span<byte>(a);
                var values = new ReadOnlySpan<byte>(targets);

                int idx = span.IndexOfAny(values);
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

                Span<byte> span = new Span<byte>(a);
                var values = new ReadOnlySpan<byte>(new byte[] { 200, 200, 200, 200, 200, 200, 200, 200, 200 });
                int idx = span.IndexOfAny(values);
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
                var values = new ReadOnlySpan<byte>(new byte[] { 99, 98, 99, 98, 99, 98 });
                int index = span.IndexOfAny(values);
                Assert.Equal(-1, index);
            }

            for (int length = 1; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length + 2];
                a[0] = 99;
                a[length + 1] = 99;
                Span<byte> span = new Span<byte>(a, 1, length - 1);
                var values = new ReadOnlySpan<byte>(new byte[] { 99, 99, 99, 99, 99, 99 });
                int index = span.IndexOfAny(values);
                Assert.Equal(-1, index);
            }
        }

        [Theory]
        [InlineData("a", "a", 'a', 0)]
        [InlineData("ab", "a", 'a', 0)]
        [InlineData("aab", "a", 'a', 1)]
        [InlineData("acab", "a", 'a', 2)]
        [InlineData("acab", "c", 'c', 1)]
        [InlineData("abcdefghijklmnopqrstuvwxyz", "lo", 'o', 14)]
        [InlineData("abcdefghijklmnopqrstuvwxyz", "ol", 'o', 14)]
        [InlineData("abcdefghijklmnopqrstuvwxyz", "ll", 'l', 11)]
        [InlineData("abcdefghijklmnopqrstuvwxyz", "lmr", 'r', 17)]
        [InlineData("abcdefghijklmnopqrstuvwxyz", "rml", 'r', 17)]
        [InlineData("abcdefghijklmnopqrstuvwxyz", "mlr", 'r', 17)]
        [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz", "lmr", 'r', 43)]
        [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrzzzzzzzz", "lmr", 'r', 43)]
        [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqxzzzzzzzz", "lmr", 'm', 38)]
        [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqlzzzzzzzz", "lmr", 'l', 43)]
        [InlineData("/localhost:5000/PATH/%2FPATH2/ HTTP/1.1", " %?", ' ', 30)]
        [InlineData("/localhost:5000/PATH/%2FPATH2/?key=value HTTP/1.1", " %?", ' ', 40)]
        [InlineData("/localhost:5000/PATH/PATH2/?key=value HTTP/1.1", " %?", ' ', 37)]
        [InlineData("/localhost:5000/PATH/PATH2/ HTTP/1.1", " %?", ' ', 27)]
        public static void LastIndexOfAnyStrings_Byte(string raw, string search, char expectResult, int expectIndex)
        {
            byte[] buffers = Encoding.UTF8.GetBytes(raw);
            var span = new Span<byte>(buffers);
            char[] searchFor = search.ToCharArray();
            byte[] searchForBytes = Encoding.UTF8.GetBytes(searchFor);

            var index = -1;
            if (searchFor.Length == 1)
            {
                //index = span.LastIndexOf((byte)searchFor[0]);
                index = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                    x => x == (byte)searchFor[0],
                    span.Length);
            }
            else if (searchFor.Length == 2)
            {
                //index = span.LastIndexOfAny((byte)searchFor[0], (byte)searchFor[1]);
                index = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                    x => x == (byte)searchFor[0] || x == (byte)searchFor[1],
                    span.Length);
            }
            else if (searchFor.Length == 3)
            {
                //index = span.LastIndexOfAny((byte)searchFor[0], (byte)searchFor[1], (byte)searchFor[2]);
                index = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                    x => x == (byte)searchFor[0] || x == (byte)searchFor[1] || x == (byte)searchFor[2],
                    span.Length);
            }
            else
            {
                //index = span.LastIndexOfAny(new ReadOnlySpan<byte>(searchForBytes));
                index = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                    x => searchForBytes.Contains(x),
                    span.Length);
            }

            var found = span[index];
            Assert.Equal((byte)expectResult, found);
            Assert.Equal(expectIndex, index);
        }

        [Fact]
        public static void ZeroLengthLastIndexOfAny_Byte_TwoByte()
        {
            Span<byte> sp = new Span<byte>(EmptyArray<byte>.Instance);
            //int idx = sp.LastIndexOfAny<byte>(0, 0);
            int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(sp),
                x => x == 0,
                sp.Length);
            Assert.Equal(-1, idx);
        }

        [Fact]
        public static void DefaultFilledLastIndexOfAny_Byte_TwoByte()
        {
            Random rnd = new Random(42);

            for (int length = 0; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                Span<byte> span = new Span<byte>(a);

                byte[] targets = { default, 99 };

                for (int i = 0; i < length; i++)
                {
                    int index = rnd.Next(0, 2) == 0 ? 0 : 1;
                    byte target0 = targets[index];
                    byte target1 = targets[(index + 1) % 2];
                    //int idx = span.LastIndexOfAny(target0, target1);
                    int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                        x => x == target0 || x == target1,
                        span.Length);
                    Assert.Equal(span.Length - 1, idx);
                }
            }
        }

        [Fact]
        public static void TestMatchLastIndexOfAny_Byte_TwoByte()
        {
            for (int length = 0; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                for (int i = 0; i < length; i++)
                {
                    a[i] = (byte)(i + 1);
                }
                Span<byte> span = new Span<byte>(a);

                for (int targetIndex = 0; targetIndex < length; targetIndex++)
                {
                    byte target0 = a[targetIndex];
                    byte target1 = 0;
                    //int idx = span.LastIndexOfAny(target0, target1);
                    int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                        x => x == target0 || x == target1,
                        span.Length);
                    Assert.Equal(targetIndex, idx);
                }

                for (int targetIndex = 0; targetIndex < length - 1; targetIndex++)
                {
                    byte target0 = a[targetIndex];
                    byte target1 = a[targetIndex + 1];
                    //int idx = span.LastIndexOfAny(target0, target1);
                    int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                        x => x == target0 || x == target1,
                        span.Length);
                    Assert.Equal(targetIndex + 1, idx);
                }

                for (int targetIndex = 0; targetIndex < length - 1; targetIndex++)
                {
                    byte target0 = 0;
                    byte target1 = a[targetIndex + 1];
                    //int idx = span.LastIndexOfAny(target0, target1);
                    int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                        x => x == target0 || x == target1,
                        span.Length);
                    Assert.Equal(targetIndex + 1, idx);
                }
            }
        }

        [Fact]
        public static void TestNoMatchLastIndexOfAny_Byte_TwoByte()
        {
            var rnd = new Random(42);
            for (int length = 0; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                byte target0 = (byte)rnd.Next(1, 256);
                byte target1 = (byte)rnd.Next(1, 256);
                Span<byte> span = new Span<byte>(a);

                //int idx = span.LastIndexOfAny(target0, target1);
                int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                    x => x == target0 || x == target1,
                    span.Length);
                Assert.Equal(-1, idx);
            }
        }

        [Fact]
        public static void TestMultipleMatchLastIndexOfAny_Byte_TwoByte()
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
                //int idx = span.LastIndexOfAny<byte>(200, 200);
                int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                    x => x == 200 || x == 200,
                    span.Length);
                Assert.Equal(length - 1, idx);
            }
        }

        [Fact]
        public static void MakeSureNoChecksGoOutOfRangeLastIndexOfAny_Byte_TwoByte()
        {
            for (int length = 1; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length + 2];
                a[0] = 99;
                a[length + 1] = 98;
                Span<byte> span = new Span<byte>(a, 1, length - 1);
                //int index = span.LastIndexOfAny<byte>(99, 98);
                int index = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                    x => x == 99 || x == 98,
                    span.Length);
                Assert.Equal(-1, index);
            }

            for (int length = 1; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length + 2];
                a[0] = 99;
                a[length + 1] = 99;
                Span<byte> span = new Span<byte>(a, 1, length - 1);
                //int index = span.LastIndexOfAny<byte>(99, 99);
                int index = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                    x => x == 99 || x == 99,
                    span.Length);
                Assert.Equal(-1, index);
            }
        }

        [Fact]
        public static void ZeroLengthIndexOf_Byte_ThreeByte()
        {
            Span<byte> sp = new Span<byte>(EmptyArray<byte>.Instance);
            //int idx = sp.LastIndexOfAny<byte>(0, 0, 0);
            int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(sp),
                x => x == 0,
                sp.Length);
            Assert.Equal(-1, idx);
        }

        [Fact]
        public static void DefaultFilledLastIndexOfAny_Byte_ThreeByte()
        {
            Random rnd = new Random(42);

            for (int length = 0; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                Span<byte> span = new Span<byte>(a);

                byte[] targets = { default, 99, 98 };

                for (int i = 0; i < length; i++)
                {
                    int index = rnd.Next(0, 3);
                    byte target0 = targets[index];
                    byte target1 = targets[(index + 1) % 2];
                    byte target2 = targets[(index + 1) % 3];
                    //int idx = span.LastIndexOfAny(target0, target1, target2);
                    int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                        x => x == target0 || x == target1 || x == target2,
                        span.Length);
                    Assert.Equal(span.Length - 1, idx);
                }
            }
        }

        [Fact]
        public static void TestMatchLastIndexOfAny_Byte_ThreeByte()
        {
            for (int length = 0; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                for (int i = 0; i < length; i++)
                {
                    a[i] = (byte)(i + 1);
                }
                Span<byte> span = new Span<byte>(a);

                for (int targetIndex = 0; targetIndex < length; targetIndex++)
                {
                    byte target0 = a[targetIndex];
                    byte target1 = 0;
                    byte target2 = 0;
                    //int idx = span.LastIndexOfAny(target0, target1, target2);
                    int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                        x => x == target0 || x == target1 || x == target2,
                        span.Length);
                    Assert.Equal(targetIndex, idx);
                }

                for (int targetIndex = 0; targetIndex < length - 2; targetIndex++)
                {
                    byte target0 = a[targetIndex];
                    byte target1 = a[targetIndex + 1];
                    byte target2 = a[targetIndex + 2];
                    //int idx = span.LastIndexOfAny(target0, target1, target2);
                    int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                        x => x == target0 || x == target1 || x == target2,
                        span.Length);
                    Assert.Equal(targetIndex + 2, idx);
                }

                for (int targetIndex = 0; targetIndex < length - 2; targetIndex++)
                {
                    byte target0 = 0;
                    byte target1 = 0;
                    byte target2 = a[targetIndex + 2];
                    //int idx = span.LastIndexOfAny(target0, target1, target2);
                    int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                        x => x == target0 || x == target1 || x == target2,
                        span.Length);
                    Assert.Equal(targetIndex + 2, idx);
                }
            }
        }

        [Fact]
        public static void TestNoMatchLastIndexOfAny_Byte_ThreeByte()
        {
            var rnd = new Random(42);
            for (int length = 0; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                byte target0 = (byte)rnd.Next(1, 256);
                byte target1 = (byte)rnd.Next(1, 256);
                byte target2 = (byte)rnd.Next(1, 256);
                Span<byte> span = new Span<byte>(a);

                //int idx = span.LastIndexOfAny(target0, target1, target2);
                int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                    x => x == target0 || x == target1 || x == target2,
                    span.Length);
                Assert.Equal(-1, idx);
            }
        }

        [Fact]
        public static void TestMultipleMatchLastIndexOfAny_Byte_ThreeByte()
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
                //int idx = span.LastIndexOfAny<byte>(200, 200, 200);
                int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                    x => x == 200,
                    span.Length);
                Assert.Equal(length - 1, idx);
            }
        }

        [Fact]
        public static void MakeSureNoChecksGoOutOfRangeLastIndexOfAny_Byte_ThreeByte()
        {
            for (int length = 1; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length + 2];
                a[0] = 99;
                a[length + 1] = 98;
                Span<byte> span = new Span<byte>(a, 1, length - 1);
                //int index = span.LastIndexOfAny<byte>(99, 98, 99);
                int index = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                    x => x == 99 || x == 98,
                    span.Length);
                Assert.Equal(-1, index);
            }

            for (int length = 1; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length + 2];
                a[0] = 99;
                a[length + 1] = 99;
                Span<byte> span = new Span<byte>(a, 1, length - 1);
                //int index = span.LastIndexOfAny<byte>(99, 99, 99);
                int index = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                    x => x == 99,
                    span.Length);
                Assert.Equal(-1, index);
            }
        }

        [Fact]
        public static void ZeroLengthLastIndexOfAny_Byte_ManyByte()
        {
            Span<byte> sp = new Span<byte>(EmptyArray<byte>.Instance);
            var values = new ReadOnlySpan<byte>(new byte[] { 0, 0, 0, 0 });
            int idx = sp.LastIndexOfAny(values);
            Assert.Equal(-1, idx);

            values = new ReadOnlySpan<byte>(new byte[] { });
            idx = sp.LastIndexOfAny(values);
            Assert.Equal(0, idx);
        }

        [Fact]
        public static void DefaultFilledLastIndexOfAny_Byte_ManyByte()
        {
            for (int length = 0; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                Span<byte> span = new Span<byte>(a);

                //var values = new ReadOnlySpan<byte>(new byte[] { default, 99, 98, 0 });
                var values = new byte[] { default, 99, 98, 0 };

                for (int i = 0; i < length; i++)
                {
                    //int idx = span.LastIndexOfAny(values);
                    int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                        x => values.Contains(x),
                        span.Length);
                    Assert.Equal(span.Length - 1, idx);
                }
            }
        }

        [Fact]
        public static void TestMatchLastIndexOfAny_Byte_ManyByte()
        {
            for (int length = 0; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length];
                for (int i = 0; i < length; i++)
                {
                    a[i] = (byte)(i + 1);
                }
                Span<byte> span = new Span<byte>(a);

                for (int targetIndex = 0; targetIndex < length; targetIndex++)
                {
                    //var values = new ReadOnlySpan<byte>(new byte[] { a[targetIndex], 0, 0, 0 });
                    var values = new byte[] { a[targetIndex], 0, 0, 0 };
                    //int idx = span.LastIndexOfAny(values);
                    int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                        x => values.Contains(x),
                        span.Length);
                    Assert.Equal(targetIndex, idx);
                }

                for (int targetIndex = 0; targetIndex < length - 3; targetIndex++)
                {
                    //var values = new ReadOnlySpan<byte>(new byte[] { a[targetIndex], a[targetIndex + 1], a[targetIndex + 2], a[targetIndex + 3] });
                    var values = new byte[] { a[targetIndex], a[targetIndex + 1], a[targetIndex + 2], a[targetIndex + 3] };
                    //int idx = span.LastIndexOfAny(values);
                    int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                        x => values.Contains(x),
                        span.Length);
                    Assert.Equal(targetIndex + 3, idx);
                }

                for (int targetIndex = 0; targetIndex < length - 3; targetIndex++)
                {
                    //var values = new ReadOnlySpan<byte>(new byte[] { 0, 0, 0, a[targetIndex + 3] });
                    var values = new byte[] { 0, 0, 0, a[targetIndex + 3] };
                    //int idx = span.LastIndexOfAny(values);
                    int idx = PlatformDependent.FindLastIndex(ref MemoryMarshal.GetReference(span),
                        x => values.Contains(x),
                        span.Length);
                    Assert.Equal(targetIndex + 3, idx);
                }
            }
        }

        [Fact]
        public static void TestMatchValuesLargerLastIndexOfAny_Byte_ManyByte()
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
                Span<byte> span = new Span<byte>(a);

                byte[] targets = new byte[length * 2];
                for (int i = 0; i < targets.Length; i++)
                {
                    if (i == length + 1)
                    {
                        continue;
                    }
                    targets[i] = (byte)rnd.Next(1, 255);
                }

                var values = new ReadOnlySpan<byte>(targets);
                int idx = span.LastIndexOfAny(values);
                Assert.Equal(expectedIndex, idx);
            }
        }

        [Fact]
        public static void TestNoMatchLastIndexOfAny_Byte_ManyByte()
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
                Span<byte> span = new Span<byte>(a);
                var values = new ReadOnlySpan<byte>(targets);

                int idx = span.LastIndexOfAny(values);
                Assert.Equal(-1, idx);
            }
        }

        [Fact]
        public static void TestNoMatchValuesLargerLastIndexOfAny_Byte_ManyByte()
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
                Span<byte> span = new Span<byte>(a);
                var values = new ReadOnlySpan<byte>(targets);

                int idx = span.LastIndexOfAny(values);
                Assert.Equal(-1, idx);
            }
        }

        [Fact]
        public static void TestMultipleMatchLastIndexOfAny_Byte_ManyByte()
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

                Span<byte> span = new Span<byte>(a);
                var values = new ReadOnlySpan<byte>(new byte[] { 200, 200, 200, 200, 200, 200, 200, 200, 200 });
                int idx = span.LastIndexOfAny(values);
                Assert.Equal(length - 1, idx);
            }
        }

        [Fact]
        public static void MakeSureNoChecksGoOutOfRangeLastIndexOfAny_Byte_ManyByte()
        {
            for (int length = 1; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length + 2];
                a[0] = 99;
                a[length + 1] = 98;
                Span<byte> span = new Span<byte>(a, 1, length - 1);
                var values = new ReadOnlySpan<byte>(new byte[] { 99, 98, 99, 98, 99, 98 });
                int index = span.LastIndexOfAny(values);
                Assert.Equal(-1, index);
            }

            for (int length = 1; length < byte.MaxValue; length++)
            {
                byte[] a = new byte[length + 2];
                a[0] = 99;
                a[length + 1] = 99;
                Span<byte> span = new Span<byte>(a, 1, length - 1);
                var values = new ReadOnlySpan<byte>(new byte[] { 99, 99, 99, 99, 99, 99 });
                int index = span.LastIndexOfAny(values);
                Assert.Equal(-1, index);
            }
        }
    }
}

#endif