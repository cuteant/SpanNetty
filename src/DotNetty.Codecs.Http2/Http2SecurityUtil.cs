/*
 * Copyright 2012 The Netty Project
 *
 * The Netty Project licenses this file to you under the Apache License,
 * version 2.0 (the "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at:
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Codecs.Http2
{
    using System.Collections.Generic;

    /// <summary>
    /// Provides utilities related to security requirements specific to HTTP/2.
    /// </summary>
    public static class Http2SecurityUtil
    {
        /// <summary>
        /// The following list is derived from <a
        /// href="http://docs.oracle.com/javase/8/docs/technotes/guides/security/SunProviders.html">SunJSSE Supported
        /// Ciphers</a> and <a
        /// href="https://wiki.mozilla.org/Security/Server_Side_TLS#Modern_compatibility">Mozilla Modern Cipher
        /// Suites</a> in accordance with the <a
        /// href="https://tools.ietf.org/html/rfc7540#section-9.2.2">HTTP/2 Specification</a>.
        ///
        /// According to the <a href="http://docs.oracle.com/javase/8/docs/technotes/guides/security/StandardNames.html">
        /// JSSE documentation</a> "the names mentioned in the TLS RFCs prefixed with TLS_ are functionally equivalent
        /// to the JSSE cipher suites prefixed with SSL_".
        /// Both variants are used to support JVMs supporting the one or the other.
        /// </summary>
        public static readonly IReadOnlyList<string> Ciphers;

        static Http2SecurityUtil()
        {
            Ciphers = new List<string>
            {
                /* openssl = ECDHE-ECDSA-AES256-GCM-SHA384 */
                "TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384",
                /* openssl = ECDHE-RSA-AES256-GCM-SHA384 */
                "TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384",
                /* openssl = ECDHE-ECDSA-CHACHA20-POLY1305 */
                "TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256",
                /* openssl = ECDHE-RSA-CHACHA20-POLY1305 */
                "TLS_ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256",
                /* openssl = ECDHE-ECDSA-AES128-GCM-SHA256 */
                "TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256",

                /* REQUIRED BY HTTP/2 SPEC */
                /* openssl = ECDHE-RSA-AES128-GCM-SHA256 */
                "TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256"
                /* REQUIRED BY HTTP/2 SPEC */
            };

        }
    }
}
