// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using DotNetty.Common.Utilities;
    using Xunit;

    public sealed class HttpMethodTest
    {
        [Fact]
        public void ValueOf_NonStandardVerb_HonorCaseSensitivity()
        {
            var expectedHttpMethod = "Foo";
            var httpMethod = HttpMethod.ValueOf(new AsciiString(expectedHttpMethod));
            Assert.Equal(expectedHttpMethod, httpMethod.Name);
        }
    }
}
