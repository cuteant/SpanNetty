namespace DotNetty.TestSuite.Transport
{
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;

    public class SocketEventLoopTest : AbstractSingleThreadEventLoopTest<TcpServerSocketChannel>
    {
        protected override IEventLoopGroup NewEventLoopGroup()
        {
            return new MultithreadEventLoopGroup();
        }

        protected override IChannel NewChannel()
        {
            return new TcpSocketChannel();
        }
    }
}