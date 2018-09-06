// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Transport.Channels;

    public class WebSocketFrameAggregator : MessageAggregator2<WebSocketFrame, WebSocketFrame, ContinuationWebSocketFrame, WebSocketFrame>
    {
        public WebSocketFrameAggregator(int maxContentLength)
            : base(maxContentLength)
        {
        }

        public override bool TryAcceptInboundMessage(object msg, out WebSocketFrame message)
        {
            message = msg as WebSocketFrame;
            if (null == message) { return false; }

            switch (message.Opcode)
            {
                case Opcode.Text:
                case Opcode.Binary:
                    return !message.IsFinalFragment;
                case Opcode.Cont:
                    return true;
                default:
                    return false;
            }
            //return (this.IsContentMessage(message) || this.IsStartMessage(message))
            //    && !this.IsAggregated(message);
        }

        protected override bool IsStartMessage(WebSocketFrame msg)
        {
            switch (msg.Opcode)
            {
                case Opcode.Text:
                case Opcode.Binary:
                    return true;
                default:
                    return false;
            }
        }

        protected override bool IsContentMessage(WebSocketFrame msg) => msg.Opcode == Opcode.Cont;

        protected override bool IsLastContentMessage(ContinuationWebSocketFrame msg) => msg.Opcode == Opcode.Cont && msg.IsFinalFragment;

        protected override bool IsAggregated(WebSocketFrame msg)
        {
            switch (msg.Opcode)
            {
                case Opcode.Text:
                case Opcode.Binary:
                    return msg.IsFinalFragment;
                case Opcode.Cont:
                    return false;
                default:
                    return true;
            }
            //if (msg.IsFinalFragment) { return msg.Opcode != Opcode.Cont; }
            //return !this.IsStartMessage(msg) && msg.Opcode != Opcode.Cont;
        }

        protected override bool IsContentLengthInvalid(WebSocketFrame start, int maxContentLength) => false;

        protected override object NewContinueResponse(WebSocketFrame start, int maxContentLength, IChannelPipeline pipeline) => null;

        protected override bool CloseAfterContinueResponse(object msg) => throw new NotSupportedException();

        protected override bool IgnoreContentAfterContinueResponse(object msg) => throw new NotSupportedException();

        protected override WebSocketFrame BeginAggregation(WebSocketFrame start, IByteBuffer content)
        {
            switch (start.Opcode)
            {
                case Opcode.Text:
                    return new TextWebSocketFrame(true, start.Rsv, content);
                case Opcode.Binary:
                    return new BinaryWebSocketFrame(true, start.Rsv, content);
                default:
                    // Should not reach here.
                    return ThrowHelper.ThrowException_UnkonwFrameType();
            }
        }

        protected override void Decode(IChannelHandlerContext context, WebSocketFrame message, List<object> output)
        {
            switch (message.Opcode)
            {
                case Opcode.Text:
                case Opcode.Binary:
                    this.handlingOversizedMessage = false;
                    if (this.currentMessage != null)
                    {
                        this.currentMessage.Release();
                        this.currentMessage = default;

                        ThrowHelper.ThrowMessageAggregationException_StartMessage();
                    }

                    // A streamed message - initialize the cumulative buffer, and wait for incoming chunks.
                    CompositeByteBuffer content0 = context.Allocator.CompositeBuffer(this.maxCumulationBufferComponents);
                    AppendPartialContent(content0, message.Content);
                    this.currentMessage = this.BeginAggregation(message, content0);
                    break;

                case Opcode.Cont:
                    if (this.currentMessage == null)
                    {
                        // it is possible that a TooLongFrameException was already thrown but we can still discard data
                        // until the begging of the next request/response.
                        return;
                    }

                    // Merge the received chunk into the content of the current message.
                    var content = (CompositeByteBuffer)this.currentMessage.Content;

                    var contMsg = (ContinuationWebSocketFrame)message;

                    // Handle oversized message.
                    if (content.ReadableBytes > this.MaxContentLength - contMsg.Content.ReadableBytes)
                    {
                        this.InvokeHandleOversizedMessage(context, this.currentMessage);
                        return;
                    }

                    // Append the content of the chunk.
                    AppendPartialContent(content, contMsg.Content);

                    if (this.IsLastContentMessage(contMsg))
                    {
                        //this.FinishAggregation(this.currentMessage);

                        // All done
                        output.Add(this.currentMessage);
                        this.currentMessage = default;
                    }
                    break;

                default:
                    ThrowHelper.ThrowMessageAggregationException_UnknownAggregationState();
                    break;
            }
        }
    }
}
