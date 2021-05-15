namespace DotNetty.Transport.Tests.Channel
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using Moq;
    using Xunit;

    [Collection("Transport Tests")]
    public class AbstractChannelTest
    {
        [Fact]
        public void EnsureInitialRegistrationFiresActive()
        {
            var eventLoop = new Mock<IEventLoop>();
            // This allows us to have a single-threaded test
            eventLoop.SetupGet(x => x.InEventLoop).Returns(true);

            TestChannel channel = new TestChannel();
            var handler = new Mock<IChannelHandler>();
            channel.Pipeline.AddLast(handler.Object);

            RegisterChannel(eventLoop.Object, channel);

            handler.Verify(
                x => x.HandlerAdded(It.IsAny<IChannelHandlerContext>())
                );
            handler.Verify(
                x => x.ChannelRegistered(It.IsAny<IChannelHandlerContext>())
                );
            handler.Verify(
                x => x.ChannelActive(It.IsAny<IChannelHandlerContext>())
                );
        }

        [Fact]
        public void EnsureSubsequentRegistrationDoesNotFireActive()
        {
            var eventLoop = new Mock<IEventLoop>();
            // This allows us to have a single-threaded test
            eventLoop.SetupGet(x => x.InEventLoop).Returns(true);

            eventLoop
                .Setup(
                    x => x.Execute(It.IsAny<IRunnable>()))
                .Callback((IRunnable task) =>
                {
                    task.Run();
                });
            eventLoop
                .Setup(
                    x => x.Execute(It.IsAny<Action>()))
                .Callback((Action task) =>
                {
                    task();
                });

            TestChannel channel = new TestChannel();
            var handler = new Mock<IChannelHandler>();

            channel.Pipeline.AddLast(handler.Object);

            RegisterChannel(eventLoop.Object, channel);
            channel.Unsafe.Deregister(new DefaultPromise());

            RegisterChannel(eventLoop.Object, channel);

            handler.Verify(
                x => x.HandlerAdded(It.IsAny<IChannelHandlerContext>())
                );

            // Should register twice
            handler.Verify(
                x => x.ChannelRegistered(It.IsAny<IChannelHandlerContext>()),
                Times.Exactly(2));
            handler.Verify(
                x => x.ChannelActive(It.IsAny<IChannelHandlerContext>())
                );
            handler.Verify(
                x => x.ChannelUnregistered(It.IsAny<IChannelHandlerContext>())
                );
        }

        [Fact]
        public void EnsureDefaultChannelId()
        {
            TestChannel channel = new TestChannel();
            IChannelId channelId = channel.Id;
            Assert.True(channelId is DefaultChannelId);
        }

        [Fact]
        public async Task TestClosedChannelExceptionCarryIOException()
        {
            IOException ioException = new IOException();
            IChannel channel = new TestChannel0(ioException);

            var loopGroup = new DefaultEventLoopGroup(1);
            IEventLoop loop = loopGroup.GetNext();
            try
            {
                RegisterChannel(loop, channel);
                await channel.ConnectAsync(new IPEndPoint(IPAddress.IPv6Any, 8888));
                try
                {
                    await channel.WriteAndFlushAsync("");
                }
                catch (Exception exc)
                {
                    Assert.Same(ioException, exc);
                }

                AssertClosedChannelException(channel.WriteAndFlushAsync(""), ioException);
                AssertClosedChannelException(channel.WriteAsync(""), ioException);
                AssertClosedChannelException(channel.BindAsync(new IPEndPoint(IPAddress.IPv6Any, 8888)), ioException);
            }
            finally
            {
                channel.CloseAsync().Ignore();
                await loopGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5));
            }
        }

        class TestChannel0 : TestChannel<TestChannel0, TestChannel0.TestUnsafe>
        {
            private readonly IOException _ioException;
            private bool _open = true;
            private bool _active;

            public TestChannel0(IOException ioException)
            {
                _ioException = ioException;
            }

            protected override void DoClose()
            {
                _active = false;
                _open = false;
            }

            protected override void DoWrite(ChannelOutboundBuffer input)
            {
                throw _ioException;
            }

            public override bool IsOpen => _open;

            public override bool IsActive => _active;

            public class TestUnsafe : TestUnsafe0
            {
                public override Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
                {
                    _channel._active = true;
                    return TaskUtil.Completed;
                }
            }
        }

        private static void AssertClosedChannelException(Task future, IOException expected)
        {
            try
            {
                future.GetAwaiter().GetResult();
            }
            catch (Exception cause)
            {
                Assert.True(cause is ClosedChannelException);
                if (cause.InnerException is object)
                {
                    Assert.Same(expected, cause.InnerException);
                }
            }
        }

        private static void RegisterChannel(IEventLoop eventLoop, IChannel channel)
        {
            var future = channel.Unsafe.RegisterAsync(eventLoop);
            future.GetAwaiter().GetResult(); // Cause any exceptions to be thrown
        }

        class TestChannel : TestChannel<TestChannel, TestChannel.TestUnsafe>
        {
            public class TestUnsafe : TestUnsafe0
            {

            }
        }

        abstract class TestChannel<TChannel, TUnsafe> : AbstractChannel<TChannel, TUnsafe>
            where TChannel : TestChannel<TChannel, TUnsafe>
            where TUnsafe : TestChannel<TChannel, TUnsafe>.TestUnsafe0, new()
        {
            private static readonly ChannelMetadata TEST_METADATA = new ChannelMetadata(false);

            private readonly IChannelConfiguration _config;

            public TestChannel()
                : base(null)
            {
                _config = new DefaultChannelConfiguration(this);
            }

            public override IChannelConfiguration Configuration => _config;

            public override bool IsOpen => true;

            public override bool IsActive => true;

            public override ChannelMetadata Metadata => TEST_METADATA;

            protected override EndPoint LocalAddressInternal => null;

            protected override EndPoint RemoteAddressInternal => null;

            protected override void DoBeginRead()
            {
            }

            protected override void DoBind(EndPoint localAddress)
            {
            }

            protected override void DoClose()
            {
            }

            protected override void DoDisconnect()
            {
            }

            protected override void DoWrite(ChannelOutboundBuffer input)
            {
            }

            protected override bool IsCompatible(IEventLoop eventLoop) => true;

            public class TestUnsafe0 : AbstractUnsafe
            {
                public override Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
                {
                    return TaskUtil.FromException(new NotSupportedException());
                }
            }
        }
    }
}