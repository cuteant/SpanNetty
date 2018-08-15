// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System.Net.Sockets;

    public sealed class SocketDatagramChannel : SocketDatagramChannel<SocketDatagramChannel>
    {
        public SocketDatagramChannel() : base() { }

        public SocketDatagramChannel(AddressFamily addressFamily) : base(addressFamily) { }

        public SocketDatagramChannel(Socket socket) : base(socket) { }
    }

    partial class SocketDatagramChannel<TChannel> : AbstractSocketMessageChannel<TChannel, SocketDatagramChannel<TChannel>.DatagramChannelUnsafe>, IDatagramChannel
        where TChannel : SocketDatagramChannel<TChannel>
    {
        public SocketDatagramChannel()
#if NET40
            : this(new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
#else
            // .Net4.5+，默认为AddressFamily.InterNetworkV6，并设置 DualMode 为 true，双线绑定
            : this(new Socket(SocketType.Dgram, ProtocolType.Udp))
#endif
        {
        }
    }
}