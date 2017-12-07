using System.Net.Sockets;

namespace DotNetty.Transport.Channels.Sockets
{

    public interface ITcpSocketChannelFactory
    {
        ISocketChannel CreateChannel<TServerChannel>(TServerChannel channel, Socket socket) where TServerChannel : IServerSocketChannel;
    }
}
