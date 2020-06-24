namespace DotNetty.Codecs.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit;

    public class ReplayingDecoderTest
    {
        [Fact]
        public void TestLineProtocol()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new LineDecoder());

            // Ordinary input
            ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { (byte)'A' }));
            Assert.Null(ch.ReadInbound());
            ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { (byte)'B' }));
            Assert.Null(ch.ReadInbound());
            ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { (byte)'C' }));
            Assert.Null(ch.ReadInbound());
            ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { (byte)'\n' }));

            IByteBuffer buf = Unpooled.WrappedBuffer(new byte[] { (byte)'A', (byte)'B', (byte)'C' });
            IByteBuffer buf2 = ch.ReadInbound<IByteBuffer>();
            Assert.Equal(buf, buf2);

            buf.Release();
            buf2.Release();

            // Truncated input
            ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { (byte)'A' }));
            Assert.Null(ch.ReadInbound());

            ch.Finish();
            Assert.Null(ch.ReadInbound());
        }

        sealed class LineDecoder : ReplayingDecoder
        {
            protected internal override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
            {
                var bytes = input.BytesBefore((byte)'\n');
                IByteBuffer msg = input.ReadBytes(bytes);
                output.Add(msg);
                input.SkipBytes(1);
            }
        }

        [Fact]
        public void TestReplacement()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new BloatedLineDecoder());

            // "AB" should be forwarded to LineDecoder by BloatedLineDecoder.
            ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { (byte)'A', (byte)'B' }));
            Assert.Null(ch.ReadInbound());

            // "C\n" should be appended to "AB" so that LineDecoder decodes it correctly.
            ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { (byte)'C', (byte)'\n' }));

            IByteBuffer buf = Unpooled.WrappedBuffer(new byte[] { (byte)'A', (byte)'B', (byte)'C' });
            IByteBuffer buf2 = ch.ReadInbound<IByteBuffer>();
            Assert.Equal(buf, buf2);

            buf.Release();
            buf2.Release();

            ch.Finish();
            Assert.Null(ch.ReadInbound());
        }

        sealed class BloatedLineDecoder : ChannelHandlerAdapter
        {
            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                context.Pipeline.Replace(this, "less-bloated", new LineDecoder());
                context.Pipeline.FireChannelRead(message);
            }
        }

        [Fact]
        public void TestSingleDecode()
        {
            LineDecoder decoder = new LineDecoder();
            decoder.SingleDecode = true;
            EmbeddedChannel ch = new EmbeddedChannel(decoder);

            // "C\n" should be appended to "AB" so that LineDecoder decodes it correctly.
            ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { (byte)'C', (byte)'\n', (byte)'B', (byte)'\n' }));

            IByteBuffer buf = Unpooled.WrappedBuffer(new byte[] { (byte)'C' });
            IByteBuffer buf2 = ch.ReadInbound<IByteBuffer>();
            Assert.Equal(buf, buf2);

            buf.Release();
            buf2.Release();

            Assert.Null(ch.ReadInbound()); // "Must be null as it must only decode one frame"

            ch.Read();
            ch.Finish();

            buf = Unpooled.WrappedBuffer(new byte[] { (byte)'B' });
            buf2 = ch.ReadInbound<IByteBuffer>();
            Assert.Equal(buf, buf2);

            buf.Release();
            buf2.Release();

            Assert.Null(ch.ReadInbound());
        }

        [Fact]
        public void TestRemoveItself()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new RemoveItselfDecoder());

            IByteBuffer buf = Unpooled.WrappedBuffer(new byte[] { (byte)'a', (byte)'b', (byte)'c' });
            channel.WriteInbound(buf.Copy());
            IByteBuffer b = channel.ReadInbound<IByteBuffer>();
            Assert.Equal(b, buf.SkipBytes(1));
            b.Release();
            buf.Release();
        }

        sealed class RemoveItselfDecoder : ReplayingDecoder
        {
            private bool _removed;

            protected internal override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
            {
                Assert.False(_removed);
                input.ReadByte();
                context.Pipeline.Remove(this);
                _removed = true;
            }
        }

        [Fact]
        public void TestRemoveItselfWithReplayError()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new RemoveItselfWithReplayErrorDecoder());

            IByteBuffer buf = Unpooled.WrappedBuffer(new byte[] { (byte)'a', (byte)'b', (byte)'c' });
            channel.WriteInbound(buf.Copy());
            IByteBuffer b = channel.ReadInbound<IByteBuffer>();

            Assert.Equal(b, buf); // "Expect to have still all bytes in the buffer"
            b.Release();
            buf.Release();
        }

        sealed class RemoveItselfWithReplayErrorDecoder : ReplayingDecoder
        {
            private bool _removed;

            protected internal override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
            {
                Assert.False(_removed);
                context.Pipeline.Remove(this);

                input.ReadBytes(1000);

                _removed = true;
            }
        }

        [Fact]
        public void TestRemoveItselfWriteBuffer()
        {
            IByteBuffer buf = Unpooled.Buffer().WriteBytes(new byte[] { (byte)'a', (byte)'b', (byte)'c' });
            EmbeddedChannel channel = new EmbeddedChannel(new RemoveItselfWriteBufferDecoder(buf));

            channel.WriteInbound(buf.Copy());
            IByteBuffer b = channel.ReadInbound<IByteBuffer>();
            Assert.Equal(b, Unpooled.WrappedBuffer(new byte[] { (byte)'b', (byte)'c' }));
            b.Release();
            buf.Release();
        }

        sealed class RemoveItselfWriteBufferDecoder : ReplayingDecoder
        {
            private readonly IByteBuffer _buf;
            private bool _removed;

            public RemoveItselfWriteBufferDecoder(IByteBuffer buf) => _buf = buf;

            protected internal override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
            {
                Assert.False(_removed);
                input.ReadByte();
                context.Pipeline.Remove(this);

                // This should not let it keep call decode
                _buf.WriteByte('d');
                _removed = true;
            }
        }

        [Fact]
        public void TestFireChannelReadCompleteOnInactive()
        {
            var queue = new ConcurrentQueue<int>();
            IByteBuffer buf = Unpooled.Buffer().WriteBytes(new byte[] { (byte)'a', (byte)'b' });
            EmbeddedChannel channel = new EmbeddedChannel(
                new FireChannelReadCompleteOnInactiveDecoder(),
                new FireChannelReadCompleteOnInactiveHander(queue));
            Assert.False(channel.WriteInbound(buf));
            channel.Finish();
            queue.TryDequeue(out var value);
            Assert.Equal(1, value);
            queue.TryDequeue(out value);
            Assert.Equal(1, value);
            queue.TryDequeue(out value);
            Assert.Equal(2, value);
            queue.TryDequeue(out value);
            Assert.Equal(3, value);
            Assert.Empty(queue);
        }

        sealed class FireChannelReadCompleteOnInactiveDecoder : ReplayingDecoder
        {
            protected internal override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
            {
                int readable = input.ReadableBytes;
                Assert.True(readable > 0);
                input.SkipBytes(readable);
                output.Add("data");
            }

            protected override void DecodeLast(IChannelHandlerContext context, IByteBuffer input, List<object> output)
            {
                Assert.False(input.IsReadable());
                output.Add("data");
            }
        }

        sealed class FireChannelReadCompleteOnInactiveHander : ChannelHandlerAdapter
        {
            public readonly ConcurrentQueue<int> _queue;

            public FireChannelReadCompleteOnInactiveHander(ConcurrentQueue<int> queue) => _queue = queue;

            public override void ChannelInactive(IChannelHandlerContext context)
            {
                _queue.Enqueue(3);
            }

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                _queue.Enqueue(1);
            }

            public override void ChannelReadComplete(IChannelHandlerContext context)
            {
                if (!context.Channel.IsActive)
                {
                    _queue.Enqueue(2);
                }
            }
        }

        [Fact]
        public void TestChannelInputShutdownEvent()
        {
            AtomicReference<Exception> error = new AtomicReference<Exception>();
            EmbeddedChannel channel = new EmbeddedChannel(new ChannelInputShutdownEventDecoder(error));

            Assert.False(channel.WriteInbound(Unpooled.WrappedBuffer(new byte[] { 0, 1 })));
            channel.Pipeline.FireUserEventTriggered(ChannelInputShutdownEvent.Instance);
            Assert.False(channel.FinishAndReleaseAll());

            var err = error.Value;
            if (err != null)
            {
                throw err;
            }
        }

        sealed class ChannelInputShutdownEventDecoder : ReplayingDecoder<int>
        {
            private readonly AtomicReference<Exception> _error;
            private bool _decoded;

            public ChannelInputShutdownEventDecoder(AtomicReference<Exception> error) => _error = error;

            protected internal override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
            {
                if (!(input is ReplayingDecoderByteBuffer))
                {
                    _error.Value = new Exception($"in must be of type {nameof(ReplayingDecoderByteBuffer)} but was {input.GetType().Name}");
                }
                if (!_decoded)
                {
                    _decoded = true;
                    input.ReadByte();
                    ExchangeState(1);
                }
                else
                {
                    // This will throw an ReplayingError
                    input.SkipBytes(int.MaxValue);
                }
            }
        }

        [Fact]
        public void HandlerRemovedWillNotReleaseBufferIfDecodeInProgress()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new HandlerRemovedWillNotReleaseBufferIfDecodeInProgressDecoder());
            byte[] bytes = new byte[1024];
            (new Random()).NextBytes(bytes);

            Assert.True(channel.WriteInbound(Unpooled.WrappedBuffer(bytes)));
            Assert.True(channel.FinishAndReleaseAll());
        }

        sealed class HandlerRemovedWillNotReleaseBufferIfDecodeInProgressDecoder : ReplayingDecoder<int>
        {
            protected internal override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
            {
                context.Pipeline.Remove(this);
                Assert.True(input.ReferenceCount != 0);
            }

            protected override void HandlerRemovedInternal(IChannelHandlerContext context)
            {
                AssertCumulationReleased(InternalBuffer);
            }
        }

        private static void AssertCumulationReleased(IByteBuffer byteBuf)
        {
            Assert.True(byteBuf == null || byteBuf == Unpooled.Empty || byteBuf.ReferenceCount == 0,
                "unexpected value: " + byteBuf);
        }
    }
}
