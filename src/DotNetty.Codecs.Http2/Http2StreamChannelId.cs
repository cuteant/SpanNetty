// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Transport.Channels;

    public class Http2StreamChannelId : IChannelId, IEquatable<Http2StreamChannelId>, IEquatable<IChannelId>
    {
        private readonly int id;
        private readonly IChannelId parentId;

        public Http2StreamChannelId(IChannelId parentId, int id)
        {
            this.parentId = parentId;
            this.id = id;
        }

        public string AsLongText()
        {
            return parentId.AsLongText() + '/' + id;
        }

        public string AsShortText()
        {
            return parentId.AsShortText() + '/' + id;
        }

        public int CompareTo(IChannelId other)
        {
            if (other is Http2StreamChannelId otherId)
            {
                int res = parentId.CompareTo(otherId.parentId);
                if (res == 0)
                {
                    return id - otherId.id;
                }
                else
                {
                    return res;
                }
            }
            return parentId.CompareTo(other);
        }

        public override bool Equals(object obj)
        {
            return obj is Http2StreamChannelId streamChannelId && this.Equals(streamChannelId);
        }

        public bool Equals(Http2StreamChannelId other)
        {
            if (ReferenceEquals(this, other)) { return true; }
            if (null == other) { return false; }
            return id == other.id && parentId.Equals(other.parentId);
        }

        bool IEquatable<IChannelId>.Equals(IChannelId other)
        {
            if (ReferenceEquals(this, other)) { return true; }
            return other is Http2StreamChannelId streamChannelId && this.Equals(streamChannelId);
        }

        public override int GetHashCode()
        {
            return this.id * 31 + parentId.GetHashCode();
        }
    }
}
