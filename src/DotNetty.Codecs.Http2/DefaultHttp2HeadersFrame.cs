// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Common.Utilities;

    /// <summary>
    /// The default <see cref="IHttp2HeadersFrame"/> implementation.
    /// </summary>
    public sealed class DefaultHttp2HeadersFrame : AbstractHttp2StreamFrame, IHttp2HeadersFrame
    {
        private readonly IHttp2Headers _headers;
        private readonly bool _endStream;
        private readonly int _padding;

        public DefaultHttp2HeadersFrame(IHttp2Headers headers)
            : this(headers, false)
        {
        }

        /// <summary>
        /// Equivalent to {@code new DefaultHttp2HeadersFrame(headers, endStream, 0)}.
        /// </summary>
        /// <param name="headers">the non-<c>null</c> headers to send</param>
        /// <param name="endStream"></param>
        public DefaultHttp2HeadersFrame(IHttp2Headers headers, bool endStream)
            : this(headers, endStream, 0)
        {
        }

        /// <summary>
        /// Construct a new headers message.
        /// </summary>
        /// <param name="headers">the non-<c>null</c> headers to send</param>
        /// <param name="endStream">whether these headers should terminate the stream</param>
        /// <param name="padding">additional bytes that should be added to obscure the true content size. Must be between 0 and
        /// 256 (inclusive).</param>
        public DefaultHttp2HeadersFrame(IHttp2Headers headers, bool endStream, int padding)
        {
            if (headers is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.headers); }

            _headers = headers;
            _endStream = endStream;
            Http2CodecUtil.VerifyPadding(padding);
            _padding = padding;
        }

        public override string Name => "HEADERS";

        public IHttp2Headers Headers => _headers;

        public int Padding => _padding;

        public bool IsEndStream => _endStream;

        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(stream=" + Stream + ", headers=" + _headers
                   + ", endStream=" + _endStream + ", padding=" + _padding + ')';
        }

        protected override bool Equals0(IHttp2StreamFrame other)
        {
            return other is DefaultHttp2HeadersFrame headersFrame
                && base.Equals0(other)
                && _headers.Equals(headersFrame._headers)
                && _endStream == headersFrame._endStream
                && _padding == headersFrame._padding;
        }

        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = hash * 31 + _headers.GetHashCode();
            hash = hash * 31 + (_endStream ? 0 : 1);
            hash = hash * 31 + _padding;
            return hash;
        }
    }
}
