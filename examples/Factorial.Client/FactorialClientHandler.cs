// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Factorial.Client
{
    using System;
    using System.Collections.Concurrent;
    using System.Numerics;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class FactorialClientHandler : SimpleChannelInboundHandler<BigInteger>
    {
        IChannelHandlerContext _ctx;
        int _receivedMessages;
        int _next = 1;
        readonly BlockingCollection<BigInteger> _answer = new BlockingCollection<BigInteger>();

        public BigInteger GetFactorial() => _answer.Take();

        public override void ChannelActive(IChannelHandlerContext ctx)
        {
            _ctx = ctx;
            SendNumbers();
        }

        protected override void ChannelRead0(IChannelHandlerContext ctx, BigInteger msg)
        {
            _receivedMessages++;
            if (_receivedMessages == ClientSettings.Count)
            {
                ctx.CloseAsync().ContinueWith(t => _answer.Add(msg));
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            Console.WriteLine("{0}", cause.ToString());
            ctx.CloseAsync();
        }

        void SendNumbers()
        {
            // Do not send more than 4096 numbers.
            Task future = null;
            for (int i = 0; (i < 4096) && (_next <= ClientSettings.Count); i++)
            {
                future = _ctx.WriteAsync(new BigInteger(_next));
                _next++;
            }
            if (_next <= ClientSettings.Count)
            {
                future.ContinueWith(t =>
                {
                    if (t.IsSuccess())
                    {
                        SendNumbers();
                    }
                    else
                    {
                        _ctx.Channel.CloseAsync();
                    }
                });
            }
            _ctx.Flush();
        }
    }
}