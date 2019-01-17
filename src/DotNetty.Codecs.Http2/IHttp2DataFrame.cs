// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;

    /// <summary>
    /// HTTP/2 DATA frame.
    /// </summary>
    public interface IHttp2DataFrame : IHttp2StreamFrame, IByteBufferHolder
    {
        /// <summary>
        /// Frame padding to use. Will be non-negative and less than 256.
        /// </summary>
        /// <returns></returns>
        int Padding { get; }

        /// <summary>
        /// Returns the number of bytes that are flow-controlled initially, so even if the <see cref="IByteBufferHolder.Content"/> is consumed
        /// this will not change.
        /// </summary>
        /// <returns></returns>
        int InitialFlowControlledBytes { get; }

        /// <summary>
        /// Returns <c>true</c> if the END_STREAM flag ist set.
        /// </summary>
        /// <returns></returns>
        bool IsEndStream { get; }
    }
}
