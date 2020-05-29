// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Buffers;

    public class DefaultHttp2HeadersEncoder : IHttp2HeadersEncoder, IHttp2HeadersEncoderConfiguration
    {
        readonly HpackEncoder hpackEncoder;
        readonly ISensitivityDetector sensitivityDetector;
        readonly IByteBuffer tableSizeChangeOutput = Unpooled.Buffer();

        public DefaultHttp2HeadersEncoder()
            : this(NeverSensitiveDetector.Instance)
        {
        }

        public DefaultHttp2HeadersEncoder(ISensitivityDetector sensitivityDetector)
            : this(sensitivityDetector, new HpackEncoder())
        {
        }

        public DefaultHttp2HeadersEncoder(ISensitivityDetector sensitivityDetector, bool ignoreMaxHeaderListSize)
            : this(sensitivityDetector, new HpackEncoder(ignoreMaxHeaderListSize))
        {
        }

        public DefaultHttp2HeadersEncoder(ISensitivityDetector sensitivityDetector, bool ignoreMaxHeaderListSize, int dynamicTableArraySizeHint)
            : this(sensitivityDetector, ignoreMaxHeaderListSize, dynamicTableArraySizeHint, HpackEncoder.HuffCodeThreshold)
        {
        }

        public DefaultHttp2HeadersEncoder(ISensitivityDetector sensitivityDetector, bool ignoreMaxHeaderListSize,
                                          int dynamicTableArraySizeHint, int huffCodeThreshold)
            : this(sensitivityDetector, new HpackEncoder(ignoreMaxHeaderListSize, dynamicTableArraySizeHint, huffCodeThreshold))
        {
        }

        /// <summary>
        /// Exposed Used for testing only! Default values used in the initial settings frame are overridden intentionally
        /// for testing but violate the RFC if used outside the scope of testing.
        /// </summary>
        /// <param name="sensitivityDetector"></param>
        /// <param name="hpackEncoder"></param>
        internal DefaultHttp2HeadersEncoder(ISensitivityDetector sensitivityDetector, HpackEncoder hpackEncoder)
        {
            if (sensitivityDetector is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.sensitivityDetector); }
            if (hpackEncoder is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.hpackEncoder); }

            this.sensitivityDetector = sensitivityDetector;
            this.hpackEncoder = hpackEncoder;
        }

        public void EncodeHeaders(int streamId, IHttp2Headers headers, IByteBuffer buffer)
        {
            try
            {
                // If there was a change in the table size, serialize the output from the hpackEncoder
                // resulting from that change.
                if (this.tableSizeChangeOutput.IsReadable())
                {
                    buffer.WriteBytes(this.tableSizeChangeOutput);
                    this.tableSizeChangeOutput.Clear();
                }

                this.hpackEncoder.EncodeHeaders(streamId, buffer, headers, this.sensitivityDetector);
            }
            catch (Http2Exception)
            {
                throw;
            }
            catch (Exception t)
            {
                ThrowHelper.ThrowConnectionError_FailedEncodingHeadersBlock(t);
            }
        }

        public void SetMaxHeaderTableSize(long max)
        {
            this.hpackEncoder.SetMaxHeaderTableSize(this.tableSizeChangeOutput, max);
        }

        public long MaxHeaderTableSize => this.hpackEncoder.GetMaxHeaderTableSize();

        public void SetMaxHeaderListSize(long max)
        {
            this.hpackEncoder.SetMaxHeaderListSize(max);
        }

        public long MaxHeaderListSize => this.hpackEncoder.GetMaxHeaderListSize();

        public IHttp2HeadersEncoderConfiguration Configuration => this;
    }
}