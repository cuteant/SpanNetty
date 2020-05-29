// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Common.Utilities;

    /// <summary>
    /// The default <see cref="IHttp2PingFrame"/> implementation.
    /// </summary>
    public class DefaultHttp2PingFrame : IHttp2PingFrame
    {
        private readonly long _content;
        private readonly bool _ack;

        public DefaultHttp2PingFrame(long content)
            : this(content, false)
        {
        }

        public DefaultHttp2PingFrame(long content, bool ack)
        {
            _content = content;
            _ack = ack;
        }

        public virtual string Name => "PING";

        public bool Ack => _ack;

        public long Content => _content;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) { return true; }

            return obj is DefaultHttp2PingFrame pingFrame
                && _ack == pingFrame._ack
                && _content == pingFrame._content;
        }

        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = hash * 31 + (_ack ? 1 : 0);
            return hash;
        }

        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(content=" + _content + ", ack=" + _ack + ')';
        }
    }
}
