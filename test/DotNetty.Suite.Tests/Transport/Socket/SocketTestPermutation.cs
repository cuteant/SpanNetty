namespace DotNetty.Suite.Tests.Transport.Socket
{
    using System;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using DotNetty.Buffers;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using DotNetty.Transport.Libuv;

    static class SocketTestPermutation
    {
        internal const int BOSSES = 2;
        internal const int WORKERS = 3;

        internal static readonly AddressFamily[] AddressFamilyTypes =
        {
            AddressFamily.InterNetwork,
            AddressFamily.InterNetworkV6
        };

        internal static readonly IByteBufferAllocator[] Allocators =
        {
            PooledByteBufferAllocator.Default,
            UnpooledByteBufferAllocator.Default,
            ArrayPooledByteBufferAllocator.Default
        };

        public static IPAddress GetLoopbackAddress(AddressFamily addressFamily)
        {
            if (addressFamily == AddressFamily.InterNetwork)
            {
                return IPAddress.Loopback;
            }

            if (addressFamily == AddressFamily.InterNetworkV6)
            {
                return IPAddress.IPv6Loopback;
            }

            throw new NotSupportedException($"Address family {addressFamily} is not supported. Expecting InterNetwork/InterNetworkV6");
        }

        public static NetworkInterface LoopbackInterface(AddressFamily addressFamily)
        {
            NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            if (addressFamily == AddressFamily.InterNetwork)
            {
                return networkInterfaces[NetworkInterface.LoopbackInterfaceIndex];
            }

            if (addressFamily == AddressFamily.InterNetworkV6)
            {
                return networkInterfaces[NetworkInterface.IPv6LoopbackInterfaceIndex];
            }

            throw new NotSupportedException($"Address family {addressFamily} is not supported. Expecting InterNetwork/InterNetworkV6");
        }

        internal class DummyHandler : SimpleChannelInboundHandler<DatagramPacket>
        {
            protected override void ChannelRead0(IChannelHandlerContext ctx, DatagramPacket msg)
            {
                // Do nothing
            }
        }
    }

    public sealed class DefaultClientBootstrapFactory : IClientBootstrapFactory
    {
        public static readonly DefaultClientBootstrapFactory Instance = new DefaultClientBootstrapFactory();

        private DefaultClientBootstrapFactory() { }

        public Bootstrap NewInstance()
        {
            return new Bootstrap()
                .Group(new MultithreadEventLoopGroup(SocketTestPermutation.WORKERS))
                .Channel<TcpSocketChannel>();
        }
    }

    public sealed class DatagramBootstrapFactory : IClientBootstrapFactory
    {
        public static readonly DatagramBootstrapFactory Instance = new DatagramBootstrapFactory();

        private DatagramBootstrapFactory() { }

        public Bootstrap NewInstance()
        {
            return new Bootstrap()
                .Group(new MultithreadEventLoopGroup(SocketTestPermutation.WORKERS))
                .Channel<SocketDatagramChannel>();
        }
    }

    public sealed class LibuvClientBootstrapFactory : IClientBootstrapFactory
    {
        public static readonly LibuvClientBootstrapFactory Instance = new LibuvClientBootstrapFactory();

        private LibuvClientBootstrapFactory() { }

        public Bootstrap NewInstance()
        {
            return new Bootstrap()
                .Group(new EventLoopGroup(SocketTestPermutation.WORKERS))
                .Channel<TcpChannel>();
        }
    }

    public sealed class DefaultServerBootstrapFactory : IServerBootstrapFactory
    {
        public static readonly DefaultServerBootstrapFactory Instance = new DefaultServerBootstrapFactory();

        private DefaultServerBootstrapFactory() { }

        public ServerBootstrap NewInstance()
        {
            return new ServerBootstrap()
                .Group(new MultithreadEventLoopGroup(SocketTestPermutation.BOSSES), new MultithreadEventLoopGroup(SocketTestPermutation.WORKERS))
                .Channel<TcpServerSocketChannel>();
        }
    }

    public sealed class LibuvServerBootstrapFactory : IServerBootstrapFactory
    {
        public static readonly LibuvServerBootstrapFactory Instance = new LibuvServerBootstrapFactory();

        private LibuvServerBootstrapFactory() { }

        public ServerBootstrap NewInstance()
        {
            var dispatcher = new DispatcherEventLoopGroup();
            return new ServerBootstrap()
                .Group(dispatcher, new WorkerEventLoopGroup(dispatcher, SocketTestPermutation.WORKERS))
                .Channel<TcpServerChannel>();
        }
    }
}
