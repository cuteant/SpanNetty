using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using DotNetty.Buffers;
using DotNetty.Common.Internal.Logging;
using DotNetty.Common.Utilities;
using DotNetty.Handlers.Tls;
using DotNetty.Tests.Common;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;

namespace DotNetty.Handlers.Proxy.Tests
{
    internal abstract class ProxyServer
    {
        protected readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<ProxyServer>();

        private readonly TcpServerSocketChannel _ch;
        private readonly ConcurrentQueue<Exception> _recordedExceptions = new ConcurrentQueue<Exception>();
        
        protected readonly TestMode TestMode;
        protected readonly string Username;
        protected readonly string Password;
        protected readonly EndPoint Destination;

        /**
     * Starts a new proxy server with disabled authentication for testing purpose.
     *
     * @param useSsl {@code true} if and only if implicit SSL is enabled
     * @param testMode the test mode
     * @param destination the expected destination. If the client requests proxying to a different destination, this
     * server will reject the connection request.
     */
        protected ProxyServer(bool useSsl, TestMode testMode, EndPoint destination)
            : this(useSsl, testMode, destination, null, null)
        {
        }

        /**
     * Starts a new proxy server with disabled authentication for testing purpose.
     *
     * @param useSsl {@code true} if and only if implicit SSL is enabled
     * @param testMode the test mode
     * @param username the expected username. If the client tries to authenticate with a different username, this server
     * will fail the authentication request.
     * @param password the expected password. If the client tries to authenticate with a different password, this server
     * will fail the authentication request.
     * @param destination the expected destination. If the client requests proxying to a different destination, this
     * server will reject the connection request.
     */
        protected ProxyServer(bool useSsl, TestMode testMode, EndPoint destination, string username, string password)
        {
            TestMode = testMode;
            Destination = destination;
            Username = username;
            Password = password;

            var b = new ServerBootstrap()
                .Channel<TcpServerSocketChannel>()
                .Group(ProxyHandlerTest.Group)
                .ChildHandler(new ActionChannelInitializer<ISocketChannel>(ch =>
                {
                    var p = ch.Pipeline;
                    if (useSsl)
                    {
                        p.AddLast(TlsHandler.Server(TestResourceHelper.GetTestCertificate()));
                    }

                    Configure(ch);
                }));
            
            _ch = (TcpServerSocketChannel) b.BindAsync(IPAddress.Loopback, 0).Result;
        }

        public IPEndPoint Address 
            => new IPEndPoint(IPAddress.Loopback, ((IPEndPoint) _ch.LocalAddress).Port);

        protected abstract void Configure(ISocketChannel ch);

        private void RecordException(Exception t)
        {
            Logger.Warn("Unexpected exception from proxy server:", t);
            _recordedExceptions.Enqueue(t);
        }

        /**
         * Clears all recorded exceptions.
         */
        public void ClearExceptions()
        {
            while (_recordedExceptions.TryDequeue(out _))
            {
                
            }
        }

        /**
         * Logs all recorded exceptions and raises the last one so that the caller can fail.
         */
        public void CheckExceptions()
        {
            Exception t;
            for (;;)
            {       
                if (!_recordedExceptions.TryDequeue(out t))
                {
                    break;
                }

                Logger.Warn("Unexpected exception:", t);
            }

            if (t != null)
            {
                throw t;
            }
        }

        public void Stop()
        {
            _ch.CloseAsync();
        }

        protected abstract class IntermediaryHandler : SimpleChannelInboundHandler<object>
        {
            private readonly ProxyServer _server;
            private readonly ConcurrentQueue<object> _received = new ConcurrentQueue<object>();

            private bool _finished;
            private IChannel _backend;

            protected IntermediaryHandler(ProxyServer server)
            {
                _server = server;
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
            {
                if (_finished)
                {
                    _received.Enqueue(ReferenceCountUtil.Retain(msg));
                    Flush();
                    return;
                }

                bool finished = HandleProxyProtocol(ctx, msg);
                if (finished)
                {
                    _finished = true;
                    Task<IChannel> f = ConnectToDestination(ctx.Channel.EventLoop, new BackendHandler(_server, ctx));
                    f.ContinueWith(future =>
                    {
                        if (!future.IsSuccess())
                        {
                            _server.RecordException(future.Exception);
                            ctx.CloseAsync();
                        }
                        else
                        {
                            _backend = future.Result;
                            Flush();
                        }
                    }, TaskContinuationOptions.ExecuteSynchronously);
                }
            }

            private void Flush()
            {
                if (_backend != null)
                {
                    for (;;)
                    {
                        if (!_received.TryDequeue(out var msg))
                        {
                            break;
                        }

                        _backend.WriteAsync(msg);
                        _backend.Flush();
                    }
                }
            }

            protected abstract bool HandleProxyProtocol(IChannelHandlerContext ctx, object msg);

            protected abstract EndPoint IntermediaryDestination { get; set; }

            private Task<IChannel> ConnectToDestination(IEventLoop loop, IChannelHandler handler)
            {
                var b = new Bootstrap()
                    .Channel<TcpSocketChannel>()
                    .Group(loop)
                    .Handler(handler);

                return b.ConnectAsync(IntermediaryDestination);
            }

            public override void ChannelReadComplete(IChannelHandlerContext ctx)
            {
                ctx.Flush();
            }

            public override void ChannelInactive(IChannelHandlerContext ctx)
            {
                if (_backend != null)
                {
                    _backend.CloseAsync();
                }
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                _server.RecordException(cause);
                ctx.CloseAsync();
            }

            private sealed class BackendHandler : ChannelHandlerAdapter
            {
                private readonly ProxyServer _server;
                private readonly IChannelHandlerContext _frontend;

                internal BackendHandler(ProxyServer server, IChannelHandlerContext frontend)
                {
                    _server = server;
                    _frontend = frontend;
                }

                public override void ChannelRead(IChannelHandlerContext ctx, object msg)
                {
                    _frontend.WriteAsync(msg);
                }

                public override void ChannelReadComplete(IChannelHandlerContext ctx)
                {
                    _frontend.Flush();
                }

                public override void ChannelInactive(IChannelHandlerContext ctx)
                {
                    _frontend.CloseAsync();
                }

                public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
                {
                    _server.RecordException(cause);
                    ctx.CloseAsync();
                }
            }
        }

        protected abstract class TerminalHandler : SimpleChannelInboundHandler<object>
        {
            private readonly ProxyServer _server;
            private bool _finished;

            protected TerminalHandler(ProxyServer server)
            {
                _server = server;
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
            {
                if (_finished)
                {
                    string str = ((IByteBuffer) msg).ToString(Encoding.ASCII);
                    if ("A\n".Equals(str))
                    {
                        ctx.WriteAsync(Unpooled.CopiedBuffer("1\n", Encoding.ASCII));
                    }
                    else if ("B\n".Equals(str))
                    {
                        ctx.WriteAsync(Unpooled.CopiedBuffer("2\n", Encoding.ASCII));
                    }
                    else if ("C\n".Equals(str))
                    {
                        ctx.WriteAsync(Unpooled.CopiedBuffer("3\n", Encoding.ASCII))
                            .ContinueWith(_ => ctx.Channel.CloseAsync(), TaskContinuationOptions.ExecuteSynchronously);
                    }
                    else
                    {
                        throw new InvalidOperationException("unexpected message: " + str);
                    }

                    return;
                }

                bool finished = HandleProxyProtocol(ctx, msg);
                if (finished)
                {
                    _finished = true;
                }
            }

            protected abstract bool HandleProxyProtocol(IChannelHandlerContext ctx, object msg);

            public override void ChannelReadComplete(IChannelHandlerContext ctx)
            {
                ctx.Flush();
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                _server.RecordException(cause);
                ctx.CloseAsync();
            }
        }
    }
}