// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Echo.Server
{
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using System;
    using System.Text;

    /// <summary>
    /// Handler implementation for the echo server.
    /// </summary>
    public class EchoServerHandler : ChannelHandlerAdapter
    {
        public override bool IsSharable => true;

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            if (message is IByteBuffer buffer)
            {
                Console.WriteLine($"Received from client: {buffer.ToString(Encoding.UTF8)}");
            }
            context.WriteAsync(message);
        }

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine($"{exception}");
            context.CloseAsync();
        }
    }
}
