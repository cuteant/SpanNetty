// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Discard.Server
{
    using DotNetty.Transport.Channels;
    using System;

    /// <summary>
    /// Handles a server-side channel.
    /// </summary>
    public class DiscardServerHandler : SimpleChannelInboundHandler<object>
    {
        protected override void ChannelRead0(IChannelHandlerContext context, object message)
        {
            // discard
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            // Close the connection when an exception is raised.
            Console.WriteLine($"{exception}");
            context.CloseAsync();
        }
    }
}
