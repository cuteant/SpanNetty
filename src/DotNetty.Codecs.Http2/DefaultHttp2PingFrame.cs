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
        private readonly long content;
        private readonly bool ack;

        public DefaultHttp2PingFrame(long content)
            : this(content, false)
        {
        }

        /// <summary>
        /// A user cannot send a ping ack, as this is done automatically when a ping is received.
        /// </summary>
        /// <param name="content"></param>
        /// <param name="ack"></param>
        public DefaultHttp2PingFrame(long content, bool ack)
        {
            this.content = content;
            this.ack = ack;
        }

        public virtual string Name => "PING";

        public bool Ack => this.ack;

        public long Content => this.content;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) { return true; }

            return obj is DefaultHttp2PingFrame pingFrame
                && this.ack == pingFrame.ack
                && this.content == pingFrame.content;
        }

        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = hash * 31 + (this.ack ? 1 : 0);
            return hash;
        }

        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(content=" + this.content + ", ack=" + this.ack + ')';
        }
    }
}
