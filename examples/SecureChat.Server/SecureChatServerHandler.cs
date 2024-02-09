// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SecureChat.Server
{
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Groups;
    using System;
    using System.Net;

    public class SecureChatServerHandler : SimpleChannelInboundHandler<string>
    {
        static volatile IChannelGroup group;
        static object syncObject = new object();

        public override void ChannelActive(IChannelHandlerContext contex)
        {
            IChannelGroup g = group;
            if (g == null)
            {
                lock (syncObject)
                {
                    if (group == null)
                    {
                        g = group = new DefaultChannelGroup(contex.Executor);
                    }
                }
            }

            var hostname = Dns.GetHostName();
            contex.WriteAndFlushAsync($"Welcome to {hostname} secure chat server!\n");
            g.Add(contex.Channel);
        }

        class EveryOneBut : IChannelMatcher
        {
            readonly IChannelId id;

            public EveryOneBut(IChannelId id)
            {
                this.id = id;
            }

            public bool Matches(IChannel channel) => channel.Id != this.id;
        }

        protected override void ChannelRead0(IChannelHandlerContext context, string msg)
        {
            //send message to all but this one
            string broadcast = $"[{context.Channel.RemoteAddress}] {msg}\n";
            string response = $"[you] {msg}\n";
            group.WriteAndFlushAsync(broadcast, new EveryOneBut(context.Channel.Id));
            context.WriteAndFlushAsync(response);

            if (string.Equals("bye", msg, StringComparison.OrdinalIgnoreCase))
            {
                context.CloseAsync();
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine($"{exception}");
            context.CloseAsync();
        }

        public override bool IsSharable => true;
    }
}
