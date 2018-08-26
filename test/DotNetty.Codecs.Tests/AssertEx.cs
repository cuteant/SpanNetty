// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Tests
{
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using Xunit;

    sealed class AssertEx
    {
        public static void Equal(string expected, ICharSequence actual)
        {
#if TEST40
            Assert.Equal(expected, actual.ToString());
#else
            Assert.Equal(expected, actual);
#endif
        }

        public static void Equal(ICharSequence expected, string actual)
        {
#if TEST40
            Assert.Equal(expected, actual.ToString());
#else
            Assert.Equal(expected, actual);
#endif
        }
        public static void Equal(ICharSequence expected, ICharSequence actual)
        {
#if TEST40
            Assert.True(expected.Equals(actual));
#else
            Assert.Equal(expected, actual);
#endif
        }

        public static void Equal(IByteBuffer expected, IByteBuffer actual, bool autoRelease = false)
        {
            try
            {
#if TEST40
                Assert.True(ByteBufferUtil.Equals(expected, actual));
#else
                Assert.Equal(expected, actual);
#endif
            }
            finally
            {
                if (autoRelease)
                {
                    expected.Release();
                    actual.Release();
                }
            }
        }
    }
}
