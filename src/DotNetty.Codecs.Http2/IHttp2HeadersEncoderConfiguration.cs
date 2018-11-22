// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System.ComponentModel;

    /// <summary>
    /// Configuration related elements for the <see cref="IHttp2HeadersEncoder"/> interface
    /// </summary>
    public interface IHttp2HeadersEncoderConfiguration
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
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_HEADER_TABLE_SIZE</a>.
        /// The initial value returned by this method must be <see cref="Http2CodecUtil.DefaultHeaderTableSize"/>.
        /// </summary>
        long MaxHeaderTableSize { get; }

        /// <summary>
        /// Represents the value for
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_HEADER_LIST_SIZE</a>.
        /// <para>This method should only be called by Netty (not users) as a result of a receiving a <c>SETTINGS</c> frame.</para>
        /// </summary>
        /// <param name="max"></param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        void SetMaxHeaderListSize(long max);

        /// <summary>
        /// Represents the value for
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_HEADER_LIST_SIZE</a>.
        /// </summary>
        long MaxHeaderListSize { get; }
    }
}
