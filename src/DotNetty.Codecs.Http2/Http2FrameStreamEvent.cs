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
    public sealed class Http2FrameStreamEvent
    {
        public enum EventType
        {
            State,
            Writability
        }

        public readonly IHttp2FrameStream Stream;
        public readonly EventType Type;

        private Http2FrameStreamEvent(IHttp2FrameStream stream, EventType type)
        {
            Stream = stream;
            Type = type;
        }

        public static Http2FrameStreamEvent StateChanged(IHttp2FrameStream stream)
        {
            return new Http2FrameStreamEvent(stream, EventType.State);
        }

        public static Http2FrameStreamEvent WritabilityChanged(IHttp2FrameStream stream)
        {
            return new Http2FrameStreamEvent(stream, EventType.Writability);
        }
    }
}
