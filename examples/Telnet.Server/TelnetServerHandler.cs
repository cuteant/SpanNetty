// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Telnet.Server
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;

    public class TelnetServerHandler : SimpleChannelInboundHandler<string>
    {
        public override void ChannelActive(IChannelHandlerContext context)
        {
            context.WriteAsync($"Welcome to {Dns.GetHostName()} !\r\n");
            context.WriteAndFlushAsync($"It is {DateTime.Now} now !\r\n");
        }

        protected override void ChannelRead0(IChannelHandlerContext context, string message)
        {
            // Generate and write a response.
            string response;
            bool close = false;

            if (string.IsNullOrEmpty(message))
            {
                response = "Please type something.\r\n";
            }
            else if (string.Equals("bye", message, StringComparison.OrdinalIgnoreCase))
            {
                response = "Have a good day!\r\n";
                close = true;
            }
            else
            {
                response = $"Did you say '{message}'?\r\n";
            }

            Task wait_close = context.WriteAndFlushAsync(response);
            if (close)
            {
                Task.WaitAll(wait_close);
                context.CloseAsync();
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            context.Flush();
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine($"{exception}");
            context.CloseAsync();
        }

        public override bool IsSharable => true;
    }
}