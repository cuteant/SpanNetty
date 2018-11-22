// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;

    /// <summary>
    /// Provides empty implementations of all <see cref="IHttp2ConnectionListener"/> methods.
    /// </summary>
    public class Http2ConnectionAdapter : IHttp2ConnectionListener
    {
        public virtual void OnGoAwayReceived(int lastStreamId, Http2Error errorCode, IByteBuffer debugData)
        {
        }

        public virtual void OnGoAwaySent(int lastStreamId, Http2Error errorCode, IByteBuffer debugData)
        {
        }

        public virtual void OnStreamActive(IHttp2Stream stream)
        {
        }

        public virtual void OnStreamAdded(IHttp2Stream stream)
        {
        }

        public virtual void OnStreamClosed(IHttp2Stream stream)
        {
        }

        public virtual void OnStreamHalfClosed(IHttp2Stream stream)
        {
        }

        public virtual void OnStreamRemoved(IHttp2Stream stream)
        {
        }
    }
}
