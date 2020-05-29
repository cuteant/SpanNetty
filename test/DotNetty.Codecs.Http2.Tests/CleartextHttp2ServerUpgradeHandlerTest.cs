
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Moq;
    using Xunit;

    public class CleartextHttp2ServerUpgradeHandlerTest : IDisposable
    {
        private EmbeddedChannel _channel;
        private Mock<IHttp2FrameListener> _frameListener;
        private Http2ConnectionHandler _http2ConnectionHandler;
        private List<object> _userEvents;

        public void Dispose()
        {
            _channel?.FinishAndReleaseAll();
        }

        sealed class TestUpgradeCodecFactory0 : HttpServerUpgradeHandler.IUpgradeCodecFactory
        {
            readonly Http2ConnectionHandler _connHandler;

            public TestUpgradeCodecFactory0(Http2ConnectionHandler connHandler) => _connHandler = connHandler;

            public HttpServerUpgradeHandler.IUpgradeCodec NewUpgradeCodec(ICharSequence protocol)
            {
                return new Http2ServerUpgradeCodec(_connHandler);
            }
        }
        sealed class TestChannelHandler : ChannelHandlerAdapter
        {
            readonly List<object> _userEvents;

            public TestChannelHandler(List<object> userEvents) => _userEvents = userEvents;

            public override void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
                _userEvents.Add(evt);
            }
        }
        private void SetUpServerChannel()
        {
            _frameListener = new Mock<IHttp2FrameListener>();

            _http2ConnectionHandler = new Http2ConnectionHandlerBuilder() { FrameListener = _frameListener.Object }.Build();

            var upgradeCodecFactory = new TestUpgradeCodecFactory0(_http2ConnectionHandler);

            _userEvents = new List<object>();

            HttpServerCodec httpServerCodec = new HttpServerCodec();
            HttpServerUpgradeHandler upgradeHandler = new HttpServerUpgradeHandler(httpServerCodec, upgradeCodecFactory);

            CleartextHttp2ServerUpgradeHandler handler = new CleartextHttp2ServerUpgradeHandler(
                    httpServerCodec, upgradeHandler, _http2ConnectionHandler);
            _channel = new EmbeddedChannel(handler, new TestChannelHandler(_userEvents));
        }

        [Fact]
        public void PriorKnowledge()
        {
            SetUpServerChannel();

            _channel.WriteInbound(Http2CodecUtil.ConnectionPrefaceBuf());

            var settingsFrame = SettingsFrameBuf();

            Assert.False(_channel.WriteInbound(settingsFrame));

            Assert.Single(_userEvents);
            Assert.True(_userEvents[0] is PriorKnowledgeUpgradeEvent);

            Assert.Equal(100, _http2ConnectionHandler.Connection.Local.MaxActiveStreams);
            Assert.Equal(65535, _http2ConnectionHandler.Connection.Local.FlowController.InitialWindowSize);

            _frameListener.Verify(
                x => x.OnSettingsRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<Http2Settings>(v => v.SequenceEqual(ExpectedSettings()))));
        }

        [Fact]
        public void Upgrade()
        {
            string upgradeString = "GET / HTTP/1.1\r\n" +
                                   "Host: example.com\r\n" +
                                   "Connection: Upgrade, HTTP2-Settings\r\n" +
                                   "Upgrade: h2c\r\n" +
                                   "HTTP2-Settings: AAMAAABkAAQAAP__\r\n\r\n";
            ValidateClearTextUpgrade(upgradeString);
        }

        [Fact]
        public void UpgradeWithMultipleConnectionHeaders()
        {
            string upgradeString = "GET / HTTP/1.1\r\n" +
                    "Host: example.com\r\n" +
                    "Connection: keep-alive\r\n" +
                    "Connection: Upgrade, HTTP2-Settings\r\n" +
                    "Upgrade: h2c\r\n" +
                    "HTTP2-Settings: AAMAAABkAAQAAP__\r\n\r\n";
            ValidateClearTextUpgrade(upgradeString);
        }

        [Fact]
        public void RequiredHeadersInSeparateConnectionHeaders()
        {
            string upgradeString = "GET / HTTP/1.1\r\n" +
                    "Host: example.com\r\n" +
                    "Connection: keep-alive\r\n" +
                    "Connection: HTTP2-Settings\r\n" +
                    "Connection: Upgrade\r\n" +
                    "Upgrade: h2c\r\n" +
                    "HTTP2-Settings: AAMAAABkAAQAAP__\r\n\r\n";
            ValidateClearTextUpgrade(upgradeString);
        }

        [Fact]
        public void PriorKnowledgeInFragments()
        {
            SetUpServerChannel();

            var connectionPreface = Http2CodecUtil.ConnectionPrefaceBuf();
            Assert.False(_channel.WriteInbound(connectionPreface.ReadBytes(5), connectionPreface));

            var settingsFrame = SettingsFrameBuf();
            Assert.False(_channel.WriteInbound(settingsFrame));

            Assert.Single(_userEvents);
            Assert.True(_userEvents[0] is PriorKnowledgeUpgradeEvent);

            Assert.Equal(100, _http2ConnectionHandler.Connection.Local.MaxActiveStreams);
            Assert.Equal(65535, _http2ConnectionHandler.Connection.Local.FlowController.InitialWindowSize);

            _frameListener.Verify(
                x => x.OnSettingsRead(
                    It.IsAny<IChannelHandlerContext>(),
                    It.Is<Http2Settings>(v => v.SequenceEqual(ExpectedSettings()))));
        }

        [Fact]
        public void Downgrade()
        {
            SetUpServerChannel();

            string requestString = "GET / HTTP/1.1\r\n" +
                             "Host: example.com\r\n\r\n";
            var inbound = Unpooled.Buffer().WriteBytes(Encoding.ASCII.GetBytes(requestString));

            Assert.True(_channel.WriteInbound(inbound));

            object firstInbound = _channel.ReadInbound<object>();
            Assert.True(firstInbound is IHttpRequest);
            IHttpRequest request = (IHttpRequest)firstInbound;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/", request.Uri);
            Assert.Equal(HttpVersion.Http11, request.ProtocolVersion);
            Assert.Equal(new DefaultHttpHeaders().Add((AsciiString)"Host", "example.com"), request.Headers);

            _channel.ReadInbound<ILastHttpContent>().Release();

            Assert.Null(_channel.ReadInbound<object>());
        }


        sealed class TestUpgradeCodecFactory1 : HttpServerUpgradeHandler.IUpgradeCodecFactory
        {
            readonly Http2MultiplexCodec _http2Codec;

            public TestUpgradeCodecFactory1(Http2MultiplexCodec http2Codec) => _http2Codec = http2Codec;

            public HttpServerUpgradeHandler.IUpgradeCodec NewUpgradeCodec(ICharSequence protocol)
            {
                return new Http2ServerUpgradeCodec(_http2Codec);
            }
        }
        [Fact]
        public void UsedHttp2MultiplexCodec()
        {
            Http2MultiplexCodec http2Codec = new Http2MultiplexCodecBuilder(true, new ActionChannelInitializer<IChannel>(ch => { })).Build();
            var upgradeCodecFactory = new TestUpgradeCodecFactory1(http2Codec);
            _http2ConnectionHandler = http2Codec;

            _userEvents = new List<object>();

            HttpServerCodec httpServerCodec = new HttpServerCodec();
            HttpServerUpgradeHandler upgradeHandler = new HttpServerUpgradeHandler(httpServerCodec, upgradeCodecFactory);

            CleartextHttp2ServerUpgradeHandler handler = new CleartextHttp2ServerUpgradeHandler(
                    httpServerCodec, upgradeHandler, http2Codec);
            _channel = new EmbeddedChannel(handler, new TestChannelHandler(_userEvents));

            Assert.False(_channel.WriteInbound(Http2CodecUtil.ConnectionPrefaceBuf()));

            var settingsFrame = SettingsFrameBuf();

            Assert.True(_channel.WriteInbound(settingsFrame));

            Assert.Single(_userEvents);
            Assert.True(_userEvents[0] is PriorKnowledgeUpgradeEvent);
        }

        private static IByteBuffer SettingsFrameBuf()
        {
            IByteBuffer settingsFrame = Unpooled.Buffer();
            settingsFrame.WriteMedium(12); // Payload length
            settingsFrame.WriteByte(0x4); // Frame type
            settingsFrame.WriteByte(0x0); // Flags
            settingsFrame.WriteInt(0x0); // StreamId
            settingsFrame.WriteShort(0x3);
            settingsFrame.WriteInt(100);
            settingsFrame.WriteShort(0x4);
            settingsFrame.WriteInt(65535);

            return settingsFrame;
        }

        private static Http2Settings ExpectedSettings()
        {
            return new Http2Settings().MaxConcurrentStreams(100).InitialWindowSize(65535);
        }

        private void ValidateClearTextUpgrade(string upgradeString)
        {
            SetUpServerChannel();

            var upgrade = Unpooled.CopiedBuffer(upgradeString, Encoding.ASCII);

            Assert.False(_channel.WriteInbound(upgrade));

            Assert.Single(_userEvents);

            var userEvent = _userEvents[0];
            Assert.True(userEvent is HttpServerUpgradeHandler.UpgradeEvent);
            Assert.Equal("h2c", ((HttpServerUpgradeHandler.UpgradeEvent)userEvent).Protocol);
            ReferenceCountUtil.Release(userEvent);

            Assert.Equal(100, _http2ConnectionHandler.Connection.Local.MaxActiveStreams);
            Assert.Equal(65535, _http2ConnectionHandler.Connection.Local.FlowController.InitialWindowSize);

            Assert.Equal(1, _http2ConnectionHandler.Connection.NumActiveStreams);
            Assert.NotNull(_http2ConnectionHandler.Connection.Stream(1));

            IHttp2Stream stream = _http2ConnectionHandler.Connection.Stream(1);
            Assert.Equal(Http2StreamState.HalfClosedRemote, stream.State);
            Assert.False(stream.IsHeadersSent);

            string expectedHttpResponse = "HTTP/1.1 101 Switching Protocols\r\n" +
                    "connection: upgrade\r\n" +
                    "upgrade: h2c\r\n\r\n";
            var responseBuffer = _channel.ReadOutbound<IByteBuffer>();
            Assert.Equal(expectedHttpResponse, responseBuffer.ToString(Encoding.UTF8));
            responseBuffer.Release();

            // Check that the preface was send (a.k.a the settings frame)
            var settingsBuffer = _channel.ReadOutbound<IByteBuffer>();
            Assert.NotNull(settingsBuffer);
            settingsBuffer.Release();

            Assert.Null(_channel.ReadOutbound<object>());
        }
    }
}
