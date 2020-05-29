
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit;

    [Collection("BootstrapEnv")]
    public class Http2MultiplexTransportTest : IDisposable
    {
        private ServerBootstrap _sb;
        private Bootstrap _bs;
        private IChannel _clientChannel;
        private IChannel _serverChannel;
        private volatile IChannel _serverConnectedChannel;

        public Http2MultiplexTransportTest()
        {
        }

        public void Dispose()
        {
            if (_clientChannel != null)
            {
                _clientChannel.CloseAsync().GetAwaiter().GetResult();
                _clientChannel = null;
            }
            if (_serverChannel != null)
            {
                _serverChannel.CloseAsync().GetAwaiter().GetResult();
                _serverChannel = null;
            }
            var serverConnectedChannel = _serverConnectedChannel;
            if (serverConnectedChannel != null)
            {
                serverConnectedChannel.CloseAsync().GetAwaiter().GetResult();
                _serverConnectedChannel = null;
            }
            Task.WhenAll(
                _sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                _sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                _bs.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5))).GetAwaiter().GetResult();
        }

        [Fact]
        public void AsyncSettingsAckWithMultiplexCodec()
        {
            AsyncSettingsAck0(new Http2MultiplexCodecBuilder(true, new HttpInboundHandler()).Build(), null);
        }

        [Fact]
        public void AsyncSettingsAckWithMultiplexHandler()
        {
            AsyncSettingsAck0(new Http2FrameCodecBuilder(true).Build(),
                    new Http2MultiplexHandler(new HttpInboundHandler()));
        }

        private void AsyncSettingsAck0(Http2FrameCodec codec, IChannelHandler multiplexer)
        {
            // The client expects 2 settings frames. One from the connection setup and one from this test.
            CountdownEvent serverAckOneLatch = new CountdownEvent(1);
            CountdownEvent serverAckAllLatch = new CountdownEvent(2);
            CountdownEvent clientSettingsLatch = new CountdownEvent(2);
            CountdownEvent serverConnectedChannelLatch = new CountdownEvent(1);
            AtomicReference<IChannel> serverConnectedChannelRef = new AtomicReference<IChannel>();
            _sb = new ServerBootstrap();
            _sb.Group(new MultithreadEventLoopGroup(1), new MultithreadEventLoopGroup());
            _sb.Channel<TcpServerSocketChannel>();
            _sb.ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
            {
                ch.Pipeline.AddLast(codec);
                if (multiplexer != null)
                {
                    ch.Pipeline.AddLast(multiplexer);
                }
                ch.Pipeline.AddLast(new TestServerInboundHandler(serverAckOneLatch, serverAckAllLatch, serverConnectedChannelLatch, serverConnectedChannelRef));
            }));
            var loopback = IPAddress.IPv6Loopback;
            _serverChannel = _sb.BindAsync(loopback, 0).GetAwaiter().GetResult();

            _bs = new Bootstrap();
            _bs.Group(new MultithreadEventLoopGroup());
            _bs.Channel<TcpSocketChannel>();
            _bs.Handler(new ActionChannelInitializer<IChannel>(ch =>
            {
                var builder = Http2MultiplexCodecBuilder.ForClient(new HttpInboundHandler());
                builder.AutoAckSettingsFrame = false;
                ch.Pipeline.AddLast(builder.Build());
                ch.Pipeline.AddLast(new TestClientInboundHandler(clientSettingsLatch));
            }));
            var port = ((IPEndPoint)_serverChannel.LocalAddress).Port;
            var ccf = _bs.ConnectAsync(loopback, port);
            _clientChannel = ccf.GetAwaiter().GetResult();
            serverConnectedChannelLatch.Wait();
            _serverConnectedChannel = serverConnectedChannelRef.Value;

            _serverConnectedChannel.WriteAndFlushAsync(new DefaultHttp2SettingsFrame(new Http2Settings()
                .MaxConcurrentStreams(10))).GetAwaiter().GetResult();

            clientSettingsLatch.Wait();

            // We expect a timeout here because we want to asynchronously generate the SETTINGS ACK below.
            Assert.False(serverAckOneLatch.Wait(TimeSpan.FromMilliseconds(300)));

            // We expect 2 settings frames, the initial settings frame during connection establishment and the setting frame
            // written in this test. We should ack both of these settings frames.
            _clientChannel.WriteAndFlushAsync(DefaultHttp2SettingsAckFrame.Instance).GetAwaiter().GetResult();
            _clientChannel.WriteAndFlushAsync(DefaultHttp2SettingsAckFrame.Instance).GetAwaiter().GetResult();

            serverAckAllLatch.Wait();
        }

        sealed class TestClientInboundHandler : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _clientSettingsLatch;

            public TestClientInboundHandler(CountdownEvent clientSettingsLatch)
            {
                _clientSettingsLatch = clientSettingsLatch;
            }

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                if (message is IHttp2SettingsFrame)
                {
                    _clientSettingsLatch.SafeSignal();
                }
                ReferenceCountUtil.Release(message);
            }
        }

        sealed class TestServerInboundHandler : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _serverAckOneLatch;
            private readonly CountdownEvent _serverAckAllLatch;
            private readonly CountdownEvent _serverConnectedChannelLatch;
            private readonly AtomicReference<IChannel> _serverConnectedChannelRef;

            public TestServerInboundHandler(CountdownEvent serverAckOneLatch, CountdownEvent serverAckAllLatch,
                CountdownEvent serverConnectedChannelLatch, AtomicReference<IChannel> serverConnectedChannelRef)
            {
                _serverAckOneLatch = serverAckOneLatch;
                _serverAckAllLatch = serverAckAllLatch;
                _serverConnectedChannelLatch = serverConnectedChannelLatch;
                _serverConnectedChannelRef = serverConnectedChannelRef;
            }

            public override void ChannelActive(IChannelHandlerContext context)
            {
                _serverConnectedChannelRef.Value = context.Channel;
                _serverConnectedChannelLatch.SafeSignal();
            }

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                if (message is IHttp2SettingsAckFrame)
                {
                    _serverAckOneLatch.SafeSignal();
                    _serverAckAllLatch.SafeSignal();
                }
                ReferenceCountUtil.Release(message);
            }
        }

        sealed class HttpInboundHandler : ChannelHandlerAdapter
        {
            public override bool IsSharable => true;
        }
    }
}
