// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    public interface IHttp2StreamFrame : IHttp2Frame
    {
        /// <summary>
        /// Gets or sets the <see cref="IHttp2FrameStream"/> object for this frame.
        /// </summary>
        /// <returns><c>null</c> if the frame has yet to be associated with a stream.</returns>
        IHttp2FrameStream Stream { get; set; }
    }
}
