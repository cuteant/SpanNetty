
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit;

    [Collection("BootstrapEnv")]
    public class Http2MultiplexTransportTest : IDisposable
    {
        sealed class DISCARD_HANDLER : ChannelHandlerAdapter
        {
            public static readonly DISCARD_HANDLER Instance = new DISCARD_HANDLER();

            public override bool IsSharable => true;

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                ReferenceCountUtil.Release(message);
            }

            public override void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
                ReferenceCountUtil.Release(evt);
            }
        }

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
            Task.WaitAll(
                _sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                _sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                _bs.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void AsyncSettingsAckWithMultiplexCodec()
        {
            AsyncSettingsAck0(new Http2MultiplexCodecBuilder(true, DISCARD_HANDLER.Instance).Build(), null);
        }

        [Fact]
        public void AsyncSettingsAckWithMultiplexHandler()
        {
            AsyncSettingsAck0(new Http2FrameCodecBuilder(true).Build(),
                    new Http2MultiplexHandler(DISCARD_HANDLER.Instance));
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
                var builder = Http2MultiplexCodecBuilder.ForClient(DISCARD_HANDLER.Instance);
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

        [Fact]
        public void FlushNotDiscarded()
        {
            var executorService = new DefaultEventExecutor();

            try
            {
                _sb = new ServerBootstrap();
                _sb.Group(new MultithreadEventLoopGroup(1), new MultithreadEventLoopGroup());
                _sb.Channel<TcpServerSocketChannel>();
                _sb.ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new Http2FrameCodecBuilder(true).Build());
                    ch.Pipeline.AddLast(new Http2MultiplexHandler(new TestChannelInboundHandlerAdapter(executorService)));
                }));
                var loopback = IPAddress.IPv6Loopback;
                _serverChannel = _sb.BindAsync(loopback, 0).GetAwaiter().GetResult();

                CountdownEvent latch = new CountdownEvent(1);

                _bs = new Bootstrap();
                _bs.Group(new MultithreadEventLoopGroup());
                _bs.Channel<TcpSocketChannel>();
                _bs.Handler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new Http2FrameCodecBuilder(false).Build());
                    ch.Pipeline.AddLast(new Http2MultiplexHandler(DISCARD_HANDLER.Instance));
                }));
                var port = ((IPEndPoint)_serverChannel.LocalAddress).Port;
                var ccf = _bs.ConnectAsync(loopback, port);
                _clientChannel = ccf.GetAwaiter().GetResult();
                Http2StreamChannelBootstrap h2Bootstrap = new Http2StreamChannelBootstrap(_clientChannel);
                h2Bootstrap.Handler(new TestFlushNotDiscardedHandler(latch));
                IHttp2StreamChannel streamChannel = h2Bootstrap.OpenAsync().GetAwaiter().GetResult();
                streamChannel.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers(), true)).GetAwaiter().GetResult();
                latch.Wait();
            }
            finally
            {
                executorService.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            }
        }

        class TestChannelInboundHandlerAdapter : ChannelHandlerAdapter
        {
            private readonly SingleThreadEventExecutor _executorService;

            public TestChannelInboundHandlerAdapter(SingleThreadEventExecutor executorService)
            {
                _executorService = executorService;
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                if (msg is IHttp2HeadersFrame headersFrame && headersFrame.IsEndStream)
                {
                    _executorService.Schedule(() =>
                    {
                        ctx.WriteAndFlushAsync(new DefaultHttp2HeadersFrame(new DefaultHttp2Headers(), false))
                            .ContinueWith(t =>
                            {
                                ctx.WriteAsync(new DefaultHttp2DataFrame(
                                        Unpooled.CopiedBuffer("Hello World", Encoding.ASCII),
                                        true));
                                ctx.Channel.EventLoop.Execute(() => ctx.Flush());
                            }, TaskContinuationOptions.ExecuteSynchronously);
                    }, TimeSpan.FromMilliseconds(500));
                }
                ReferenceCountUtil.Release(msg);
            }
        }

        class TestFlushNotDiscardedHandler : ChannelHandlerAdapter
        {
            private readonly CountdownEvent _latch;

            public TestFlushNotDiscardedHandler(CountdownEvent latch)
            {
                _latch = latch;
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                if (msg is IHttp2DataFrame http2DataFrame && http2DataFrame.IsEndStream)
                {
                    _latch.SafeSignal();
                }
                ReferenceCountUtil.Release(msg);
            }

            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                context.WriteAsync(message, promise);
            }
        }
    }
}
