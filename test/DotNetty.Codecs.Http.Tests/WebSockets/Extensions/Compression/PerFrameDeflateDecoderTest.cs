// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets.Extensions.Compression
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Compression;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Codecs.Http.WebSockets.Extensions;
    using DotNetty.Codecs.Http.WebSockets.Extensions.Compression;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class PerFrameDeflateDecoderTest
    {
        readonly Random _random;

        public PerFrameDeflateDecoderTest()
        {
            _random = new Random();
        }

        [Fact]
        public void CompressedFrame()
        {
            var encoderChannel = new EmbeddedChannel(
                ZlibCodecFactory.NewZlibEncoder(ZlibWrapper.None, 9, 15, 8));
            var decoderChannel = new EmbeddedChannel(new PerFrameDeflateDecoder(false));

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
            var decoderChannel = new EmbeddedChannel(new PerFrameDeflateDecoder(false));

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

        // See https://github.com/netty/netty/issues/4348
        [Fact]
        public void CompressedEmptyFrame()
        {
            var encoderChannel = new EmbeddedChannel(
                ZlibCodecFactory.NewZlibEncoder(ZlibWrapper.None, 9, 15, 8));
            var decoderChannel = new EmbeddedChannel(new PerFrameDeflateDecoder(false));

            Assert.True(encoderChannel.WriteOutbound(Unpooled.Empty));
            var compressedPayload = encoderChannel.ReadOutbound<IByteBuffer>();
            var compressedFrame =
                new BinaryWebSocketFrame(true, WebSocketRsv.Rsv1 | WebSocketRsv.Rsv3, compressedPayload);

            // execute
            Assert.True(decoderChannel.WriteInbound(compressedFrame));
            var uncompressedFrame = decoderChannel.ReadInbound<BinaryWebSocketFrame>();

            // test
            Assert.NotNull(uncompressedFrame);
            Assert.NotNull(uncompressedFrame.Content);
            Assert.Equal(WebSocketRsv.Rsv3, uncompressedFrame.Rsv);
            Assert.Equal(0, uncompressedFrame.Content.ReadableBytes);
            uncompressedFrame.Release();
        }

        [Fact]
        public void DecompressionSkip()
        {
            EmbeddedChannel encoderChannel = new EmbeddedChannel(
                    ZlibCodecFactory.NewZlibEncoder(ZlibWrapper.None, 9, 15, 8));
            EmbeddedChannel decoderChannel = new EmbeddedChannel(new PerFrameDeflateDecoder(false, AlwaysSkipWebSocketExtensionFilter.Instance));

            byte[] payload = new byte[300];
            _random.NextBytes(payload);

            Assert.True(encoderChannel.WriteOutbound(Unpooled.WrappedBuffer(payload)));
            var compressedPayload = encoderChannel.ReadOutbound<IByteBuffer>();

            BinaryWebSocketFrame compressedBinaryFrame = new BinaryWebSocketFrame(
                    true, WebSocketRsv.Rsv1 | WebSocketRsv.Rsv3, compressedPayload);

            Assert.True(decoderChannel.WriteInbound(compressedBinaryFrame));

            var inboundBinaryFrame = decoderChannel.ReadInbound<BinaryWebSocketFrame>();

            Assert.NotNull(inboundBinaryFrame);
            Assert.NotNull(inboundBinaryFrame.Content);
            Assert.Equal(compressedPayload, inboundBinaryFrame.Content);
            Assert.Equal(5, inboundBinaryFrame.Rsv);

            Assert.True(inboundBinaryFrame.Release());

            Assert.True(encoderChannel.FinishAndReleaseAll());
            Assert.False(decoderChannel.Finish());
        }
    }
}
