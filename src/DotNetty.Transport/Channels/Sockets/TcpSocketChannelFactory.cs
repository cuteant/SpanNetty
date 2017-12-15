using System.Net.Sockets;

namespace DotNetty.Transport.Channels.Sockets
{
    public sealed class TcpSocketChannelFactory : ITcpSocketChannelFactory
    {
        public ISocketChannel CreateChannel<TServerChannel>(TServerChannel channel, Socket socket)
            where TServerChannel : IServerSocketChannel
        {
            return new TcpSocketChannel(channel, socket, true);
        }
    }
}
