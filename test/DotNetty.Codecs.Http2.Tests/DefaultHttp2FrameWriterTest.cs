
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using Moq;
    using Xunit;

    public class DefaultHttp2FrameWriterTest : IDisposable
    {
        private DefaultHttp2FrameWriter _frameWriter;

        private IByteBuffer _outbound;

        private IByteBuffer _expectedOutbound;

        private IPromise _promise;

        private IHttp2HeadersEncoder _http2HeadersEncoder;

        private Mock<IChannel> _channel;

        private Mock<IChannelHandlerContext> _ctx;

        public DefaultHttp2FrameWriterTest()
        {
            _http2HeadersEncoder = new DefaultHttp2HeadersEncoder(
                    NeverSensitiveDetector.Instance, new HpackEncoder(false, 16, 0));

            _frameWriter = new DefaultHttp2FrameWriter(new DefaultHttp2HeadersEncoder(
                    NeverSensitiveDetector.Instance, new HpackEncoder(false, 16, 0)));

            _outbound = Unpooled.Buffer();

            _expectedOutbound = Unpooled.Empty;

            _promise = new TaskCompletionSource();

            _channel = new Mock<IChannel>();

            Task localAnswer(object msg)
            {
                if (msg is IByteBuffer buf)
                {
                    _outbound.WriteBytes(buf);
                }
                ReferenceCountUtil.Release(msg);
                return TaskUtil.Completed;
            }

            Task localAnswer0(object msg, IPromise promise)
            {
                if (msg is IByteBuffer buf)
                {
                    _outbound.WriteBytes(buf);
                }
                ReferenceCountUtil.Release(msg);
                return TaskUtil.Completed;
            }
            _ctx = new Mock<IChannelHandlerContext>();
            _ctx.Setup(x => x.WriteAsync(It.IsAny<object>())).Returns<object>(localAnswer);
            _ctx.Setup(x => x.WriteAsync(It.IsAny<object>(), It.IsAny<IPromise>())).Returns<object, IPromise>(localAnswer0);
            _ctx.Setup(x => x.Allocator).Returns(UnpooledByteBufferAllocator.Default);
            _ctx.Setup(x => x.Channel).Returns(_channel.Object);
        }

        public void Dispose()
        {
            _outbound.Release();
            _expectedOutbound.Release();
            _frameWriter.Close();
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

            _frameWriter.WriteHeadersAsync(_ctx.Object, streamId, headers, 0, true, _promise);

            byte[] expectedPayload = HeaderPayload(streamId, headers);
            byte[] expectedFrameBytes = {
                (byte) 0x00, (byte) 0x00, (byte) 0x0a, // payload length = 10
                (byte) 0x01, // payload type = 1
                (byte) 0x05, // flags = (0x01 | 0x04)
                (byte) 0x00, (byte) 0x00, (byte) 0x00, (byte) 0x01 // stream id = 1
            };
            _expectedOutbound = Unpooled.CopiedBuffer(expectedFrameBytes, expectedPayload);
            Assert.Equal(_expectedOutbound, _outbound);
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

            _frameWriter.WriteHeadersAsync(_ctx.Object, streamId, headers, 5, true, _promise);

            byte[] expectedPayload = HeaderPayload(streamId, headers, (byte)4);
            byte[] expectedFrameBytes = {
                (byte) 0x00, (byte) 0x00, (byte) 0x0f, // payload length = 16
                (byte) 0x01, // payload type = 1
                (byte) 0x0d, // flags = (0x01 | 0x04 | 0x08)
                (byte) 0x00, (byte) 0x00, (byte) 0x00, (byte) 0x01 // stream id = 1
            };
            _expectedOutbound = Unpooled.CopiedBuffer(expectedFrameBytes, expectedPayload);
            Assert.Equal(_expectedOutbound, _outbound);
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

            _frameWriter.WriteHeadersAsync(_ctx.Object, streamId, headers, 0, false, _promise);

            byte[] expectedPayload = HeaderPayload(streamId, headers);
            byte[] expectedFrameBytes = {
                (byte) 0x00, (byte) 0x00, (byte) 0x0a, // payload length = 10
                (byte) 0x01, // payload type = 1
                (byte) 0x04, // flags = 0x04
                (byte) 0x00, (byte) 0x00, (byte) 0x00, (byte) 0x01 // stream id = 1
            };
            _expectedOutbound = Unpooled.CopiedBuffer(expectedFrameBytes, expectedPayload);
            Assert.Equal(_expectedOutbound, _outbound);
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

            _http2HeadersEncoder.Configuration.SetMaxHeaderListSize(int.MaxValue);
            _frameWriter.HeadersConfiguration.SetMaxHeaderListSize(int.MaxValue);
            _frameWriter.SetMaxFrameSize(Http2CodecUtil.MaxFrameSizeLowerBound);
            _frameWriter.WriteHeadersAsync(_ctx.Object, streamId, headers, 0, true, _promise);

            byte[] expectedPayload = HeaderPayload(streamId, headers);

            // First frame: HEADER(length=0x4000, flags=0x01)
            Assert.Equal(Http2CodecUtil.MaxFrameSizeLowerBound, _outbound.ReadUnsignedMedium());
            Assert.Equal(0x01, _outbound.ReadByte());
            Assert.Equal(0x01, _outbound.ReadByte());
            Assert.Equal(streamId, _outbound.ReadInt());

            byte[] firstPayload = new byte[Http2CodecUtil.MaxFrameSizeLowerBound];
            _outbound.ReadBytes(firstPayload);

            int remainPayloadLength = expectedPayload.Length - Http2CodecUtil.MaxFrameSizeLowerBound;
            // Second frame: CONTINUATION(length=remainPayloadLength, flags=0x04)
            Assert.Equal(remainPayloadLength, _outbound.ReadUnsignedMedium());
            Assert.Equal(0x09, _outbound.ReadByte());
            Assert.Equal(0x04, _outbound.ReadByte());
            Assert.Equal(streamId, _outbound.ReadInt());

            byte[] secondPayload = new byte[remainPayloadLength];
            _outbound.ReadBytes(secondPayload);

            Assert.True(PlatformDependent.ByteArrayEquals(expectedPayload, 0, firstPayload, 0, firstPayload.Length));
            Assert.True(PlatformDependent.ByteArrayEquals(expectedPayload, firstPayload.Length, secondPayload, 0, secondPayload.Length));
        }

        [Fact]
        public void WriteLargeHeaderWithPadding()
        {
            int streamId = 1;
            IHttp2Headers headers = new DefaultHttp2Headers()
            {
                Method = HttpMethod.Get.AsciiName,
                Path = (AsciiString)"/",
                Authority = (AsciiString)"foo.com",
                Scheme = (AsciiString)"https"
            };
            headers = DummyHeaders(headers, 20);

            _http2HeadersEncoder.Configuration.SetMaxHeaderListSize(int.MaxValue);
            _frameWriter.HeadersConfiguration.SetMaxHeaderListSize(int.MaxValue);
            _frameWriter.SetMaxFrameSize(Http2CodecUtil.MaxFrameSizeLowerBound);
            _frameWriter.WriteHeadersAsync(_ctx.Object, streamId, headers, 5, true, _promise);

            byte[] expectedPayload = BuildLargeHeaderPayload(streamId, headers, (byte)4,
                    Http2CodecUtil.MaxFrameSizeLowerBound);

            // First frame: HEADER(length=0x4000, flags=0x09)
            Assert.Equal(Http2CodecUtil.MaxFrameSizeLowerBound,
                    _outbound.ReadUnsignedMedium());
            Assert.Equal(0x01, _outbound.ReadByte());
            Assert.Equal(0x09, _outbound.ReadByte()); // 0x01 + 0x08
            Assert.Equal(streamId, _outbound.ReadInt());

            byte[] firstPayload = new byte[Http2CodecUtil.MaxFrameSizeLowerBound];
            _outbound.ReadBytes(firstPayload);

            int remainPayloadLength = expectedPayload.Length - Http2CodecUtil.MaxFrameSizeLowerBound;
            // Second frame: CONTINUATION(length=remainPayloadLength, flags=0x04)
            Assert.Equal(remainPayloadLength, _outbound.ReadUnsignedMedium());
            Assert.Equal(0x09, _outbound.ReadByte());
            Assert.Equal(0x04, _outbound.ReadByte());
            Assert.Equal(streamId, _outbound.ReadInt());

            byte[] secondPayload = new byte[remainPayloadLength];
            _outbound.ReadBytes(secondPayload);

            Assert.Equal(expectedPayload.Slice(0, firstPayload.Length), firstPayload);
            Assert.Equal(expectedPayload.Slice(firstPayload.Length, expectedPayload.Length - firstPayload.Length), secondPayload);
        }

        [Fact]
        public void WriteFrameZeroPayload()
        {
            _frameWriter.WriteFrameAsync(_ctx.Object, (Http2FrameTypes)0xf, 0, new Http2Flags(), Unpooled.Empty, _promise);

            byte[] expectedFrameBytes = {
                (byte) 0x00, (byte) 0x00, (byte) 0x00, // payload length
                (byte) 0x0f, // payload type
                (byte) 0x00, // flags
                (byte) 0x00, (byte) 0x00, (byte) 0x00, (byte) 0x00 // stream id
            };

            _expectedOutbound = Unpooled.WrappedBuffer(expectedFrameBytes);
            Assert.Equal(_expectedOutbound, _outbound);
        }

        [Fact]
        public void WriteFrameHasPayload()
        {
            byte[] payload = { (byte)0x01, (byte)0x03, (byte)0x05, (byte)0x07, (byte)0x09 };

            // will auto release after frameWriter.writeFrame succeed
            var payloadByteBuf = Unpooled.WrappedBuffer(payload);
            _frameWriter.WriteFrameAsync(_ctx.Object, (Http2FrameTypes)0xf, 0, new Http2Flags(), payloadByteBuf, _promise);

            byte[] expectedFrameHeaderBytes = {
                (byte) 0x00, (byte) 0x00, (byte) 0x05, // payload length
                (byte) 0x0f, // payload type
                (byte) 0x00, // flags
                (byte) 0x00, (byte) 0x00, (byte) 0x00, (byte) 0x00 // stream id
            };
            _expectedOutbound = Unpooled.CopiedBuffer(expectedFrameHeaderBytes, payload);
            Assert.Equal(_expectedOutbound, _outbound);
        }

        private byte[] HeaderPayload(int streamId, IHttp2Headers headers, byte padding)
        {
            if (padding == 0)
            {
                return HeaderPayload(streamId, headers);
            }

            var outputStream = new MemoryStream();
            try
            {
                outputStream.WriteByte(padding);
                var bts = HeaderPayload(streamId, headers);
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
                _http2HeadersEncoder.EncodeHeaders(streamId, headers, byteBuf);
                byte[] bytes = new byte[byteBuf.ReadableBytes];
                byteBuf.ReadBytes(bytes);
                return bytes;
            }
            finally
            {
                byteBuf.Release();
            }
        }

        private byte[] BuildLargeHeaderPayload(int streamId, IHttp2Headers headers, byte padding, int maxFrameSize)
        {
            var outputStream = new MemoryStream();
            var bw = new BinaryWriter(outputStream);
            try
            {
                bw.Write(padding);
                byte[] payload = HeaderPayload(streamId, headers);
                int firstPayloadSize = maxFrameSize - (padding + 1); //1 for padding length
                outputStream.Write(payload, 0, firstPayloadSize);
#if DESKTOPCLR
                outputStream.Write(new byte[padding], 0, padding);
#else
                outputStream.Write(new byte[padding]);
#endif
                outputStream.Write(payload, firstPayloadSize, payload.Length - firstPayloadSize);
                return outputStream.ToArray();
            }
            finally
            {
                outputStream.Close();
                bw.Dispose();
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
