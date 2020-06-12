// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Transport.Channels;

    public class Http2StreamChannelId : IChannelId, IEquatable<Http2StreamChannelId>, IEquatable<IChannelId>
    {
        private readonly int _id;
        private readonly IChannelId _parentId;

        public Http2StreamChannelId(IChannelId parentId, int id)
        {
            _parentId = parentId;
            _id = id;
        }

        public string AsLongText()
        {
            return _parentId.AsLongText() + '/' + _id;
        }

        public string AsShortText()
        {
            return _parentId.AsShortText() + '/' + _id;
        }

        public int CompareTo(IChannelId other)
        {
            if (other is Http2StreamChannelId otherId)
            {
                int res = _parentId.CompareTo(otherId._parentId);
                if (0u >= (uint)res)
                {
                    return _id - otherId._id;
                }
                else
                {
                    return res;
                }
            }
            return _parentId.CompareTo(other);
        }

        public override bool Equals(object obj)
        {
            return obj is Http2StreamChannelId streamChannelId && Equals(streamChannelId);
        }

        public bool Equals(Http2StreamChannelId other)
        {
            if (ReferenceEquals(this, other)) { return true; }
            if (other is null) { return false; }
            return _id == other._id && _parentId.Equals(other._parentId);
        }

        bool IEquatable<IChannelId>.Equals(IChannelId other)
        {
            if (ReferenceEquals(this, other)) { return true; }
            return other is Http2StreamChannelId streamChannelId && Equals(streamChannelId);
        }

        public override int GetHashCode()
        {
            return _id * 31 + _parentId.GetHashCode();
        }
    }
}
