// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Buffers;

    public class DefaultHttp2HeadersDecoder : IHttp2HeadersDecoder, IHttp2HeadersDecoderConfiguration
    {
        private const float HeadersCountWeightNew = 1 / 5f;
        private const float HeadersCountWeightHistorical = 1 - HeadersCountWeightNew;

        private readonly HpackDecoder hpackDecoder;
        private readonly bool validateHeaders;
        private long maxHeaderListSizeGoAway;

        /// <summary>
        /// Used to calculate an exponential moving average of header sizes to get an estimate of how large the data
        /// structure for storing headers should be.
        /// </summary>
        float headerArraySizeAccumulator = 8;

        public DefaultHttp2HeadersDecoder()
            : this(true)
        {
        }

        public DefaultHttp2HeadersDecoder(bool validateHeaders)
            : this(validateHeaders, Http2CodecUtil.DefaultHeaderListSize)
        {
        }

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="validateHeaders"><c>true</c> to validate headers are valid according to the RFC.</param>
        /// <param name="maxHeaderListSize">This is the only setting that can be configured before notifying the peer.
        /// This is because <a href="https://tools.ietf.org/html/rfc7540#section-6.5.1">SETTINGS_MAX_HEADER_LIST_SIZE</a>
        /// allows a lower than advertised limit from being enforced, and the default limit is unlimited
        /// (which is dangerous).</param>
        public DefaultHttp2HeadersDecoder(bool validateHeaders, long maxHeaderListSize)
            : this(validateHeaders, maxHeaderListSize, Http2CodecUtil.DefaultInitialHuffmanDecodeCapacity)
        {
        }

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="validateHeaders"><c>true</c> to validate headers are valid according to the RFC.</param>
        /// <param name="maxHeaderListSize">This is the only setting that can be configured before notifying the peer.
        /// This is because <a href="https://tools.ietf.org/html/rfc7540#section-6.5.1">SETTINGS_MAX_HEADER_LIST_SIZE</a>
        /// allows a lower than advertised limit from being enforced, and the default limit is unlimited
        /// (which is dangerous).</param>
        /// <param name="initialHuffmanDecodeCapacity">Size of an intermediate buffer used during huffman decode.</param>
        public DefaultHttp2HeadersDecoder(
            bool validateHeaders, long maxHeaderListSize, int initialHuffmanDecodeCapacity)
            : this(validateHeaders, new HpackDecoder(maxHeaderListSize, initialHuffmanDecodeCapacity))
        {
        }

        /// <summary>
        /// Exposed Used for testing only! Default values used in the initial settings frame are overridden intentionally
        /// for testing but violate the RFC if used outside the scope of testing.
        /// </summary>
        /// <param name="validateHeaders"></param>
        /// <param name="hpackDecoder"></param>
        internal DefaultHttp2HeadersDecoder(bool validateHeaders, HpackDecoder hpackDecoder)
        {
            if (null == hpackDecoder) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.hpackDecoder); }

            this.hpackDecoder = hpackDecoder;
            this.validateHeaders = validateHeaders;
            this.maxHeaderListSizeGoAway = Http2CodecUtil.CalculateMaxHeaderListSizeGoAway(hpackDecoder.GetMaxHeaderListSize());
        }


        public void SetMaxHeaderTableSize(long max)
        {
            this.hpackDecoder.SetMaxHeaderTableSize(max);
        }



        public long MaxHeaderTableSize => this.hpackDecoder.GetMaxHeaderTableSize();


        public void SetMaxHeaderListSize(long max, long goAwayMax)
        {
            if (goAwayMax < max || goAwayMax < 0)
            {
                ThrowHelper.ThrowConnectionError_HeaderListSizeGoAwayNonNegative(goAwayMax, max);
            }
            this.hpackDecoder.SetMaxHeaderListSize(max);
            this.maxHeaderListSizeGoAway = goAwayMax;
        }


        public long MaxHeaderListSize => this.hpackDecoder.GetMaxHeaderListSize();


        public long MaxHeaderListSizeGoAway => this.maxHeaderListSizeGoAway;


        public IHttp2HeadersDecoderConfiguration Configuration => this;


        public IHttp2Headers DecodeHeaders(int streamId, IByteBuffer headerBlock)
        {
            try
            {
                IHttp2Headers headers = NewHeaders();
                this.hpackDecoder.Decode(streamId, headerBlock, headers, this.validateHeaders);
                headerArraySizeAccumulator = HeadersCountWeightNew * headers.Size +
                    HeadersCountWeightHistorical * headerArraySizeAccumulator;
                return headers;
            }
            catch (Http2Exception)
            {
                throw;
            }
            catch (Exception e)
            {
                // Default handler for any other types of errors that may have occurred. For example,
                // the Header builder throws IllegalArgumentException if the key or value was invalid
                // for any reason (e.g. the key was an invalid pseudo-header).
                return ThrowHelper.ThrowConnectionError_FailedEncodingHe1adersBlock(e);
            }
        }

        /// <summary>
        /// A weighted moving average estimating how many headers are expected during the decode process.
        /// </summary>
        /// <returns>an estimate of how many headers are expected during the decode process.</returns>
        protected int NumberOfHeadersGuess()
        {
            return (int)headerArraySizeAccumulator;
        }

        /// <summary>
        /// Determines if the headers should be validated as a result of the decode operation.
        /// </summary>
        /// <returns><c>true</c> if the headers should be validated as a result of the decode operation.</returns>
        protected bool ValidateHeaders()
        {
            return this.validateHeaders;
        }

        /// <summary>
        /// Create a new <see cref="IHttp2Headers"/> object which will store the results of the decode operation.
        /// </summary>
        /// <returns>a new <see cref="IHttp2Headers"/> object which will store the results of the decode operation.</returns>
        protected IHttp2Headers NewHeaders()
        {
            return new DefaultHttp2Headers(this.validateHeaders, (int)headerArraySizeAccumulator);
        }
    }
}
