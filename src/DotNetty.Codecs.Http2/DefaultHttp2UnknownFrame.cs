// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    public sealed class DefaultHttp2UnknownFrame : DefaultByteBufferHolder, IHttp2UnknownFrame
    {
        private readonly Http2FrameTypes frameType;
        private readonly Http2Flags flags;
        private IHttp2FrameStream stream;

        public DefaultHttp2UnknownFrame(Http2FrameTypes frameType, Http2Flags flags)
            : this(frameType, flags, Unpooled.Empty)
        {
        }

        public DefaultHttp2UnknownFrame(Http2FrameTypes frameType, Http2Flags flags, IByteBuffer data)
            : base(data)
        {
            this.frameType = frameType;
            this.flags = flags;
        }

        public Http2FrameTypes FrameType => this.frameType;

        public Http2Flags Flags => this.flags;

        public IHttp2FrameStream Stream { get => this.stream; set => this.stream = value; }

        public string Name => "UNKNOWN";

        public override IByteBufferHolder Copy()
        {
            return this.Replace(this.Content.Copy());
        }

        public override IByteBufferHolder Duplicate()
        {
            return this.Replace(this.Content.Duplicate());
        }

        public override IByteBufferHolder RetainedDuplicate()
        {
            return this.Replace(this.Content.RetainedDuplicate());
        }

        public override IByteBufferHolder Replace(IByteBuffer content)
        {
            return new DefaultHttp2UnknownFrame(this.frameType, this.flags, content) { Stream = this.stream };
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) { return true; }

            var unknownFrame = obj as DefaultHttp2UnknownFrame;
            if (null == unknownFrame) { return false; }

            var thisStream = this.stream;
            var otherStream = unknownFrame.Stream;

            return base.Equals(obj)
                && this.flags.Equals(unknownFrame.flags)
                && this.frameType == unknownFrame.frameType
                && (ReferenceEquals(thisStream, otherStream) || (thisStream != null && thisStream.Equals(otherStream)));
        }

        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = hash * 31 + (byte)this.frameType;
            hash = hash * 31 + this.flags.GetHashCode();
            if (this.stream != null)
            {
                hash = hash * 31 + this.stream.GetHashCode();
            }

            return hash;
        }

        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(frameType=" + this.frameType + ", stream=" + this.stream +
                    ", flags=" + this.flags + ", content=" + this.ContentToString() + ')';
        }
    }
}
