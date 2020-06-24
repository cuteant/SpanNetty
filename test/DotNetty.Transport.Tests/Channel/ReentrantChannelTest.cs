namespace DotNetty.Transport.Tests.Channel
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Local;
    using Xunit;

    public class ReentrantChannelTest : BaseChannelTest
    {
        [Fact]
        public async Task TestWritabilityChanged()
        {

            LocalAddress addr = new LocalAddress("testWritabilityChanged");

            ServerBootstrap sb = GetLocalServerBootstrap();
            await sb.BindAsync(addr);

            Bootstrap cb = GetLocalClientBootstrap();

            SetInterest(LoggingHandler.Event.WRITE, LoggingHandler.Event.FLUSH, LoggingHandler.Event.WRITABILITY);

            var clientChannel = await cb.ConnectAsync(addr);
            clientChannel.Configuration.WriteBufferLowWaterMark = 512;
            clientChannel.Configuration.WriteBufferHighWaterMark = 1024;

            // What is supposed to happen from this point:
            //
            // 1. Because this write attempt has been made from a non-I/O thread,
            //    ChannelOutboundBuffer.pendingWriteBytes will be increased before
            //    write() event is really evaluated.
            //    -> channelWritabilityChanged() will be triggered,
            //       because the Channel became unwritable.
            //
            // 2. The write() event is handled by the pipeline in an I/O thread.
            //    -> write() will be triggered.
            //
            // 3. Once the write() event is handled, ChannelOutboundBuffer.pendingWriteBytes
            //    will be decreased.
            //    -> channelWritabilityChanged() will be triggered,
            //       because the Channel became writable again.
            //
            // 4. The message is added to the ChannelOutboundBuffer and thus
            //    pendingWriteBytes will be increased again.
            //    -> channelWritabilityChanged() will be triggered.
            //
            // 5. The flush() event causes the write request in theChannelOutboundBuffer
            //    to be removed.
            //    -> flush() and channelWritabilityChanged() will be triggered.
            //
            // Note that the channelWritabilityChanged() in the step 4 can occur between
            // the flush() and the channelWritabilityChanged() in the step 5, because
            // the flush() is invoked from a non-I/O thread while the other are from
            // an I/O thread.

            var future = clientChannel.WriteAsync(CreateTestBuf(2000));

            clientChannel.Flush();
            await future;

            await clientChannel.CloseAsync();

            AssertLog(
                    // Case 1:
                    "WRITABILITY: writable=False\n" +
                    "WRITE\n" +
                    "WRITABILITY: writable=False\n" +
                    "WRITABILITY: writable=False\n" +
                    "FLUSH\n" +
                    "WRITABILITY: writable=True\n",
                    // Case 2:
                    "WRITABILITY: writable=False\n" +
                    "WRITE\n" +
                    "WRITABILITY: writable=False\n" +
                    "FLUSH\n" +
                    "WRITABILITY: writable=True\n" +
                    "WRITABILITY: writable=True\n");
        }

        /**
         * Similar to {@link #testWritabilityChanged()} with slight variation.
         */
        [Fact]
        public async Task TestFlushInWritabilityChanged()
        {
            LocalAddress addr = new LocalAddress("testFlushInWritabilityChanged");

            ServerBootstrap sb = GetLocalServerBootstrap();
            await sb.BindAsync(addr);

            Bootstrap cb = GetLocalClientBootstrap();

            SetInterest(LoggingHandler.Event.WRITE, LoggingHandler.Event.FLUSH, LoggingHandler.Event.WRITABILITY);

            var clientChannel = await cb.ConnectAsync(addr);
            clientChannel.Configuration.WriteBufferLowWaterMark = 512;
            clientChannel.Configuration.WriteBufferHighWaterMark = 1024;

            clientChannel.Pipeline.AddLast(new ChannelInboundHandlerAdapter0());

            Assert.True(clientChannel.IsWritable);

            await clientChannel.WriteAsync(CreateTestBuf(2000));
            await clientChannel.CloseAsync();

            AssertLog(
                    // Case 1:
                    "WRITABILITY: writable=False\n" +
                    "FLUSH\n" +
                    "WRITE\n" +
                    "WRITABILITY: writable=False\n" +
                    "WRITABILITY: writable=False\n" +
                    "FLUSH\n" +
                    "WRITABILITY: writable=True\n",
                    // Case 2:
                    "WRITABILITY: writable=False\n" +
                    "FLUSH\n" +
                    "WRITE\n" +
                    "WRITABILITY: writable=False\n" +
                    "FLUSH\n" +
                    "WRITABILITY: writable=True\n" +
                    "WRITABILITY: writable=True\n");
        }

        class ChannelInboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            public override void ChannelWritabilityChanged(IChannelHandlerContext ctx)
            {
                if (!ctx.Channel.IsWritable)
                {
                    ctx.Channel.Flush();
                }
                ctx.FireChannelWritabilityChanged();
            }
        }

        [Fact]
        public async Task TestWriteFlushPingPong()
        {
            LocalAddress addr = new LocalAddress("testWriteFlushPingPong");

            ServerBootstrap sb = GetLocalServerBootstrap();
            await sb.BindAsync(addr);

            Bootstrap cb = GetLocalClientBootstrap();

            SetInterest(LoggingHandler.Event.WRITE, LoggingHandler.Event.FLUSH, LoggingHandler.Event.CLOSE, LoggingHandler.Event.EXCEPTION);

            var clientChannel = await cb.ConnectAsync(addr);

            clientChannel.Pipeline.AddLast(new ChannelOutboundHandlerAdapter0());

            clientChannel.WriteAndFlushAsync(CreateTestBuf(2000)).Ignore();
            await clientChannel.CloseAsync();

            AssertLog(
                    "WRITE\n" +
                    "FLUSH\n" +
                    "WRITE\n" +
                    "FLUSH\n" +
                    "WRITE\n" +
                    "FLUSH\n" +
                    "WRITE\n" +
                    "FLUSH\n" +
                    "WRITE\n" +
                    "FLUSH\n" +
                    "WRITE\n" +
                    "FLUSH\n" +
                    "CLOSE\n");
        }

        class ChannelOutboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            private int _writeCount;
            private int _flushCount;

            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                if (_writeCount < 5)
                {
                    _writeCount++;
                    context.Channel.Flush();
                }
                base.Write(context, message, promise);
            }

            public override void Flush(IChannelHandlerContext context)
            {
                if (_flushCount < 5)
                {
                    _flushCount++;
                    context.Channel.WriteAsync(CreateTestBuf(2000));
                }
                base.Flush(context);
            }
        }

        [Fact]
        public async Task TestCloseInFlush()
        {
            LocalAddress addr = new LocalAddress("testCloseInFlush");

            ServerBootstrap sb = GetLocalServerBootstrap();
            await sb.BindAsync(addr);

            Bootstrap cb = GetLocalClientBootstrap();

            SetInterest(LoggingHandler.Event.WRITE, LoggingHandler.Event.FLUSH, LoggingHandler.Event.CLOSE, LoggingHandler.Event.EXCEPTION);

            var clientChannel = await cb.ConnectAsync(addr);

            clientChannel.Pipeline.AddLast(new ChannelOutboundHandlerAdapter1());

            var task = clientChannel.WriteAsync(CreateTestBuf(2000));
            await task.WithTimeout(TimeSpan.FromSeconds(5));
            await clientChannel.CloseCompletion;

            AssertLog("WRITE\nFLUSH\nCLOSE\n");
        }

        class ChannelOutboundHandlerAdapter1 : ChannelHandlerAdapter
        {
            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                promise.Task.CloseOnComplete(context.Channel);
                base.Write(context, message, promise);
                context.Channel.Flush();
            }
        }

        [Fact]
        public async Task TestFlushFailure()
        {
            LocalAddress addr = new LocalAddress("testFlushFailure");

            ServerBootstrap sb = GetLocalServerBootstrap();
            await sb.BindAsync(addr);

            Bootstrap cb = GetLocalClientBootstrap();

            SetInterest(LoggingHandler.Event.WRITE, LoggingHandler.Event.FLUSH, LoggingHandler.Event.CLOSE, LoggingHandler.Event.EXCEPTION);

            var clientChannel = await cb.ConnectAsync(addr);

            clientChannel.Pipeline.AddLast(new ChannelOutboundHandlerAdapter2());

            try
            {
                await clientChannel.WriteAndFlushAsync(CreateTestBuf(2000));
                Assert.False(true);
            }
            catch (Exception cce)
            {
                if (cce is AggregateException aggregateException)
                {
                    cce = aggregateException.InnerException;
                }
                // FIXME:  shouldn't this contain the "intentional failure" exception?
                Assert.IsType<ClosedChannelException>(cce);
            }

            await clientChannel.CloseCompletion;

            AssertLog("WRITE\nCLOSE\n");
        }

        class ChannelOutboundHandlerAdapter2 : ChannelHandlerAdapter
        {
            public override void Flush(IChannelHandlerContext context)
            {
                throw new Exception("intentional failure");
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                context.CloseAsync();
            }
        }
    }
}