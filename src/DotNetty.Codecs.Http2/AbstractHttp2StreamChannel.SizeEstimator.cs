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
    using DotNetty.Transport.Channels;

    partial class AbstractHttp2StreamChannel
    {
        /// <summary>
        /// Number of bytes to consider non-payload messages. 9 is arbitrary, but also the minimum size of an HTTP/2 frame.
        /// Primarily is non-zero.
        /// </summary>
        private static readonly int MinHttp2FrameSize = 9;

        /// <summary>
        /// Returns the flow-control size for DATA frames, and <see cref="MinHttp2FrameSize"/> for all other frames.
        /// </summary>
        private sealed class FlowControlledFrameSizeEstimator : IMessageSizeEstimator
        {
            public static readonly IMessageSizeEstimator Instance = new FlowControlledFrameSizeEstimator();

            private FlowControlledFrameSizeEstimator() { }

            public IMessageSizeEstimatorHandle NewHandle() => FlowControlledFrameSizeEstimatorHandle.Instance;
        }
        private sealed class FlowControlledFrameSizeEstimatorHandle : IMessageSizeEstimatorHandle
        {
            public static readonly IMessageSizeEstimatorHandle Instance = new FlowControlledFrameSizeEstimatorHandle();

            private FlowControlledFrameSizeEstimatorHandle() { }

            public int Size(object msg)
            {
                return msg is IHttp2DataFrame frame
                    // Guard against overflow.
                    ? (int)Math.Min(int.MaxValue, frame.InitialFlowControlledBytes + (long)MinHttp2FrameSize)
                    : MinHttp2FrameSize;
            }
        }
    }
}
