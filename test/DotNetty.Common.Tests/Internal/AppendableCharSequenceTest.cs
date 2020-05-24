
namespace DotNetty.Common.Tests.Internal
{
    using System;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;
    using Xunit;

    public class AppendableCharSequenceTest
    {
        [Fact]
        public void TestSimpleAppend()
        {
            TestSimpleAppend0(new AppendableCharSequence(128));
        }

        [Fact]
        public void TestAppendString()
        {
            TestAppendString0(new AppendableCharSequence(128));
        }

        [Fact]
        public void TestAppendAppendableCharSequence()
        {
            AppendableCharSequence seq = new AppendableCharSequence(128);

            String text = "testdata";
            AppendableCharSequence seq2 = new AppendableCharSequence(128);
            seq2.Append((AsciiString)text);
            seq.Append(seq2);

            Assert.Equal(text, seq.ToString());
            Assert.Equal(text.Substring(1, text.Length - 2), seq.SubSequence(1, text.Length - 1).ToString());

            AssertEqualsChars((AsciiString)text, seq);
        }

        [Fact]
        public void TestSimpleAppendWithExpand()
        {
            TestSimpleAppend0(new AppendableCharSequence(2));
        }

        [Fact]
        public void TestAppendStringWithExpand()
        {
            TestAppendString0(new AppendableCharSequence(2));
        }

        [Fact]
        public void TestSubSequence()
        {
            AppendableCharSequence master = new AppendableCharSequence(26);
            master.Append((AsciiString)"abcdefghijlkmonpqrstuvwxyz");
            Assert.Equal("abcdefghij", master.SubSequence(0, 10).ToString());
        }

        [Fact]
        public void TestEmptySubSequence()
        {
            AppendableCharSequence master = new AppendableCharSequence(26);
            master.Append((AsciiString)"abcdefghijlkmonpqrstuvwxyz");
            AppendableCharSequence sub = (AppendableCharSequence)master.SubSequence(0, 0);
            Assert.Empty(sub);
            sub.Append('b');
            Assert.Equal('b', sub[0]);
        }

        private static void TestSimpleAppend0(AppendableCharSequence seq)
        {
            string text = "testdata";
            for (int i = 0; i < text.Length; i++)
            {
                seq.Append(text[i]);
            }

            Assert.Equal(text, seq.ToString());
            Assert.Equal(text.Substring(1, text.Length - 2), seq.SubSequence(1, text.Length - 1).ToString());

            AssertEqualsChars((AsciiString)text, seq);

            seq.Reset();
            Assert.Empty(seq);
        }

        private static void TestAppendString0(AppendableCharSequence seq)
        {
            string text = "testdata";
            seq.Append((AsciiString)text);

            Assert.Equal(text, seq.ToString());
            Assert.Equal(text.Substring(1, text.Length - 2), seq.SubSequence(1, text.Length - 1).ToString());

            AssertEqualsChars((AsciiString)text, seq);

            seq.Reset();
            Assert.Empty(seq);
        }

        private static void AssertEqualsChars(ICharSequence seq1, ICharSequence seq2)
        {
            Assert.Equal(seq1.Count, seq2.Count);
            for (int i = 0; i < seq1.Count; i++)
            {
                Assert.Equal(seq1[i], seq2[i]);
            }
        }
    }
}
