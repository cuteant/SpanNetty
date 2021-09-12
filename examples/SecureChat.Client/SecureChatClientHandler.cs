// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SecureChat.Client
{
    using DotNetty.Transport.Channels;
    using System;

    public class SecureChatClientHandler : SimpleChannelInboundHandler<string>
    {
        protected override void ChannelRead0(IChannelHandlerContext context, string message) => Console.WriteLine(message);

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine(DateTime.Now.Millisecond);
            Console.WriteLine($"{exception}");
            context.CloseAsync();
        }
    }
}
