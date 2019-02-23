// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    partial class WebSocketClientProtocolHandshakeHandler : ChannelHandlerAdapter
    {
        readonly WebSocketClientHandshaker handshaker;

        internal WebSocketClientProtocolHandshakeHandler(WebSocketClientHandshaker handshaker)
        {
            this.handshaker = handshaker;
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            base.ChannelActive(context);
#if NET40
            Action<Task> fireUserEventTriggeredAction = (Task t) =>
            {
                if (t.IsSuccess())
                {
                    context.FireUserEventTriggered(WebSocketClientProtocolHandler.ClientHandshakeStateEvent.HandshakeIssued);
                }
                else
                {
                    context.FireExceptionCaught(t.Exception);
                }
            };
            this.handshaker.HandshakeAsync(context.Channel)
                .ContinueWith(fireUserEventTriggeredAction, TaskContinuationOptions.ExecuteSynchronously);
#else
            this.handshaker.HandshakeAsync(context.Channel)
                .ContinueWith(FireUserEventTriggeredAction, context, TaskContinuationOptions.ExecuteSynchronously);
#endif
        }

        public override void ChannelRead(IChannelHandlerContext ctx, object msg)
        {
            var response = msg as IFullHttpResponse;
            if (null == response)
            {
                ctx.FireChannelRead(msg);
                return;
            }

            try
            {
                if (!this.handshaker.IsHandshakeComplete)
                {
                    this.handshaker.FinishHandshake(ctx.Channel, response);
                    ctx.FireUserEventTriggered(WebSocketClientProtocolHandler.ClientHandshakeStateEvent.HandshakeComplete);
                    ctx.Pipeline.Remove(this);
                    return;
                }

                ThrowHelper.ThrowInvalidOperationException_WebSocketClientHandshaker();
            }
            finally
            {
                response.Release();
            }
        }
    }
}
