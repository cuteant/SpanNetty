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

using System;
using System.Net;
using System.Text;

namespace DotNetty.Handlers.Proxy
{
    /// <summary>
    /// Creates a new event that indicates a successful connection attempt to the destination address.
    /// </summary>
    public sealed class ProxyConnectionEvent
    {
        private string _strVal;

        public ProxyConnectionEvent(string protocol, string authScheme, EndPoint proxyAddress,
            EndPoint destinationAddress)
        {
            Protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
            AuthScheme = authScheme ?? throw new ArgumentNullException(nameof(authScheme));
            ProxyAddress = proxyAddress ?? throw new ArgumentNullException(nameof(proxyAddress));
            DestinationAddress = destinationAddress ?? throw new ArgumentNullException(nameof(destinationAddress));
        }

        /// <summary>
        ///Returns the name of the proxy protocol in use.
        /// </summary>
        public string Protocol { get; }

        /// <summary>
        /// Returns the name of the authentication scheme in use.
        /// </summary>
        public string AuthScheme { get; }

        /// <summary>
        ///  Returns the address of the proxy server.
        /// </summary>
        public EndPoint ProxyAddress { get; }

        /// <summary>
        ///  Returns the address of the destination.
        /// </summary>
        public EndPoint DestinationAddress { get; }

        public override string ToString()
        {
            if (_strVal != null) return _strVal;

            var buf = new StringBuilder(128)
                .Append(typeof(ProxyConnectionEvent).Name)
                .Append('(')
                .Append(Protocol)
                .Append(", ")
                .Append(AuthScheme)
                .Append(", ")
                .Append(ProxyAddress)
                .Append(" => ")
                .Append(DestinationAddress)
                .Append(')');

            return _strVal = buf.ToString();
        }
    }
}