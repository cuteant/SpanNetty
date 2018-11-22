
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using Moq;
    using Xunit;

    /**
     * Tests encoding/decoding each HTTP2 frame type.
     */
    public class Http2FrameRoundtripTest : IDisposable
    {
        private static readonly byte[] MESSAGE = Encoding.UTF8.GetBytes("hello world");
        private const int STREAM_ID = 0x7FFFFFFF;
        private const int WINDOW_UPDATE = 0x7FFFFFFF;
        private const Http2Error ERROR_CODE = (Http2Error)0xFFFFFFFFL;

        private Mock<IHttp2FrameListener> listener;
        private Mock<IChannelHandlerContext> ctx;
        private Mock<IEventExecutor> executor;
        private Mock<IChannel> channel;
        private Mock<IByteBufferAllocator> alloc;

        private IHttp2FrameWriter writer;
        private IHttp2FrameReader reader;
        private readonly List<IByteBuffer> needReleasing = new List<IByteBuffer>();

        public Http2FrameRoundtripTest()
        {
            this.alloc = new Mock<IByteBufferAllocator>();
            this.alloc.Setup(x => x.Buffer()).Returns(() => Unpooled.Buffer());
            this.alloc.Setup(x => x.Buffer(It.IsAny<int>())).Returns<int>(c => Unpooled.Buffer(c));
            this.channel = new Mock<IChannel>();
            this.executor = new Mock<IEventExecutor>();
            this.listener = new Mock<IHttp2FrameListener>();
            this.ctx = new Mock<IChannelHandlerContext>();
            this.ctx.Setup(x => x.Allocator).Returns(this.alloc.Object);
            this.ctx.Setup(x => x.Executor).Returns(this.executor.Object);
            this.ctx.Setup(x => x.Channel).Returns(this.channel.Object);
            this.ctx.Setup(x => x.NewPromise()).Returns(() => new TaskCompletionSource());

            this.writer = new DefaultHttp2FrameWriter(new DefaultHttp2HeadersEncoder(NeverSensitiveDetector.Instance, Http2TestUtil.NewTestEncoder()));
            this.reader = new DefaultHttp2FrameReader(new DefaultHttp2HeadersDecoder(false, Http2TestUtil.NewTestDecoder()));
        }

        public void Dispose()
        {
            try
            {
                // Release all of the buffers.
                foreach (var buf in this.needReleasing)
                {
                    buf.Release();
                }
                // Now verify that all of the reference counts are zero.
                foreach (var buf in this.needReleasing)
                {
                    int expectedFinalRefCount = 0;
                    if (/*buf.isReadOnly() ||*/ buf is EmptyByteBuffer || buf is UnreleasableByteBuffer)
                    {
                        // Special case for when we're writing slices of the padding buffer.
                        expectedFinalRefCount = 1;
                    }
                    Assert.Equal(expectedFinalRefCount, buf.ReferenceCount);
                }
            }
            finally
            {
                needReleasing.Clear();
            }
        }

        [Fact]
        public void EmptyDataShouldMatch()
        {
            var data = Unpooled.Empty;
            this.writer.WriteDataAsync(this.ctx.Object, STREAM_ID, data.Slice(), 0, false, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnDataRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<IByteBuffer>(d => d.Equals(data)),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == false)));
        }

        [Fact]
        public void DataShouldMatch()
        {
            var data = Data(10);
            this.writer.WriteDataAsync(this.ctx.Object, STREAM_ID, data.Slice(), 1, false, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnDataRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<IByteBuffer>(d => d.Equals(data)),
                    It.Is<int>(v => v == 1),
                    It.Is<bool>(v => v == false)));
        }

        [Fact]
        public void DataWithPaddingShouldMatch()
        {
            var data = Data(10);
            this.writer.WriteDataAsync(this.ctx.Object, STREAM_ID, data.Slice(), Http2CodecUtil.MaxPadding, true, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnDataRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<IByteBuffer>(d => d.Equals(data)),
                    It.Is<int>(v => v == Http2CodecUtil.MaxPadding),
                    It.Is<bool>(v => v == true)));
        }


        [Fact]
        public void LargeDataFrameShouldMatch()
        {
            // Create a large message to force chunking.
            var originalData = Data(1024 * 1024);
            int originalPadding = 100;
            var endOfStream = true;

            writer.WriteDataAsync(this.ctx.Object, STREAM_ID, originalData.Slice(), originalPadding,
                    endOfStream, this.ctx.Object.NewPromise());
            this.ReadFrames();

            // Verify that at least one frame was sent with eos=false and exactly one with eos=true.
            this.listener.Verify(
                x => x.OnDataRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<int>(),
                    It.Is<bool>(v => v == false)),
                Times.AtLeastOnce());
            this.listener.Verify(
                x => x.OnDataRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.IsAny<IByteBuffer>(),
                    It.IsAny<int>(),
                    It.Is<bool>(v => v == true)));

            // Capture the read data and padding.
            var dataCaptor = new ArgumentCaptor<IByteBuffer>();
            var paddingCaptor = new ArgumentCaptor<int>();
            this.listener.Verify(
                x => x.OnDataRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<IByteBuffer>(d => dataCaptor.Capture(d)),
                    It.Is<int>(v => paddingCaptor.Capture(v)),
                    It.IsAny<bool>()),
                Times.AtLeastOnce());

            // Make sure the data matches the original.
            foreach (var chunk in dataCaptor.GetAllValues())
            {
                var originalChunk = originalData.ReadSlice(chunk.ReadableBytes);
                Assert.Equal(originalChunk, chunk);
            }
            Assert.False(originalData.IsReadable());

            // Make sure the padding matches the original.
            int totalReadPadding = 0;
            foreach (int framePadding in paddingCaptor.GetAllValues())
            {
                totalReadPadding += framePadding;
            }
            Assert.Equal(originalPadding, totalReadPadding);
        }

        [Fact]
        public void EmptyHeadersShouldMatch()
        {
            IHttp2Headers headers = EmptyHttp2Headers.Instance;
            writer.WriteHeadersAsync(this.ctx.Object, STREAM_ID, headers, 0, true, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnHeadersRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<IHttp2Headers>(h => h.Equals(headers)),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true)));
        }

        [Fact]
        public void EmptyHeadersWithPaddingShouldMatch()
        {
            IHttp2Headers headers = EmptyHttp2Headers.Instance;
            writer.WriteHeadersAsync(this.ctx.Object, STREAM_ID, headers, Http2CodecUtil.MaxPadding, true, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnHeadersRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<IHttp2Headers>(h => h.Equals(headers)),
                    It.Is<int>(v => v == Http2CodecUtil.MaxPadding),
                    It.Is<bool>(v => v == true)));
        }

        [Fact]
        public void BinaryHeadersWithoutPriorityShouldMatch()
        {
            IHttp2Headers headers = BinaryHeaders();
            writer.WriteHeadersAsync(this.ctx.Object, STREAM_ID, headers, 0, true, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnHeadersRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<IHttp2Headers>(h => h.Equals(headers)),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true)));
        }

        [Fact]
        public void HeadersFrameWithoutPriorityShouldMatch()
        {
            IHttp2Headers headers = Headers();
            writer.WriteHeadersAsync(this.ctx.Object, STREAM_ID, headers, 0, true, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnHeadersRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<IHttp2Headers>(h => h.Equals(headers)),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true)));
        }

        [Fact]
        public void HeadersFrameWithPriorityShouldMatch()
        {
            IHttp2Headers headers = Headers();
            writer.WriteHeadersAsync(this.ctx.Object, STREAM_ID, headers, 4, (short)255, true, 0, true, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnHeadersRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<IHttp2Headers>(h => h.Equals(headers)),
                    It.Is<int>(v => v == 4),
                    It.Is<short>(v => v == 255),
                    It.Is<bool>(v => v == true),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true)));
        }

        [Fact]
        public void HeadersWithPaddingWithoutPriorityShouldMatch()
        {
            IHttp2Headers headers = Headers();
            writer.WriteHeadersAsync(this.ctx.Object, STREAM_ID, headers, Http2CodecUtil.MaxPadding, true, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnHeadersRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<IHttp2Headers>(h => h.Equals(headers)),
                    It.Is<int>(v => v == Http2CodecUtil.MaxPadding),
                    It.Is<bool>(v => v == true)));
        }

        [Fact]
        public void HeadersWithPaddingWithPriorityShouldMatch()
        {
            IHttp2Headers headers = Headers();
            writer.WriteHeadersAsync(this.ctx.Object, STREAM_ID, headers, 2, (short)3, true, 1, true, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnHeadersRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<IHttp2Headers>(h => h.Equals(headers)),
                    It.Is<int>(v => v == 2),
                    It.Is<short>(v => v == 3),
                    It.Is<bool>(v => v == true),
                    It.Is<int>(v => v == 1),
                    It.Is<bool>(v => v == true)));
        }

        [Fact]
        public void ContinuedHeadersShouldMatch()
        {
            IHttp2Headers headers = LargeHeaders();
            writer.WriteHeadersAsync(this.ctx.Object, STREAM_ID, headers, 2, (short)3, true, 0, true, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnHeadersRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<IHttp2Headers>(h => h.Equals(headers)),
                    It.Is<int>(v => v == 2),
                    It.Is<short>(v => v == 3),
                    It.Is<bool>(v => v == true),
                    It.Is<int>(v => v == 0),
                    It.Is<bool>(v => v == true)));
        }

        [Fact]
        public void ContinuedHeadersWithPaddingShouldMatch()
        {
            IHttp2Headers headers = LargeHeaders();
            writer.WriteHeadersAsync(this.ctx.Object, STREAM_ID, headers, 2, (short)3, true, Http2CodecUtil.MaxPadding, true, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnHeadersRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<IHttp2Headers>(h => h.Equals(headers)),
                    It.Is<int>(v => v == 2),
                    It.Is<short>(v => v == 3),
                    It.Is<bool>(v => v == true),
                    It.Is<int>(v => v == Http2CodecUtil.MaxPadding),
                    It.Is<bool>(v => v == true)));
        }

        [Fact]
        public void HeadersThatAreTooBigShouldFail()
        {
            this.reader = new DefaultHttp2FrameReader(false);
            int maxListSize = 100;
            this.reader.Configuration.HeadersConfiguration.SetMaxHeaderListSize(maxListSize, maxListSize);
            IHttp2Headers headers = HeadersOfSize(maxListSize + 1);
            writer.WriteHeadersAsync(this.ctx.Object, STREAM_ID, headers, 2, (short)3, true, Http2CodecUtil.MaxPadding, true, this.ctx.Object.NewPromise());

            Assert.Throws<HeaderListSizeException>(() => this.ReadFrames());

            this.listener.Verify(
                x => x.OnHeadersRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.IsAny<int>(),
                    It.IsAny<IHttp2Headers>(),
                    It.IsAny<int>(),
                    It.IsAny<short>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>()),
                Times.Never());
        }

        [Fact]
        public void EmptyPushPromiseShouldMatch()
        {
            IHttp2Headers headers = EmptyHttp2Headers.Instance;
            writer.WritePushPromiseAsync(this.ctx.Object, STREAM_ID, 2, headers, 0, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnPushPromiseRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<int>(v => v == 2),
                    It.Is<IHttp2Headers>(h => h.Equals(headers)),
                    It.Is<int>(v => v == 0)));
        }

        [Fact]
        public void PushPromiseFrameShouldMatch()
        {
            IHttp2Headers headers = Headers();
            writer.WritePushPromiseAsync(this.ctx.Object, STREAM_ID, 1, headers, 5, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnPushPromiseRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<int>(v => v == 1),
                    It.Is<IHttp2Headers>(h => h.Equals(headers)),
                    It.Is<int>(v => v == 5)));
        }

        [Fact]
        public void PushPromiseWithPaddingShouldMatch()
        {
            IHttp2Headers headers = Headers();
            writer.WritePushPromiseAsync(this.ctx.Object, STREAM_ID, 2, headers, Http2CodecUtil.MaxPadding, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnPushPromiseRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<int>(v => v == 2),
                    It.Is<IHttp2Headers>(h => h.Equals(headers)),
                    It.Is<int>(v => v == Http2CodecUtil.MaxPadding)));
        }

        [Fact]
        public void ContinuedPushPromiseShouldMatch()
        {
            IHttp2Headers headers = LargeHeaders();
            writer.WritePushPromiseAsync(this.ctx.Object, STREAM_ID, 2, headers, 0, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnPushPromiseRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<int>(v => v == 2),
                    It.Is<IHttp2Headers>(h => h.Equals(headers)),
                    It.Is<int>(v => v == 0)));
        }

        [Fact]
        public void ContinuedPushPromiseWithPaddingShouldMatch()
        {
            IHttp2Headers headers = LargeHeaders();
            writer.WritePushPromiseAsync(this.ctx.Object, STREAM_ID, 2, headers, 0xFF, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnPushPromiseRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<int>(v => v == 2),
                    It.Is<IHttp2Headers>(h => h.Equals(headers)),
                    It.Is<int>(v => v == 0xFF)));
        }

        [Fact]
        public void GoAwayFrameShouldMatch()
        {
            string text = "test";
            var data = Buf(Encoding.UTF8.GetBytes(text));

            writer.WriteGoAwayAsync(this.ctx.Object, STREAM_ID, ERROR_CODE, data.Slice(), this.ctx.Object.NewPromise());
            this.ReadFrames();

            var captor = new ArgumentCaptor<IByteBuffer>();
            this.listener.Verify(
                x => x.OnGoAwayRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<Http2Error>(v => v == ERROR_CODE),
                    It.Is<IByteBuffer>(v => captor.Capture(v))));
            Assert.Equal(data, captor.GetValue());
        }

        [Fact]
        public void PingFrameShouldMatch()
        {
            writer.WritePingAsync(this.ctx.Object, false, 1234567, this.ctx.Object.NewPromise());
            this.ReadFrames();

            var captor = new ArgumentCaptor<long>();
            this.listener.Verify(
                x => x.OnPingRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<long>(v => captor.Capture(v))));
            Assert.Equal(1234567, captor.GetValue());
        }

        [Fact]
        public void PingAckFrameShouldMatch()
        {
            writer.WritePingAsync(this.ctx.Object, true, 1234567, this.ctx.Object.NewPromise());
            this.ReadFrames();

            var captor = new ArgumentCaptor<long>();
            this.listener.Verify(
                x => x.OnPingAckRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<long>(v => captor.Capture(v))));
            Assert.Equal(1234567, captor.GetValue());
        }

        [Fact]
        public void PriorityFrameShouldMatch()
        {
            writer.WritePriorityAsync(this.ctx.Object, STREAM_ID, 1, (short)1, true, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnPriorityRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<int>(v => v == 1),
                    It.Is<short>(v => v == 1),
                    It.Is<bool>(v => v == true)));
        }

        [Fact]
        public void RstStreamFrameShouldMatch()
        {
            writer.WriteRstStreamAsync(this.ctx.Object, STREAM_ID, ERROR_CODE, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnRstStreamRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<Http2Error>(v => v == ERROR_CODE)));
        }

        [Fact]
        public void EmptySettingsFrameShouldMatch()
        {
            Http2Settings settings = new Http2Settings();
            writer.WriteSettingsAsync(this.ctx.Object, settings, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnSettingsRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<Http2Settings>(v => v.SequenceEqual(settings))));
        }

        [Fact]
        public void SettingsShouldStripShouldMatch()
        {
            Http2Settings settings = new Http2Settings();
            settings.PushEnabled(true);
            settings.HeaderTableSize(4096);
            settings.InitialWindowSize(123);
            settings.MaxConcurrentStreams(456);

            writer.WriteSettingsAsync(this.ctx.Object, settings, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnSettingsRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<Http2Settings>(v => v.SequenceEqual(settings))));
        }

        [Fact]
        public void SettingsAckShouldMatch()
        {
            writer.WriteSettingsAckAsync(this.ctx.Object, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnSettingsAckRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object)));
        }

        [Fact]
        public void WindowUpdateFrameShouldMatch()
        {
            writer.WriteWindowUpdateAsync(this.ctx.Object, STREAM_ID, WINDOW_UPDATE, this.ctx.Object.NewPromise());
            this.ReadFrames();
            this.listener.Verify(
                x => x.OnWindowUpdateRead(
                    It.Is<IChannelHandlerContext>(c => c == this.ctx.Object),
                    It.Is<int>(id => id == STREAM_ID),
                    It.Is<int>(v => v == WINDOW_UPDATE)));
        }


        private void ReadFrames()
        {
            // Now read all of the written frames.
            var write = this.CaptureWrites();
            this.reader.ReadFrame(this.ctx.Object, write, this.listener.Object);
        }

        private static IByteBuffer Data(int size)
        {
            byte[] data = new byte[size];
            for (int ix = 0; ix < data.Length;)
            {
                int length = Math.Min(MESSAGE.Length, data.Length - ix);
                Array.Copy(MESSAGE, 0, data, ix, length);
                ix += length;
            }
            return Buf(data);
        }

        private static IByteBuffer Buf(byte[] bytes)
        {
            return Unpooled.WrappedBuffer(bytes);
        }

        private IByteBuffer ReleaseLater(IByteBuffer buf)
        {
            this.needReleasing.Add(buf);
            return buf;
        }

        private IByteBuffer CaptureWrites()
        {
            var captor = new ArgumentCaptor<IByteBuffer>();
            this.ctx.Verify(x => x.WriteAsync(It.Is<IByteBuffer>(t => captor.Capture(t)), It.IsAny<IPromise>()), Times.AtLeastOnce());
            var composite = (CompositeByteBuffer)this.ReleaseLater(Unpooled.CompositeBuffer());
            foreach (var item in captor.GetAllValues())
            {
                var buf = this.ReleaseLater((IByteBuffer)item.Retain());
                composite.AddComponent(true, buf);
            }
            return composite;

        }

        private static IHttp2Headers Headers()
        {
            var headers = new DefaultHttp2Headers(false);
            headers.Method = AsciiString.Of("GET");
            headers.Scheme = AsciiString.Of("https");
            headers.Authority = AsciiString.Of("example.org");
            headers.Path = AsciiString.Of("/some/path/resource2");
            headers.Add(Http2TestUtil.RandomString(), Http2TestUtil.RandomString());
            return headers;
        }

        private static IHttp2Headers LargeHeaders()
        {
            DefaultHttp2Headers headers = new DefaultHttp2Headers(false);
            for (int i = 0; i < 100; ++i)
            {
                string key = "this-is-a-test-header-key-" + i;
                string value = "this-is-a-test-header-value-" + i;
                headers.Add(AsciiString.Of(key), AsciiString.Of(value));
            }
            return headers;
        }

        private static IHttp2Headers HeadersOfSize(int minSize)
        {
            AsciiString singleByte = new AsciiString(new byte[] { 0 }, false);
            DefaultHttp2Headers headers = new DefaultHttp2Headers(false);
            for (int size = 0; size < minSize; size += 2)
            {
                headers.Add(singleByte, singleByte);
            }
            return headers;
        }

        private static IHttp2Headers BinaryHeaders()
        {
            DefaultHttp2Headers headers = new DefaultHttp2Headers(false);
            for (int ix = 0; ix < 10; ++ix)
            {
                headers.Add(Http2TestUtil.RandomString(), Http2TestUtil.RandomString());
            }
            return headers;
        }
    }
}
