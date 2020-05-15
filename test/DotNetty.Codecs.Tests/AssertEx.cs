// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Tests
{
    using DotNetty.Buffers;
    using Xunit;

    sealed class AssertEx
    {
        public static void Equal(IByteBuffer expected, IByteBuffer actual, bool autoRelease = false)
        {
            try
            {
                Assert.Equal(expected, actual);
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
