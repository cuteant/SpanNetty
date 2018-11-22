// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// The default <see cref="IHttp2DataFrame"/> implementation.
    /// </summary>
    public sealed class DefaultHttp2DataFrame : AbstractHttp2StreamFrame, IHttp2DataFrame
    {
        private readonly IByteBuffer content;
        private readonly bool endStream;
        private readonly int padding;
        private readonly int initialFlowControlledBytes;

        /// <summary>
        /// Equivalent to <see cref="DefaultHttp2DataFrame(IByteBuffer, bool)"/>.
        /// </summary>
        /// <param name="content">non-<c>null</c> payload</param>
        public DefaultHttp2DataFrame(IByteBuffer content)
            : this(content, false)
        {
        }

        /// <summary>
        /// Equivalent to {@code new DefaultHttp2DataFrame(Unpooled.EMPTY_BUFFER, endStream)}.
        /// </summary>
        /// <param name="endStream">whether this data should terminate the stream</param>
        public DefaultHttp2DataFrame(bool endStream)
            : this(Unpooled.Empty, endStream)
        {
        }

        /// <summary>
        /// Equivalent to {@code new DefaultHttp2DataFrame(content, endStream, 0)}.
        /// </summary>
        /// <param name="content">non-<c>null</c> payload</param>
        /// <param name="endStream">whether this data should terminate the stream</param>
        public DefaultHttp2DataFrame(IByteBuffer content, bool endStream)
            : this(content, endStream, 0)
        {
        }

        /// <summary>
        /// Construct a new data message.
        /// </summary>
        /// <param name="content">non-<c>null</c> payload</param>
        /// <param name="endStream">whether this data should terminate the stream</param>
        /// <param name="padding">additional bytes that should be added to obscure the true content size. Must be between 0 and
        /// 256 (inclusive).</param>
        public DefaultHttp2DataFrame(IByteBuffer content, bool endStream, int padding)
        {
            if (null == content) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.content); }

            this.content = content;
            this.endStream = endStream;
            Http2CodecUtil.VerifyPadding(padding);
            this.padding = padding;
            if (Content.ReadableBytes + (long)padding > int.MaxValue)
            {
                ThrowHelper.ThrowArgumentException_InvalidContendAndPadding();
            }
            initialFlowControlledBytes = content.ReadableBytes + padding;
        }

        public override string Name => "DATA";

        public bool IsEndStream => this.endStream;

        public int Padding => this.padding;

        public IByteBuffer Content
        {
            get
            {
                var refCnt = this.content.ReferenceCount;
                if (refCnt <= 0)
                {
                    ThrowHelper.ThrowIllegalReferenceCountException(refCnt);
                }
                return this.content;
            }
        }

        public int InitialFlowControlledBytes => this.initialFlowControlledBytes;

        public IByteBufferHolder Copy()
        {
            return this.Replace(this.Content.Copy());
        }

        public IByteBufferHolder Duplicate()
        {
            return this.Replace(this.Content.Duplicate());
        }

        public IByteBufferHolder RetainedDuplicate()
        {
            return this.Replace(this.Content.RetainedDuplicate());
        }

        public IByteBufferHolder Replace(IByteBuffer content)
        {
            return new DefaultHttp2DataFrame(content, this.endStream, this.padding);
        }

        public int ReferenceCount => this.content.ReferenceCount;

        public bool Release() => this.content.Release();

        public bool Release(int decrement) => this.content.Release(decrement);

        public IReferenceCounted Retain()
        {
            this.content.Retain();
            return this;
        }

        public IReferenceCounted Retain(int increment)
        {
            this.content.Retain(increment);
            return this;
        }

        public IReferenceCounted Touch()
        {
            this.content.Touch();
            return this;
        }

        public IReferenceCounted Touch(object hint)
        {
            this.content.Touch(hint);
            return this;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(stream=" + this.Stream + ", content=" + this.content
                   + ", endStream=" + this.endStream + ", padding=" + this.padding + ')';
        }

        protected override bool Equals0(IHttp2StreamFrame other)
        {
            return other is DefaultHttp2DataFrame otherFrame
                && base.Equals0(other)
                && this.content.Equals(otherFrame.Content)
                && this.endStream == otherFrame.endStream
                && this.padding == otherFrame.padding;
        }

        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = hash * 31 + this.content.GetHashCode();
            hash = hash * 31 + (this.endStream ? 0 : 1);
            hash = hash * 31 + this.padding;
            return hash;
        }
    }
}
