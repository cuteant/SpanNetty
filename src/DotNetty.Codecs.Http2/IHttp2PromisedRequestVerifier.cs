// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Provides an extensibility point for users to define the validity of push requests.
    /// See <a href="https://tools.ietf.org/html/rfc7540#section-8.2">[RFC 7540], Section 8.2</a>.
    /// </summary>
    public interface IHttp2PromisedRequestVerifier
    {
        /// <summary>
        /// Determine if a <see cref="IHttp2Headers"/> are authoritative for a particular {@link ChannelHandlerContext}.
        /// </summary>
        /// <remarks>See <a href="https://tools.ietf.org/html/rfc7540#section-10.1">[RFC 7540], Section 10.1</a>.</remarks>
        /// <param name="ctx">The context on which the <paramref name="headers"/> where received on.</param>
        /// <param name="headers">The headers to be verified.</param>
        /// <returns>Return <c>true</c> if the <paramref name="ctx"/> is authoritative for the <paramref name="headers"/>, <c>false</c> otherwise.</returns>
        bool IsAuthoritative(IChannelHandlerContext ctx, IHttp2Headers headers);

        /// <summary>
        /// Determine if a request is cacheable.
        /// </summary>
        /// <remarks>See <a href="https://tools.ietf.org/html/rfc7231#section-4.2.3">[RFC 7231], Section 4.2.3</a>.</remarks>
        /// <param name="headers">The headers for a push request.</param>
        /// <returns>Return <c>true</c> if the request associated with <paramref name="headers"/> is known to be cacheable,
        /// <c>false</c> otherwise.</returns>
        bool IsCacheable(IHttp2Headers headers);

        /// <summary>
        /// Determine if a request is safe.
        /// </summary>
        /// <remarks>See <a href="https://tools.ietf.org/html/rfc7231#section-4.2.1">[RFC 7231], Section 4.2.1</a>.</remarks>
        /// <param name="headers">The headers for a push request.</param>
        /// <returns>Return <c>true</c> if the request associated with <paramref name="headers"/> is known to be safe,
        /// <c>false</c> otherwise.</returns>
        bool IsSafe(IHttp2Headers headers);
    }
}
