// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;

    public abstract class AbstractHttp2StreamFrame : IHttp2StreamFrame, IEquatable<IHttp2StreamFrame>
    {
        IHttp2FrameStream stream;

        public abstract string Name { get; }

        public IHttp2FrameStream Stream
        {
            get => this.stream;
            set => this.stream = value;
        }

        public sealed override bool Equals(object obj)
        {
            return obj is IHttp2StreamFrame streamFrame && this.Equals(streamFrame);
        }

        public bool Equals(IHttp2StreamFrame other)
        {
            if (ReferenceEquals(this, other)) { return true; }

            if (null == other) { return false; }

            return Equals0(other);
        }

        protected virtual bool Equals0(IHttp2StreamFrame other)
        {
            var thisStream = this.stream;
            var otherStream = other.Stream;
            return ReferenceEquals(thisStream, otherStream) || (thisStream is object && thisStream.Equals(otherStream));
        }

        public override int GetHashCode()
        {
            var thisStream = this.stream;
            if (null == thisStream) { return base.GetHashCode(); }

            return thisStream.GetHashCode();
        }
    }
}
