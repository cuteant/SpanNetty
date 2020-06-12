// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets.Extensions.Compression
{
    using System;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Compression;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Codecs.Http.WebSockets.Extensions;
    using DotNetty.Codecs.Http.WebSockets.Extensions.Compression;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class PerMessageDeflateDecoderTest
    {
        readonly Random _random;

        public PerMessageDeflateDecoderTest()
        {
            _random = new Random();
        }

        [Fact]
        public void CompressedFrame()
        {
            var encoderChannel = new EmbeddedChannel(
                ZlibCodecFactory.NewZlibEncoder(ZlibWrapper.None, 9, 15, 8));
            var decoderChannel = new EmbeddedChannel(new PerMessageDeflateDecoder(false));

            // initialize
            var payload = new byte[300];
            _random.NextBytes(payload);

            Assert.True(encoderChannel.WriteOutbound(Unpooled.WrappedBuffer(payload)));
            var compressedPayload = encoderChannel.ReadOutbound<IByteBuffer>();

            var compressedFrame = new BinaryWebSocketFrame(true,
                WebSocketRsv.Rsv1 | WebSocketRsv.Rsv3,
                compressedPayload.Slice(0, compressedPayload.ReadableBytes - 4));

            // execute
            Assert.True(decoderChannel.WriteInbound(compressedFrame));
            var uncompressedFrame = decoderChannel.ReadInbound<BinaryWebSocketFrame>();

            // test
            Assert.NotNull(uncompressedFrame);
            Assert.NotNull(uncompressedFrame.Content);
            Assert.Equal(WebSocketRsv.Rsv3, uncompressedFrame.Rsv);
            Assert.Equal(300, uncompressedFrame.Content.ReadableBytes);

            var finalPayload = new byte[300];
            uncompressedFrame.Content.ReadBytes(finalPayload);
            Assert.Equal(payload, finalPayload);
            uncompressedFrame.Release();
        }

        [Fact]
        public void NormalFrame()
        {
            var decoderChannel = new EmbeddedChannel(new PerMessageDeflateDecoder(false));

            // initialize
            var payload = new byte[300];
            _random.NextBytes(payload);

            var frame = new BinaryWebSocketFrame(true,
                WebSocketRsv.Rsv3, Unpooled.WrappedBuffer(payload));

            // execute
            Assert.True(decoderChannel.WriteInbound(frame));
            var newFrame = decoderChannel.ReadInbound<BinaryWebSocketFrame>();

            // test
            Assert.NotNull(newFrame);
            Assert.NotNull(newFrame.Content);
            Assert.Equal(WebSocketRsv.Rsv3, newFrame.Rsv);
            Assert.Equal(300, newFrame.Content.ReadableBytes);

            var finalPayload = new byte[300];
            newFrame.Content.ReadBytes(finalPayload);
            Assert.Equal(payload, finalPayload);
            newFrame.Release();
        }

        [Fact]
        public void FragmentedFrame()
        {
            var encoderChannel = new EmbeddedChannel(
                    ZlibCodecFactory.NewZlibEncoder(ZlibWrapper.None, 9, 15, 8));
            var decoderChannel = new EmbeddedChannel(new PerMessageDeflateDecoder(false));

            // initialize
            var payload = new byte[300];
            _random.NextBytes(payload);

            Assert.True(encoderChannel.WriteOutbound(Unpooled.WrappedBuffer(payload)));
            var compressedPayload = encoderChannel.ReadOutbound<IByteBuffer>();
            compressedPayload = compressedPayload.Slice(0, compressedPayload.ReadableBytes - 4);

            int oneThird = compressedPayload.ReadableBytes / 3;
            var compressedFrame1 = new BinaryWebSocketFrame(false,
                    WebSocketRsv.Rsv1 | WebSocketRsv.Rsv3,
                    compressedPayload.Slice(0, oneThird));
            var compressedFrame2 = new ContinuationWebSocketFrame(false,
                    WebSocketRsv.Rsv3, compressedPayload.Slice(oneThird, oneThird));
            var compressedFrame3 = new ContinuationWebSocketFrame(true,
                    WebSocketRsv.Rsv3, compressedPayload.Slice(oneThird * 2,
                            compressedPayload.ReadableBytes - oneThird * 2));

            // execute
            Assert.True(decoderChannel.WriteInbound(compressedFrame1.Retain()));
            Assert.True(decoderChannel.WriteInbound(compressedFrame2.Retain()));
            Assert.True(decoderChannel.WriteInbound(compressedFrame3));
            var uncompressedFrame1 = decoderChannel.ReadInbound<BinaryWebSocketFrame>();
            var uncompressedFrame2 = decoderChannel.ReadInbound<ContinuationWebSocketFrame>();
            var uncompressedFrame3 = decoderChannel.ReadInbound<ContinuationWebSocketFrame>();

            // test
            Assert.NotNull(uncompressedFrame1);
            Assert.NotNull(uncompressedFrame2);
            Assert.NotNull(uncompressedFrame3);
            Assert.Equal(WebSocketRsv.Rsv3, uncompressedFrame1.Rsv);
            Assert.Equal(WebSocketRsv.Rsv3, uncompressedFrame2.Rsv);
            Assert.Equal(WebSocketRsv.Rsv3, uncompressedFrame3.Rsv);

            IByteBuffer finalPayloadWrapped = Unpooled.WrappedBuffer(uncompressedFrame1.Content,
                    uncompressedFrame2.Content, uncompressedFrame3.Content);
            Assert.Equal(300, finalPayloadWrapped.ReadableBytes);

            var finalPayload = new byte[300];
            finalPayloadWrapped.ReadBytes(finalPayload);
            Assert.Equal(payload, finalPayload);
            finalPayloadWrapped.Release();
        }

        [Fact]
        public void MultiCompressedPayloadWithinFrame()
        {
            var encoderChannel = new EmbeddedChannel(
                ZlibCodecFactory.NewZlibEncoder(ZlibWrapper.None, 9, 15, 8));
            var decoderChannel = new EmbeddedChannel(new PerMessageDeflateDecoder(false));

            // initialize
            var payload1 = new byte[100];
            _random.NextBytes(payload1);
            var payload2 = new byte[100];
            _random.NextBytes(payload2);

            Assert.True(encoderChannel.WriteOutbound(Unpooled.WrappedBuffer(payload1)));
            var compressedPayload1 = encoderChannel.ReadOutbound<IByteBuffer>();
            Assert.True(encoderChannel.WriteOutbound(Unpooled.WrappedBuffer(payload2)));
            var compressedPayload2 = encoderChannel.ReadOutbound<IByteBuffer>();

            var compressedFrame = new BinaryWebSocketFrame(true,
                WebSocketRsv.Rsv1 | WebSocketRsv.Rsv3,
                Unpooled.WrappedBuffer(
                    compressedPayload1,
                    compressedPayload2.Slice(0, compressedPayload2.ReadableBytes - 4)));

            // execute
            Assert.True(decoderChannel.WriteInbound(compressedFrame));
            var uncompressedFrame = decoderChannel.ReadInbound<BinaryWebSocketFrame>();

            // test
            Assert.NotNull(uncompressedFrame);
            Assert.NotNull(uncompressedFrame.Content);
            Assert.Equal(WebSocketRsv.Rsv3, uncompressedFrame.Rsv);
            Assert.Equal(200, uncompressedFrame.Content.ReadableBytes);

            var finalPayload1 = new byte[100];
            uncompressedFrame.Content.ReadBytes(finalPayload1);
            Assert.Equal(payload1, finalPayload1);
            var finalPayload2 = new byte[100];
            uncompressedFrame.Content.ReadBytes(finalPayload2);
            Assert.Equal(payload2, finalPayload2);
            uncompressedFrame.Release();
        }

        [Fact]
        public void DecompressionSkipForBinaryFrame()
        {
            EmbeddedChannel encoderChannel = new EmbeddedChannel(
                    ZlibCodecFactory.NewZlibEncoder(ZlibWrapper.None, 9, 15, 8));
            EmbeddedChannel decoderChannel = new EmbeddedChannel(new PerMessageDeflateDecoder(false, AlwaysSkipWebSocketExtensionFilter.Instance));

            byte[] payload = new byte[300];
            _random.NextBytes(payload);

            Assert.True(encoderChannel.WriteOutbound(Unpooled.WrappedBuffer(payload)));
            var compressedPayload = encoderChannel.ReadOutbound<IByteBuffer>();

            BinaryWebSocketFrame compressedBinaryFrame = new BinaryWebSocketFrame(true, WebSocketRsv.Rsv1, compressedPayload);
            Assert.True(decoderChannel.WriteInbound(compressedBinaryFrame));

            var inboundFrame = decoderChannel.ReadInbound<WebSocketFrame>();

            Assert.Equal(WebSocketRsv.Rsv1, inboundFrame.Rsv);
            Assert.Equal(compressedPayload, inboundFrame.Content);
            Assert.True(inboundFrame.Release());

            Assert.True(encoderChannel.FinishAndReleaseAll());
            Assert.False(decoderChannel.Finish());
        }

        [Fact]
        public void SelectivityDecompressionSkip()
        {
            var selectivityDecompressionFilter = new SelectivityDecompressionFilter0();
            EmbeddedChannel encoderChannel = new EmbeddedChannel(
                    ZlibCodecFactory.NewZlibEncoder(ZlibWrapper.None, 9, 15, 8));
            EmbeddedChannel decoderChannel = new EmbeddedChannel(
                    new PerMessageDeflateDecoder(false, selectivityDecompressionFilter));

            string textPayload = "compressed payload";
            byte[] binaryPayload = new byte[300];
            _random.NextBytes(binaryPayload);

            Assert.True(encoderChannel.WriteOutbound(Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes(textPayload))));
            Assert.True(encoderChannel.WriteOutbound(Unpooled.WrappedBuffer(binaryPayload)));
            var compressedTextPayload = encoderChannel.ReadOutbound<IByteBuffer>();
            var compressedBinaryPayload = encoderChannel.ReadOutbound<IByteBuffer>();

            TextWebSocketFrame compressedTextFrame = new TextWebSocketFrame(true, WebSocketRsv.Rsv1, compressedTextPayload);
            BinaryWebSocketFrame compressedBinaryFrame = new BinaryWebSocketFrame(true, WebSocketRsv.Rsv1, compressedBinaryPayload);

            Assert.True(decoderChannel.WriteInbound(compressedTextFrame));
            Assert.True(decoderChannel.WriteInbound(compressedBinaryFrame));

            var inboundTextFrame = decoderChannel.ReadInbound<TextWebSocketFrame>();
            var inboundBinaryFrame = decoderChannel.ReadInbound<BinaryWebSocketFrame>();

            Assert.Equal(WebSocketRsv.Rsv1, inboundTextFrame.Rsv);
            Assert.Equal(compressedTextPayload, inboundTextFrame.Content);
            Assert.True(inboundTextFrame.Release());

            Assert.Equal(0, inboundBinaryFrame.Rsv);
            Assert.Equal(binaryPayload, ByteBufferUtil.GetBytes(inboundBinaryFrame.Content));
            Assert.True(inboundBinaryFrame.Release());

            Assert.True(encoderChannel.FinishAndReleaseAll());
            Assert.False(decoderChannel.Finish());
        }

        [Fact]
        public void IllegalStateWhenDecompressionInProgress()
        {
            var selectivityDecompressionFilter = new SelectivityDecompressionFilter1();
            EmbeddedChannel encoderChannel = new EmbeddedChannel(
                    ZlibCodecFactory.NewZlibEncoder(ZlibWrapper.None, 9, 15, 8));
            EmbeddedChannel decoderChannel = new EmbeddedChannel(
                    new PerMessageDeflateDecoder(false, selectivityDecompressionFilter));

            byte[] firstPayload = new byte[200];
            _random.NextBytes(firstPayload);

            byte[] finalPayload = new byte[50];
            _random.NextBytes(finalPayload);

            Assert.True(encoderChannel.WriteOutbound(Unpooled.WrappedBuffer(firstPayload)));
            Assert.True(encoderChannel.WriteOutbound(Unpooled.WrappedBuffer(finalPayload)));
            var compressedFirstPayload = encoderChannel.ReadOutbound<IByteBuffer>();
            var compressedFinalPayload = encoderChannel.ReadOutbound<IByteBuffer>();
            Assert.True(encoderChannel.FinishAndReleaseAll());

            BinaryWebSocketFrame firstPart = new BinaryWebSocketFrame(false, WebSocketRsv.Rsv1, compressedFirstPayload);
            ContinuationWebSocketFrame finalPart = new ContinuationWebSocketFrame(true, WebSocketRsv.Rsv1, compressedFinalPayload);
            Assert.True(decoderChannel.WriteInbound(firstPart));

            var outboundFirstPart = decoderChannel.ReadInbound<BinaryWebSocketFrame>();
            //first part is decompressed
            Assert.Equal(0, outboundFirstPart.Rsv);
            Assert.Equal(firstPayload, ByteBufferUtil.GetBytes(outboundFirstPart.Content));
            Assert.True(outboundFirstPart.Release());

            //final part throwing exception
            try
            {
                decoderChannel.WriteInbound(finalPart);
                Assert.False(true);
            }
            catch (Exception exc)
            {
                Assert.IsType<DecoderException>(exc);
            }
            finally
            {
                Assert.True(finalPart.Release());
                Assert.False(encoderChannel.FinishAndReleaseAll());
            }
        }

        [Fact]
        public void EmptyFrameDecompression()
        {
            EmbeddedChannel decoderChannel = new EmbeddedChannel(new PerMessageDeflateDecoder(false));

            TextWebSocketFrame emptyDeflateBlockFrame = new TextWebSocketFrame(true, WebSocketRsv.Rsv1, DeflateDecoder.EmptyDeflateBlock);

            Assert.True(decoderChannel.WriteInbound(emptyDeflateBlockFrame));
            var emptyBufferFrame = decoderChannel.ReadInbound<TextWebSocketFrame>();

            Assert.False(emptyBufferFrame.Content.IsReadable());

            // Composite empty buffer
            Assert.True(emptyBufferFrame.Release());
            Assert.False(decoderChannel.Finish());
        }

        [Fact]
        public void FragmentedFrameWithLeftOverInLastFragment()
        {
            string hexDump = "677170647a777a737574656b707a787a6f6a7561756578756f6b7868616371716c657a6d64697479766d726f6" +
                             "269746c6376777464776f6f72767a726f64667278676764687775786f6762766d776d706b76697773777a7072" +
                             "6a6a737279707a7078697a6c69616d7461656d646278626d786f66666e686e776a7a7461746d7a776668776b6" +
                             "f6f736e73746575637a6d727a7175707a6e74627578687871767771697a71766c64626d78726d6d7675756877" +
                             "62667963626b687a726d676e646263776e67797264706d6c6863626577616967706a78636a72697464756e627" +
                             "977616f79736475676f76736f7178746a7a7479626c64636b6b6778637768746c62";
            EmbeddedChannel encoderChannel = new EmbeddedChannel(
                    ZlibCodecFactory.NewZlibEncoder(ZlibWrapper.None, 9, 15, 8));
            EmbeddedChannel decoderChannel = new EmbeddedChannel(new PerMessageDeflateDecoder(false));

            IByteBuffer originPayload = Unpooled.WrappedBuffer(StringUtil.DecodeHexDump(hexDump));
            Assert.True(encoderChannel.WriteOutbound(originPayload.Duplicate().Retain()));

            var compressedPayload = encoderChannel.ReadOutbound<IByteBuffer>();
            compressedPayload = compressedPayload.Slice(0, compressedPayload.ReadableBytes - 4);

            int oneThird = compressedPayload.ReadableBytes / 3;

            TextWebSocketFrame compressedFrame1 = new TextWebSocketFrame(
                    false, WebSocketRsv.Rsv1, compressedPayload.Slice(0, oneThird));
            ContinuationWebSocketFrame compressedFrame2 = new ContinuationWebSocketFrame(
                    false, WebSocketRsv.Rsv3, compressedPayload.Slice(oneThird, oneThird));
            ContinuationWebSocketFrame compressedFrame3 = new ContinuationWebSocketFrame(
                    false, WebSocketRsv.Rsv3, compressedPayload.Slice(oneThird * 2, oneThird));
            int offset = oneThird * 3;
            ContinuationWebSocketFrame compressedFrameWithExtraData = new ContinuationWebSocketFrame(
                    true, WebSocketRsv.Rsv3, compressedPayload.Slice(offset,
                         compressedPayload.ReadableBytes - offset));

            // check that last fragment contains only one extra byte
            Assert.Equal(1, compressedFrameWithExtraData.Content.ReadableBytes);
            Assert.Equal(1, compressedFrameWithExtraData.Content.GetByte(0));

            // write compressed frames
            Assert.True(decoderChannel.WriteInbound(compressedFrame1.Retain()));
            Assert.True(decoderChannel.WriteInbound(compressedFrame2.Retain()));
            Assert.True(decoderChannel.WriteInbound(compressedFrame3.Retain()));
            Assert.True(decoderChannel.WriteInbound(compressedFrameWithExtraData));

            // read uncompressed frames
            TextWebSocketFrame uncompressedFrame1 = decoderChannel.ReadInbound<TextWebSocketFrame>();
            ContinuationWebSocketFrame uncompressedFrame2 = decoderChannel.ReadInbound<ContinuationWebSocketFrame>();
            ContinuationWebSocketFrame uncompressedFrame3 = decoderChannel.ReadInbound<ContinuationWebSocketFrame>();
            ContinuationWebSocketFrame uncompressedExtraData = decoderChannel.ReadInbound<ContinuationWebSocketFrame>();
            Assert.False(uncompressedExtraData.Content.IsReadable());

            var uncompressedPayload = Unpooled.WrappedBuffer(uncompressedFrame1.Content, uncompressedFrame2.Content,
                                          uncompressedFrame3.Content, uncompressedExtraData.Content);
            Assert.Equal(originPayload, uncompressedPayload);

            Assert.True(originPayload.Release());
            Assert.True(uncompressedPayload.Release());

            Assert.True(encoderChannel.FinishAndReleaseAll());
            Assert.False(decoderChannel.Finish());
        }

        sealed class SelectivityDecompressionFilter0 : IWebSocketExtensionFilter
        {
            public bool MustSkip(WebSocketFrame frame)
            {
                return frame is TextWebSocketFrame && frame.Content.ReadableBytes < 100;
            }
        }

        sealed class SelectivityDecompressionFilter1 : IWebSocketExtensionFilter
        {
            public bool MustSkip(WebSocketFrame frame)
            {
                return frame.Content.ReadableBytes < 100;
            }
        }
    }
}
