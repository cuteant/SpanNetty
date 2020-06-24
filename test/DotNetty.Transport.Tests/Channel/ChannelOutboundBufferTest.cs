namespace DotNetty.Transport.Tests.Channel
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class ChannelOutboundBufferTest
    {
        [Fact]
        public void TestEmptyNioBuffers()
        {
            TestChannel channel = new TestChannel();
            ChannelOutboundBuffer buffer = new ChannelOutboundBuffer(channel);
            Assert.Equal(0, buffer.NioBufferCount);
            var buffers = buffer.GetSharedBufferList();
            Assert.NotNull(buffers);
            foreach (var b in buffers)
            {
                Assert.Null(b.Array);
            }
            Assert.Equal(0, buffer.NioBufferCount);
            Release(buffer);
        }

        [Fact]
        public void TestNioBuffersSingleBacked()
        {
            TestChannel channel = new TestChannel();

            ChannelOutboundBuffer buffer = new ChannelOutboundBuffer(channel);
            Assert.Equal(0, buffer.NioBufferCount);

            var buf = Unpooled.CopiedBuffer("buf1", Encoding.ASCII);
            var nioBuf = buf.GetIoBuffer(buf.ReaderIndex, buf.ReadableBytes);
            buffer.AddMessage(buf, buf.ReadableBytes, channel.VoidPromise());
            Assert.Equal(0, buffer.NioBufferCount); // Should still be 0 as not flushed yet
            buffer.AddFlush();
            var buffers = buffer.GetSharedBufferList();
            Assert.NotNull(buffers);
            Assert.Equal(1, buffer.NioBufferCount); // Should still be 0 as not flushed yet
            for (int i = 0; i < buffer.NioBufferCount; i++)
            {
                if (i == 0)
                {
                    Assert.Equal(buffers[i], nioBuf);
                }
                else
                {
                    Assert.Null(buffers[i].Array);
                }
            }
            Release(buffer);
        }

        [Fact]
        public void TestNioBuffersExpand()
        {
            TestChannel channel = new TestChannel();

            ChannelOutboundBuffer buffer = new ChannelOutboundBuffer(channel);

            IByteBuffer buf = Unpooled.DirectBuffer().WriteBytes(Encoding.ASCII.GetBytes("buf1"));
            for (int i = 0; i < 64; i++)
            {
                buffer.AddMessage(buf.Copy(), buf.ReadableBytes, channel.VoidPromise());
            }
            Assert.Equal(0, buffer.NioBufferCount); // Should still be 0 as not flushed yet
            buffer.AddFlush();
            var buffers = buffer.GetSharedBufferList();
            Assert.Equal(64, buffer.NioBufferCount);
            for (int i = 0; i < buffer.NioBufferCount; i++)
            {
                Assert.Equal(buffers[i], buf.GetIoBuffer(buf.ReaderIndex, buf.ReadableBytes));
            }
            Release(buffer);
            buf.Release();
        }

        [Fact]
        public void TestNioBuffersExpand2()
        {
            TestChannel channel = new TestChannel();

            ChannelOutboundBuffer buffer = new ChannelOutboundBuffer(channel);

            CompositeByteBuffer comp = Unpooled.CompositeBuffer(256);
            IByteBuffer buf = Unpooled.DirectBuffer().WriteBytes(Encoding.ASCII.GetBytes("buf1"));
            for (int i = 0; i < 65; i++)
            {
                comp.AddComponent(true, buf.Copy());
            }
            buffer.AddMessage(comp, comp.ReadableBytes, channel.VoidPromise());

            Assert.Equal(0, buffer.NioBufferCount); // Should still be 0 as not flushed yet
            buffer.AddFlush();
            var buffers = buffer.GetSharedBufferList();
            Assert.Equal(65, buffer.NioBufferCount);
            for (int i = 0; i < buffer.NioBufferCount; i++)
            {
                if (i < 65)
                {
                    Assert.Equal(buffers[i], buf.GetIoBuffer(buf.ReaderIndex, buf.ReadableBytes));
                }
                else
                {
                    Assert.Null(buffers[i].Array);
                }
            }
            Release(buffer);
            buf.Release();
        }

        [Fact]
        public void TestNioBuffersMaxCount()
        {
            TestChannel channel = new TestChannel();

            ChannelOutboundBuffer buffer = new ChannelOutboundBuffer(channel);

            CompositeByteBuffer comp = Unpooled.CompositeBuffer(256);
            IByteBuffer buf = Unpooled.DirectBuffer().WriteBytes(Encoding.ASCII.GetBytes("buf1"));
            for (int i = 0; i < 65; i++)
            {
                comp.AddComponent(true, buf.Copy());
            }
            Assert.Equal(65, comp.IoBufferCount);
            buffer.AddMessage(comp, comp.ReadableBytes, channel.VoidPromise());
            Assert.Equal(0, buffer.NioBufferCount); // Should still be 0 as not flushed yet
            buffer.AddFlush();
            int maxCount = 10;    // less than comp.nioBufferCount()
            var buffers = buffer.GetSharedBufferList(maxCount, int.MaxValue);
            Assert.True(buffer.NioBufferCount <= maxCount); // Should not be greater than maxCount
            for (int i = 0; i < buffer.NioBufferCount; i++)
            {
                Assert.Equal(buffers[i], buf.GetIoBuffer(buf.ReaderIndex, buf.ReadableBytes));
            }
            Release(buffer);
            buf.Release();
        }

        private static void Release(ChannelOutboundBuffer buffer)
        {
            for (; ; )
            {
                if (!buffer.Remove())
                {
                    break;
                }
            }
        }

        [Fact]
        public void TestWritability()
        {
            StringBuilder buf = new StringBuilder();
            EmbeddedChannel ch = new EmbeddedChannel(new ChannelInboundHandlerAdapter0(buf));

            ch.Configuration.WriteBufferLowWaterMark = (128 + ChannelOutboundBuffer.ChannelOutboundBufferEntryOverhead);
            ch.Configuration.WriteBufferHighWaterMark = (256 + ChannelOutboundBuffer.ChannelOutboundBufferEntryOverhead);

            ch.WriteAsync(Unpooled.Buffer().WriteZero(128));
            // Ensure exceeding the low watermark does not make channel unwritable.
            ch.WriteAsync(Unpooled.Buffer().WriteZero(2));
            Assert.Equal("", buf.ToString());

            ch.Unsafe.OutboundBuffer.AddFlush();

            // Ensure exceeding the high watermark makes channel unwritable.
            ch.WriteAsync(Unpooled.Buffer().WriteZero(127));
            Assert.Equal("False ", buf.ToString());

            // Ensure going down to the low watermark makes channel writable again by flushing the first write.
            Assert.True(ch.Unsafe.OutboundBuffer.Remove());
            Assert.True(ch.Unsafe.OutboundBuffer.Remove());
            Assert.Equal(ch.Unsafe.OutboundBuffer.TotalPendingWriteBytes,
                 (127L + ChannelOutboundBuffer.ChannelOutboundBufferEntryOverhead));
            Assert.Equal("False True ", buf.ToString());

            SafeClose(ch);
        }

        [Fact]
        public void TestUserDefinedWritability()
        {
            StringBuilder buf = new StringBuilder();
            EmbeddedChannel ch = new EmbeddedChannel(new ChannelInboundHandlerAdapter0(buf));

            ch.Configuration.WriteBufferLowWaterMark = (128 + ChannelOutboundBuffer.ChannelOutboundBufferEntryOverhead);
            ch.Configuration.WriteBufferHighWaterMark = (256 + ChannelOutboundBuffer.ChannelOutboundBufferEntryOverhead);

            ChannelOutboundBuffer cob = ch.Unsafe.OutboundBuffer;

            // Ensure that the default value of a user-defined writability flag is true.
            for (int i = 1; i <= 30; i++)
            {
                Assert.True(cob.GetUserDefinedWritability(i));
            }

            // Ensure that setting a user-defined writability flag to false affects channel.isWritable();
            cob.SetUserDefinedWritability(1, false);
            ch.RunPendingTasks();
            Assert.Equal("False ", buf.ToString());

            // Ensure that setting a user-defined writability flag to true affects channel.isWritable();
            cob.SetUserDefinedWritability(1, true);
            ch.RunPendingTasks();
            Assert.Equal("False True ", buf.ToString());

            SafeClose(ch);
        }

        [Fact]
        public void TestUserDefinedWritability2()
        {
            StringBuilder buf = new StringBuilder();
            EmbeddedChannel ch = new EmbeddedChannel(new ChannelInboundHandlerAdapter0(buf));

            ch.Configuration.WriteBufferLowWaterMark = 128;
            ch.Configuration.WriteBufferHighWaterMark = 256;

            ChannelOutboundBuffer cob = ch.Unsafe.OutboundBuffer;

            // Ensure that setting a user-defined writability flag to false affects channel.isWritable()
            cob.SetUserDefinedWritability(1, false);
            ch.RunPendingTasks();
            Assert.Equal("False ", buf.ToString());

            // Ensure that setting another user-defined writability flag to false does not trigger
            // channelWritabilityChanged.
            cob.SetUserDefinedWritability(2, false);
            ch.RunPendingTasks();
            Assert.Equal("False ", buf.ToString());

            // Ensure that setting only one user-defined writability flag to true does not affect channel.isWritable()
            cob.SetUserDefinedWritability(1, true);
            ch.RunPendingTasks();
            Assert.Equal("False ", buf.ToString());

            // Ensure that setting all user-defined writability flags to true affects channel.isWritable()
            cob.SetUserDefinedWritability(2, true);
            ch.RunPendingTasks();
            Assert.Equal("False True ", buf.ToString());

            SafeClose(ch);
        }

        [Fact]
        public void TestMixedWritability()
        {
            StringBuilder buf = new StringBuilder();
            EmbeddedChannel ch = new EmbeddedChannel(new ChannelInboundHandlerAdapter0(buf));

            ch.Configuration.WriteBufferLowWaterMark = 128;
            ch.Configuration.WriteBufferHighWaterMark = 256;

            ChannelOutboundBuffer cob = ch.Unsafe.OutboundBuffer;

            // Trigger channelWritabilityChanged() by writing a lot.
            ch.WriteAsync(Unpooled.Buffer().WriteZero(257));
            Assert.Equal("False ", buf.ToString());

            // Ensure that setting a user-defined writability flag to false does not trigger channelWritabilityChanged()
            cob.SetUserDefinedWritability(1, false);
            ch.RunPendingTasks();
            Assert.Equal("False ", buf.ToString());

            // Ensure reducing the totalPendingWriteBytes down to zero does not trigger channelWritabilityChanged()
            // because of the user-defined writability flag.
            ch.Flush();
            Assert.Equal(0L, cob.TotalPendingWriteBytes);
            Assert.Equal("False ", buf.ToString());

            // Ensure that setting all user-defined writability flags to true affects channel.isWritable()
            cob.SetUserDefinedWritability(1, true);
            ch.RunPendingTasks();
            Assert.Equal("False True ", buf.ToString());

            SafeClose(ch);
        }

        class ChannelInboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            private readonly StringBuilder _buf;

            public ChannelInboundHandlerAdapter0(StringBuilder buf) => _buf = buf;

            public override void ChannelWritabilityChanged(IChannelHandlerContext context)
            {
                _buf.Append(context.Channel.IsWritable);
                _buf.Append(' ');
            }
        }

        [Fact(Skip = "TODO")]
        public void TestWriteTaskRejected()
        {
        }

        sealed class ChannelOutboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                base.Write(context, message, promise);
            }
        }

        sealed class TestChannel : AbstractChannel<TestChannel, TestChannel.TestUnsafe>
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

            protected override EndPoint LocalAddressInternal => throw new NotSupportedException();

            protected override EndPoint RemoteAddressInternal => throw new NotSupportedException();

            protected override void DoBeginRead()
            {
                throw new NotSupportedException();
            }

            protected override void DoBind(EndPoint localAddress)
            {
                throw new NotSupportedException();
            }

            protected override void DoClose()
            {
                throw new NotSupportedException();
            }

            protected override void DoDisconnect()
            {
                throw new NotSupportedException();
            }

            protected override void DoWrite(ChannelOutboundBuffer input)
            {
                throw new NotSupportedException();
            }

            protected override bool IsCompatible(IEventLoop eventLoop) => false;

            public class TestUnsafe : AbstractUnsafe
            {
                public override Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
                {
                    return TaskUtil.FromException(new NotSupportedException());
                }
            }
        }

        private static void SafeClose(EmbeddedChannel ch)
        {
            ch.Finish();
            for (; ; )
            {
                IByteBuffer m = ch.ReadOutbound<IByteBuffer>();
                if (m == null)
                {
                    break;
                }
                m.Release();
            }
        }
    }
}