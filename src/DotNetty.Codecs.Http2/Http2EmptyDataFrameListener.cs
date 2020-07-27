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
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Enforce a limit on the maximum number of consecutive empty DATA frames (without end_of_stream flag) that are allowed
    /// before the connection will be closed.
    /// </summary>
    sealed class Http2EmptyDataFrameListener : Http2FrameListenerDecorator
    {
        private readonly int _maxConsecutiveEmptyFrames;

        private bool _violationDetected;
        private int _emptyDataFrames;

        public Http2EmptyDataFrameListener(IHttp2FrameListener listener, int maxConsecutiveEmptyFrames)
            : base(listener)
        {
            if ((uint)(maxConsecutiveEmptyFrames - 1) > SharedConstants.TooBigOrNegative)
            {
                ThrowHelper.ThrowArgumentException_Positive(maxConsecutiveEmptyFrames, ExceptionArgument.maxConsecutiveEmptyFrames);
            }
            _maxConsecutiveEmptyFrames = maxConsecutiveEmptyFrames;
        }

        public override int OnDataRead(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream)
        {
            if (endOfStream || data.IsReadable())
            {
                _emptyDataFrames = 0;
            }
            else if (_emptyDataFrames++ == _maxConsecutiveEmptyFrames && !_violationDetected)
            {
                _violationDetected = true;
                ThrowHelper.ThrowStreamError_Maximum_number_of_empty_data_frames_without_end_of_stream_flag_received(_maxConsecutiveEmptyFrames);
            }

            return base.OnDataRead(ctx, streamId, data, padding, endOfStream);
        }

        public override void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream)
        {
            _emptyDataFrames = 0;
            base.OnHeadersRead(ctx, streamId, headers, padding, endOfStream);
        }

        public override void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream)
        {
            _emptyDataFrames = 0;
            base.OnHeadersRead(ctx, streamId, headers, streamDependency, weight, exclusive, padding, endOfStream);
        }
    }
}