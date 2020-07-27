/*
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

    public class DefaultHttp2HeadersEncoder : IHttp2HeadersEncoder, IHttp2HeadersEncoderConfiguration
    {
        private readonly HpackEncoder _hpackEncoder;
        private readonly ISensitivityDetector _sensitivityDetector;
        private readonly IByteBuffer _tableSizeChangeOutput = Unpooled.Buffer();

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

            _sensitivityDetector = sensitivityDetector;
            _hpackEncoder = hpackEncoder;
        }

        public void EncodeHeaders(int streamId, IHttp2Headers headers, IByteBuffer buffer)
        {
            try
            {
                // If there was a change in the table size, serialize the output from the hpackEncoder
                // resulting from that change.
                if (_tableSizeChangeOutput.IsReadable())
                {
                    _ = buffer.WriteBytes(_tableSizeChangeOutput);
                    _ = _tableSizeChangeOutput.Clear();
                }

                _hpackEncoder.EncodeHeaders(streamId, buffer, headers, _sensitivityDetector);
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
            _hpackEncoder.SetMaxHeaderTableSize(_tableSizeChangeOutput, max);
        }

        public long MaxHeaderTableSize => _hpackEncoder.GetMaxHeaderTableSize();

        public void SetMaxHeaderListSize(long max)
        {
            _hpackEncoder.SetMaxHeaderListSize(max);
        }

        public long MaxHeaderListSize => _hpackEncoder.GetMaxHeaderListSize();

        public IHttp2HeadersEncoderConfiguration Configuration => this;
    }
}