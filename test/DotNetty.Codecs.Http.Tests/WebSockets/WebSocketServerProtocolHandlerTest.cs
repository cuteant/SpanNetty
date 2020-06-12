// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    using static HttpResponseStatus;
    using static HttpVersion;

    public class WebSocketServerProtocolHandlerTest : IDisposable
    {
        readonly Queue<IFullHttpResponse> _responses;

        public WebSocketServerProtocolHandlerTest()
        {
            _responses = new Queue<IFullHttpResponse>();
        }

        [Fact]
        public void HttpUpgradeRequest()
        {
            EmbeddedChannel ch = CreateChannel(new MockOutboundHandler(this));
            IChannelHandlerContext handshakerCtx = ch.Pipeline.Context<WebSocketServerProtocolHandshakeHandler>();
            WriteUpgradeRequest(ch);

            IFullHttpResponse response = _responses.Dequeue();
            Assert.Equal(SwitchingProtocols, response.Status);
            response.Release();
            Assert.NotNull(WebSocketServerProtocolHandler.GetHandshaker(handshakerCtx.Channel));
            Assert.False(ch.Finish());
        }

        class ReplacedBeforeHandshakeHandler : ChannelHandlerAdapter
        {
            public override void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
                if (evt is WebSocketServerProtocolHandler.HandshakeComplete)
                {
                    // We should have removed the handler already.
                    Assert.Null(context.Pipeline.Context<WebSocketServerProtocolHandshakeHandler>());
                }
            }
        }

        [Fact]
        public void WebSocketServerProtocolHandshakeHandlerReplacedBeforeHandshake()
        {
            EmbeddedChannel ch = CreateChannel(new MockOutboundHandler(this));
            IChannelHandlerContext handshakerCtx = ch.Pipeline.Context<WebSocketServerProtocolHandshakeHandler>();
            ch.Pipeline.AddLast(new ReplacedBeforeHandshakeHandler());
            WriteUpgradeRequest(ch);

            IFullHttpResponse response = _responses.Dequeue();
            Assert.Equal(SwitchingProtocols, response.Status);
            response.Release();
            Assert.NotNull(WebSocketServerProtocolHandler.GetHandshaker(handshakerCtx.Channel));
            Assert.False(ch.Finish());
        }

        [Fact]
        public void SubsequentHttpRequestsAfterUpgradeShouldReturn403()
        {
            EmbeddedChannel ch = CreateChannel();

            WriteUpgradeRequest(ch);

            IFullHttpResponse response = _responses.Dequeue();
            Assert.Equal(SwitchingProtocols, response.Status);
            response.Release();

            ch.WriteInbound(new DefaultFullHttpRequest(Http11, HttpMethod.Get, "/test"));
            response = _responses.Dequeue();
            Assert.Equal(Forbidden, response.Status);
            response.Release();
            Assert.False(ch.Finish());
        }

        [Fact]
        public void HttpUpgradeRequestInvalidUpgradeHeader()
        {
            EmbeddedChannel ch = CreateChannel();
            IFullHttpRequest httpRequestWithEntity = new WebSocketRequestBuilder()
                .HttpVersion(Http11)
                .Method(HttpMethod.Get)
                .Uri("/test")
                .Connection("Upgrade")
                .Version00()
                .Upgrade("BogusSocket")
                .Build();

            ch.WriteInbound(httpRequestWithEntity);

            IFullHttpResponse response = _responses.Dequeue();
            Assert.Equal(BadRequest, response.Status);
            Assert.Equal("not a WebSocket handshake request: missing upgrade", GetResponseMessage(response));
            response.Release();
            Assert.False(ch.Finish());
        }

        [Fact]
        public void HttpUpgradeRequestMissingWsKeyHeader()
        {
            EmbeddedChannel ch = CreateChannel();
            IHttpRequest httpRequest = new WebSocketRequestBuilder()
                .HttpVersion(Http11)
                .Method(HttpMethod.Get)
                .Uri("/test")
                .Key(null)
                .Connection("Upgrade")
                .Upgrade(HttpHeaderValues.Websocket)
                .Version13()
                .Build();

            ch.WriteInbound(httpRequest);

            IFullHttpResponse response = _responses.Dequeue();
            Assert.Equal(BadRequest, response.Status);
            Assert.Equal("not a WebSocket request: missing key", GetResponseMessage(response));
            response.Release();
            Assert.False(ch.Finish());
        }

        [Fact]
        public void CreateUTF8Validator()
        {
            var config = WebSocketServerProtocolConfig.NewBuilder()
                    .WithUTF8Validator(true)
                    .WebsocketPath("/test")
                    .Build();

            EmbeddedChannel ch = new EmbeddedChannel(
                    new WebSocketServerProtocolHandler(config),
                    new HttpRequestDecoder(),
                    new HttpResponseEncoder(),
                    new MockOutboundHandler(this));
            WriteUpgradeRequest(ch);

            IFullHttpResponse response = _responses.Dequeue();
            Assert.Equal(HttpResponseStatus.SwitchingProtocols, response.Status);
            response.Release();

            Assert.NotNull(ch.Pipeline.Get<Utf8FrameValidator>());
        }

        [Fact]
        public void DoNotCreateUTF8Validator()
        {
            var config = WebSocketServerProtocolConfig.NewBuilder()
                    .WebsocketPath("/test")
                    .WithUTF8Validator(false)
                    .Build();

            EmbeddedChannel ch = new EmbeddedChannel(
                    new WebSocketServerProtocolHandler(config),
                    new HttpRequestDecoder(),
                    new HttpResponseEncoder(),
                    new MockOutboundHandler(this));
            WriteUpgradeRequest(ch);

            IFullHttpResponse response = _responses.Dequeue();
            Assert.Equal(HttpResponseStatus.SwitchingProtocols, response.Status);
            response.Release();

            Assert.Null(ch.Pipeline.Get<Utf8FrameValidator>());
        }

        [Fact]
        public void HandleTextFrame()
        {
            var customTextFrameHandler = new CustomTextFrameHandler();
            EmbeddedChannel ch = CreateChannel(customTextFrameHandler);
            WriteUpgradeRequest(ch);

            IFullHttpResponse response = _responses.Dequeue();
            Assert.Equal(HttpResponseStatus.SwitchingProtocols, response.Status);
            response.Release();

            if (ch.Pipeline.Context<HttpRequestDecoder>() != null)
            {
                // Removing the HttpRequestDecoder because we are writing a TextWebSocketFrame and thus
                // decoding is not necessary.
                ch.Pipeline.Remove<HttpRequestDecoder>();
            }

            ch.WriteInbound(new TextWebSocketFrame("payload"));

            Assert.Equal("processed: payload", customTextFrameHandler.Content);
            Assert.False(ch.Finish());
        }

        [Fact]
        public void ExplicitCloseFrameSentWhenServerChannelClosed()
        {
            WebSocketCloseStatus closeStatus = WebSocketCloseStatus.EndpointUnavailable;
            EmbeddedChannel client = CreateClient();
            EmbeddedChannel server = CreateServer();

            Assert.False(server.WriteInbound(client.ReadOutbound<object>()));
            Assert.False(client.WriteInbound(server.ReadOutbound<object>()));

            // When server channel closed with explicit close-frame
            Assert.True(server.WriteOutbound(new CloseWebSocketFrame(closeStatus)));
            server.CloseAsync();

            // Then client receives provided close-frame
            Assert.True(client.WriteInbound(server.ReadOutbound<object>()));
            Assert.False(server.Open);

            CloseWebSocketFrame closeMessage = client.ReadInbound<CloseWebSocketFrame>();
            Assert.Equal(closeMessage.StatusCode(), closeStatus.Code);
            closeMessage.Release();

            client.CloseAsync();
            Assert.True(ReferenceCountUtil.Release(client.ReadOutbound<object>()));
            Assert.False(client.FinishAndReleaseAll());
            Assert.False(server.FinishAndReleaseAll());
        }

        [Fact]
        public void CloseFrameSentWhenServerChannelClosedSilently()
        {
            EmbeddedChannel client = CreateClient();
            EmbeddedChannel server = CreateServer();

            Assert.False(server.WriteInbound(client.ReadOutbound<object>()));
            Assert.False(client.WriteInbound(server.ReadOutbound<object>()));

            // When server channel closed without explicit close-frame
            server.CloseAsync();

            // Then client receives NORMAL_CLOSURE close-frame
            Assert.True(client.WriteInbound(server.ReadOutbound<object>()));
            Assert.False(server.Open);

            CloseWebSocketFrame closeMessage = client.ReadInbound<CloseWebSocketFrame>();
            Assert.Equal(closeMessage.StatusCode(), WebSocketCloseStatus.NormalClosure.Code);
            closeMessage.Release();

            client.CloseAsync();
            Assert.True(ReferenceCountUtil.Release(client.ReadOutbound<object>()));
            Assert.False(client.FinishAndReleaseAll());
            Assert.False(server.FinishAndReleaseAll());
        }

        [Fact]
        public void ExplicitCloseFrameSentWhenClientChannelClosed()
        {
            WebSocketCloseStatus closeStatus = WebSocketCloseStatus.InvalidPayloadData;
            EmbeddedChannel client = CreateClient();
            EmbeddedChannel server = CreateServer();

            Assert.False(server.WriteInbound(client.ReadOutbound<object>()));
            Assert.False(client.WriteInbound(server.ReadOutbound<object>()));

            // When client channel closed with explicit close-frame
            Assert.True(client.WriteOutbound(new CloseWebSocketFrame(closeStatus)));
            client.CloseAsync();

            // Then client receives provided close-frame
            Assert.False(server.WriteInbound(client.ReadOutbound<object>()));
            Assert.False(client.Open);
            Assert.False(server.Open);

            CloseWebSocketFrame closeMessage = Decode<CloseWebSocketFrame>(server.ReadOutbound<IByteBuffer>());
            Assert.Equal(closeMessage.StatusCode(), closeStatus.Code);
            closeMessage.Release();

            Assert.False(client.FinishAndReleaseAll());
            Assert.False(server.FinishAndReleaseAll());
        }

        [Fact]
        public void CloseFrameSentWhenClientChannelClosedSilently()
        {
            EmbeddedChannel client = CreateClient();
            EmbeddedChannel server = CreateServer();

            Assert.False(server.WriteInbound(client.ReadOutbound<object>()));
            Assert.False(client.WriteInbound(server.ReadOutbound<object>()));

            // When client channel closed without explicit close-frame
            client.CloseAsync();

            // Then server receives NORMAL_CLOSURE close-frame
            Assert.False(server.WriteInbound(client.ReadOutbound<object>()));
            Assert.False(client.Open);
            Assert.False(server.Open);

            CloseWebSocketFrame closeMessage = Decode<CloseWebSocketFrame>(server.ReadOutbound<IByteBuffer>());
            Assert.Equal(closeMessage, new CloseWebSocketFrame(WebSocketCloseStatus.NormalClosure));
            closeMessage.Release();

            Assert.False(client.FinishAndReleaseAll());
            Assert.False(server.FinishAndReleaseAll());
        }

        private EmbeddedChannel CreateClient(params IChannelHandler[] handlers)
        {
            WebSocketClientProtocolConfig clientConfig = WebSocketClientProtocolConfig.NewBuilder()
                .WebSocketUri("http://test/test")
                .DropPongFrames(false)
                .HandleCloseFrames(false)
                .Build();
            EmbeddedChannel ch = new EmbeddedChannel(false, false,
                new HttpClientCodec(),
                new HttpObjectAggregator(8192),
                new WebSocketClientProtocolHandler(clientConfig)
            );
            ch.Pipeline.AddLast(handlers);
            ch.Register();
            return ch;
        }

        private EmbeddedChannel CreateServer(params IChannelHandler[] handlers)
        {
            WebSocketServerProtocolConfig serverConfig = WebSocketServerProtocolConfig.NewBuilder()
                .WebsocketPath("/test")
                .DropPongFrames(false)
                .Build();
            EmbeddedChannel ch = new EmbeddedChannel(false, false,
                new HttpServerCodec(),
                new HttpObjectAggregator(8192),
                new WebSocketServerProtocolHandler(serverConfig)
            );
            ch.Pipeline.AddLast(handlers);
            ch.Register();
            return ch;
        }

        private T Decode<T>(IByteBuffer input) where T : class
        {
            EmbeddedChannel ch = new EmbeddedChannel(new WebSocket13FrameDecoder(true, false, 65536, true));
            Assert.True(ch.WriteInbound(input));
            var decoded = ch.ReadInbound<object>();
            Assert.NotNull(decoded);
            Assert.False(ch.Finish());
            return decoded as T;
        }

        EmbeddedChannel CreateChannel() => CreateChannel(null);

        EmbeddedChannel CreateChannel(IChannelHandler handler)
        {
            WebSocketServerProtocolConfig serverConfig = WebSocketServerProtocolConfig.NewBuilder()
                .WebsocketPath("/test")
                .SendCloseFrame(null)
                .Build();
            return new EmbeddedChannel(
                new WebSocketServerProtocolHandler(serverConfig),
                new HttpRequestDecoder(),
                new HttpResponseEncoder(),
                new MockOutboundHandler(this),
                handler);
        }

        static void WriteUpgradeRequest(EmbeddedChannel ch) => ch.WriteInbound(WebSocketRequestBuilder.Successful());

        static string GetResponseMessage(IFullHttpResponse response) => response.Content.ToString(Encoding.UTF8);

        sealed class MockOutboundHandler : ChannelHandlerAdapter
        {
            readonly WebSocketServerProtocolHandlerTest _owner;

            public MockOutboundHandler(WebSocketServerProtocolHandlerTest owner)
            {
                _owner = owner;
            }

            public override void Write(IChannelHandlerContext ctx, object msg, IPromise promise)
            {
                _owner._responses.Enqueue((IFullHttpResponse)msg);
                promise.TryComplete();
            }

            public override void Flush(IChannelHandlerContext ctx)
            {
            }
        }

        sealed class CustomTextFrameHandler : ChannelHandlerAdapter
        {
            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                Assert.Null(Content);
                Content = "processed: " + ((TextWebSocketFrame)msg).Text();
                ReferenceCountUtil.Release(msg);
            }

            public string Content { get; private set; }
        }

        public void Dispose() => _responses.Clear();
    }
}
