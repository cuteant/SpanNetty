// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    partial class SocketDatagramChannel<TChannel>
    {
        public sealed class DatagramChannelUnsafe : SocketMessageUnsafe
        {
            public DatagramChannelUnsafe() //SocketDatagramChannel channel)
                : base() //channel)
            {
            }

            protected override bool CanWrite => _channel.Open && _channel.Registered;
        }
    }
}