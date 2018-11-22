// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System.ComponentModel;

    /// <summary>
    /// Configuration related elements for the <see cref="IHttp2HeadersDecoder"/> interface
    /// </summary>
    public interface IHttp2HeadersDecoderConfiguration
    {
        /// <summary>
        /// Represents the value for
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_HEADER_TABLE_SIZE</a>.
        /// <para>This method should only be called by Netty (not users) as a result of a receiving a <c>SETTINGS</c> frame.</para>
        /// </summary>
        /// <param name="max"></param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        void SetMaxHeaderTableSize(long max);

        /// <summary>
        /// Represents the value for
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_HEADER_TABLE_SIZE</a>. The initial value
        /// returned by this method must be <see cref="Http2CodecUtil.DefaultHeaderTableSize"/>.
        /// </summary>
        long MaxHeaderTableSize { get; }

        /// <summary>
        /// Configure the maximum allowed size in bytes of each set of headers.
        /// <para>This method should only be called by Netty (not users) as a result of a receiving a <c>SETTINGS</c> frame.</para>
        /// </summary>
        /// <param name="max"><a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_HEADER_LIST_SIZE</a>.
        /// If this limit is exceeded the implementation should attempt to keep the HPACK header tables up to date
        /// by processing data from the peer, but a <c>RST_STREAM</c> frame will be sent for the offending stream.</param>
        /// <param name="goAwayMax">Must be <paramref name="goAwayMax"/> >= <paramref name="max"/>. A <c>GO_AWAY</c> frame will be generated if this limit is exceeded
        /// for any particular stream.</param>
        /// <exception cref="Http2Exception">if limits exceed the RFC's boundaries or <paramref name="max"/> > <paramref name="goAwayMax"/>.</exception>
        [EditorBrowsable(EditorBrowsableState.Never)]
        void SetMaxHeaderListSize(long max, long goAwayMax);

        /// <summary>
        /// Represents the value for
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_HEADER_LIST_SIZE</a>.
        /// </summary>
        long MaxHeaderListSize { get; }

        /// <summary>
        /// Represents the upper bound in bytes for a set of headers before a <c>GO_AWAY</c> should be sent.
        /// This will be
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_HEADER_LIST_SIZE</a>.
        /// </summary>
        long MaxHeaderListSizeGoAway { get; }
    }
}
