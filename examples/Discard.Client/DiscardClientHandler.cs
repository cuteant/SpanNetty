// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Discard.Client
{
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using Examples.Common;
    using System;

    /// <summary>
    /// Handles a client-side channel.
    /// </summary>
    public class DiscardClientHandler : SimpleChannelInboundHandler<object>
    {
        IChannelHandlerContext _context;
        byte[] _array;

        public override void ChannelActive(IChannelHandlerContext context)
        {
            _array = new byte[ClientSettings.Size];
            _context = context;

            // Send the initial messages.
            GenerateTraffic();
        }

        protected override void ChannelRead0(IChannelHandlerContext context, object message)
        {
            // Server is supposed to send nothing, but if it sends something, discard it.
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            // Close the connection when an exception is raised.
            Console.WriteLine($"{exception}");
            _context.CloseAsync();
        }

        async void GenerateTraffic()
        {
            try
            {
                IByteBuffer buffer = Unpooled.WrappedBuffer(_array);
                // Flush the outbound buffer to the socket.
                // Once flushed, generate the same amount of traffic again.
                await _context.WriteAndFlushAsync(buffer);
                GenerateTraffic();
            }
            catch
            {
                await _context.CloseAsync();
            }
        }
    }
}
