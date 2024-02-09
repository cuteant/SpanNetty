// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace QuoteOfTheMoment.Client
{
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using System;
    using System.Text;

    public class QuoteOfTheMomentClientHandler : SimpleChannelInboundHandler<DatagramPacket>
    {
        protected override void ChannelRead0(IChannelHandlerContext context, DatagramPacket packet)
        {
            Console.WriteLine($"Client Received => {packet}");

            if (!packet.Content.IsReadable())
            {
                return;
            }

            string message = packet.Content.ToString(Encoding.UTF8);
            if (!message.StartsWith("QOTM: "))
            {
                return;
            }

            Console.WriteLine($"Quote of the Moment: {message.Substring(6)}");
            context.CloseAsync();
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine($"{exception}");
            context.CloseAsync();
        }
    }
}
