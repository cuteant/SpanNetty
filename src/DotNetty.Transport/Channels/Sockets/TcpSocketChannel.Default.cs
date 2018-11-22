// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Net.Sockets;
    using DotNetty.Common.Concurrency;

    public sealed class TcpSocketChannel : TcpSocketChannel<TcpSocketChannel>
    {
        /// <summary>Create a new instance</summary>
        public TcpSocketChannel() : base() { }

        /// <summary>Create a new instance</summary>
        public TcpSocketChannel(AddressFamily addressFamily) : base(addressFamily) { }

        /// <summary>Create a new instance using the given <see cref="ISocketChannel" />.</summary>
        public TcpSocketChannel(Socket socket) : base(socket) { }

        /// <summary>Create a new instance</summary>
        /// <param name="parent">
        ///     the <see cref="IChannel" /> which created this instance or <c>null</c> if it was created by the
        ///     user
        /// </param>
        /// <param name="socket">the <see cref="ISocketChannel" /> which will be used</param>
        public TcpSocketChannel(IChannel parent, Socket socket) : base(parent, socket) { }

        internal TcpSocketChannel(IChannel parent, Socket socket, bool connected) : base(parent, socket, connected) { }
    }

    partial class TcpSocketChannel<TChannel> : AbstractSocketByteChannel<TChannel, TcpSocketChannel<TChannel>.TcpSocketChannelUnsafe>, ISocketChannel
        where TChannel : TcpSocketChannel<TChannel>
    {
        private static readonly Action<object, object> ShutdownOutputAction = OnShutdownOutput;

        private static void OnShutdownOutput(object channel, object promise)
        {
            ((TcpSocketChannel<TChannel>)channel).ShutdownOutput0((IPromise)promise);
        }
    }
}