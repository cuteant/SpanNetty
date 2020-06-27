// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Factorial.Server
{
    using System;
    using System.Numerics;
    using DotNetty.Transport.Channels;

    public class FactorialServerHandler : SimpleChannelInboundHandler<BigInteger>
    {
        BigInteger _lastMultiplier = new BigInteger(1);
        BigInteger _factorial = new BigInteger(1);

        protected override void ChannelRead0(IChannelHandlerContext ctx, BigInteger msg)
        {
            _lastMultiplier = msg;
            _factorial *= msg;
            ctx.WriteAndFlushAsync(_factorial);
        }

        public override void ChannelInactive(IChannelHandlerContext ctx) => Console.WriteLine("Factorial of {0} is: {1}", _lastMultiplier, _factorial);

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception e) => ctx.CloseAsync();
    }
}