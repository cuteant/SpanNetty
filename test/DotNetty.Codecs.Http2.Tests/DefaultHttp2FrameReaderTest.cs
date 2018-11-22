
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using Moq;
    using Xunit;

    public class DefaultHttp2FrameReaderTest : IDisposable
    {
        private Mock<IHttp2FrameListener> listener;

        private Mock<IChannelHandlerContext> ctx;

        private DefaultHttp2FrameReader frameReader;

        // Used to generate frame
        private HpackEncoder hpackEncoder;

        public DefaultHttp2FrameReaderTest()
        {
            this.listener = new Mock<IHttp2FrameListener>();
            this.ctx = new Mock<IChannelHandlerContext>();
            this.ctx.Setup(x => x.Allocator).Returns(UnpooledByteBufferAllocator.Default);

            this.frameReader = new DefaultHttp2FrameReader();
            this.hpackEncoder = new HpackEncoder();
        }

        public void Dispose()
        {
            this.frameReader.Close();
        }

        [Fact]
        public void ReadHeaderFrame()
        {
            int streamId = 1;

            var input = Unpooled.Buffer();
            try
            {
                var headers = new DefaultHttp2Headers
                {
                    Authority = (AsciiString)"foo",
                    Method = (AsciiString)"get",
                    Path = (AsciiString)"/",
                    Scheme = (AsciiString)"https"
                };
                Http2Flags flags = new Http2Flags().EndOfHeaders(true).EndOfStream(true);
                this.WriteHeaderFrame(input, streamId, headers, flags);
                this.frameReader.ReadFrame(this.ctx.Object, input, this.listener.Object);

                this.listener.Verify(x => x.OnHeadersRead(this.ctx.Object, 1, headers, 0, true));
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void ReadHeaderFrameAndContinuationFrame()
        {
            int streamId = 1;

            var input = Unpooled.Buffer();
            try
            {
                var headers = new DefaultHttp2Headers
                {
                    Authority = (AsciiString)"foo",
                    Method = (AsciiString)"get",
                    Path = (AsciiString)"/",
                    Scheme = (AsciiString)"https"
                };
                this.WriteHeaderFrame(input, streamId, headers,
                        new Http2Flags().EndOfHeaders(false).EndOfStream(true));
                var headers0 = new DefaultHttp2Headers();
                headers0.Add((AsciiString)"foo", (AsciiString)"bar");
                this.WriteContinuationFrame(input, streamId, headers0,
                        new Http2Flags().EndOfHeaders(true));

                this.frameReader.ReadFrame(this.ctx.Object, input, this.listener.Object);
                headers.Add((AsciiString)"foo", (AsciiString)"bar");
                this.listener.Verify(x => x.OnHeadersRead(this.ctx.Object, 1, headers, 0, true));
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void ReadUnknownFrame()
        {
            var input = Unpooled.Buffer();
            var payload = Unpooled.Buffer();
            try
            {
                payload.WriteByte(1);

                Http2CodecUtil.WriteFrameHeader(input, payload.ReadableBytes, (Http2FrameTypes)0xff, new Http2Flags(), 0);
                input.WriteBytes(payload);
                this.frameReader.ReadFrame(this.ctx.Object, input, this.listener.Object);

                this.listener.Verify(x => x.OnUnknownFrame(this.ctx.Object, (Http2FrameTypes)0xff, 0, new Http2Flags(), payload.Slice(0, 1)));
            }
            finally
            {
                payload.Release();
                input.Release();
            }
        }

        [Fact]
        public void FailedWhenUnknownFrameInMiddleOfHeaderBlock()
        {
            int streamId = 1;

            var input = Unpooled.Buffer();
            try
            {
                var headers = new DefaultHttp2Headers
                {
                    Authority = (AsciiString)"foo",
                    Method = (AsciiString)"get",
                    Path = (AsciiString)"/",
                    Scheme = (AsciiString)"https"
                };
                Http2Flags flags = new Http2Flags().EndOfHeaders(false).EndOfStream(true);
                this.WriteHeaderFrame(input, streamId, headers, flags);
                Http2CodecUtil.WriteFrameHeader(input, 0, (Http2FrameTypes)0xff, new Http2Flags(), streamId);
                Assert.Throws<Http2Exception>(() => this.frameReader.ReadFrame(this.ctx.Object, input, this.listener.Object));
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void FailedWhenContinuationFrameStreamIdMismatch()
        {
            var input = Unpooled.Buffer();
            try
            {
                var headers = new DefaultHttp2Headers
                {
                    Authority = (AsciiString)"foo",
                    Method = (AsciiString)"get",
                    Path = (AsciiString)"/",
                    Scheme = (AsciiString)"https"
                };
                this.WriteHeaderFrame(input, 1, headers,
                                 new Http2Flags().EndOfHeaders(false).EndOfStream(true));
                var headers0 = new DefaultHttp2Headers();
                headers0.Add((AsciiString)"foo", (AsciiString)"bar");
                this.WriteContinuationFrame(input, 3, headers0,
                        new Http2Flags().EndOfHeaders(true));
                Assert.Throws<Http2Exception>(() => frameReader.ReadFrame(this.ctx.Object, input, this.listener.Object));
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void FailedWhenContinuationFrameNotFollowHeaderFrame()
        {
            var input = Unpooled.Buffer();
            try
            {
                var headers0 = new DefaultHttp2Headers();
                headers0.Add((AsciiString)"foo", (AsciiString)"bar");
                this.WriteContinuationFrame(input, 1, headers0,
                                       new Http2Flags().EndOfHeaders(true));
                Assert.Throws<Http2Exception>(() => frameReader.ReadFrame(this.ctx.Object, input, this.listener.Object));
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void FailedWhenHeaderFrameDependsOnItself()
        {
            var input = Unpooled.Buffer();
            try
            {
                var headers = new DefaultHttp2Headers
                {
                    Authority = (AsciiString)"foo",
                    Method = (AsciiString)"get",
                    Path = (AsciiString)"/",
                    Scheme = (AsciiString)"https"
                };
                this.WriteHeaderFramePriorityPresent(
                        input, 1, headers,
                        new Http2Flags().EndOfHeaders(true).EndOfStream(true).PriorityPresent(true),
                        1, 10);
                Assert.Throws<StreamException>(() => frameReader.ReadFrame(this.ctx.Object, input, this.listener.Object));
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void ReadHeaderAndData()
        {
            var input = Unpooled.Buffer();
            var dataPayload = Unpooled.Buffer();
            try
            {
                var headers = new DefaultHttp2Headers
                {
                    Authority = (AsciiString)"foo",
                    Method = (AsciiString)"get",
                    Path = (AsciiString)"/",
                    Scheme = (AsciiString)"https"
                };
                dataPayload.WriteByte(1);
                this.WriteHeaderFrameWithData(input, 1, headers, dataPayload);

                frameReader.ReadFrame(this.ctx.Object, input, this.listener.Object);

                this.listener.Verify(x => x.OnHeadersRead(this.ctx.Object, 1, headers, 0, false));
                this.listener.Verify(x => x.OnDataRead(this.ctx.Object, 1, dataPayload.Slice(0, 1), 0, true));
            }
            finally
            {
                input.Release();
                dataPayload.Release();
            }
        }

        [Fact]
        public void FailedWhenDataFrameNotAssociateWithStream()
        {
            var input = Unpooled.Buffer();
            var payload = Unpooled.Buffer();
            try
            {
                payload.WriteByte(1);

                Http2CodecUtil.WriteFrameHeader(input, payload.ReadableBytes, Http2FrameTypes.Data, new Http2Flags().EndOfStream(true), 0);
                input.WriteBytes(payload);
                Assert.Throws<Http2Exception>(() => frameReader.ReadFrame(this.ctx.Object, input, this.listener.Object));
            }
            finally
            {
                payload.Release();
                input.Release();
            }
        }

        [Fact]
        public void ReadPriorityFrame()
        {
            var input = Unpooled.Buffer();
            try
            {
                WritePriorityFrame(input, 1, 0, 10);
                frameReader.ReadFrame(this.ctx.Object, input, this.listener.Object);
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void FailedWhenPriorityFrameDependsOnItself()
        {
            var input = Unpooled.Buffer();
            try
            {
                WritePriorityFrame(input, 1, 1, 10);
                Assert.Throws<StreamException>(() => frameReader.ReadFrame(this.ctx.Object, input, this.listener.Object));
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void FailedWhenWindowUpdateFrameWithZeroDelta()
        {
            var input = Unpooled.Buffer();
            try
            {
                Http2CodecUtil.WriteFrameHeader(input, 4, Http2FrameTypes.WindowUpdate, new Http2Flags(), 0);
                input.WriteInt(0);
                Assert.Throws<Http2Exception>(() => frameReader.ReadFrame(this.ctx.Object, input, this.listener.Object));
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void ReadSettingsFrame()
        {
            var input = Unpooled.Buffer();
            try
            {
                Http2CodecUtil.WriteFrameHeader(input, 6, Http2FrameTypes.Settings, new Http2Flags(), 0);
                input.WriteShort(Http2CodecUtil.SettingsMaxHeaderListSize);
                input.WriteInt(1024);
                frameReader.ReadFrame(this.ctx.Object, input, this.listener.Object);

                this.listener.Object.OnSettingsRead(this.ctx.Object, new Http2Settings().MaxHeaderListSize(1024));
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void ReadAckSettingsFrame()
        {
            var input = Unpooled.Buffer();
            try
            {
                Http2CodecUtil.WriteFrameHeader(input, 0, Http2FrameTypes.Settings, new Http2Flags().Ack(true), 0);
                frameReader.ReadFrame(this.ctx.Object, input, this.listener.Object);

                this.listener.Object.OnSettingsAckRead(this.ctx.Object);
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void FailedWhenSettingsFrameOnNonZeroStream()
        {
            var input = Unpooled.Buffer();
            try
            {
                Http2CodecUtil.WriteFrameHeader(input, 6, Http2FrameTypes.Settings, new Http2Flags(), 1);
                input.WriteShort(Http2CodecUtil.SettingsMaxHeaderListSize);
                input.WriteInt(1024);
                Assert.Throws<Http2Exception>(() => frameReader.ReadFrame(this.ctx.Object, input, this.listener.Object));
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void FailedWhenAckSettingsFrameWithPayload()
        {
            var input = Unpooled.Buffer();
            try
            {
                Http2CodecUtil.WriteFrameHeader(input, 1, Http2FrameTypes.Settings, new Http2Flags().Ack(true), 0);
                input.WriteByte(1);
                Assert.Throws<Http2Exception>(() => frameReader.ReadFrame(this.ctx.Object, input, this.listener.Object));
            }
            finally
            {
                input.Release();
            }
        }

        [Fact]
        public void FailedWhenSettingsFrameWithWrongPayloadLength()
        {
            var input = Unpooled.Buffer();
            try
            {
                Http2CodecUtil.WriteFrameHeader(input, 8, Http2FrameTypes.Settings, new Http2Flags(), 0);
                input.WriteInt(Http2CodecUtil.SettingsMaxHeaderListSize);
                input.WriteInt(1024);
                Assert.Throws<Http2Exception>(() => frameReader.ReadFrame(this.ctx.Object, input, this.listener.Object));
            }
            finally
            {
                input.Release();
            }
        }

        private void WriteHeaderFrame(IByteBuffer output, int streamId, IHttp2Headers headers, Http2Flags flags)
        {
            IByteBuffer headerBlock = Unpooled.Buffer();
            try
            {
                hpackEncoder.EncodeHeaders(streamId, headerBlock, headers, NeverSensitiveDetector.Instance);
                Http2CodecUtil.WriteFrameHeader(output, headerBlock.ReadableBytes, Http2FrameTypes.Headers, flags, streamId);
                output.WriteBytes(headerBlock, headerBlock.ReadableBytes);
            }
            finally
            {
                headerBlock.Release();
            }
        }

        private void WriteHeaderFrameWithData(IByteBuffer output, int streamId, IHttp2Headers headers, IByteBuffer dataPayload)
        {
            IByteBuffer headerBlock = Unpooled.Buffer();
            try
            {
                hpackEncoder.EncodeHeaders(streamId, headerBlock, headers, NeverSensitiveDetector.Instance);
                Http2CodecUtil.WriteFrameHeader(output, headerBlock.ReadableBytes, Http2FrameTypes.Headers,
                        new Http2Flags().EndOfHeaders(true), streamId);
                output.WriteBytes(headerBlock, headerBlock.ReadableBytes);

                Http2CodecUtil.WriteFrameHeader(output, dataPayload.ReadableBytes, Http2FrameTypes.Data, new Http2Flags().EndOfStream(true), streamId);
                output.WriteBytes(dataPayload);
            }
            finally
            {
                headerBlock.Release();
            }
        }

        private void WriteHeaderFramePriorityPresent(IByteBuffer output, int streamId, IHttp2Headers headers, Http2Flags flags, int streamDependency, int weight)
        {
            IByteBuffer headerBlock = Unpooled.Buffer();
            try
            {
                headerBlock.WriteInt(streamDependency);
                headerBlock.WriteByte(weight - 1);
                hpackEncoder.EncodeHeaders(streamId, headerBlock, headers, NeverSensitiveDetector.Instance);
                Http2CodecUtil.WriteFrameHeader(output, headerBlock.ReadableBytes, Http2FrameTypes.Headers, flags, streamId);
                output.WriteBytes(headerBlock, headerBlock.ReadableBytes);
            }
            finally
            {
                headerBlock.Release();
            }
        }

        private void WriteContinuationFrame(IByteBuffer output, int streamId, IHttp2Headers headers, Http2Flags flags)
        {
            IByteBuffer headerBlock = Unpooled.Buffer();
            try
            {
                hpackEncoder.EncodeHeaders(streamId, headerBlock, headers, NeverSensitiveDetector.Instance);
                Http2CodecUtil.WriteFrameHeader(output, headerBlock.ReadableBytes, Http2FrameTypes.Continuation, flags, streamId);
                output.WriteBytes(headerBlock, headerBlock.ReadableBytes);
            }
            finally
            {
                headerBlock.Release();
            }
        }

        private static void WritePriorityFrame(IByteBuffer output, int streamId, int streamDependency, int weight)
        {
            Http2CodecUtil.WriteFrameHeader(output, 5, Http2FrameTypes.Priority, new Http2Flags(), streamId);
            output.WriteInt(streamDependency);
            output.WriteByte(weight - 1);
        }
    }
}
