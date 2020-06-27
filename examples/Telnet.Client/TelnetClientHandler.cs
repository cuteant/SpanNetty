// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Telnet.Client
{
    using System;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Handles a client-side channel.
    /// </summary>
    public class TelnetClientHandler : SimpleChannelInboundHandler<string>
    {
        public override bool IsSharable => true;

        protected override void ChannelRead0(IChannelHandlerContext contex, string msg)
        {
            Console.WriteLine(msg);
        }

        public override void ExceptionCaught(IChannelHandlerContext contex, Exception e)
        {
            Console.WriteLine(DateTime.Now.Millisecond);
            Console.WriteLine("{0}", e.StackTrace);
            contex.CloseAsync();
        }
    }
}