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
    using System;

    public abstract class AbstractHttp2StreamFrame : IHttp2StreamFrame, IEquatable<IHttp2StreamFrame>
    {
        private IHttp2FrameStream _stream;

        public abstract string Name { get; }

        public IHttp2FrameStream Stream
        {
            get => _stream;
            set => _stream = value;
        }

        public sealed override bool Equals(object obj)
        {
            return obj is IHttp2StreamFrame streamFrame && Equals(streamFrame);
        }

        public bool Equals(IHttp2StreamFrame other)
        {
            if (ReferenceEquals(this, other)) { return true; }

            if (other is null) { return false; }

            return Equals0(other);
        }

        protected virtual bool Equals0(IHttp2StreamFrame other)
        {
            var thisStream = _stream;
            var otherStream = other.Stream;
            return ReferenceEquals(thisStream, otherStream) || (thisStream is object && thisStream.Equals(otherStream));
        }

        public override int GetHashCode()
        {
            var thisStream = _stream;
            if (thisStream is null) { return base.GetHashCode(); }

            return thisStream.GetHashCode();
        }
    }
}
