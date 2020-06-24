namespace DotNetty.Transport.Tests.Channel.Local
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Linq;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using DotNetty.Transport.Channels.Local;
    using Xunit;
    using Thread = DotNetty.Common.Concurrency.XThread;

    public class LocalTransportThreadModelTest : IDisposable
    {
        enum EventType
        {
            EXCEPTION_CAUGHT,
            USER_EVENT,
            MESSAGE_RECEIVED_LAST,
            INACTIVE,
            ACTIVE,
            UNREGISTERED,
            REGISTERED,
            MESSAGE_RECEIVED,
            WRITE,
            READ
        }

        sealed class ChannelInboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                // Discard
                ReferenceCountUtil.Release(message);
            }
        }

        private readonly IEventLoopGroup _group;
        private readonly LocalAddress _localAddr;

        public LocalTransportThreadModelTest()
        {
            // Configure a test server
            _group = new MultithreadEventLoopGroup(1);
            ServerBootstrap sb = new ServerBootstrap();
            sb.Group(_group)
                .Channel<LocalServerChannel>()
                .ChildHandler(new ActionChannelInitializer<LocalChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new ChannelInboundHandlerAdapter0());
                }));
            _localAddr = (LocalAddress)sb.BindAsync(LocalAddress.Any).GetAwaiter().GetResult().LocalAddress;
        }

        public void Dispose()
        {
            _group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
        }

        //[Fact]
        //public async Task TestStagedExecution()
        //{
        //    IEventLoopGroup l = new MultithreadEventLoopGroup(4/*, new DefaultThreadFactory("l")*/);
        //    IEventExecutorGroup e1 = new MultithreadEventLoopGroup(4/*, new DefaultThreadFactory("e1")*/);
        //    IEventExecutorGroup e2 = new MultithreadEventLoopGroup(4/*, new DefaultThreadFactory("e2")*/);
        //    ThreadNameAuditor h1 = new ThreadNameAuditor();
        //    ThreadNameAuditor h2 = new ThreadNameAuditor();
        //    ThreadNameAuditor h3 = new ThreadNameAuditor(true);

        //    IChannel ch = new LocalChannel();
        //    // With no EventExecutor specified, h1 will be always invoked by EventLoop 'l'.
        //    ch.Pipeline.AddLast(h1);
        //    // h2 will be always invoked by EventExecutor 'e1'.
        //    ch.Pipeline.AddLast(e1, h2);
        //    // h3 will be always invoked by EventExecutor 'e2'.
        //    ch.Pipeline.AddLast(e2, h3);

        //    await l.RegisterAsync(ch);
        //    await ch.ConnectAsync(_localAddr);

        //    // Fire inbound events from all possible starting points.
        //    ch.Pipeline.FireChannelRead("1");
        //    ch.Pipeline.Context(h1).FireChannelRead("2");
        //    ch.Pipeline.Context(h2).FireChannelRead("3");
        //    ch.Pipeline.Context(h3).FireChannelRead("4");
        //    // Fire outbound events from all possible starting points.
        //    ch.Pipeline.WriteAsync("5").Ignore();
        //    ch.Pipeline.Context(h3).WriteAsync("6").Ignore();
        //    ch.Pipeline.Context(h2).WriteAsync("7").Ignore();
        //    await ch.Pipeline.Context(h1).WriteAndFlushAsync("8");

        //    await ch.CloseAsync();

        //    // Wait until all events are handled completely.
        //    while (h1._outboundThreadNames.Count < 3 || h3._inboundThreadNames.Count < 3 ||
        //           h1._removalThreadNames.Count < 1)
        //    {
        //        if (h1._exception.Value != null)
        //        {
        //            throw h1._exception.Value;
        //        }
        //        if (h2._exception.Value != null)
        //        {
        //            throw h2._exception.Value;
        //        }
        //        if (h3._exception.Value != null)
        //        {
        //            throw h3._exception.Value;
        //        }

        //        Thread.Sleep(10);
        //    }

        //    String currentName = Thread.CurrentThread.Name;

        //    try
        //    {
        //        // Events should never be handled from the current thread.
        //        Assert.DoesNotContain(currentName, h1._inboundThreadNames);
        //        Assert.DoesNotContain(currentName, h2._inboundThreadNames);
        //        Assert.DoesNotContain(currentName, h3._inboundThreadNames);
        //        Assert.DoesNotContain(currentName, h1._outboundThreadNames);
        //        Assert.DoesNotContain(currentName, h2._outboundThreadNames);
        //        Assert.DoesNotContain(currentName, h3._outboundThreadNames);
        //        Assert.DoesNotContain(currentName, h1._removalThreadNames);
        //        Assert.DoesNotContain(currentName, h2._removalThreadNames);
        //        Assert.DoesNotContain(currentName, h3._removalThreadNames);

        //        // Assert that events were handled by the correct executor.
        //        foreach (string name in h1._inboundThreadNames)
        //        {
        //            Assert.True(name.startsWith("l-"));
        //        }
        //        foreach (string name in h2._inboundThreadNames)
        //        {
        //            Assert.True(name.startsWith("e1-"));
        //        }
        //        foreach (string name in h3._inboundThreadNames)
        //        {
        //            Assert.True(name.startsWith("e2-"));
        //        }
        //        foreach (string name in h1._outboundThreadNames)
        //        {
        //            Assert.True(name.startsWith("l-"));
        //        }
        //        foreach (string name in h2._outboundThreadNames)
        //        {
        //            Assert.True(name.startsWith("e1-"));
        //        }
        //        foreach (string name in h3._outboundThreadNames)
        //        {
        //            Assert.True(name.startsWith("e2-"));
        //        }
        //        foreach (string name in h1._removalThreadNames)
        //        {
        //            Assert.True(name.startsWith("l-"));
        //        }
        //        foreach (string name in h2._removalThreadNames)
        //        {
        //            Assert.True(name.startsWith("e1-"));
        //        }
        //        foreach (string name in h3._removalThreadNames)
        //        {
        //            Assert.True(name.startsWith("e2-"));
        //        }

        //        // Assert that the events for the same handler were handled by the same thread.
        //        HashSet<string> names = new HashSet<string>();
        //        names.addAll(h1._inboundThreadNames);
        //        names.addAll(h1._outboundThreadNames);
        //        names.addAll(h1._removalThreadNames);
        //        Assert.assertEquals(1, names.size());

        //        names.clear();
        //        names.addAll(h2.inboundThreadNames);
        //        names.addAll(h2.outboundThreadNames);
        //        names.addAll(h2.removalThreadNames);
        //        Assert.assertEquals(1, names.size());

        //        names.clear();
        //        names.addAll(h3.inboundThreadNames);
        //        names.addAll(h3.outboundThreadNames);
        //        names.addAll(h3.removalThreadNames);
        //        Assert.assertEquals(1, names.size());

        //        // Count the number of events
        //        Assert.assertEquals(1, h1.inboundThreadNames.size());
        //        Assert.assertEquals(2, h2.inboundThreadNames.size());
        //        Assert.assertEquals(3, h3.inboundThreadNames.size());
        //        Assert.assertEquals(3, h1.outboundThreadNames.size());
        //        Assert.assertEquals(2, h2.outboundThreadNames.size());
        //        Assert.assertEquals(1, h3.outboundThreadNames.size());
        //        Assert.assertEquals(1, h1.removalThreadNames.size());
        //        Assert.assertEquals(1, h2.removalThreadNames.size());
        //        Assert.assertEquals(1, h3.removalThreadNames.size());
        //    }
        //    catch (AssertionError e)
        //    {
        //        System.out.println("H1I: " + h1.inboundThreadNames);
        //        System.out.println("H2I: " + h2.inboundThreadNames);
        //        System.out.println("H3I: " + h3.inboundThreadNames);
        //        System.out.println("H1O: " + h1.outboundThreadNames);
        //        System.out.println("H2O: " + h2.outboundThreadNames);
        //        System.out.println("H3O: " + h3.outboundThreadNames);
        //        System.out.println("H1R: " + h1.removalThreadNames);
        //        System.out.println("H2R: " + h2.removalThreadNames);
        //        System.out.println("H3R: " + h3.removalThreadNames);
        //        throw e;
        //    }
        //    finally
        //    {
        //        l.shutdownGracefully();
        //        e1.shutdownGracefully();
        //        e2.shutdownGracefully();

        //        l.terminationFuture().sync();
        //        e1.terminationFuture().sync();
        //        e2.terminationFuture().sync();
        //    }

        //}

        sealed class ThreadNameAuditor : ChannelDuplexHandler
        {
            internal readonly AtomicReference<Exception> _exception = new AtomicReference<Exception>();

            internal readonly ConcurrentQueue<string> _inboundThreadNames = new ConcurrentQueue<string>();
            internal readonly ConcurrentQueue<string> _outboundThreadNames = new ConcurrentQueue<string>();
            internal readonly ConcurrentQueue<string> _removalThreadNames = new ConcurrentQueue<string>();
            internal readonly bool _discard;

            public ThreadNameAuditor()
                : this(false)
            {
            }

            public ThreadNameAuditor(bool discard)
            {
                _discard = discard;
            }

            public override void HandlerRemoved(IChannelHandlerContext context)
            {
                _removalThreadNames.Enqueue(Thread.CurrentThread.Name);
            }

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                _inboundThreadNames.Enqueue(Thread.CurrentThread.Name);
                if (!_discard)
                {
                    context.FireChannelRead(message);
                }
            }

            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                _outboundThreadNames.Enqueue(Thread.CurrentThread.Name);
                context.WriteAsync(message, promise);
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                if (_exception.Value is null)
                {
                    _exception.Value = exception;
                }
                //System.err.print("[" + Thread.currentThread().getName() + "] ");
                //cause.printStackTrace();
                base.ExceptionCaught(context, exception);
            }
        }

        /// <summary>
        /// Converts integers into a binary stream.
        /// </summary>
        sealed class MessageForwarder1 : ChannelDuplexHandler
        {
            private readonly AtomicReference<Exception> _exception = new AtomicReference<Exception>();
            private volatile int _inCnt;
            private volatile int _outCnt;
            private volatile Thread _thread;

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                var t = _thread;
                if (t is null)
                {
                    _thread = Thread.CurrentThread;
                }
                else
                {
                    Assert.Same(t, Thread.CurrentThread);
                }

                IByteBuffer output = context.Allocator.Buffer(4);
                int m = ((int)message);
                int expected = _inCnt++;
                Assert.Equal(expected, m);
                output.WriteInt(m);

                context.FireChannelRead(output);
            }

            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                Assert.Same(_thread, Thread.CurrentThread);

                // Don't let the write request go to the server-side channel - just swallow.
                bool swallow = this == context.Pipeline.First();

                IByteBuffer m = (IByteBuffer)message;
                int count = m.ReadableBytes / 4;
                for (int j = 0; j < count; j++)
                {
                    int actual = m.ReadInt();
                    int expected = _outCnt++;
                    Assert.Equal(expected, actual);
                    if (!swallow)
                    {
                        context.WriteAsync(actual);
                    }
                }
                context.WriteAndFlushAsync(Unpooled.Empty, promise);
                m.Release();
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                if (_exception.Value is null)
                {
                    _exception.Value = exception;
                }
                //System.err.print("[" + Thread.currentThread().getName() + "] ");
                //cause.printStackTrace();
                base.ExceptionCaught(context, exception);
            }
        }

        /// <summary>
        /// Converts a binary stream into integers.
        /// </summary>
        sealed class MessageForwarder2 : ChannelDuplexHandler
        {
            private readonly AtomicReference<Exception> _exception = new AtomicReference<Exception>();
            private volatile int _inCnt;
            private volatile int _outCnt;
            private volatile Thread _thread;

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                var t = _thread;
                if (t is null)
                {
                    _thread = Thread.CurrentThread;
                }
                else
                {
                    Assert.Same(t, Thread.CurrentThread);
                }

                IByteBuffer m = (IByteBuffer)message;
                int count = m.ReadableBytes / 4;
                for (int j = 0; j < count; j++)
                {
                    int actual = m.ReadInt();
                    int expected = _inCnt++;
                    Assert.Equal(expected, actual);
                    context.FireChannelRead(actual);
                }
                m.Release();
            }

            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                Assert.Same(_thread, Thread.CurrentThread);

                IByteBuffer output = context.Allocator.Buffer(4);
                int m = (int)message;
                int expected = _outCnt++;
                Assert.Equal(expected, m);
                output.WriteInt(m);

                context.WriteAsync(output, promise);
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                if (_exception.Value is null)
                {
                    _exception.Value = exception;
                }
                //System.err.print("[" + Thread.currentThread().getName() + "] ");
                //cause.printStackTrace();
                base.ExceptionCaught(context, exception);
            }
        }

        /// <summary>
        /// Simply forwards the received object to the next handler.
        /// </summary>
        sealed class MessageForwarder3 : ChannelDuplexHandler
        {
            private readonly AtomicReference<Exception> _exception = new AtomicReference<Exception>();
            private volatile int _inCnt;
            private volatile int _outCnt;
            private volatile Thread _thread;

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                var t = _thread;
                if (t is null)
                {
                    _thread = Thread.CurrentThread;
                }
                else
                {
                    Assert.Same(t, Thread.CurrentThread);
                }

                int actual = (int)message;
                int expected = _inCnt++;
                Assert.Equal(expected, actual);

                context.FireChannelRead(message);
            }

            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                Assert.Same(_thread, Thread.CurrentThread);

                int actual = (int)message;
                int expected = _outCnt++;
                Assert.Equal(expected, actual);

                context.WriteAsync(message, promise);
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                if (_exception.Value is null)
                {
                    _exception.Value = exception;
                }
                //System.err.print('[' + Thread.currentThread().getName() + "] ");
                //cause.printStackTrace();
                base.ExceptionCaught(context, exception);
            }
        }

        /// <summary>
        /// Discards all received messages.
        /// </summary>
        sealed class MessageDiscarder : ChannelDuplexHandler
        {
            private readonly AtomicReference<Exception> _exception = new AtomicReference<Exception>();
            private volatile int _inCnt;
            private volatile int _outCnt;
            private volatile Thread _thread;

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                var t = _thread;
                if (t is null)
                {
                    _thread = Thread.CurrentThread;
                }
                else
                {
                    Assert.Same(t, Thread.CurrentThread);
                }
                int actual = (int)message;
                int expected = _inCnt++;
                Assert.Equal(expected, actual);
            }

            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                Assert.Same(_thread, Thread.CurrentThread);

                int actual = (int)message;
                int expected = _outCnt++;
                Assert.Equal(expected, actual);
                context.WriteAsync(message, promise);
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                if (_exception.Value is null)
                {
                    _exception.Value = exception;
                }
                base.ExceptionCaught(context, exception);
            }
        }
    }
}