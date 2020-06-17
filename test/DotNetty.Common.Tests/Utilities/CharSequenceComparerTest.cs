namespace DotNetty.Common.Tests.Utilities
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Common.Utilities;
    using Xunit;

    public class CharSequenceComparerTest
    {
        [Fact]
        public void CharSequenceComparerDefaultTest()
        {
            var dict = new Dictionary<ICharSequence, int>(AsciiString.CaseSensitiveHasher);
            dict.Add(AsciiString.Of("town"), 0);
            dict.Add(AsciiString.Of("foo"), 1);
            dict.Add(AsciiString.Of("bar"), 2);
            Assert.True(dict.ContainsKey((AsciiString)"town"));
            Assert.Equal(0, dict[(AsciiString)"town"]);
            Assert.True(dict.ContainsKey((AsciiString)"foo"));
            Assert.Equal(1, dict[(AsciiString)"foo"]);
            Assert.True(dict.ContainsKey((AsciiString)"bar"));
            Assert.Equal(2, dict[(AsciiString)"bar"]);


            Assert.True(dict.ContainsKey(new StringCharSequence("town")));
            Assert.Equal(0, dict[new StringCharSequence("town")]);
            Assert.True(dict.ContainsKey(new StringCharSequence("foo")));
            Assert.Equal(1, dict[new StringCharSequence("foo")]);
            Assert.True(dict.ContainsKey(new StringCharSequence("bar")));
            Assert.Equal(2, dict[new StringCharSequence("bar")]);

            dict.Clear();
        }

        [Fact]
        public void CharSequenceComparerIgnoreCaseTest()
        {
            var dict = new Dictionary<ICharSequence, int>(AsciiString.CaseInsensitiveHasher);
            dict.Add(AsciiString.Of("Town"), 0);
            dict.Add(AsciiString.Of("fOo"), 1);
            dict.Add(AsciiString.Of("baR"), 2);
            Assert.True(dict.ContainsKey((AsciiString)"town"));
            Assert.Equal(0, dict[(AsciiString)"town"]);
            Assert.True(dict.ContainsKey((AsciiString)"foo"));
            Assert.Equal(1, dict[(AsciiString)"foo"]);
            Assert.True(dict.ContainsKey((AsciiString)"bar"));
            Assert.Equal(2, dict[(AsciiString)"bar"]);

            Assert.True(AsciiString.Of("town").ContentEqualsIgnoreCase(new StringCharSequence("town")));

            Assert.True(dict.ContainsKey(new StringCharSequence("town")));
            Assert.Equal(0, dict[new StringCharSequence("town")]);
            Assert.True(dict.ContainsKey(new StringCharSequence("foo")));
            Assert.Equal(1, dict[new StringCharSequence("foo")]);
            Assert.True(dict.ContainsKey(new StringCharSequence("bar")));
            Assert.Equal(2, dict[new StringCharSequence("bar")]);

            dict.Clear();
        }
    }
}