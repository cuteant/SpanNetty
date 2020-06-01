// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System.Net.Sockets;

    public sealed class TcpServerSocketChannel : TcpServerSocketChannel<TcpServerSocketChannel, TcpSocketChannelFactory>
    {
        public TcpServerSocketChannel() : base() { }

        /// <summary>Create a new instance</summary>
        public TcpServerSocketChannel(AddressFamily addressFamily) : base(addressFamily) { }

        /// <summary>Create a new instance using the given <see cref="Socket"/>.</summary>
        public TcpServerSocketChannel(Socket socket) : base(socket) { }
    }

    partial class TcpServerSocketChannel<TServerChannel, TChannelFactory> : AbstractSocketChannel<TServerChannel, TcpServerSocketChannel<TServerChannel, TChannelFactory>.TcpServerSocketChannelUnsafe>, IServerSocketChannel
        where TServerChannel : TcpServerSocketChannel<TServerChannel, TChannelFactory>
        where TChannelFactory : ITcpSocketChannelFactory, new()
    {
        private readonly TChannelFactory _channelFactory;

        /// <summary>
        ///     Create a new instance using the given <see cref="Socket"/>.
        /// </summary>
        public TcpServerSocketChannel(Socket socket)
            : base(null, socket)
        {
            this._config = new TcpServerSocketChannelConfig((TServerChannel)this, socket);
            _channelFactory = new TChannelFactory();
        }
    }
}