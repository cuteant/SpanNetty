// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;

    public interface IHttp2GoAwayFrame : IHttp2Frame, IByteBufferHolder
    {
        /// <summary>
        /// The reason for beginning closure of the connection. Represented as an HTTP/2 error code.
        /// </summary>
        /// <returns></returns>
        Http2Error ErrorCode { get; }

        /// <summary>
        /// Gets or sets the number of IDs to reserve for the receiver to use while GOAWAY is in transit. This allows
        /// for new streams currently en route to still be created, up to a point, which allows for very
        /// graceful shutdown of both sides.
        /// </summary>
        /// <returns></returns>
        int ExtraStreamIds { get; set; }

        /// <summary>
        /// Gets the last stream identifier if set, or <c>-1</c> else.
        /// </summary>
        /// <returns></returns>
        int LastStreamId { get; }
    }
}
