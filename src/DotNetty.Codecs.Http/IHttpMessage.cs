// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    /// <summary>
    /// An interface that defines an HTTP message, providing common properties for
    /// <see cref="IHttpRequest"/> and <see cref="IHttpResponse"/>.
    /// </summary>
    public interface IHttpMessage : IHttpObject
    {
        /// <summary>
        /// Returns the protocol version of this <see cref="IHttpMessage"/>
        /// </summary>
        HttpVersion ProtocolVersion { get; }

        /// <summary>
        /// Set the protocol version of this <see cref="IHttpMessage"/>
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        IHttpMessage SetProtocolVersion(HttpVersion version);

        /// <summary>
        /// Returns the headers of this message.
        /// </summary>
        HttpHeaders Headers { get; }
    }
}
