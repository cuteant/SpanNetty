// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System.ComponentModel;

    public interface IHttp2FrameSizePolicy
    {
        /// <summary>
        /// Sets the maximum allowed frame size. Attempts to write frames longer than this maximum will fail.
        /// <para>This value is used to represent
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_FRAME_SIZE</a>. This method should
        /// only be called by Netty (not users) as a result of a receiving a <c>SETTINGS</c> frame.</para>
        /// </summary>
        /// <param name="max"></param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        void SetMaxFrameSize(int max);

        /// <summary>
        /// Gets the maximum allowed frame size.
        /// <para>This value is used to represent
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_FRAME_SIZE</a>. The initial value
        /// defined by the RFC is unlimited but enforcing a lower limit is generally permitted.
        /// <see cref="Http2CodecUtil.DefaultMaxFrameSize"/> can be used as a more conservative default.</para>
        /// </summary>
        int MaxFrameSize { get; }
    }
}
