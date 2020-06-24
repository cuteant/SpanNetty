// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Moq;
    using Xunit;

    public sealed class WebSocket08FrameDecoderTest
    {
        [Fact]
        public void ChannelInactive()
        {
            var decoder = new WebSocket08FrameDecoder(true, true, 65535, false);
            var ctx = new Mock<IChannelHandlerContext>(MockBehavior.Strict);
            ctx.Setup(x => x.FireChannelInactive()).Returns(ctx.Object);

            decoder.ChannelInactive(ctx.Object);
            ctx.Verify(x => x.FireChannelInactive(), Times.Once);
        }

        [Fact]
        public void SupportIanaStatusCodes()
        {
            var forbiddenIanaCodes = new HashSet<int>();
            forbiddenIanaCodes.Add(1004);
            forbiddenIanaCodes.Add(1005);
            forbiddenIanaCodes.Add(1006);
            var validIanaCodes = new HashSet<int>();
            for (int i = 1000; i < 1015; i++)
            {
                validIanaCodes.Add(i);
            }
            validIanaCodes.ExceptWith(forbiddenIanaCodes);

            foreach (int statusCode in validIanaCodes)
            {
                var encoderChannel = new EmbeddedChannel(new WebSocket08FrameEncoder(true));
                var decoderChannel = new EmbeddedChannel(new WebSocket08FrameDecoder(true, true, 65535, false));

                Assert.True(encoderChannel.WriteOutbound(new CloseWebSocketFrame(statusCode, AsciiString.Of("Bye"))));
                Assert.True(encoderChannel.Finish());
                var serializedCloseFrame = encoderChannel.ReadOutbound<IByteBuffer>();
                Assert.Null(encoderChannel.ReadOutbound());

                Assert.True(decoderChannel.WriteInbound(serializedCloseFrame));
                Assert.True(decoderChannel.Finish());

                var outputFrame = decoderChannel.ReadInbound<CloseWebSocketFrame>();
                Assert.Null(decoderChannel.ReadOutbound());
                try
                {
                    Assert.Equal(statusCode, outputFrame.StatusCode());
                }
                finally
                {
                    outputFrame.Release();
                }
            }
        }
    }
}
