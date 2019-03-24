namespace DotNetty.Codecs.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class ByteToMessageDecoderTest
    {
        private static readonly RNGCryptoServiceProvider s_randomNumberGeneratorCsp = new RNGCryptoServiceProvider();

        [Fact]
        public void RemoveItself()
        {
            var removed = false;
            EmbeddedChannel channel = new EmbeddedChannel(new ActionByteToMessageDecoder((IChannelHandlerContext context, IByteBuffer input, List<object> outpu) =>
            {
                Assert.False(removed);
                input.ReadByte();
                context.Channel.Pipeline.Remove(context.Handler);
                removed = true;
            }));

            var buf = Unpooled.WrappedBuffer(new byte[] { (byte)'a', (byte)'b', (byte)'c' });
            channel.WriteInbound(buf.Copy());
            var b = channel.ReadInbound<IByteBuffer>();
            AssertEx.Equal(b, buf.SkipBytes(1));
            b.Release();
            buf.Release();

        }

        [Fact]
        public void RemoveItselfWriteBuffer()
        {
            var buf = Unpooled.Buffer().WriteBytes(new byte[] { (byte)'a', (byte)'b', (byte)'c' });
            var removed = false;
            EmbeddedChannel channel = new EmbeddedChannel(new ActionByteToMessageDecoder((IChannelHandlerContext context, IByteBuffer input, List<object> outpu) =>
            {
                Assert.False(removed);
                input.ReadByte();
                context.Channel.Pipeline.Remove(context.Handler);

                // This should not let it keep call decode
                buf.WriteByte((int)'d');
                removed = true;
            }));

            channel.WriteInbound(buf.Copy());
            var expected = Unpooled.WrappedBuffer(new byte[] { (byte)'b', (byte)'c' });
            var b = channel.ReadInbound<IByteBuffer>();
            AssertEx.Equal(expected, b);
            expected.Release();
            buf.Release();
            b.Release();
        }

        /**
         * Verifies that internal buffer of the ByteToMessageDecoder is released once decoder is removed from pipeline. In
         * this case input is read fully.
         */
        [Fact]
        public void InternalBufferClearReadAll()
        {
            var buf = Unpooled.Buffer().WriteBytes(new byte[] { (byte)'a' });
            EmbeddedChannel channel = NewInternalBufferTestChannel();
            Assert.False(channel.WriteInbound(buf));
            Assert.False(channel.Finish());
        }

        /**
         * Verifies that internal buffer of the ByteToMessageDecoder is released once decoder is removed from pipeline. In
         * this case input was not fully read.
         */
        [Fact]
        public void InternalBufferClearReadPartly()
        {
            var buf = Unpooled.Buffer().WriteBytes(new byte[] { (byte)'a', (byte)'b' });
            EmbeddedChannel channel = NewInternalBufferTestChannel();
            Assert.True(channel.WriteInbound(buf));
            Assert.True(channel.Finish());
            var expected = Unpooled.WrappedBuffer(new byte[] { (byte)'b' });
            var b = channel.ReadInbound<IByteBuffer>();
            AssertEx.Equal(expected, b);
            Assert.Null(channel.ReadInbound<IByteBuffer>());
            expected.Release();
            b.Release();
        }

        private EmbeddedChannel NewInternalBufferTestChannel()
        {
            return new EmbeddedChannel(new ByteToMessageDecoder0());
        }

        [Fact]
        public void HandlerRemovedWillNotReleaseBufferIfDecodeInProgress()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new ActionByteToMessageDecoder((IChannelHandlerContext context, IByteBuffer input, List<object> outpu) =>
            {
                context.Channel.Pipeline.Remove(context.Handler);
                Assert.True(input.ReferenceCount != 0);
            }));
            byte[] bytes = new byte[1024];
            s_randomNumberGeneratorCsp.GetBytes(bytes);

            Assert.True(channel.WriteInbound(Unpooled.WrappedBuffer(bytes)));
            Assert.True(channel.FinishAndReleaseAll());
        }

        internal static void AssertCumulationReleased(IByteBuffer byteBuf)
        {
            Assert.True(byteBuf == null || byteBuf == Unpooled.Empty || byteBuf.ReferenceCount == 0);
        }

        [Fact]
        public void FireChannelReadCompleteOnInactive()
        {
            var queue = new ConcurrentQueue<int>();
            var buf = Unpooled.Buffer().WriteBytes(new byte[] { (byte)'a', (byte)'b' });
            EmbeddedChannel channel = new EmbeddedChannel(new ActionByteToMessageDecoder1((IChannelHandlerContext context, IByteBuffer input, List<object> outpu) =>
            {
                int readable = input.ReadableBytes;
                Assert.True(readable > 0);
                input.SkipBytes(readable);
            }), new ChannelReadCompleteOnInactiveAdapter(queue));

            Assert.False(channel.WriteInbound(buf));
            channel.Finish();
            queue.TryDequeue(out var result);
            Assert.Equal(1, result);
            queue.TryDequeue(out result);
            Assert.Equal(2, result);
            queue.TryDequeue(out result);
            Assert.Equal(3, result);
            Assert.True(queue.IsEmpty);
        }

        // See https://github.com/netty/netty/issues/4635
        [Fact]
        public void RemoveWhileInCallDecode()
        {
            var upgradeMessage = new object();
            var decoder = new UpgradeByteToMessageDecoder(upgradeMessage);
            EmbeddedChannel channel = new EmbeddedChannel(decoder, new UpgradeChannelHandlerAdapter(decoder, upgradeMessage));

            var buf = Unpooled.WrappedBuffer(new byte[] { (byte)'a', (byte)'b', (byte)'c' });
            Assert.True(channel.WriteInbound(buf.Copy()));
            var b = channel.ReadInbound<IByteBuffer>();
            AssertEx.Equal(b, buf.SkipBytes(1));
            Assert.False(channel.Finish());
            buf.Release();
            b.Release();
        }

        [Fact]
        public void DecodeLastEmptyBuffer()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new ActionByteToMessageDecoder((IChannelHandlerContext context, IByteBuffer input, List<object> output) =>
            {
                int readable = input.ReadableBytes;
                Assert.True(readable > 0);
                output.Add(input.ReadBytes(readable));
            }));
            byte[] bytes = new byte[1024];
            s_randomNumberGeneratorCsp.GetBytes(bytes);

            Assert.True(channel.WriteInbound(Unpooled.WrappedBuffer(bytes)));
            AssertEx.Equal(Unpooled.WrappedBuffer(bytes), channel.ReadInbound<IByteBuffer>(), true);
            Assert.Null(channel.ReadInbound<IByteBuffer>());
            Assert.False(channel.Finish());
            Assert.Null(channel.ReadInbound<IByteBuffer>());
        }

        [Fact]
        public void DecodeLastNonEmptyBuffer()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new DecodeLastNonEmptyBufferDecoder());

            byte[] bytes = new byte[1024];
            s_randomNumberGeneratorCsp.GetBytes(bytes);

            Assert.True(channel.WriteInbound(Unpooled.WrappedBuffer(bytes)));
            AssertEx.Equal(Unpooled.WrappedBuffer(bytes, 0, bytes.Length - 1), channel.ReadInbound<IByteBuffer>(), true);
            Assert.Null(channel.ReadInbound<IByteBuffer>());
            Assert.True(channel.Finish());
            AssertEx.Equal(Unpooled.WrappedBuffer(bytes, bytes.Length - 1, 1), channel.ReadInbound<IByteBuffer>(), true);
            Assert.Null(channel.ReadInbound<IByteBuffer>());
        }

        //@Test
        //    public void testReadOnlyBuffer()
        //{
        //    EmbeddedChannel channel = new EmbeddedChannel(new ByteToMessageDecoder() {
        //            @Override
        //            protected void decode(ChannelHandlerContext ctx, ByteBuf in, List<Object> out) throws Exception {
        //    }
        //});
        //        assertFalse(channel.writeInbound(Unpooled.buffer(8).writeByte(1).asReadOnly()));
        //        assertFalse(channel.writeInbound(Unpooled.wrappedBuffer(new byte[] { (byte) 2 })));
        //        assertFalse(channel.finish());
        //    }

        [Fact]
        public void ReleaseWhenMergeCumulateThrows()
        {
            var error = new Exception();
            var input = Unpooled.Buffer().WriteZero(12);
            var cumulation = new UnpooledHeapByteBufThrowExceptionWhenWriteBytes(UnpooledByteBufferAllocator.Default, 0, 64, error);
            var expected = Assert.Throws<Exception>(()=> ByteToMessageDecoder.MergeCumulator(UnpooledByteBufferAllocator.Default, cumulation, input));
            Assert.Same(error, expected);
            Assert.Equal(0, input.ReferenceCount);
        }

        [Fact]
        public void ReleaseWhenCompositeCumulateThrows()
        {
            var error = new Exception();
            var input = Unpooled.Buffer().WriteZero(12);
            var cumulation = new CompositeByteBufferThrowExceptionWhenAddComponent(UnpooledByteBufferAllocator.Default, false, 64, error);
            var expected = Assert.Throws<Exception>(() => ByteToMessageDecoder.CompositionCumulation(UnpooledByteBufferAllocator.Default, cumulation, input));
            Assert.Same(error, expected);
            Assert.Equal(0, input.ReferenceCount);
        }
    }

    class ActionByteToMessageDecoder : ByteToMessageDecoder
    {
        private readonly Action<IChannelHandlerContext, IByteBuffer, List<object>> _decodeAction;

        public ActionByteToMessageDecoder(Action<IChannelHandlerContext, IByteBuffer, List<object>> decodeAction)
        {
            _decodeAction = decodeAction;
        }
        protected internal override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            _decodeAction(context, input, output);
        }
    }

    class ActionByteToMessageDecoder0 : ActionByteToMessageDecoder
    {
        public ActionByteToMessageDecoder0(Action<IChannelHandlerContext, IByteBuffer, List<object>> decodeAction) : base(decodeAction) { }

        protected override void HandlerRemovedInternal(IChannelHandlerContext context)
        {
            ByteToMessageDecoderTest.AssertCumulationReleased(InternalBuffer);
        }
    }

    class ActionByteToMessageDecoder1 : ActionByteToMessageDecoder
    {
        public ActionByteToMessageDecoder1(Action<IChannelHandlerContext, IByteBuffer, List<object>> decodeAction) : base(decodeAction) { }

        protected override void DecodeLast(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            Assert.False(input.IsReadable());
            output.Add("data");
        }
    }

    class ByteToMessageDecoder0 : ByteToMessageDecoder
    {
        protected internal override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            var byteBuf = this.InternalBuffer;
            Assert.Equal(1, byteBuf.ReferenceCount);
            input.ReadByte();
            // Removal from pipeline should clear internal buffer
            context.Channel.Pipeline.Remove(this);
        }

        protected override void HandlerRemovedInternal(IChannelHandlerContext context)
        {
            ByteToMessageDecoderTest.AssertCumulationReleased(InternalBuffer);
        }
    }

    class ChannelReadCompleteOnInactiveAdapter : ChannelHandlerAdapter
    {
        private readonly ConcurrentQueue<int> _queue;

        public ChannelReadCompleteOnInactiveAdapter(ConcurrentQueue<int> queue) => _queue = queue;

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
            if (!context.Channel.Active)
            {
                _queue.Enqueue(2);
            }
        }
    }

    class UpgradeByteToMessageDecoder : ByteToMessageDecoder
    {
        private readonly object _upgradeMessage;
        public UpgradeByteToMessageDecoder(object upgradeMessage) => _upgradeMessage = upgradeMessage;

        protected internal override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            Assert.Equal('a', (char)input.ReadByte());
            output.Add(_upgradeMessage);
        }
    }

    class UpgradeChannelHandlerAdapter : ChannelHandlerAdapter
    {
        private readonly object _upgradeMessage;
        private readonly UpgradeByteToMessageDecoder _decoder;
        public UpgradeChannelHandlerAdapter(UpgradeByteToMessageDecoder decoder, object upgradeMessage)
        {
            _decoder = decoder;
            _upgradeMessage = upgradeMessage;
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            if (object.ReferenceEquals(message, _upgradeMessage))
            {
                context.Channel.Pipeline.Remove(_decoder);
                return;
            }
            context.FireChannelRead(message);
        }
    }

    class DecodeLastNonEmptyBufferDecoder : ByteToMessageDecoder
    {
        private bool decodeLast;

        protected internal override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            int readable = input.ReadableBytes;
            Assert.True(readable > 0);
            if (!this.decodeLast && readable == 1)
            {
                return;
            }
            output.Add(input.ReadBytes(this.decodeLast ? readable : readable - 1));
        }

        protected override void DecodeLast(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            Assert.False(this.decodeLast);
            this.decodeLast = true;
            base.DecodeLast(context, input, output);
        }
    }

    class UnpooledHeapByteBufThrowExceptionWhenWriteBytes : UnpooledHeapByteBuffer
    {
        readonly Exception exception;
        public UnpooledHeapByteBufThrowExceptionWhenWriteBytes(IByteBufferAllocator alloc, int initialCapacity, int maxCapacity, Exception exception)
            :base(alloc, initialCapacity, maxCapacity)
        {
            this.exception = exception;
        }

        //public override IByteBuffer WriteBytes(IByteBuffer src)
        //{
        //    throw this.exception;
        //}

        public override IByteBuffer WriteBytes(IByteBuffer src, int length)
        {
            throw this.exception;
        }
    }

    class CompositeByteBufferThrowExceptionWhenAddComponent : CompositeByteBuffer
    {
        readonly Exception exception;
        public CompositeByteBufferThrowExceptionWhenAddComponent(IByteBufferAllocator allocator, bool direct, int maxNumComponents, Exception exception)
            :base(allocator, direct, maxNumComponents)
        {
            this.exception = exception;
        }

        public override CompositeByteBuffer AddComponent(bool increaseWriterIndex, IByteBuffer buffer)
        {
            throw this.exception;
        }
    }
}