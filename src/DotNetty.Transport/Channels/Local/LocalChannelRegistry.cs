#if !NET40
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Local
{
    using System.Collections.Concurrent;
    using System.Net;

    public static class LocalChannelRegistry
    {
        static readonly ConcurrentDictionary<LocalAddress, IChannel> BoundChannels = new ConcurrentDictionary<LocalAddress, IChannel>();

        internal static LocalAddress Register(IChannel channel, LocalAddress oldLocalAddress, EndPoint localAddress) 
        {
            if (oldLocalAddress != null) 
            {
                ThrowHelper.ThrowChannelException_AlreadyBound();
            }

            var addr = localAddress as LocalAddress;
            if (null == addr) 
            {
                ThrowHelper.ThrowChannelException_UnsupportedAddrType(localAddress);
            }

            if (LocalAddress.Any.Equals(addr)) 
            {
                addr = new LocalAddress(channel);
            }

            var result = BoundChannels.GetOrAdd(addr, channel);
            if (!ReferenceEquals(result, channel))
            {
                ThrowHelper.ThrowChannelException_AddrAlreadyInUseBy(result);
            }
            
            return addr;
        }

        internal static IChannel Get(EndPoint localAddress) 
            => localAddress is LocalAddress key && BoundChannels.TryGetValue(key, out var ch) ? ch : null;

        internal static void Unregister(LocalAddress localAddress) 
            => BoundChannels.TryRemove(localAddress, out var _);
    }
}
#endif
