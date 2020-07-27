/*
 * Copyright 2012 The Netty Project
 *
 * The Netty Project licenses this file to you under the Apache License,
 * version 2.0 (the "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at:
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

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
