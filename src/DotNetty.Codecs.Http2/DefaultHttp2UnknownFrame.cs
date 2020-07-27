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
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    public sealed class DefaultHttp2UnknownFrame : DefaultByteBufferHolder, IHttp2UnknownFrame
    {
        private readonly Http2FrameTypes _frameType;
        private readonly Http2Flags _flags;
        private IHttp2FrameStream _stream;

        public DefaultHttp2UnknownFrame(Http2FrameTypes frameType, Http2Flags flags)
            : this(frameType, flags, Unpooled.Empty)
        {
        }

        public DefaultHttp2UnknownFrame(Http2FrameTypes frameType, Http2Flags flags, IByteBuffer data)
            : base(data)
        {
            _frameType = frameType;
            _flags = flags;
        }

        public Http2FrameTypes FrameType => _frameType;

        public Http2Flags Flags => _flags;

        public IHttp2FrameStream Stream { get => _stream; set => _stream = value; }

        public string Name => "UNKNOWN";

        public override IByteBufferHolder Copy()
        {
            return Replace(Content.Copy());
        }

        public override IByteBufferHolder Duplicate()
        {
            return Replace(Content.Duplicate());
        }

        public override IByteBufferHolder RetainedDuplicate()
        {
            return Replace(Content.RetainedDuplicate());
        }

        public override IByteBufferHolder Replace(IByteBuffer content)
        {
            return new DefaultHttp2UnknownFrame(_frameType, _flags, content) { Stream = _stream };
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) { return true; }

            var otherFrame = obj as DefaultHttp2UnknownFrame;
            if (otherFrame is null) { return false; }

            var thisStream = _stream;
            var otherStream = otherFrame.Stream;
            return (ReferenceEquals(thisStream, otherStream) || otherStream is object && otherStream.Equals(thisStream))
                   && _flags.Equals(otherFrame._flags)
                   && _frameType == otherFrame._frameType
                   && base.Equals(otherFrame);
        }

        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = hash * 31 + (byte)_frameType;
            hash = hash * 31 + _flags.GetHashCode();
            if (_stream is object)
            {
                hash = hash * 31 + _stream.GetHashCode();
            }

            return hash;
        }

        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(frameType=" + _frameType + ", stream=" + _stream +
                    ", flags=" + _flags + ", content=" + ContentToString() + ')';
        }
    }
}
