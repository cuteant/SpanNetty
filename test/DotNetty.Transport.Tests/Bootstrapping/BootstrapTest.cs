namespace DotNetty.Transport.Tests.Bootstrapping
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Local;
    using Xunit;

    public class BootstrapTest : IDisposable
    {
        private readonly IEventLoopGroup _groupA;
        private readonly IEventLoopGroup _groupB;
        private readonly IChannelHandler _dummyHandler;

        public BootstrapTest()
        {
            _groupA = new MultithreadEventLoopGroup(1);
            _groupB = new MultithreadEventLoopGroup(1);
            _dummyHandler = new DummyHandler();
        }

        public void Dispose()
        {
            _groupA.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            _groupB.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            _groupA.TerminationCompletion.GetAwaiter().GetResult();
            _groupB.TerminationCompletion.GetAwaiter().GetResult();
        }

        [Fact]
        public async Task TestBindDeadLock()
        {
            Bootstrap bootstrapA = new Bootstrap();
            bootstrapA.Group(_groupA);
            bootstrapA.Channel<LocalChannel>();
            bootstrapA.Handler(_dummyHandler);

            Bootstrap bootstrapB = new Bootstrap();
            bootstrapB.Group(_groupB);
            bootstrapB.Channel<LocalChannel>();
            bootstrapB.Handler(_dummyHandler);

            var bindFutures = new List<Task<int>>();

            // Try to bind from each other.
            for (int i = 0; i < 1024; i++)
            {
                bindFutures.Add(_groupA.GetNext().SubmitAsync(() =>
                {
                    bootstrapB.BindAsync(LocalAddress.Any);
                    return i;
                }));
                bindFutures.Add(_groupB.GetNext().SubmitAsync(() =>
                {
                    bootstrapA.BindAsync(LocalAddress.Any);
                    return i;
                }));
            }

            for (int i = 0; i < bindFutures.Count; i++)
            {
                Task<int> result = bindFutures[i];
                await result;
            }
        }

        [Fact]
        public async Task TestConnectDeadLock()
        {
            Bootstrap bootstrapA = new Bootstrap();
            bootstrapA.Group(_groupA);
            bootstrapA.Channel<LocalChannel>();
            bootstrapA.Handler(_dummyHandler);

            Bootstrap bootstrapB = new Bootstrap();
            bootstrapB.Group(_groupB);
            bootstrapB.Channel<LocalChannel>();
            bootstrapB.Handler(_dummyHandler);

            var bindFutures = new List<Task<int>>();

            // Try to bind from each other.
            for (int i = 0; i < 1024; i++)
            {
                bindFutures.Add(_groupA.GetNext().SubmitAsync(() =>
                {
                    bootstrapB.ConnectAsync(LocalAddress.Any);
                    return i;
                }));
                bindFutures.Add(_groupB.GetNext().SubmitAsync(() =>
                {
                    bootstrapA.ConnectAsync(LocalAddress.Any);
                    return i;
                }));
            }

            for (int i = 0; i < bindFutures.Count; i++)
            {
                Task<int> result = bindFutures[i];
                await result;
            }
        }

        [Fact]
        public void TestAsyncResolutionSuccess()
        {
            Bootstrap bootstrapA = new Bootstrap();
            bootstrapA.Group(_groupA);
            bootstrapA.Channel<LocalChannel>();
            bootstrapA.Resolver(new TestAddressResolverGroup(true));
            bootstrapA.Handler(_dummyHandler);

            ServerBootstrap bootstrapB = new ServerBootstrap();
            bootstrapB.Group(_groupB);
            bootstrapB.Channel<LocalServerChannel>();
            bootstrapB.ChildHandler(_dummyHandler);
            var localAddress = bootstrapB.BindAsync(LocalAddress.Any).GetAwaiter().GetResult().LocalAddress;

            // Connect to the server using the asynchronous resolver.
            bootstrapA.ConnectAsync(localAddress).GetAwaiter().GetResult();
        }

        [Fact]
        public void TestAsyncResolutionFailure()
        {
            Bootstrap bootstrapA = new Bootstrap();
            bootstrapA.Group(_groupA);
            bootstrapA.Channel<LocalChannel>();
            bootstrapA.Resolver(new TestAddressResolverGroup(false));
            bootstrapA.Handler(_dummyHandler);

            ServerBootstrap bootstrapB = new ServerBootstrap();
            bootstrapB.Group(_groupB);
            bootstrapB.Channel<LocalServerChannel>();
            bootstrapB.ChildHandler(_dummyHandler);
            var localAddress = bootstrapB.BindAsync(LocalAddress.Any).GetAwaiter().GetResult().LocalAddress;

            // Connect to the server using the asynchronous resolver.
            var connectFuture = bootstrapA.ConnectAsync(localAddress);
            
            // Should fail with the UnknownHostException.
            Assert.True(TaskUtil.WaitAsync(connectFuture, TimeSpan.FromSeconds(10)).GetAwaiter().GetResult());
            Assert.False(connectFuture.IsSuccess());
            Assert.IsType<System.Net.Sockets.SocketException>(connectFuture.Exception.InnerException);
            //Assert.False(connectFuture.Channel().isOpen());
        }

        sealed class DummyHandler : ChannelHandlerAdapter
        {
            public override bool IsSharable => true;
        }

        sealed class TestAddressResolverGroup : INameResolver
        {
            public bool _success;

            public TestAddressResolverGroup(bool success) => _success = success;

            public bool IsResolved(EndPoint address)
            {
                return false;
            }

            public Task<EndPoint> ResolveAsync(EndPoint address)
            {
                if (_success)
                {
                    return Task.FromResult(address);
                }
                else
                {
                    return Task.FromException<EndPoint>(new System.Net.Sockets.SocketException());
                }
            }
        }
    }
}