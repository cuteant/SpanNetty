// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets.Extensions.Compression
{
    using System.Collections.Generic;
    using System.Linq;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Compression;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Codecs.Http.WebSockets.Extensions;
    using DotNetty.Codecs.Http.WebSockets.Extensions.Compression;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    using static Http.WebSockets.Extensions.Compression.PerMessageDeflateServerExtensionHandshaker;

    public sealed class PerMessageDeflateClientExtensionHandshakerTest
    {
        [Fact]
        public void NormalData()
        {
            var handshaker = new PerMessageDeflateClientExtensionHandshaker();
            WebSocketExtensionData data = handshaker.NewRequestData();

            Assert.Equal(PerMessageDeflateExtension, data.Name);
            Assert.Equal(ZlibCodecFactory.IsSupportingWindowSizeAndMemLevel ? 1 : 0, data.Parameters.Count);
        }

        [Fact]
        public void CustomData()
        {
            var handshaker = new PerMessageDeflateClientExtensionHandshaker(6, true, 10, true, true);
            WebSocketExtensionData data = handshaker.NewRequestData();

            Assert.Equal(PerMessageDeflateExtension, data.Name);
            Assert.Contains(ClientMaxWindow, data.Parameters.Keys);
            Assert.Contains(ServerMaxWindow, data.Parameters.Keys);
            Assert.Equal("10", data.Parameters[ServerMaxWindow]);
        }

        [Fact]
        public void NormalHandshake()
        {
            var handshaker = new PerMessageDeflateClientExtensionHandshaker();

            IWebSocketClientExtension extension = handshaker.HandshakeExtension(
                new WebSocketExtensionData(PerMessageDeflateExtension, new Dictionary<string, string>()));

            Assert.NotNull(extension);
            Assert.Equal(WebSocketRsv.Rsv1, extension.Rsv);
            Assert.IsType<PerMessageDeflateDecoder>(extension.NewExtensionDecoder());
            Assert.IsType<PerMessageDeflateEncoder>(extension.NewExtensionEncoder());
        }

        [Fact]
        public void CustomHandshake()
        {
            var handshaker = new PerMessageDeflateClientExtensionHandshaker(6, true, 10, true, true);

            var parameters = new Dictionary<string, string>
            {
                { ClientMaxWindow, "12" },
                { ServerMaxWindow, "10" },
                { ClientNoContext, null },
                { ServerNoContext, null }
            };
            IWebSocketClientExtension extension = handshaker.HandshakeExtension(
                new WebSocketExtensionData(PerMessageDeflateExtension, parameters));

            Assert.NotNull(extension);
            Assert.Equal(WebSocketRsv.Rsv1, extension.Rsv);
            Assert.IsType<PerMessageDeflateDecoder>(extension.NewExtensionDecoder());
            Assert.IsType<PerMessageDeflateEncoder>(extension.NewExtensionEncoder());

            parameters = new Dictionary<string, string>
            {
                { ServerMaxWindow, "10" },
                { ServerNoContext, null }
            };
            extension = handshaker.HandshakeExtension(
                new WebSocketExtensionData(PerMessageDeflateExtension, parameters));

            Assert.NotNull(extension);
            Assert.Equal(WebSocketRsv.Rsv1, extension.Rsv);
            Assert.IsType<PerMessageDeflateDecoder>(extension.NewExtensionDecoder());
            Assert.IsType<PerMessageDeflateEncoder>(extension.NewExtensionEncoder());

            parameters = new Dictionary<string, string>();
            extension = handshaker.HandshakeExtension(
                new WebSocketExtensionData(PerMessageDeflateExtension, parameters));

            Assert.Null(extension);
        }

        [Fact]
        public void DecoderNoClientContext()
        {
            PerMessageDeflateClientExtensionHandshaker handshaker =
                    new PerMessageDeflateClientExtensionHandshaker(6, true, MaxWindowSize, true, false);

            sbyte[] firstPayload = new sbyte[] {
                76, -50, -53, 10, -62, 48, 20, 4, -48, 95, 41, 89, -37, 36, 77, 90, 31, -39, 41, -72, 112, 33, -120, 20,
                20, 119, -79, 70, 123, -95, 121, -48, 92, -116, 80, -6, -17, -58, -99, -37, -31, 12, 51, 19, 1, -9, -12,
                68, -111, -117, 25, 58, 111, 77, -127, -66, -64, -34, 20, 59, -64, -29, -2, 90, -100, -115, 30, 16, 114,
                -68, 61, 29, 40, 89, -112, -73, 25, 35, 120, -105, -67, -32, -43, -70, -84, 120, -55, 69, 43, -124, 106,
                -92, 18, -110, 114, -50, 111, 25, -3, 10, 17, -75, 13, 127, -84, 106, 90, -66, 84, -75, 84, 53, -89,
                -75, 92, -3, -40, -61, 119, 49, -117, 30, 49, 68, -59, 88, 74, -119, -34, 1, -83, -7, -48, 124, -124,
                -23, 16, 88, -118, 121, 54, -53, 1, 44, 32, 81, 19, 25, -115, -43, -32, -64, -67, -120, -110, -101, 121,
                -2, 2
            };

            sbyte[] secondPayload = new sbyte[] {
                -86, 86, 42, 46, 77, 78, 78, 45, 6, 26, 83, 82, 84, -102, -86, 3, -28, 38, 21, 39, 23, 101, 38, -91, 2,
                -51, -51, 47, 74, 73, 45, 114, -54, -49, -49, -10, 49, -78, -118, 112, 10, 9, 13, 118, 1, -102, 84,
                -108, 90, 88, 10, 116, 27, -56, -84, 124, -112, -13, 16, 26, 116, -108, 18, -117, -46, -127, 6, 69, 99,
                -45, 24, 91, 91, 11, 0
            };

            var parameters = new Dictionary<string, string>()
            {
                {  ClientNoContext, null }
            };

            var extension = handshaker.HandshakeExtension(
                    new WebSocketExtensionData(PerMessageDeflateExtension, parameters));
            Assert.NotNull(extension);

            var decoderChannel = new EmbeddedChannel(extension.NewExtensionDecoder());
            Assert.True(
                    decoderChannel.WriteInbound(new TextWebSocketFrame(true, WebSocketRsv.Rsv1, Unpooled.CopiedBuffer(firstPayload.Select(_ => (byte)_).ToArray()))));
            TextWebSocketFrame firstFrameDecompressed = decoderChannel.ReadInbound<TextWebSocketFrame>();
            Assert.True(
                    decoderChannel.WriteInbound(new TextWebSocketFrame(true, WebSocketRsv.Rsv1, Unpooled.CopiedBuffer(secondPayload.Select(_ => (byte)_).ToArray()))));
            TextWebSocketFrame secondFrameDecompressed = decoderChannel.ReadInbound<TextWebSocketFrame>();

            Assert.NotNull(firstFrameDecompressed);
            Assert.NotNull(firstFrameDecompressed.Content);
            //Assert.True(firstFrameDecompressed instanceof TextWebSocketFrame);
            Assert.Equal(firstFrameDecompressed.Text(),
                         "{\"info\":\"Welcome to the BitMEX Realtime API.\",\"version\"" +
                         ":\"2018-10-02T22:53:23.000Z\",\"timestamp\":\"2018-10-15T06:43:40.437Z\"," +
                         "\"docs\":\"https://www.bitmex.com/app/wsAPI\",\"limit\":{\"remaining\":39}}");
            Assert.True(firstFrameDecompressed.Release());

            Assert.NotNull(secondFrameDecompressed);
            Assert.NotNull(secondFrameDecompressed.Content);
            //Assert.True(secondFrameDecompressed instanceof TextWebSocketFrame);
            Assert.Equal(secondFrameDecompressed.Text(),
                         "{\"success\":true,\"subscribe\":\"orderBookL2:XBTUSD\"," +
                         "\"request\":{\"op\":\"subscribe\",\"args\":[\"orderBookL2:XBTUSD\"]}}");
            Assert.True(secondFrameDecompressed.Release());

            Assert.False(decoderChannel.Finish());
        }
    }
}
