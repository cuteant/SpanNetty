// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Transport.Channels;

    public interface IHttp2StreamChannel : IChannel
    {
        /// <summary>
        /// Returns the <see cref="IHttp2FrameStream"/> that belongs to this channel.
        /// </summary>
        /// <returns></returns>
        IHttp2FrameStream Stream { get; }
    }
}
