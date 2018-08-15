// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using DotNetty.Transport.Channels;
using DotNetty.Transport.Libuv.Native;

namespace DotNetty.Transport.Libuv
{
    public sealed class TcpServerChannel : TcpServerChannel<TcpServerChannel, TcpChannelFactory>
    {
        public TcpServerChannel() : base() { }
    }

    public partial class TcpServerChannel<TServerChannel, TChannelFactory> : NativeChannel<TServerChannel, TcpServerChannel<TServerChannel, TChannelFactory>.TcpServerChannelUnsafe>, IServerChannel
        where TServerChannel : TcpServerChannel<TServerChannel, TChannelFactory>
        where TChannelFactory : ITcpChannelFactory, new()
    {
        private readonly TChannelFactory _channelFactory;

        public TcpServerChannel() : base(null)
        {
            this.config = new TcpServerChannelConfig(this);
            _channelFactory = new TChannelFactory();
        }
    }
}
