
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using Moq;
    using Xunit;

    public class DefaultHttp2FrameWriterTest : IDisposable
    {
        private DefaultHttp2FrameWriter frameWriter;

        private IByteBuffer outbound;

        private IByteBuffer expectedOutbound;

        private IPromise promise;

        private IHttp2HeadersEncoder http2HeadersEncoder;

        private Mock<IChannel> channel;

        private Mock<IChannelHandlerContext> ctx;

        public DefaultHttp2FrameWriterTest()
        {
            this.frameWriter = new DefaultHttp2FrameWriter();

            this.outbound = Unpooled.Buffer();

            this.expectedOutbound = Unpooled.Empty;

            this.promise = new TaskCompletionSource();

            this.http2HeadersEncoder = new DefaultHttp2HeadersEncoder();

            this.channel = new Mock<IChannel>();

            Task localAnswer(object msg)
            {
                if (msg is IByteBuffer buf)
                {
                    this.outbound.WriteBytes(buf);
                }
                ReferenceCountUtil.Release(msg);
                return TaskUtil.Completed;
            }

            Task localAnswer0(object msg, IPromise promise)
            {
                if (msg is IByteBuffer buf)
                {
                    this.outbound.WriteBytes(buf);
                }
                ReferenceCountUtil.Release(msg);
                return TaskUtil.Completed;
            }
            this.ctx = new Mock<IChannelHandlerContext>();
            this.ctx.Setup(x => x.WriteAsync(It.IsAny<object>())).Returns<object>(localAnswer);
            this.ctx.Setup(x => x.WriteAsync(It.IsAny<object>(), It.IsAny<IPromise>())).Returns<object, IPromise>(localAnswer0);
            this.ctx.Setup(x => x.Allocator).Returns(UnpooledByteBufferAllocator.Default);
            this.ctx.Setup(x => x.Channel).Returns(this.channel.Object);
        }

        public void Dispose()
        {
            this.outbound.Release();
            this.expectedOutbound.Release();
            this.frameWriter.Close();
        }

        [Fact]
        public void WriteHeaders()
        {
            int streamId = 1;
            var headers = new DefaultHttp2Headers();
            headers.Method = (AsciiString)"GET";
            headers.Path = (AsciiString)"/";
            headers.Authority = (AsciiString)"foo.com";
            headers.Scheme = (AsciiString)"https";

            this.frameWriter.WriteHeadersAsync(this.ctx.Object, streamId, headers, 0, true, this.promise);

            byte[] expectedPayload = this.HeaderPayload(streamId, headers);
            byte[] expectedFrameBytes = {
                (byte) 0x00, (byte) 0x00, (byte) 0x0a, // payload length = 10
                (byte) 0x01, // payload type = 1
                (byte) 0x05, // flags = (0x01 | 0x04)
                (byte) 0x00, (byte) 0x00, (byte) 0x00, (byte) 0x01 // stream id = 1
            };
            this.expectedOutbound = Unpooled.CopiedBuffer(expectedFrameBytes, expectedPayload);
            AssertEx.Equal(this.expectedOutbound, this.outbound);
        }

        [Fact]
        public void WriteHeadersWithPadding()
        {
            int streamId = 1;
            var headers = new DefaultHttp2Headers();
            headers.Method = (AsciiString)"GET";
            headers.Path = (AsciiString)"/";
            headers.Authority = (AsciiString)"foo.com";
            headers.Scheme = (AsciiString)"https";

            this.frameWriter.WriteHeadersAsync(this.ctx.Object, streamId, headers, 5, true, this.promise);

            byte[] expectedPayload = this.HeaderPayload(streamId, headers, (byte)4);
            byte[] expectedFrameBytes = {
                (byte) 0x00, (byte) 0x00, (byte) 0x0f, // payload length = 16
                (byte) 0x01, // payload type = 1
                (byte) 0x0d, // flags = (0x01 | 0x04 | 0x08)
                (byte) 0x00, (byte) 0x00, (byte) 0x00, (byte) 0x01 // stream id = 1
            };
            this.expectedOutbound = Unpooled.CopiedBuffer(expectedFrameBytes, expectedPayload);
            AssertEx.Equal(this.expectedOutbound, this.outbound);
        }

        [Fact]
        public void WriteHeadersNotEndStream()
        {
            int streamId = 1;
            var headers = new DefaultHttp2Headers();
            headers.Method = (AsciiString)"GET";
            headers.Path = (AsciiString)"/";
            headers.Authority = (AsciiString)"foo.com";
            headers.Scheme = (AsciiString)"https";

            this.frameWriter.WriteHeadersAsync(this.ctx.Object, streamId, headers, 0, false, this.promise);

            byte[] expectedPayload = this.HeaderPayload(streamId, headers);
            byte[] expectedFrameBytes = {
                (byte) 0x00, (byte) 0x00, (byte) 0x0a, // payload length = 10
                (byte) 0x01, // payload type = 1
                (byte) 0x04, // flags = 0x04
                (byte) 0x00, (byte) 0x00, (byte) 0x00, (byte) 0x01 // stream id = 1
            };
            this.expectedOutbound = Unpooled.CopiedBuffer(expectedFrameBytes, expectedPayload);
            AssertEx.Equal(this.expectedOutbound, this.outbound);
        }

        /**
         * Test large headers that exceed {@link DefaultHttp2FrameWriter#maxFrameSize}
         * the remaining headers will be sent in a CONTINUATION frame
         */
        [Fact]
        public void WriteLargeHeaders()
        {
            int streamId = 1;
            IHttp2Headers headers = new DefaultHttp2Headers();
            headers.Method = (AsciiString)"GET";
            headers.Path = (AsciiString)"/";
            headers.Authority = (AsciiString)"foo.com";
            headers.Scheme = (AsciiString)"https";
            headers = DummyHeaders(headers, 20);

            this.http2HeadersEncoder.Configuration.SetMaxHeaderListSize(int.MaxValue);
            this.frameWriter.HeadersConfiguration.SetMaxHeaderListSize(int.MaxValue);
            this.frameWriter.SetMaxFrameSize(Http2CodecUtil.MaxFrameSizeLowerBound);
            this.frameWriter.WriteHeadersAsync(this.ctx.Object, streamId, headers, 0, true, this.promise);

            byte[] expectedPayload = this.HeaderPayload(streamId, headers);

            // First frame: HEADER(length=0x4000, flags=0x01)
            Assert.Equal(Http2CodecUtil.MaxFrameSizeLowerBound, this.outbound.ReadUnsignedMedium());
            Assert.Equal(0x01, this.outbound.ReadByte());
            Assert.Equal(0x01, this.outbound.ReadByte());
            Assert.Equal(streamId, this.outbound.ReadInt());

            byte[] firstPayload = new byte[Http2CodecUtil.MaxFrameSizeLowerBound];
            this.outbound.ReadBytes(firstPayload);

            int remainPayloadLength = expectedPayload.Length - Http2CodecUtil.MaxFrameSizeLowerBound;
            // Second frame: CONTINUATION(length=remainPayloadLength, flags=0x04)
            Assert.Equal(remainPayloadLength, this.outbound.ReadUnsignedMedium());
            Assert.Equal(0x09, this.outbound.ReadByte());
            Assert.Equal(0x04, this.outbound.ReadByte());
            Assert.Equal(streamId, this.outbound.ReadInt());

            byte[] secondPayload = new byte[remainPayloadLength];
            this.outbound.ReadBytes(secondPayload);

            Assert.True(PlatformDependent.ByteArrayEquals(expectedPayload, 0, firstPayload, 0, firstPayload.Length));
            Assert.True(PlatformDependent.ByteArrayEquals(expectedPayload, firstPayload.Length, secondPayload, 0, secondPayload.Length));
        }

        [Fact]
        public void WriteFrameZeroPayload()
        {
            this.frameWriter.WriteFrameAsync(this.ctx.Object, (Http2FrameTypes)0xf, 0, new Http2Flags(), Unpooled.Empty, this.promise);

            byte[] expectedFrameBytes = {
                (byte) 0x00, (byte) 0x00, (byte) 0x00, // payload length
                (byte) 0x0f, // payload type
                (byte) 0x00, // flags
                (byte) 0x00, (byte) 0x00, (byte) 0x00, (byte) 0x00 // stream id
            };

            this.expectedOutbound = Unpooled.WrappedBuffer(expectedFrameBytes);
            AssertEx.Equal(this.expectedOutbound, this.outbound);
        }

        [Fact]
        public void WriteFrameHasPayload()
        {
            byte[] payload = { (byte)0x01, (byte)0x03, (byte)0x05, (byte)0x07, (byte)0x09 };

            // will auto release after frameWriter.writeFrame succeed
            var payloadByteBuf = Unpooled.WrappedBuffer(payload);
            this.frameWriter.WriteFrameAsync(this.ctx.Object, (Http2FrameTypes)0xf, 0, new Http2Flags(), payloadByteBuf, this.promise);

            byte[] expectedFrameHeaderBytes = {
                (byte) 0x00, (byte) 0x00, (byte) 0x05, // payload length
                (byte) 0x0f, // payload type
                (byte) 0x00, // flags
                (byte) 0x00, (byte) 0x00, (byte) 0x00, (byte) 0x00 // stream id
            };
            this.expectedOutbound = Unpooled.CopiedBuffer(expectedFrameHeaderBytes, payload);
            AssertEx.Equal(this.expectedOutbound, this.outbound);
        }

        private byte[] HeaderPayload(int streamId, IHttp2Headers headers, byte padding)
        {
            if (padding == 0)
            {
                return this.HeaderPayload(streamId, headers);
            }

            var outputStream = new MemoryStream();
            try
            {
                outputStream.WriteByte(padding);
                var bts = this.HeaderPayload(streamId, headers);
                outputStream.Write(bts, 0, bts.Length);
                bts = new byte[padding];
                outputStream.Write(bts, 0, bts.Length);
                return outputStream.ToArray();
            }
            finally
            {
                outputStream.Close();
            }
        }

        private byte[] HeaderPayload(int streamId, IHttp2Headers headers)
        {
            var byteBuf = Unpooled.Buffer();
            try
            {
                http2HeadersEncoder.EncodeHeaders(streamId, headers, byteBuf);
                byte[] bytes = new byte[byteBuf.ReadableBytes];
                byteBuf.ReadBytes(bytes);
                return bytes;
            }
            finally
            {
                byteBuf.Release();
            }
        }

        private static IHttp2Headers DummyHeaders(IHttp2Headers headers, int times)
        {
            string largeValue = Repeat("dummy-value", 100);
            for (int i = 0; i < times; i++)
            {
                headers.Add((AsciiString)string.Format("dummy-{0}", i), (AsciiString)largeValue);
            }
            return headers;
        }

        private static string Repeat(string str, int count)
        {
            return " ".PadRight(count, ' ').Replace(" ", str);
        }
    }
}
