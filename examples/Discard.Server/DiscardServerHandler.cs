// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Discard.Server
{
    using System;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Handles a server-side channel.
    /// </summary>
    public class DiscardServerHandler : SimpleChannelInboundHandler<object>
    {
        protected override void ChannelRead0(IChannelHandlerContext context, object message)
        {
            // discard
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception e)
        {
            // Close the connection when an exception is raised.
            Console.WriteLine("{0}", e.ToString());
            ctx.CloseAsync();
        }
    }
}