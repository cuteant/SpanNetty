// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// HTTP/2 PING Frame.
    /// </summary>
    public interface IHttp2PingFrame : IHttp2Frame
    {
        /// <summary>
        /// When <c>true</c>, indicates that this ping is a ping response.
        /// </summary>
        /// <returns></returns>
        bool Ack { get; }

        /// <summary>
        /// Returns the eight byte opaque data.
        /// </summary>
        /// <returns></returns>
        long Content { get; }
    }
}
