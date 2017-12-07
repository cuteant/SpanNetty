using System.Net.Sockets;

namespace DotNetty.Transport.Channels.Sockets
{
    public class DefaultTcpSocketChannelFactory : ITcpSocketChannelFactory
    {
        public virtual ISocketChannel CreateChannel<TServerChannel>(TServerChannel channel, Socket socket)
            where TServerChannel : IServerSocketChannel
        {
            return new TcpSocketChannel(channel, socket, true);
        }
    }
}
