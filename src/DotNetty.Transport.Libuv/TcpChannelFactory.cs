using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using DotNetty.Transport.Libuv.Native;

namespace DotNetty.Transport.Libuv
{
    public sealed class TcpChannelFactory : ITcpChannelFactory
    {
        public IChannel CreateChannel<TServerChannel>(TServerChannel channel, Tcp tcp) where TServerChannel : IServerChannel
        {
            return new TcpChannel(channel, tcp);
        }
    }
}
