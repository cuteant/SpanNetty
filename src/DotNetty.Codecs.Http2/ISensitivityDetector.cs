// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Common.Utilities;

    /// <summary>
    /// Determine if a header name/value pair is treated as
    /// <a href="http://tools.ietf.org/html/draft-ietf-httpbis-header-compression-12#section-7.1.3">sensitive</a>.
    /// If the object can be dynamically modified and shared across multiple connections it may need to be thread safe.
    /// </summary>
    public interface ISensitivityDetector
    {
        /// <summary>
        /// Determine if a header <paramref name="name"/>/<paramref name="value"/> pair should be treated as
        /// <a href="http://tools.ietf.org/html/draft-ietf-httpbis-header-compression-12#section-7.1.3">sensitive</a>.
        /// </summary>
        /// <param name="name">The name for the header.</param>
        /// <param name="value">The value of the header.</param>
        /// <returns><c>true</c> if a header <paramref name="name"/>/<paramref name="value"/> pair should be treated as
        /// <a href="http://tools.ietf.org/html/draft-ietf-httpbis-header-compression-12#section-7.1.3">sensitive</a>.
        /// <c>false</c> otherwise.</returns>
        bool IsSensitive(ICharSequence name, ICharSequence value);
    }
}