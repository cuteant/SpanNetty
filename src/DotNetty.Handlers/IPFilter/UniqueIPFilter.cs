// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace DotNetty.Handlers.IPFilter
{
    using System;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// This class allows one to ensure that at all times for every IP address there is at most one
    /// <see cref="IChannel"/>  connected to the server.
    /// </summary>
    public class UniqueIPFilter : AbstractRemoteAddressFilter<IPEndPoint>
    {
        const byte Filler = 0;
        //using dictionary as set. value always equals Filler.
        readonly ConcurrentDictionary<IPAddress, byte> connected = new ConcurrentDictionary<IPAddress, byte>();

        protected override bool Accept(IChannelHandlerContext ctx, IPEndPoint remoteAddress)
        {
            IPAddress remoteIp = remoteAddress.Address;
            if (!this.connected.TryAdd(remoteIp, Filler))
            {
                return false;
            }
            else
            {
                ctx.Channel.CloseCompletion.ContinueWith(s_removeIpAddrAfterCloseAction, Tuple.Create(this.connected, remoteIp), TaskContinuationOptions.ExecuteSynchronously);
            }
            return true;
        }

        static readonly Action<Task, object> s_removeIpAddrAfterCloseAction = RemoveIpAddrAfterCloseAction;
        static void RemoveIpAddrAfterCloseAction(Task t, object s)
        {
            var wrapped = (Tuple<ConcurrentDictionary<IPAddress, byte>, IPAddress>)s;
            wrapped.Item1.TryRemove(wrapped.Item2, out _);
        }

        public override bool IsSharable => true;
    }
}