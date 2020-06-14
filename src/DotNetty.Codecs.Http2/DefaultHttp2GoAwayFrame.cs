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
        private readonly Http2Error _errorCode;
        private readonly int _lastStreamId;
        private int _extraStreamIds;

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
            _errorCode = errorCode;
            _lastStreamId = lastStreamId;
        }

        public string Name => "GOAWAY";

        public Http2Error ErrorCode => _errorCode;

        public int ExtraStreamIds
        {
            get => _extraStreamIds;
            set
            {
                if ((uint)value > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_ExtraStreamIdsNonNegative(); }
                _extraStreamIds = value;
            }
        }

        public int LastStreamId => _lastStreamId;

        public override IByteBufferHolder Copy()
        {
            return new DefaultHttp2GoAwayFrame(_lastStreamId, _errorCode, Content.Copy());
        }

        public override IByteBufferHolder Replace(IByteBuffer content)
        {
            return new DefaultHttp2GoAwayFrame(_errorCode, content) { ExtraStreamIds = _extraStreamIds };
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) { return true; }

            return obj is DefaultHttp2GoAwayFrame goAwayFrame
                && _errorCode == goAwayFrame._errorCode
                && _extraStreamIds == goAwayFrame._extraStreamIds
                && base.Equals(obj);
        }

        public override int GetHashCode()
        {
            var errCode = (long)_errorCode;
            int hash = base.GetHashCode();
            hash = hash * 31 + (int)(errCode ^ (errCode.RightUShift(32)));
            hash = hash * 31 + _extraStreamIds;
            return hash;
        }

        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(errorCode=" + _errorCode + ", content=" + Content
                   + ", extraStreamIds=" + _extraStreamIds + ", lastStreamId=" + _lastStreamId + ')';
        }
    }
}
