// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// The default <see cref="IHttp2GoAwayFrame"/> implementation.
    /// </summary>
    public sealed class DefaultHttp2GoAwayFrame : DefaultByteBufferHolder, IHttp2GoAwayFrame
    {
        private readonly Http2Error errorCode;
        private readonly int lastStreamId;
        private int extraStreamIds;

        /// <summary>
        /// Equivalent to {@code new DefaultHttp2GoAwayFrame(error.code())}.
        /// </summary>
        /// <param name="error">non-<c>null</c> reason for the go away</param>
        public DefaultHttp2GoAwayFrame(Http2Error error)
            : this(error, Unpooled.Empty)
        {
        }

        /// <summary>
        /// Construct a new GOAWAY message.
        /// </summary>
        /// <param name="error">non-<c>null</c> reason for the go away</param>
        /// <param name="content">non-<c>null</c> debug data</param>
        public DefaultHttp2GoAwayFrame(Http2Error error, IByteBuffer content)
            : this(-1, error, content)
        {
        }

        /// <summary>
        /// Construct a new GOAWAY message.
        /// <para>This constructor is for internal use only. A user should not have to specify a specific last stream identifier,
        /// but use <see cref="ExtraStreamIds"/> instead.</para>
        /// </summary>
        /// <param name="lastStreamId"></param>
        /// <param name="errorCode"></param>
        /// <param name="content"></param>
        internal DefaultHttp2GoAwayFrame(int lastStreamId, Http2Error errorCode, IByteBuffer content)
            : base(content)
        {
            this.errorCode = errorCode;
            this.lastStreamId = lastStreamId;
        }

        public string Name => "GOAWAY";

        public Http2Error ErrorCode => this.errorCode;

        public int ExtraStreamIds
        {
            get => this.extraStreamIds;
            set
            {
                if (value < 0) { ThrowHelper.ThrowArgumentException_ExtraStreamIdsNonNegative(); }
                this.extraStreamIds = value;
            }
        }

        public int LastStreamId => this.lastStreamId;

        public override IByteBufferHolder Copy()
        {
            return new DefaultHttp2GoAwayFrame(lastStreamId, errorCode, this.Content.Copy());
        }

        public override IByteBufferHolder Replace(IByteBuffer content)
        {
            return new DefaultHttp2GoAwayFrame(errorCode, content) { ExtraStreamIds = extraStreamIds };
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) { return true; }

            return obj is DefaultHttp2GoAwayFrame goAwayFrame
                && this.errorCode == goAwayFrame.errorCode
                && this.extraStreamIds == goAwayFrame.extraStreamIds
                && base.Equals(obj);
        }

        public override int GetHashCode()
        {
            var errCode = (long)this.errorCode;
            int hash = base.GetHashCode();
            hash = hash * 31 + (int)(errCode ^ (errCode.RightUShift(32)));
            hash = hash * 31 + extraStreamIds;
            return hash;
        }

        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(errorCode=" + this.errorCode + ", content=" + this.Content
                   + ", extraStreamIds=" + this.extraStreamIds + ", lastStreamId=" + this.lastStreamId + ')';
        }
    }
}
