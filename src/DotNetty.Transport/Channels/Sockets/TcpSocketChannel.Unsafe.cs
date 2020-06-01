// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;

    partial class TcpSocketChannel<TChannel>
    {
        public sealed class TcpSocketChannelUnsafe : SocketByteChannelUnsafe
        {
            public TcpSocketChannelUnsafe() //TcpSocketChannel channel)
                : base() //channel)
            {
            }

            // todo: review
            //protected Executor closeExecutor()
            //{
            //    if (javaChannel().isOpen() && config().getSoLinger() > 0)
            //    {
            //        return GlobalEventExecutor.INSTANCE;
            //    }
            //    return null;
            //}
        }
    }
}