// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Factorial.Server
{
    using DotNetty.Transport.Channels;
    using System;
    using System.Numerics;

    public class FactorialServerHandler : SimpleChannelInboundHandler<BigInteger>
    {
        BigInteger _lastMultiplier = new BigInteger(1);
        BigInteger _factorial = new BigInteger(1);

        protected override void ChannelRead0(IChannelHandlerContext context, BigInteger msg)
        {
            _lastMultiplier = msg;
            _factorial *= msg;
            context.WriteAndFlushAsync(_factorial);
        }

        public override void ChannelInactive(IChannelHandlerContext context) => Console.WriteLine($"Factorial of {_lastMultiplier} is: {_factorial}");

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) => context.CloseAsync();
    }
}
