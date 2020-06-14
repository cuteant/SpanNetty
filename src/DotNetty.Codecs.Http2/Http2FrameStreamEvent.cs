// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
