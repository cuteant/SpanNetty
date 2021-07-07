namespace DotNetty.Suite.Tests.Transport.Socket
{
    using System;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit;
    using Xunit.Abstractions;

    public abstract class AbstractSocketShutdownOutputByPeerTest : AbstractServerSocketTest
    {
        public AbstractSocketShutdownOutputByPeerTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public void TestShutdownOutput(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            Configure(sb, allocator);

            TestHandler h = new TestHandler();
            Socket s = NewSocket();
            IChannel sc = null;
            try
            {
                sc = sb.ChildHandler(h).ChildOption(ChannelOption.AllowHalfClosure, true).BindAsync().GetAwaiter().GetResult();

                Connect(s, sc.LocalAddress);
                Write(s, 1);

                Assert.Equal(1, (int)h._queue.Take());

                Assert.True(h._ch.IsOpen);
                Assert.True(h._ch.IsActive);
                Assert.False(h._ch.IsInputShutdown);
                Assert.False(h._ch.IsOutputShutdown);

                ShutdownOutput(s);

                h._halfClosure.Wait();

                Assert.True(h._ch.IsOpen);
                Assert.True(h._ch.IsActive);
                Assert.True(h._ch.IsInputShutdown);
                Assert.False(h._ch.IsOutputShutdown);

                while (h._closure.CurrentCount != 1 && h._halfClosureCount.Value != 1)
                {
                    Thread.Sleep(100);
                }
            }
            finally
            {
                if (sc != null)
                {
                    sc.CloseAsync();
                }
                Close(s);
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public void TestShutdownOutputWithoutOption(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            Configure(sb, allocator);

            TestHandler h = new TestHandler();
            Socket s = NewSocket();
            IChannel sc = null;
            try
            {
                sc = sb.ChildHandler(h).BindAsync().GetAwaiter().GetResult();

                Connect(s, sc.LocalAddress);
                Write(s, 1);

                Assert.Equal(1, (int)h._queue.Take());

                Assert.True(h._ch.IsOpen);
                Assert.True(h._ch.IsActive);
                Assert.False(h._ch.IsInputShutdown);
                Assert.False(h._ch.IsOutputShutdown);

                ShutdownOutput(s);

                h._closure.Wait();

                Assert.False(h._ch.IsOpen);
                Assert.False(h._ch.IsActive);
                Assert.True(h._ch.IsInputShutdown);
                Assert.True(h._ch.IsOutputShutdown);

                while (h._halfClosure.CurrentCount != 1 && h._halfClosureCount.Value != 0)
                {
                    Thread.Sleep(100);
                }
            }
            finally
            {
                if (sc != null)
                {
                    sc.CloseAsync();
                }
                Close(s);
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        protected abstract void ShutdownOutput(Socket s);

        protected abstract void Connect(Socket s, EndPoint address);

        protected abstract void Close(Socket s);

        protected abstract void Write(Socket s, int data);

        protected abstract Socket NewSocket();

        sealed class TestHandler : SimpleChannelInboundHandler<IByteBuffer>
        {
            internal volatile IDuplexChannel _ch;
            internal readonly BlockingCollection<byte> _queue = new BlockingCollection<byte>();
            internal readonly CountdownEvent _halfClosure = new CountdownEvent(1);
            internal readonly CountdownEvent _closure = new CountdownEvent(1);
            internal readonly AtomicInteger _halfClosureCount = new AtomicInteger();

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                _ch = (IDuplexChannel)ctx.Channel;
            }

            public override void ChannelInactive(IChannelHandlerContext ctx)
            {
                _closure.Signal();
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, IByteBuffer msg)
            {
                _queue.Add(msg.ReadByte());
            }

            public override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
            {
                if (evt is ChannelInputShutdownEvent)
                {
                    _halfClosureCount.Increment();
                    _halfClosure.Signal();
                }
            }
        }
    }
}
