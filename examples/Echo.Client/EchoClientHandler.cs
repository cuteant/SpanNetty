// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Echo.Client
{
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using Examples.Common;
    using System;
    using System.Text;

    /// <summary>
    /// Handler implementation for the echo client.  It initiates the ping-pong
    /// traffic between the echo client and server by sending the first message to
    /// the server.
    /// </summary>
    public class EchoClientHandler : ChannelHandlerAdapter
    {
        readonly IByteBuffer _initialMessage;

        public EchoClientHandler()
        {
            _initialMessage = Unpooled.Buffer(ClientSettings.Size);
            byte[] messageBytes = Encoding.UTF8.GetBytes("Hello world");
            _initialMessage.WriteBytes(messageBytes);
        }

        public override void ChannelActive(IChannelHandlerContext context) => context.WriteAndFlushAsync(_initialMessage);

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            if (message is IByteBuffer byteBuffer)
            {
                Console.WriteLine($"Received from server: {byteBuffer.ToString(Encoding.UTF8)}");
            }
            context.WriteAsync(message);
        }

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            // Close the connection when an exception is raised.
            Console.WriteLine($"{exception}");
            context.CloseAsync();
        }
    }
}
