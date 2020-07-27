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
    using DotNetty.Common.Utilities;

    /// <summary>
    /// The default <see cref="IHttp2ResetFrame"/> implementation.
    /// </summary>
    public sealed class DefaultHttp2ResetFrame : AbstractHttp2StreamFrame, IHttp2ResetFrame
    {
        private readonly Http2Error _errorCode;

        /// <summary>
        /// Construct a reset message.
        /// </summary>
        /// <param name="error">the reason for reset</param>
        public DefaultHttp2ResetFrame(Http2Error error)
        {
            _errorCode = error;
        }

        public override string Name => "RST_STREAM";

        public Http2Error ErrorCode => _errorCode;

        protected override bool Equals0(IHttp2StreamFrame other)
        {
            return other is DefaultHttp2ResetFrame resetFrame
                && base.Equals0(other)
                && _errorCode == resetFrame._errorCode;
        }

        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            var errCode = (long)_errorCode;
            hash = hash * 31 + (int)(errCode ^ (errCode.RightUShift(32)));
            return hash;
        }

        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(stream=" + Stream + ", errorCode=" + _errorCode + ')';
        }
    }
}
