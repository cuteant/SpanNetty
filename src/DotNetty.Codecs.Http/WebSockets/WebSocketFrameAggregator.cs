// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Transport.Channels;

    public class WebSocketFrameAggregator : MessageAggregator2<WebSocketFrame, WebSocketFrame, ContinuationWebSocketFrame, WebSocketFrame>
    {
        public WebSocketFrameAggregator(int maxContentLength)
            : base(maxContentLength)
        {
        }

        protected override bool IsStartMessage(WebSocketFrame msg)
        {
            switch (msg)
            {
                case TextWebSocketFrame _:
                case BinaryWebSocketFrame _:
                    return true;
                default:
                    return false;
            }
        }

        protected override bool IsContentMessage(WebSocketFrame msg) => msg is ContinuationWebSocketFrame;

        protected override bool IsLastContentMessage(ContinuationWebSocketFrame msg) => this.IsContentMessage(msg) && msg.IsFinalFragment;

        protected override bool IsAggregated(WebSocketFrame msg)
        {
            if (msg.IsFinalFragment)
            {
                return !this.IsContentMessage(msg);
            }

            return !this.IsStartMessage(msg) && !this.IsContentMessage(msg);
        }

        protected override bool IsContentLengthInvalid(WebSocketFrame start, int maxContentLength) => false;

        protected override object NewContinueResponse(WebSocketFrame start, int maxContentLength, IChannelPipeline pipeline) => null;

        protected override bool CloseAfterContinueResponse(object msg) => throw new NotSupportedException();

        protected override bool IgnoreContentAfterContinueResponse(object msg) => throw new NotSupportedException();

        protected override WebSocketFrame BeginAggregation(WebSocketFrame start, IByteBuffer content)
        {
            switch (start)
            {
                case TextWebSocketFrame _:
                    return new TextWebSocketFrame(true, start.Rsv, content);
                case BinaryWebSocketFrame _:
                    return new BinaryWebSocketFrame(true, start.Rsv, content);
                default:
                    // Should not reach here.
                    return ThrowHelper.ThrowException_UnkonwFrameType();
            }
        }
    }
}
