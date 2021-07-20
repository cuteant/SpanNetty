using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Common.Internal.Logging;
using DotNetty.Common.Utilities;
using DotNetty.Handlers.Tls;
using DotNetty.Tests.Common;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace DotNetty.Handlers.Proxy.Tests
{
    public class ProxyHandlerTest : TestBase, IClassFixture<ProxyHandlerTest.ProxyHandlerTestFixture>, IDisposable
    {
        private class ProxyHandlerTestFixture : IDisposable
        {
            public void Dispose()
            {
                StopServers();
            }
        }

        private static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<ProxyHandlerTest>();

        private static readonly EndPoint DESTINATION = new DnsEndPoint("destination.com", 42);
        private static readonly EndPoint BAD_DESTINATION = new IPEndPoint(IPAddress.Parse("1.2.3.4"), 5);
        private static readonly string USERNAME = "testUser";
        private static readonly string PASSWORD = "testPassword";
        private static readonly string BAD_USERNAME = "badUser";
        private static readonly string BAD_PASSWORD = "badPassword";

        internal static readonly IEventLoopGroup Group = new DefaultEventLoopGroup(3);

        private static readonly ProxyServer DeadHttpProxy = new HttpProxyServer(false, TestMode.Unresponsive, null);
        private static readonly ProxyServer InterHttpProxy = new HttpProxyServer(false, TestMode.Intermediary, null);
        private static readonly ProxyServer AnonHttpProxy = new HttpProxyServer(false, TestMode.Terminal, DESTINATION);

        private static readonly ProxyServer HttpProxy =
            new HttpProxyServer(false, TestMode.Terminal, DESTINATION, USERNAME, PASSWORD);

        private static readonly ProxyServer DeadHttpsProxy = new HttpProxyServer(true, TestMode.Unresponsive, null);
        private static readonly ProxyServer InterHttpsProxy = new HttpProxyServer(true, TestMode.Intermediary, null);
        private static readonly ProxyServer AnonHttpsProxy = new HttpProxyServer(true, TestMode.Terminal, DESTINATION);

        private static readonly ProxyServer HttpsProxy =
            new HttpProxyServer(true, TestMode.Terminal, DESTINATION, USERNAME, PASSWORD);

        /*
        static readonly ProxyServer deadSocks4Proxy = new Socks4ProxyServer(false, TestMode.UNRESPONSIVE, null);
        static readonly ProxyServer interSocks4Proxy = new Socks4ProxyServer(false, TestMode.INTERMEDIARY, null);
        static readonly ProxyServer anonSocks4Proxy = new Socks4ProxyServer(false, TestMode.TERMINAL, DESTINATION);
        static readonly ProxyServer socks4Proxy = new Socks4ProxyServer(false, TestMode.TERMINAL, DESTINATION, USERNAME);

        static readonly ProxyServer deadSocks5Proxy = new Socks5ProxyServer(false, TestMode.UNRESPONSIVE, null);
        static readonly ProxyServer interSocks5Proxy = new Socks5ProxyServer(false, TestMode.INTERMEDIARY, null);
        static readonly ProxyServer anonSocks5Proxy = new Socks5ProxyServer(false, TestMode.TERMINAL, DESTINATION);
        static readonly ProxyServer socks5Proxy =
                new Socks5ProxyServer(false, TestMode.TERMINAL, DESTINATION, USERNAME, PASSWORD);*/

        private static readonly IEnumerable<ProxyServer> AllProxies = new[]
        {
            DeadHttpProxy, InterHttpProxy, AnonHttpProxy, HttpProxy,
            DeadHttpsProxy, InterHttpsProxy, AnonHttpsProxy, HttpsProxy
            //deadSocks4Proxy, interSocks4Proxy, anonSocks4Proxy, socks4Proxy,
            //deadSocks5Proxy, interSocks5Proxy, anonSocks5Proxy, socks5Proxy
        };

        // set to non-zero value in case you need predictable shuffling of test cases
        // look for "Seed used: *" debug message in test logs
        private static readonly int ReproducibleSeed = 0;
        
        public ProxyHandlerTest(ITestOutputHelper output) : base(output)
        {
            ClearServerExceptions();
        }
        
        [Theory]
        [MemberData(nameof(CreateTestItems))]
        public void Test(TestItem item)
        {
            item.Test();
        }

        public void Dispose()
        {
            foreach (var p in AllProxies) p.CheckExceptions();
        }

        public static List<object[]> CreateTestItems()
        {
            var items = new List<TestItem>
            {
                // HTTP -------------------------------------------------------

                new SuccessTestItem(
                    "Anonymous HTTP proxy: successful connection, AUTO_READ on",
                    DESTINATION,
                    true,
                    new HttpProxyHandler(AnonHttpProxy.Address)),

                new SuccessTestItem(
                    "Anonymous HTTP proxy: successful connection, AUTO_READ off",
                    DESTINATION,
                    false,
                    new HttpProxyHandler(AnonHttpProxy.Address)),

                new FailureTestItem(
                    "Anonymous HTTP proxy: rejected connection",
                    BAD_DESTINATION, "status: 403",
                    new HttpProxyHandler(AnonHttpProxy.Address)),

                new FailureTestItem(
                    "HTTP proxy: rejected anonymous connection",
                    DESTINATION, "status: 401",
                    new HttpProxyHandler(HttpProxy.Address)),

                new SuccessTestItem(
                    "HTTP proxy: successful connection, AUTO_READ on",
                    DESTINATION,
                    true,
                    new HttpProxyHandler(HttpProxy.Address, USERNAME, PASSWORD)),

                new SuccessTestItem(
                    "HTTP proxy: successful connection, AUTO_READ off",
                    DESTINATION,
                    false,
                    new HttpProxyHandler(HttpProxy.Address, USERNAME, PASSWORD)),

                new FailureTestItem(
                    "HTTP proxy: rejected connection",
                    BAD_DESTINATION, "status: 403",
                    new HttpProxyHandler(HttpProxy.Address, USERNAME, PASSWORD)),

                new FailureTestItem(
                    "HTTP proxy: authentication failure",
                    DESTINATION, "status: 401",
                    new HttpProxyHandler(HttpProxy.Address, BAD_USERNAME, BAD_PASSWORD)),

                new TimeoutTestItem(
                    "HTTP proxy: timeout",
                    new HttpProxyHandler(DeadHttpProxy.Address)),

                // HTTPS ------------------------------------------------------

                new SuccessTestItem(
                    "Anonymous HTTPS proxy: successful connection, AUTO_READ on",
                    DESTINATION,
                    true,
                    CreateClientTlsHandler(),
                    new HttpProxyHandler(AnonHttpsProxy.Address)),

                new SuccessTestItem(
                    "Anonymous HTTPS proxy: successful connection, AUTO_READ off",
                    DESTINATION,
                    false,
                    CreateClientTlsHandler(),
                    new HttpProxyHandler(AnonHttpsProxy.Address)),

                new FailureTestItem(
                    "Anonymous HTTPS proxy: rejected connection",
                    BAD_DESTINATION, "status: 403",
                    CreateClientTlsHandler(),
                    new HttpProxyHandler(AnonHttpsProxy.Address)),

                new FailureTestItem(
                    "HTTPS proxy: rejected anonymous connection",
                    DESTINATION, "status: 401",
                    CreateClientTlsHandler(),
                    new HttpProxyHandler(HttpsProxy.Address)),

                new SuccessTestItem(
                    "HTTPS proxy: successful connection, AUTO_READ on",
                    DESTINATION,
                    true,
                    CreateClientTlsHandler(),
                    new HttpProxyHandler(HttpsProxy.Address, USERNAME, PASSWORD)),

                new SuccessTestItem(
                    "HTTPS proxy: successful connection, AUTO_READ off",
                    DESTINATION,
                    false,
                    CreateClientTlsHandler(),
                    new HttpProxyHandler(HttpsProxy.Address, USERNAME, PASSWORD)),

                new FailureTestItem(
                    "HTTPS proxy: rejected connection",
                    BAD_DESTINATION, "status: 403",
                    CreateClientTlsHandler(),
                    new HttpProxyHandler(HttpsProxy.Address, USERNAME, PASSWORD)),

                new FailureTestItem(
                    "HTTPS proxy: authentication failure",
                    DESTINATION, "status: 401",
                    CreateClientTlsHandler(),
                    new HttpProxyHandler(HttpsProxy.Address, BAD_USERNAME, BAD_PASSWORD)),
                
                new TimeoutTestItem(
                    "HTTPS proxy: timeout",
                    CreateClientTlsHandler(),
                    new HttpProxyHandler(DeadHttpsProxy.Address))

/*
                // SOCKS4 -----------------------------------------------------

                    new SuccessTestItem(
                            "Anonymous SOCKS4: successful connection, AUTO_READ on",
                            DESTINATION,
                            true,
                            new Socks4ProxyHandler(anonSocks4Proxy.Address)),

                    new SuccessTestItem(
                            "Anonymous SOCKS4: successful connection, AUTO_READ off",
                            DESTINATION,
                            false,
                            new Socks4ProxyHandler(anonSocks4Proxy.Address)),

                    new FailureTestItem(
                            "Anonymous SOCKS4: rejected connection",
                            BAD_DESTINATION, "status: REJECTED_OR_FAILED",
                            new Socks4ProxyHandler(anonSocks4Proxy.Address)),

                    new FailureTestItem(
                            "SOCKS4: rejected anonymous connection",
                            DESTINATION, "status: IDENTD_AUTH_FAILURE",
                            new Socks4ProxyHandler(socks4Proxy.Address)),

                    new SuccessTestItem(
                            "SOCKS4: successful connection, AUTO_READ on",
                            DESTINATION,
                            true,
                            new Socks4ProxyHandler(socks4Proxy.Address, USERNAME)),

                    new SuccessTestItem(
                            "SOCKS4: successful connection, AUTO_READ off",
                            DESTINATION,
                            false,
                            new Socks4ProxyHandler(socks4Proxy.Address, USERNAME)),

                    new FailureTestItem(
                            "SOCKS4: rejected connection",
                            BAD_DESTINATION, "status: REJECTED_OR_FAILED",
                            new Socks4ProxyHandler(socks4Proxy.Address, USERNAME)),

                    new FailureTestItem(
                            "SOCKS4: authentication failure",
                            DESTINATION, "status: IDENTD_AUTH_FAILURE",
                            new Socks4ProxyHandler(socks4Proxy.Address, BAD_USERNAME)),

                    new TimeoutTestItem(
                            "SOCKS4: timeout",
                            new Socks4ProxyHandler(deadSocks4Proxy.Address)),
*/
                // SOCKS5 -----------------------------------------------------
/*
                    new SuccessTestItem(
                            "Anonymous SOCKS5: successful connection, AUTO_READ on",
                            DESTINATION,
                            true,
                            new Socks5ProxyHandler(anonSocks5Proxy.Address)),

                    new SuccessTestItem(
                            "Anonymous SOCKS5: successful connection, AUTO_READ off",
                            DESTINATION,
                            false,
                            new Socks5ProxyHandler(anonSocks5Proxy.Address)),

                    new FailureTestItem(
                            "Anonymous SOCKS5: rejected connection",
                            BAD_DESTINATION, "status: FORBIDDEN",
                            new Socks5ProxyHandler(anonSocks5Proxy.Address)),

                    new FailureTestItem(
                            "SOCKS5: rejected anonymous connection",
                            DESTINATION, "unexpected authMethod: PASSWORD",
                            new Socks5ProxyHandler(socks5Proxy.Address)),

                    new SuccessTestItem(
                            "SOCKS5: successful connection, AUTO_READ on",
                            DESTINATION,
                            true,
                            new Socks5ProxyHandler(socks5Proxy.Address, USERNAME, PASSWORD)),

                    new SuccessTestItem(
                            "SOCKS5: successful connection, AUTO_READ off",
                            DESTINATION,
                            false,
                            new Socks5ProxyHandler(socks5Proxy.Address, USERNAME, PASSWORD)),

                    new FailureTestItem(
                            "SOCKS5: rejected connection",
                            BAD_DESTINATION, "status: FORBIDDEN",
                            new Socks5ProxyHandler(socks5Proxy.Address, USERNAME, PASSWORD)),

                    new FailureTestItem(
                            "SOCKS5: authentication failure",
                            DESTINATION, "authStatus: FAILURE",
                            new Socks5ProxyHandler(socks5Proxy.Address, BAD_USERNAME, BAD_PASSWORD)),

                    new TimeoutTestItem(
                            "SOCKS5: timeout",
                            new Socks5ProxyHandler(deadSocks5Proxy.Address)),

                    // HTTP + HTTPS + SOCKS4 + SOCKS5

                    new SuccessTestItem(
                            "Single-chain: successful connection, AUTO_READ on",
                            DESTINATION,
                            true,
                            new Socks5ProxyHandler(interSocks5Proxy.Address), // SOCKS5
                            new Socks4ProxyHandler(interSocks4Proxy.Address), // SOCKS4
                            clientSslCtx.newHandler(PooledByteBufferAllocator.Default),
                            new HttpProxyHandler(interHttpsProxy.Address), // HTTPS
                            new HttpProxyHandler(interHttpProxy.Address), // HTTP
                            new HttpProxyHandler(anonHttpProxy.Address)),

                    new SuccessTestItem(
                            "Single-chain: successful connection, AUTO_READ off",
                            DESTINATION,
                            false,
                            new Socks5ProxyHandler(interSocks5Proxy.Address), // SOCKS5
                            new Socks4ProxyHandler(interSocks4Proxy.Address), // SOCKS4
                            clientSslCtx.newHandler(PooledByteBufferAllocator.Default),
                            new HttpProxyHandler(interHttpsProxy.Address), // HTTPS
                            new HttpProxyHandler(interHttpProxy.Address), // HTTP
                            new HttpProxyHandler(anonHttpProxy.Address)),

                    // (HTTP + HTTPS + SOCKS4 + SOCKS5) * 2

                    new SuccessTestItem(
                            "Double-chain: successful connection, AUTO_READ on",
                            DESTINATION,
                            true,
                            new Socks5ProxyHandler(interSocks5Proxy.Address), // SOCKS5
                            new Socks4ProxyHandler(interSocks4Proxy.Address), // SOCKS4
                            clientSslCtx.newHandler(PooledByteBufferAllocator.Default),
                            new HttpProxyHandler(interHttpsProxy.Address), // HTTPS
                            new HttpProxyHandler(interHttpProxy.Address), // HTTP
                            new Socks5ProxyHandler(interSocks5Proxy.Address), // SOCKS5
                            new Socks4ProxyHandler(interSocks4Proxy.Address), // SOCKS4
                            clientSslCtx.newHandler(PooledByteBufferAllocator.Default),
                            new HttpProxyHandler(interHttpsProxy.Address), // HTTPS
                            new HttpProxyHandler(interHttpProxy.Address), // HTTP
                            new HttpProxyHandler(anonHttpProxy.Address)),

                    new SuccessTestItem(
                            "Double-chain: successful connection, AUTO_READ off",
                            DESTINATION,
                            false,
                            new Socks5ProxyHandler(interSocks5Proxy.Address), // SOCKS5
                            new Socks4ProxyHandler(interSocks4Proxy.Address), // SOCKS4
                            clientSslCtx.newHandler(PooledByteBufferAllocator.Default),
                            new HttpProxyHandler(interHttpsProxy.Address), // HTTPS
                            new HttpProxyHandler(interHttpProxy.Address), // HTTP
                            new Socks5ProxyHandler(interSocks5Proxy.Address), // SOCKS5
                            new Socks4ProxyHandler(interSocks4Proxy.Address), // SOCKS4
                            clientSslCtx.newHandler(PooledByteBufferAllocator.Default),
                            new HttpProxyHandler(interHttpsProxy.Address), // HTTPS
                            new HttpProxyHandler(interHttpProxy.Address), // HTTP
                            new HttpProxyHandler(anonHttpProxy.Address))
                            */
            };

            // Convert the test items to the list of constructor parameters.
            var parameters = new List<object[]>(items.Count);
            foreach (var i in items)
            {
                parameters.Add(new object[] {i});
            }

            // Randomize the execution order to increase the possibility of exposing failure dependencies.
            var seed = ReproducibleSeed == 0L ? Environment.TickCount : ReproducibleSeed;
            Logger.Debug($"Seed used: {seed}\n");
            var rnd = new Random(seed);
            parameters = parameters.OrderBy(_ => rnd.Next()).ToList();
            return parameters;
        }

        private static TlsHandler CreateClientTlsHandler()
        {
            return new(s => new SslStream(s, true, (sender, certificate, chain, errors) => true),
                new ClientTlsSettings("foo"));
        }

        private static void StopServers()
        {
            foreach (var p in AllProxies) p.Stop();
        }

        private static void ClearServerExceptions()
        {
            foreach (var p in AllProxies) p.ClearExceptions();
        }

        private class SuccessTestHandler : SimpleChannelInboundHandler<object>
        {
            internal readonly Queue<Exception> Exceptions = new();
            internal readonly Queue<string> Received = new();
            internal volatile int EventCount;

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                ctx.WriteAndFlushAsync(Unpooled.CopiedBuffer("A\n", Encoding.ASCII));
                ReadIfNeeded(ctx);
            }
            
            public override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
            {
                if (evt is ProxyConnectionEvent)
                {
                    EventCount++;

                    if (
                            EventCount ==
                            1) // Note that ProxyConnectionEvent can be triggered multiple times when there are multiple
                        // ProxyHandlers in the pipeline.  Therefore, we send the 'B' message only on the first event.
                        ctx.WriteAndFlushAsync(Unpooled.CopiedBuffer("B\n", Encoding.ASCII));
                    ReadIfNeeded(ctx);
                }
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
            {
                var str = ((IByteBuffer) msg).ToString(Encoding.ASCII);
                Received.Enqueue(str);
                if ("2".Equals(str)) ctx.WriteAndFlushAsync(Unpooled.CopiedBuffer("C\n", Encoding.ASCII));
                ReadIfNeeded(ctx);
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                Exceptions.Enqueue(cause);
                ctx.CloseAsync();
            }
            
            private static void ReadIfNeeded(IChannelHandlerContext ctx)
            {
                if (!ctx.Channel.Configuration.IsAutoRead) ctx.Read();
            }
        }

        private class FailureTestHandler : SimpleChannelInboundHandler<object>
        {
            internal readonly Queue<Exception> Exceptions = new();

            /**
             * A latch that counts down when:
             * - a pending write attempt in {@link #channelActive(IChannelHandlerContext)} finishes, or
             * - the IChannel is closed.
             * By waiting until the latch goes down to 0, we can make sure all assertion failures related with all write
             * attempts have been recorded.
             */
            internal readonly CountdownEvent Latch = new(2);

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                ctx.WriteAndFlushAsync(Unpooled.CopiedBuffer("A\n", Encoding.ASCII)).ContinueWith(future =>
                {
                    Latch.Signal();
                    if (!(future.Exception.InnerException is ProxyConnectException))
                        Exceptions.Enqueue(new XunitException(
                            "Unexpected failure cause for initial write: " + future.Exception));
                });
            }

            public override void ChannelInactive(IChannelHandlerContext ctx)
            {
                Latch.Signal();
            }

            public override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
            {
                if (evt is ProxyConnectionEvent) throw new XunitException("Unexpected event: " + evt);
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
            {
                throw new XunitException("Unexpected message: " + msg);
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                Exceptions.Enqueue(cause);
                ctx.CloseAsync();
            }
        }

        public abstract class TestItem
        {
            private readonly string _name;
            
            protected readonly IChannelHandler[] ClientHandlers;
            protected readonly EndPoint Destination;

            protected TestItem(string name, EndPoint destination, params IChannelHandler[] clientHandlers)
            {
                _name = name;
                Destination = destination;
                ClientHandlers = clientHandlers;
            }

            public abstract void Test();

            protected void AssertProxyHandlers(bool success)
            {
                foreach (var h in ClientHandlers)
                    if (h is ProxyHandler)
                    {
                        var ph = (ProxyHandler) h;
                        var type = ph.GetType().Name;
                        var f = ph.ConnectFuture;
                        if (!f.IsCompleted)
                        {
                            Logger.Warn($"{type}: not done");
                        }
                        else if (f.IsSuccess())
                        {
                            if (success)
                                Logger.Debug("{0}: success", type);
                            else
                                Logger.Warn("{0}: success", type);
                        }
                        else
                        {
                            if (success)
                                Logger.Warn("{0}: failure", type, f.Exception);
                            else
                                Logger.Debug("{0}: failure", type, f.Exception);
                        }
                    }

                foreach (var h in ClientHandlers)
                    if (h is ProxyHandler)
                    {
                        var ph = (ProxyHandler) h;
                        Assert.True(ph.ConnectFuture.IsCompleted);
                        Assert.Equal(success, ph.ConnectFuture.IsSuccess());
                    }
            }

            public override string ToString()
            {
                return _name;
            }
        }

        private class SuccessTestItem : TestItem
        {
            // Probably we need to be more flexible here and as for the configuration map,
            // not a single key. But as far as it works for now, I'm leaving the impl.
            // as is, in case we need to cover more cases (like, AUTO_CLOSE, TCP_NODELAY etc)
            // feel free to replace this bool with either config or method to setup bootstrap
            private readonly bool _autoRead;
            private readonly int _expectedEventCount;

            internal SuccessTestItem(string name,
                EndPoint destination,
                bool autoRead,
                params IChannelHandler[] clientHandlers)
                : base(name, destination, clientHandlers)
            {
                var expectedEventCount = 0;
                foreach (var h in clientHandlers)
                    if (h is ProxyHandler)
                        expectedEventCount++;

                _expectedEventCount = expectedEventCount;
                _autoRead = autoRead;
            }

            public override void Test()
            {
                var testHandler = new SuccessTestHandler();
                var b = new Bootstrap()
                    .Group(Group)
                    .Channel<TcpSocketChannel>()
                    .Option(ChannelOption.AutoRead, _autoRead)
                    .Resolver(NoopNameResolver.Instance)
                    .Handler(new ActionChannelInitializer<ISocketChannel>(ch =>
                    {
                        var p = ch.Pipeline;
                        p.AddLast(ClientHandlers);
                        p.AddLast(new LineBasedFrameDecoder(64));
                        p.AddLast(testHandler);
                    }));


                var channel = b.ConnectAsync(Destination).Result;
                var finished = channel.CloseCompletion.Wait(TimeSpan.FromSeconds(10));

                Logger.Debug("Received messages: {0}", testHandler.Received);

                if (testHandler.Exceptions.Count == 0)
                    Logger.Debug("No recorded exceptions on the client side.");
                else
                    foreach (var t in testHandler.Exceptions)
                        Logger.Debug("Recorded exception on the client side: {0}", t);

                AssertProxyHandlers(true);

                Assert.Equal(testHandler.Received, new object[] {"0", "1", "2", "3"});
                Assert.Empty(testHandler.Exceptions);
                Assert.Equal(testHandler.EventCount, _expectedEventCount);
                Assert.True(finished);
            }
        }

        private class FailureTestItem : TestItem
        {
            private readonly string _expectedMessage;

            internal FailureTestItem(
                string name, EndPoint destination, string expectedMessage, params IChannelHandler[] clientHandlers)
                : base(name, destination, clientHandlers)
            {
                _expectedMessage = expectedMessage;
            }

            public override void Test()
            {
                var testHandler = new FailureTestHandler();
                var b = new Bootstrap();
                b
                    .Group(Group)
                    .Channel<TcpSocketChannel>()
                    .Resolver(NoopNameResolver.Instance)
                    .Handler(new ActionChannelInitializer<ISocketChannel>(ch =>
                    {
                        var p = ch.Pipeline;
                        p.AddLast(ClientHandlers);
                        p.AddLast(new LineBasedFrameDecoder(64));
                        p.AddLast(testHandler);
                    }));

                var finished = b.ConnectAsync(Destination).Result.CloseCompletion.Wait(TimeSpan.FromSeconds(10));
                finished &= testHandler.Latch.Wait(TimeSpan.FromSeconds(10));

                Logger.Debug("Recorded exceptions: {0}", testHandler.Exceptions);

                AssertProxyHandlers(false);

                Assert.Single(testHandler.Exceptions);
                var e = testHandler.Exceptions.Dequeue();
                Assert.IsAssignableFrom<ProxyConnectException>(e);
                Assert.Contains(_expectedMessage, e.Message);
                Assert.True(finished);
            }
        }

        private class TimeoutTestItem : TestItem
        {
            internal TimeoutTestItem(string name, params IChannelHandler[] clientHandlers)
                : base(name, null, clientHandlers)
            {
            }

            public override void Test()
            {
                const long timeout = 2000;
                foreach (var h in ClientHandlers)
                {
                    if (h is ProxyHandler handler)
                        handler.ConnectTimeout = TimeSpan.FromMilliseconds(timeout);
                }

                var testHandler = new FailureTestHandler();
                var b = new Bootstrap()
                    .Group(Group)
                    .Channel<TcpSocketChannel>()
                    .Resolver(NoopNameResolver.Instance)
                    .Handler(new ActionChannelInitializer<ISocketChannel>(ch =>
                    {
                        var p = ch.Pipeline;
                        p.AddLast(ClientHandlers);
                        p.AddLast(new LineBasedFrameDecoder(64));
                        p.AddLast(testHandler);
                    }));

                var channel = b.ConnectAsync(DESTINATION).Result;
                var cf = channel.CloseCompletion;
                var finished = cf.Wait(TimeSpan.FromMilliseconds(timeout * 2));
                finished &= testHandler.Latch.Wait(TimeSpan.FromMilliseconds(timeout * 2));

                Logger.Debug("Recorded exceptions: {0}", testHandler.Exceptions);

                AssertProxyHandlers(false);

                Assert.Single(testHandler.Exceptions);
                var e = testHandler.Exceptions.Dequeue();
                Assert.IsType<ProxyConnectException>(e);
                Assert.Contains("timeout", e.Message);
                Assert.True(finished);
            }
        }
    }
}