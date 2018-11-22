// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
    /// <summary>
    /// Provides a set of protocol names used in ALPN and NPN.
    /// @see <a href="https://tools.ietf.org/html/rfc7540#section-11.1">RFC7540 (HTTP/2)</a>
    /// @see <a href="https://tools.ietf.org/html/rfc7301#section-6">RFC7301 (TLS ALPN Extension)</a>
    /// @see <a href="https://tools.ietf.org/html/draft-agl-tls-nextprotoneg-04#section-7">TLS NPN Extension Draft</a>
    /// </summary>
    public static class ApplicationProtocolNames
    {
        /// <summary>
        /// <c>h2</c>: HTTP version 2
        /// </summary>
        public const string Http2 = "h2";

        /// <summary>
        /// <c>http/1.1</c>: HTTP version 1.1
        /// </summary>
        public const string Http11 = "http/1.1";

        /// <summary>
        /// <c>spdy/3.1</c>: SPDY version 3.1
        /// </summary>
        public const string Spdy31 = "spdy/3.1";

        /// <summary>
        /// <c>spdy/3</c>: SPDY version 3
        /// </summary>
        public const string Spdy3 = "spdy/3";

        /// <summary>
        /// <c>spdy/2</c>: SPDY version 2
        /// </summary>
        public const string Spdy2 = "spdy/2";

        /// <summary>
        /// <c>spdy/1</c>: SPDY version 1
        /// </summary>
        public const string Spdy1 = "spdy/1";
    }
}