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
        private readonly IByteBuffer _content;
        private readonly bool _endStream;
        private readonly int _padding;
        private readonly int _initialFlowControlledBytes;

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
            if (content is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.content); }

            _content = content;
            _endStream = endStream;
            Http2CodecUtil.VerifyPadding(padding);
            _padding = padding;
            if (Content.ReadableBytes + (long)padding > int.MaxValue)
            {
                ThrowHelper.ThrowArgumentException_InvalidContendAndPadding();
            }
            _initialFlowControlledBytes = content.ReadableBytes + padding;
        }

        public override string Name => "DATA";

        public bool IsEndStream => _endStream;

        public int Padding => _padding;

        public IByteBuffer Content
        {
            get
            {
                var refCnt = _content.ReferenceCount;
                if (refCnt <= 0)
                {
                    ThrowHelper.ThrowIllegalReferenceCountException(refCnt);
                }
                return _content;
            }
        }

        public int InitialFlowControlledBytes => _initialFlowControlledBytes;

        public IByteBufferHolder Copy()
        {
            return Replace(Content.Copy());
        }

        public IByteBufferHolder Duplicate()
        {
            return Replace(Content.Duplicate());
        }

        public IByteBufferHolder RetainedDuplicate()
        {
            return Replace(Content.RetainedDuplicate());
        }

        public IByteBufferHolder Replace(IByteBuffer content)
        {
            return new DefaultHttp2DataFrame(content, _endStream, _padding);
        }

        public int ReferenceCount => _content.ReferenceCount;

        public bool Release() => _content.Release();

        public bool Release(int decrement) => _content.Release(decrement);

        public IReferenceCounted Retain()
        {
            _ = _content.Retain();
            return this;
        }

        public IReferenceCounted Retain(int increment)
        {
            _ = _content.Retain(increment);
            return this;
        }

        public IReferenceCounted Touch()
        {
            _ = _content.Touch();
            return this;
        }

        public IReferenceCounted Touch(object hint)
        {
            _ = _content.Touch(hint);
            return this;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(stream=" + Stream + ", content=" + _content
                   + ", endStream=" + _endStream + ", padding=" + _padding + ')';
        }

        protected override bool Equals0(IHttp2StreamFrame other)
        {
            return other is DefaultHttp2DataFrame otherFrame
                && base.Equals0(other)
                && _content.Equals(otherFrame.Content)
                && _endStream == otherFrame._endStream
                && _padding == otherFrame._padding;
        }

        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = hash * 31 + _content.GetHashCode();
            hash = hash * 31 + (_endStream ? 0 : 1);
            hash = hash * 31 + _padding;
            return hash;
        }
    }
}
