// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System.Net.Sockets;

    partial class TcpServerSocketChannel<TServerChannel, TChannelFactory>
    {
        sealed class TcpServerSocketChannelConfig : DefaultServerSocketChannelConfig
        {
            public TcpServerSocketChannelConfig(TServerChannel channel, Socket javaSocket)
                : base(channel, javaSocket)
            {
            }

            protected override void AutoReadCleared() => ((TServerChannel)Channel).ReadPending = false;
        }
    }
}