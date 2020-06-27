// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Discard.Client
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using Examples.Common;

    /// <summary>
    /// Handles a client-side channel.
    /// </summary>
    public class DiscardClientHandler : SimpleChannelInboundHandler<object>
    {
        IChannelHandlerContext _ctx;
        byte[] _array;

        public override void ChannelActive(IChannelHandlerContext ctx)
        {
            _array = new byte[ClientSettings.Size];
            _ctx = ctx;

            // Send the initial messages.
            GenerateTraffic();
        }

        protected override void ChannelRead0(IChannelHandlerContext context, object message)
        {
            // Server is supposed to send nothing, but if it sends something, discard it.
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception e)
        {
            // Close the connection when an exception is raised.
            Console.WriteLine("{0}", e.ToString());
            _ctx.CloseAsync();
        }

        async void GenerateTraffic()
        {
            try
            {
                IByteBuffer buffer = Unpooled.WrappedBuffer(_array);
                // Flush the outbound buffer to the socket.
                // Once flushed, generate the same amount of traffic again.
                await _ctx.WriteAndFlushAsync(buffer);
                GenerateTraffic();
            }
            catch
            {
                await _ctx.CloseAsync();
            }
        }
    }
}