// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Telnet.Client
{
    using DotNetty.Transport.Channels;
    using System;

    /// <summary>
    /// Handles a client-side channel.
    /// </summary>
    public class TelnetClientHandler : SimpleChannelInboundHandler<string>
    {
        public override bool IsSharable => true;

        protected override void ChannelRead0(IChannelHandlerContext context, string message)
        {
            Console.WriteLine(message);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine(DateTime.Now.Millisecond);
            Console.WriteLine($"{exception}");
            context.CloseAsync();
        }
    }
}