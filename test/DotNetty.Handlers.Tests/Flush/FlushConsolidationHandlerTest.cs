namespace DotNetty.Handlers.Tests.Flush
{
    using System;
    using DotNetty.Common.Utilities;
    using DotNetty.Handlers.Flush;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class FlowControlHandlerTest
    {
        private const int EXPLICIT_FLUSH_AFTER_FLUSHES = 3;

        [Fact]
        public void TestFlushViaScheduledTask()
        {
            AtomicInteger flushCount = new AtomicInteger();
            EmbeddedChannel channel = NewChannel(flushCount, true);
            // Flushes should not go through immediately, as they're scheduled as an async task
            channel.Flush();
            Assert.Equal(0, flushCount.Value);
            channel.Flush();
            Assert.Equal(0, flushCount.Value);
            // Trigger the execution of the async task
            channel.RunPendingTasks();
            Assert.Equal(1, flushCount.Value);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void TestFlushViaThresholdOutsideOfReadLoop()
        {
            AtomicInteger flushCount = new AtomicInteger();
            EmbeddedChannel channel = NewChannel(flushCount, true);
            // After a given threshold, the async task should be bypassed and a flush should be triggered immediately
            for (int i = 0; i < EXPLICIT_FLUSH_AFTER_FLUSHES; i++)
            {
                channel.Flush();
            }
            Assert.Equal(1, flushCount.Value);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void TestImmediateFlushOutsideOfReadLoop()
        {
            AtomicInteger flushCount = new AtomicInteger();
            EmbeddedChannel channel = NewChannel(flushCount, false);
            channel.Flush();
            Assert.Equal(1, flushCount.Value);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void TestFlushViaReadComplete()
        {
            AtomicInteger flushCount = new AtomicInteger();
            EmbeddedChannel channel = NewChannel(flushCount, false);
            // Flush should go through as there is no read loop in progress.
            channel.Flush();
            channel.RunPendingTasks();
            Assert.Equal(1, flushCount.Value);

            // Simulate read loop;
            channel.Pipeline.FireChannelRead(1L);
            Assert.Equal(1, flushCount.Value);
            channel.Pipeline.FireChannelRead(2L);
            Assert.Equal(1, flushCount.Value);
            Assert.Null(channel.ReadOutbound());
            channel.Pipeline.FireChannelReadComplete();
            Assert.Equal(2, flushCount.Value);
            // Now flush again as the read loop is complete.
            channel.Flush();
            channel.RunPendingTasks();
            Assert.Equal(3, flushCount.Value);
            Assert.Equal(1L, channel.ReadOutbound());
            Assert.Equal(2L, channel.ReadOutbound());
            Assert.Null(channel.ReadOutbound());
            Assert.False(channel.Finish());
        }

        [Fact]
        public void TestFlushViaClose()
        {
            AtomicInteger flushCount = new AtomicInteger();
            EmbeddedChannel channel = NewChannel(flushCount, false);
            // Simulate read loop;
            channel.Pipeline.FireChannelRead(1L);
            Assert.Equal(0, flushCount.Value);
            Assert.Null(channel.ReadOutbound());
            channel.CloseAsync();
            Assert.Equal(1, flushCount.Value);
            Assert.Equal(1L, channel.ReadOutbound());
            Assert.Null(channel.ReadOutbound());
            Assert.False(channel.Finish());
        }

        [Fact]
        public void TestFlushViaDisconnect()
        {
            AtomicInteger flushCount = new AtomicInteger();
            EmbeddedChannel channel = NewChannel(flushCount, false);
            // Simulate read loop;
            channel.Pipeline.FireChannelRead(1L);
            Assert.Equal(0, flushCount.Value);
            Assert.Null(channel.ReadOutbound());
            channel.DisconnectAsync();
            Assert.Equal(1, flushCount.Value);
            Assert.Equal(1L, channel.ReadOutbound());
            Assert.Null(channel.ReadOutbound());
            Assert.False(channel.Finish());
        }

        [Fact]
        public void TestFlushViaException()
        {
            AtomicInteger flushCount = new AtomicInteger();
            EmbeddedChannel channel = NewChannel(flushCount, false);
            // Simulate read loop;
            channel.Pipeline.FireChannelRead(1L);
            Assert.Equal(0, flushCount.Value);
            Assert.Null(channel.ReadOutbound());
            channel.Pipeline.FireExceptionCaught(new InvalidOperationException());
            Assert.Equal(1, flushCount.Value);
            Assert.Equal(1L, channel.ReadOutbound<long>());
            Assert.Null(channel.ReadOutbound());
            Assert.Throws<InvalidOperationException>(() => channel.Finish());
        }

        [Fact]
        public void TestFlushViaRemoval()
        {
            AtomicInteger flushCount = new AtomicInteger();
            EmbeddedChannel channel = NewChannel(flushCount, false);
            // Simulate read loop;
            channel.Pipeline.FireChannelRead(1L);
            Assert.Equal(0, flushCount.Value);
            Assert.Null(channel.ReadOutbound());
            channel.Pipeline.Remove<FlushConsolidationHandler>();
            Assert.Equal(1, flushCount.Value);
            Assert.Equal(1L, channel.ReadOutbound<long>());
            Assert.Null(channel.ReadOutbound());
            Assert.False(channel.Finish());
        }

        // See https://github.com/netty/netty/issues/9923
        [Fact]
        public void TestResend()
        {
            AtomicInteger flushCount = new AtomicInteger();
            EmbeddedChannel channel = NewChannel(flushCount, true);
            channel.WriteAndFlushAsync(1L).ContinueWith(t =>
            {
                channel.WriteAndFlushAsync(1L);
            });
            channel.FlushOutbound();
            Assert.Equal(1L, channel.ReadOutbound<long>());
            Assert.Equal(1L, channel.ReadOutbound<long>());
            Assert.Null(channel.ReadOutbound());
            Assert.False(channel.Finish());
        }

        private static EmbeddedChannel NewChannel(AtomicInteger flushCount, bool consolidateWhenNoReadInProgress)
        {
            return new EmbeddedChannel(
                new ChannelOutboundHandlerAdapter0(flushCount),
                new FlushConsolidationHandler(EXPLICIT_FLUSH_AFTER_FLUSHES, consolidateWhenNoReadInProgress),
                new ChannelInboundHandlerAdapter0()
                );
        }

        sealed class ChannelOutboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            private readonly AtomicInteger _flushCount;

            public ChannelOutboundHandlerAdapter0(AtomicInteger flushCount) => _flushCount = flushCount;

            public override void Flush(IChannelHandlerContext ctx)
            {
                _flushCount.Increment();
                ctx.Flush();
            }
        }

        sealed class ChannelInboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                ctx.WriteAndFlushAsync(msg);
            }
        }
    }
}