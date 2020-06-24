// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System.Text;
    using System.Threading;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Handlers.Flow;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class WebSocketProtocolHandlerTest
    {
        [Fact]
        public void PingFrame()
        {
            IByteBuffer pingData = Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("Hello, world"));
            var channel = new EmbeddedChannel(new Handler());

            var inputMessage = new PingWebSocketFrame(pingData);
            Assert.False(channel.WriteInbound(inputMessage)); // the message was not propagated inbound

            // a Pong frame was written to the channel
            var response = channel.ReadOutbound<PongWebSocketFrame>();
            Assert.Equal(pingData, response.Content);

            pingData.Release();
            Assert.False(channel.Finish());
        }

        [Fact]
        public void PingPongFlowControlWhenAutoReadIsDisabled()
        {
            string text1 = "Hello, world #1";
            string text2 = "Hello, world #2";
            string text3 = "Hello, world #3";
            string text4 = "Hello, world #4";

            EmbeddedChannel channel = new EmbeddedChannel();
            channel.Configuration.IsAutoRead = false;
            channel.Pipeline.AddLast(new FlowControlHandler());
            channel.Pipeline.AddLast(new Handler() { });

            // When
            Assert.False(channel.WriteInbound(
                new PingWebSocketFrame(Unpooled.CopiedBuffer(text1, Encoding.UTF8)),
                new TextWebSocketFrame(text2),
                new TextWebSocketFrame(text3),
                new PingWebSocketFrame(Unpooled.CopiedBuffer(text4, Encoding.UTF8))
            ));

            // Then - no messages were handled or propagated
            Assert.Null(channel.ReadInbound());
            Assert.Null(channel.ReadOutbound());

            // When
            channel.Read();

            // Then - pong frame was written to the outbound
            var response1 = channel.ReadOutbound<PongWebSocketFrame>();
            Assert.Equal(text1, response1.Content.ToString(Encoding.UTF8));

            // And - one requested message was handled and propagated inbound
            var message2 = channel.ReadInbound<TextWebSocketFrame>();
            Assert.Equal(text2, message2.Text());

            // And - no more messages were handled or propagated
            Assert.Null(channel.ReadInbound());
            Assert.Null(channel.ReadOutbound());

            // When
            channel.Read();

            // Then - one requested message was handled and propagated inbound
            var message3 = channel.ReadInbound<TextWebSocketFrame>();
            Assert.Equal(text3, message3.Text());

            // And - no more messages were handled or propagated
            // Precisely, ping frame 'text4' was NOT read or handled.
            // It would be handle ONLY on the next 'channel.read()' call.
            Assert.Null(channel.ReadInbound());
            Assert.Null(channel.ReadOutbound());

            // Cleanup
            response1.Release();
            message2.Release();
            message3.Release();
            Assert.False(channel.Finish());
        }

        [Fact]
        public void PongFrameDropFrameFalse()
        {
            var channel = new EmbeddedChannel(new Handler(false));

            var pingResponse = new PongWebSocketFrame();
            Assert.True(channel.WriteInbound(pingResponse));

            AssertPropagatedInbound(pingResponse, channel);

            pingResponse.Release();
            Assert.False(channel.Finish());
        }

        [Fact]
        public void PongFrameDropFrameTrue()
        {
            var channel = new EmbeddedChannel(new Handler());

            var pingResponse = new PongWebSocketFrame();
            Assert.False(channel.WriteInbound(pingResponse)); // message was not propagated inbound
        }

        [Fact]
        public void TextFrame()
        {
            var channel = new EmbeddedChannel(new Handler());

            var textFrame = new TextWebSocketFrame();
            Assert.True(channel.WriteInbound(textFrame));

            AssertPropagatedInbound(textFrame, channel);

            textFrame.Release();
            Assert.False(channel.Finish());
        }

        [Fact]
        public void Timeout()
        {
            AtomicReference<IPromise> refp = new AtomicReference<IPromise>();
            WebSocketProtocolHandler handler = new TestWebSocketProtocolHandler(false, WebSocketCloseStatus.NormalClosure, 1) { };
            EmbeddedChannel channel = new EmbeddedChannel(new TimeoutHandler(refp), handler);

            var future = channel.WriteAndFlushAsync(new CloseWebSocketFrame());
            IChannelHandlerContext ctx = channel.Pipeline.Context<WebSocketProtocolHandler>();
            handler.Close(ctx, ctx.NewPromise());

            do
            {
                Thread.Sleep(10);
                channel.RunPendingTasks();
            } while (!future.IsCompleted);

            Assert.True(future.Exception.InnerException is WebSocketHandshakeException);
            Assert.False(refp.Value.IsCompleted);
            Assert.False(channel.Finish());
        }

        sealed class TestWebSocketProtocolHandler : WebSocketProtocolHandler
        {
            internal TestWebSocketProtocolHandler(bool dropPongFrames, WebSocketCloseStatus closeStatus, long forceCloseTimeoutMillis)
                : base(dropPongFrames, closeStatus, forceCloseTimeoutMillis) { }
        }

        sealed class TimeoutHandler : ChannelHandlerAdapter
        {
            private AtomicReference<IPromise> _refp;
            public TimeoutHandler(AtomicReference<IPromise> refp) => _refp = refp;

            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                _refp.Value = promise;
                ReferenceCountUtil.Release(message);
            }
        }

        static void AssertPropagatedInbound<T>(T message, EmbeddedChannel channel)
            where T : WebSocketFrame
        {
            var propagatedResponse = channel.ReadInbound<T>();
            Assert.Equal(message, propagatedResponse);
        }

        sealed class Handler : WebSocketProtocolHandler
        {
            public Handler(bool dropPongFrames = true) : base(dropPongFrames)
            {
            }
        }
    }
}
