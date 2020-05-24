// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class WebSocketHandshakeHandOverTest
    {
        private bool _serverReceivedHandshake;
        private WebSocketServerProtocolHandler.HandshakeComplete _serverHandshakeComplete;
        private bool _clientReceivedHandshake;
        private bool _clientReceivedMessage;
        private bool _serverReceivedCloseHandshake;
        private bool _clientForceClosed;
        private bool _clientHandshakeTimeout;

        public WebSocketHandshakeHandOverTest()
        {
            _serverReceivedHandshake = false;
            _serverHandshakeComplete = null;
            _clientReceivedHandshake = false;
            _clientReceivedMessage = false;
            _serverReceivedCloseHandshake = false;
            _clientForceClosed = false;
            _clientHandshakeTimeout = false;
        }

        sealed class CloseNoOpServerProtocolHandler : WebSocketServerProtocolHandler
        {
            private WebSocketHandshakeHandOverTest _owner;
            public CloseNoOpServerProtocolHandler(WebSocketHandshakeHandOverTest owner, string websocketPath)
                : base(websocketPath, null, false)
            {
                _owner = owner;
            }

            protected override void Decode(IChannelHandlerContext ctx, WebSocketFrame frame, List<object> output)
            {
                if (frame is CloseWebSocketFrame)
                {
                    _owner._serverReceivedCloseHandshake = true;
                    return;
                }
                base.Decode(ctx, frame, output);
            }
        }

        [Fact]
        public void Handover()
        {
            var serverHandler = new ServerHandoverHandler(this);
            EmbeddedChannel serverChannel = CreateServerChannel(serverHandler);
            EmbeddedChannel clientChannel = CreateClientChannel(new ClientHandoverHandler(this));

            // Transfer the handshake from the client to the server
            TransferAllDataWithMerge(clientChannel, serverChannel);
            Assert.True(_serverReceivedHandshake);
            Assert.NotNull(_serverHandshakeComplete);
            Assert.Equal("/test", _serverHandshakeComplete.RequestUri);
            Assert.Equal(8, _serverHandshakeComplete.RequestHeaders.Size);
            Assert.Equal("test-proto-2", _serverHandshakeComplete.SelectedSubprotocol);

            // Transfer the handshake response and the websocket message to the client
            TransferAllDataWithMerge(serverChannel, clientChannel);
            Assert.True(_clientReceivedHandshake);
            Assert.True(_clientReceivedMessage);
        }

        sealed class ServerHandoverHandler : SimpleChannelInboundHandler<object>
        {
            private readonly WebSocketHandshakeHandOverTest _owner;

            public ServerHandoverHandler(WebSocketHandshakeHandOverTest owner)
            {
                _owner = owner;
            }

            public override void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
                if (evt.Equals(WebSocketServerProtocolHandler.ServerHandshakeStateEvent.HandshakeComplete))
                {
                    _owner._serverReceivedHandshake = true;
                    // immediately send a message to the client on connect
                    context.WriteAndFlushAsync(new TextWebSocketFrame("abc"));
                }
                else if (evt is WebSocketServerProtocolHandler.HandshakeComplete complete)
                {
                    _owner._serverHandshakeComplete = complete;
                }
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
            {
                // Empty
            }
        }

        sealed class ClientHandoverHandler : SimpleChannelInboundHandler<object>
        {
            private readonly WebSocketHandshakeHandOverTest _owner;

            public ClientHandoverHandler(WebSocketHandshakeHandOverTest owner)
            {
                _owner = owner;
            }

            public override void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
                if (evt is WebSocketClientProtocolHandler.ClientHandshakeStateEvent stateEvent
                    && stateEvent == WebSocketClientProtocolHandler.ClientHandshakeStateEvent.HandshakeComplete)
                {
                    _owner._clientReceivedHandshake = true;
                }
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
            {
                if (msg is TextWebSocketFrame)
                {
                    _owner._clientReceivedMessage = true;
                }
            }
        }

        [Fact]
        public async Task ClientHandshakeTimeout()
        {
            EmbeddedChannel serverChannel = CreateServerChannel(new ServerHandshakeTimeoutHander(this));
            EmbeddedChannel clientChannel = CreateClientChannel(new ClientHandshakeTimeoutHander(this), 100);

            // Client send the handshake request to server
            TransferAllDataWithMerge(clientChannel, serverChannel);
            // Server do not send the response back
            // transferAllDataWithMerge(serverChannel, clientChannel);
            WebSocketClientProtocolHandshakeHandler handshakeHandler =
                    (WebSocketClientProtocolHandshakeHandler)clientChannel
                            .Pipeline.Get<WebSocketClientProtocolHandshakeHandler>();

            while (!handshakeHandler.GetHandshakeFuture().IsCompleted)
            {
                Thread.Sleep(10);
                // We need to run all pending tasks as the handshake timeout is scheduled on the EventLoop.
                clientChannel.RunScheduledPendingTasks();
            }
            Assert.True(_clientHandshakeTimeout);
            Assert.False(_clientReceivedHandshake);
            Assert.False(_clientReceivedMessage);
            // Should throw WebSocketHandshakeException
            try
            {
                await handshakeHandler.GetHandshakeFuture();
                Assert.False(true);
            }
            catch (Exception exc)
            {
                Assert.IsType<WebSocketHandshakeException>(exc);
            }
            finally
            {
                serverChannel.FinishAndReleaseAll();
            }
        }

        sealed class ServerHandshakeTimeoutHander : SimpleChannelInboundHandler<object>
        {
            private readonly WebSocketHandshakeHandOverTest _owner;

            public ServerHandshakeTimeoutHander(WebSocketHandshakeHandOverTest owner)
            {
                _owner = owner;
            }

            public override void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
                if (evt.Equals(WebSocketServerProtocolHandler.ServerHandshakeStateEvent.HandshakeComplete))
                {
                    _owner._serverReceivedHandshake = true;
                    // immediately send a message to the client on connect
                    context.WriteAndFlushAsync(new TextWebSocketFrame("abc"));
                }
                else if (evt is WebSocketServerProtocolHandler.HandshakeComplete handshakeComplete)
                {
                    _owner._serverHandshakeComplete = handshakeComplete;
                }
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
            {
            }
        }

        sealed class ClientHandshakeTimeoutHander : SimpleChannelInboundHandler<object>
        {
            private readonly WebSocketHandshakeHandOverTest _owner;

            public ClientHandshakeTimeoutHander(WebSocketHandshakeHandOverTest owner)
            {
                _owner = owner;
            }

            public override void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
                if (evt.Equals(WebSocketClientProtocolHandler.ClientHandshakeStateEvent.HandshakeComplete))
                {
                    _owner._clientReceivedHandshake = true;
                }
                else if (evt.Equals(WebSocketClientProtocolHandler.ClientHandshakeStateEvent.HandshakeTimeout))
                {
                    _owner._clientHandshakeTimeout = true;
                }
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
            {
                if (msg is TextWebSocketFrame)
                {
                    _owner._clientReceivedMessage = true;
                }
            }
        }

        [Fact]
        public void ClientHandshakerForceClose()
        {
            WebSocketClientHandshaker handshaker = WebSocketClientHandshakerFactory.NewHandshaker(
                    new Uri("ws://localhost:1234/test"), WebSocketVersion.V13, null, true,
                    EmptyHttpHeaders.Default, int.MaxValue, true, false, 20);

            EmbeddedChannel serverChannel = CreateServerChannel(
                new CloseNoOpServerProtocolHandler(this, "/test"),
                new ServerHandshakeForceCloseHander(this));
            EmbeddedChannel clientChannel = CreateClientChannel(handshaker, new ClientHandshakeForceCloseHander(this, handshaker));

            // Transfer the handshake from the client to the server
            TransferAllDataWithMerge(clientChannel, serverChannel);
            // Transfer the handshake from the server to client
            TransferAllDataWithMerge(serverChannel, clientChannel);

            // Transfer closing handshake
            TransferAllDataWithMerge(clientChannel, serverChannel);
            Assert.True(_serverReceivedCloseHandshake);
            // Should not be closed yet as we disabled closing the connection on the server
            Assert.False(_clientForceClosed);

            while (!_clientForceClosed)
            {
                Thread.Sleep(10);
                // We need to run all pending tasks as the force close timeout is scheduled on the EventLoop.
                clientChannel.RunPendingTasks();
            }

            // clientForceClosed would be set to TRUE after any close,
            // so check here that force close timeout was actually fired
            Assert.True(handshaker.IsForceCloseComplete);

            // Both should be empty
            Assert.False(serverChannel.FinishAndReleaseAll());
            Assert.False(clientChannel.FinishAndReleaseAll());
        }

        sealed class ServerHandshakeForceCloseHander : SimpleChannelInboundHandler<object>
        {
            private readonly WebSocketHandshakeHandOverTest _owner;

            public ServerHandshakeForceCloseHander(WebSocketHandshakeHandOverTest owner)
            {
                _owner = owner;
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
            {
            }
        }

        sealed class ClientHandshakeForceCloseHander : SimpleChannelInboundHandler<object>
        {
            private readonly WebSocketHandshakeHandOverTest _owner;
            private readonly WebSocketClientHandshaker _handshaker;

            public ClientHandshakeForceCloseHander(WebSocketHandshakeHandOverTest owner, WebSocketClientHandshaker handshaker)
            {
                _owner = owner;
                _handshaker = handshaker;
            }

            public override void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
                if (evt.Equals(WebSocketClientProtocolHandler.ClientHandshakeStateEvent.HandshakeComplete))
                {
                    context.Channel.CloseCompletion.ContinueWith(t =>
                    {
                        _owner._clientForceClosed = true;
                    }, TaskContinuationOptions.ExecuteSynchronously);
                    _handshaker.CloseAsync(context.Channel, new CloseWebSocketFrame());
                }
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
            {
            }
        }

        static void TransferAllDataWithMerge(EmbeddedChannel srcChannel, EmbeddedChannel dstChannel)
        {
            IByteBuffer mergedBuffer = null;
            for (; ; )
            {
                var srcData = srcChannel.ReadOutbound<object>();
                if (srcData != null)
                {
                    Assert.IsAssignableFrom<IByteBuffer>(srcData);
                    var srcBuf = (IByteBuffer)srcData;
                    try
                    {
                        if (mergedBuffer == null)
                        {
                            mergedBuffer = Unpooled.Buffer();
                        }
                        mergedBuffer.WriteBytes(srcBuf);
                    }
                    finally
                    {
                        srcBuf.Release();
                    }
                }
                else
                {
                    break;
                }
            }

            if (mergedBuffer != null)
            {
                dstChannel.WriteInbound(mergedBuffer);
            }
        }

        static EmbeddedChannel CreateClientChannel(IChannelHandler handler) => new EmbeddedChannel(
            new HttpClientCodec(),
            new HttpObjectAggregator(8192),
            new WebSocketClientProtocolHandler(
                new Uri("ws://localhost:1234/test"),
                WebSocketVersion.V13,
                "test-proto-2",
                false,
                null,
                65536),
            handler);

        private static EmbeddedChannel CreateClientChannel(WebSocketClientHandshaker handshaker, IChannelHandler handler)
        {
            return new EmbeddedChannel(
                    new HttpClientCodec(),
                    new HttpObjectAggregator(8192),
                    // Note that we're switching off close frames handling on purpose to test forced close on timeout.
                    new WebSocketClientProtocolHandler(handshaker, false, false),
                    handler);
        }

        static EmbeddedChannel CreateServerChannel(IChannelHandler handler) => new EmbeddedChannel(
                new HttpServerCodec(),
                new HttpObjectAggregator(8192),
                new WebSocketServerProtocolHandler("/test", "test-proto-1, test-proto-2", false),
                handler);

        private static EmbeddedChannel CreateServerChannel(WebSocketServerProtocolHandler webSocketHandler, IChannelHandler handler)
        {
            return new EmbeddedChannel(
                    new HttpServerCodec(),
                    new HttpObjectAggregator(8192),
                    webSocketHandler,
                    handler);
        }

        private static EmbeddedChannel CreateClientChannel(IChannelHandler handler, long timeoutMillis)
        {
            return new EmbeddedChannel(
                    new HttpClientCodec(),
                    new HttpObjectAggregator(8192),
                    new WebSocketClientProtocolHandler(new Uri("ws://localhost:1234/test"),
                                                       WebSocketVersion.V13, "test-proto-2",
                                                       false, null, 65536, timeoutMillis),
                    handler);
        }
    }
}
