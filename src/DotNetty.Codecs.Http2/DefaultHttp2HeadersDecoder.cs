﻿/*
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
    using System;
    using DotNetty.Buffers;

    public class DefaultHttp2HeadersDecoder : IHttp2HeadersDecoder, IHttp2HeadersDecoderConfiguration
    {
        private const float HeadersCountWeightNew = 1 / 5f;
        private const float HeadersCountWeightHistorical = 1 - HeadersCountWeightNew;

        private readonly HpackDecoder _hpackDecoder;
        private readonly bool _validateHeaders;
        private long _maxHeaderListSizeGoAway;

        /// <summary>
        /// Used to calculate an exponential moving average of header sizes to get an estimate of how large the data
        /// structure for storing headers should be.
        /// </summary>
        private float _headerArraySizeAccumulator = 8;

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
            : this(validateHeaders, maxHeaderListSize, -1)
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
        /// <param name="initialHuffmanDecodeCapacity">Does nothing, do not use.</param>
        public DefaultHttp2HeadersDecoder(
            bool validateHeaders, long maxHeaderListSize, int initialHuffmanDecodeCapacity)
            : this(validateHeaders, new HpackDecoder(maxHeaderListSize))
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
            if (hpackDecoder is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.hpackDecoder); }

            _hpackDecoder = hpackDecoder;
            _validateHeaders = validateHeaders;
            _maxHeaderListSizeGoAway = Http2CodecUtil.CalculateMaxHeaderListSizeGoAway(hpackDecoder.GetMaxHeaderListSize());
        }


        public void SetMaxHeaderTableSize(long max)
        {
            _hpackDecoder.SetMaxHeaderTableSize(max);
        }



        public long MaxHeaderTableSize => _hpackDecoder.GetMaxHeaderTableSize();


        public void SetMaxHeaderListSize(long max, long goAwayMax)
        {
            if (goAwayMax < max || goAwayMax < 0)
            {
                ThrowHelper.ThrowConnectionError_HeaderListSizeGoAwayNonNegative(goAwayMax, max);
            }
            _hpackDecoder.SetMaxHeaderListSize(max);
            _maxHeaderListSizeGoAway = goAwayMax;
        }


        public long MaxHeaderListSize => _hpackDecoder.GetMaxHeaderListSize();


        public long MaxHeaderListSizeGoAway => _maxHeaderListSizeGoAway;


        public IHttp2HeadersDecoderConfiguration Configuration => this;


        public IHttp2Headers DecodeHeaders(int streamId, IByteBuffer headerBlock)
        {
            try
            {
                IHttp2Headers headers = NewHeaders();
                _hpackDecoder.Decode(streamId, headerBlock, headers, _validateHeaders);
                _headerArraySizeAccumulator = HeadersCountWeightNew * headers.Size +
                    HeadersCountWeightHistorical * _headerArraySizeAccumulator;
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
                return ThrowHelper.FromConnectionError_FailedEncodingHe1adersBlock(e);
            }
        }

        /// <summary>
        /// A weighted moving average estimating how many headers are expected during the decode process.
        /// </summary>
        /// <returns>an estimate of how many headers are expected during the decode process.</returns>
        protected int NumberOfHeadersGuess()
        {
            return (int)_headerArraySizeAccumulator;
        }

        /// <summary>
        /// Determines if the headers should be validated as a result of the decode operation.
        /// </summary>
        /// <returns><c>true</c> if the headers should be validated as a result of the decode operation.</returns>
        protected bool ValidateHeaders()
        {
            return _validateHeaders;
        }

        /// <summary>
        /// Create a new <see cref="IHttp2Headers"/> object which will store the results of the decode operation.
        /// </summary>
        /// <returns>a new <see cref="IHttp2Headers"/> object which will store the results of the decode operation.</returns>
        protected IHttp2Headers NewHeaders()
        {
            return new DefaultHttp2Headers(_validateHeaders, (int)_headerArraySizeAccumulator);
        }
    }
}
