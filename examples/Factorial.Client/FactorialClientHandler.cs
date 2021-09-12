// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Factorial.Client
{
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using System;
    using System.Collections.Concurrent;
    using System.Numerics;
    using System.Threading.Tasks;

    public class FactorialClientHandler : SimpleChannelInboundHandler<BigInteger>
    {
        IChannelHandlerContext context;
        int _receivedMessages;
        int _next = 1;
        readonly BlockingCollection<BigInteger> _answer = new BlockingCollection<BigInteger>();

        public BigInteger GetFactorial() => _answer.Take();

        public override void ChannelActive(IChannelHandlerContext context)
        {
            this.context = context;
            SendNumbers();
        }

        protected override void ChannelRead0(IChannelHandlerContext context, BigInteger message)
        {
            _receivedMessages++;
            if (_receivedMessages == ClientSettings.Count)
            {
                context.CloseAsync().ContinueWith(t => _answer.Add(message));
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine($"{exception}");
            context.CloseAsync();
        }

        void SendNumbers()
        {
            // Do not send more than 4096 numbers.
            Task future = null;
            for (int i = 0; (i < 4096) && (_next <= ClientSettings.Count); i++)
            {
                future = context.WriteAsync(new BigInteger(_next));
                _next++;
            }

            if (_next <= ClientSettings.Count)
            {
                future.ContinueWith(task =>
                {
                    if (task.IsSuccess())
                    {
                        SendNumbers();
                    }
                    else
                    {
                        context.Channel.CloseAsync();
                    }
                });
            }

            context.Flush();
        }
    }
}