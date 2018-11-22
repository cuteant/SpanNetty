// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Transport.Channels;

    /// <summary>
    /// A default implementation of <see cref="IHttp2PromisedRequestVerifier"/> which always returns positive responses for
    /// all verification challenges.
    /// </summary>
    public sealed class AlwaysVerifyPromisedRequestVerifier : IHttp2PromisedRequestVerifier
    {
        public static readonly AlwaysVerifyPromisedRequestVerifier Instance = new AlwaysVerifyPromisedRequestVerifier();

        private AlwaysVerifyPromisedRequestVerifier() { }

        /// <inheritdoc />
        public bool IsAuthoritative(IChannelHandlerContext ctx, IHttp2Headers headers) => true;

        /// <inheritdoc />
        public bool IsCacheable(IHttp2Headers headers) => true;

        /// <inheritdoc />
        public bool IsSafe(IHttp2Headers headers) => true;
    }
}