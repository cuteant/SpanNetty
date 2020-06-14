// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets.Extensions
{
    using System.IO;
    using System.Collections.Generic;
    using DotNetty.Codecs.Http.WebSockets.Extensions;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Moq;
    using Xunit;

    using static WebSocketExtensionTestUtil;

    public sealed class WebSocketServerExtensionHandlerTest
    {
        readonly Mock<IWebSocketServerExtensionHandshaker> _mainHandshaker;
        readonly Mock<IWebSocketServerExtensionHandshaker> _fallbackHandshaker;
        readonly Mock<IWebSocketServerExtension> _mainExtension;
        readonly Mock<IWebSocketServerExtension> _fallbackExtension;

        public WebSocketServerExtensionHandlerTest()
        {
            _mainHandshaker = new Mock<IWebSocketServerExtensionHandshaker>();
            _fallbackHandshaker = new Mock<IWebSocketServerExtensionHandshaker>();
            _mainExtension = new Mock<IWebSocketServerExtension>();
            _fallbackExtension = new Mock<IWebSocketServerExtension>();
        }

        [Fact]
        public void MainSuccess()
        {
            // initialize
            _mainHandshaker.Setup(
                    x => x.HandshakeExtension(
                        It.Is<WebSocketExtensionData>(v => v.Name.Equals("main"))))
                .Returns(_mainExtension.Object);
            _mainHandshaker.Setup(
                    x => x.HandshakeExtension(
                        It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))))
                .Returns(default(IWebSocketServerExtension));

            _fallbackHandshaker.Setup(
                    x => x.HandshakeExtension(
                        It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))))
                .Returns(_fallbackExtension.Object);
            _fallbackHandshaker.Setup(
                    x => x.HandshakeExtension(
                        It.Is<WebSocketExtensionData>(v => v.Name.Equals("main"))))
                .Returns(default(IWebSocketServerExtension));

            _mainExtension.Setup(x => x.Rsv).Returns(WebSocketRsv.Rsv1);
            _mainExtension.Setup(x => x.NewReponseData()).Returns(
                new WebSocketExtensionData("main", new Dictionary<string, string>()));
            _mainExtension.Setup(x => x.NewExtensionEncoder()).Returns(new DummyEncoder());
            _mainExtension.Setup(x => x.NewExtensionDecoder()).Returns(new DummyDecoder());

            _fallbackExtension.Setup(x => x.Rsv).Returns(WebSocketRsv.Rsv1);

            // execute
            WebSocketServerExtensionHandler extensionHandler =
                    new WebSocketServerExtensionHandler(_mainHandshaker.Object, _fallbackHandshaker.Object);
            var ch = new EmbeddedChannel(extensionHandler);

            IHttpRequest req = NewUpgradeRequest("main, fallback");
            ch.WriteInbound(req);

            IHttpResponse res = NewUpgradeResponse(null);
            ch.WriteOutbound(res);

            var res2 = ch.ReadOutbound<IHttpResponse>();
            Assert.True(res2.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value));
            List<WebSocketExtensionData> resExts = WebSocketExtensionUtil.ExtractExtensions(value.ToString());

            // test
            Assert.Null(ch.Pipeline.Context(extensionHandler));
            Assert.Single(resExts);
            Assert.Equal("main", resExts[0].Name);
            Assert.Empty(resExts[0].Parameters);
            Assert.NotNull(ch.Pipeline.Get<DummyDecoder>());
            Assert.NotNull(ch.Pipeline.Get<DummyEncoder>());

            _mainHandshaker.Verify(
                x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("main"))),
                Times.AtLeastOnce);
            _mainHandshaker.Verify(
                x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))),
                Times.AtLeastOnce);
            _fallbackHandshaker.Verify(
                x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))),
                Times.AtLeastOnce);

            _mainExtension.Verify(x => x.Rsv, Times.AtLeastOnce);
            _mainExtension.Verify(x => x.NewReponseData());
            _mainExtension.Verify(x => x.NewExtensionEncoder());
            _mainExtension.Verify(x => x.NewExtensionDecoder());
            _fallbackExtension.Verify(x => x.Rsv, Times.AtLeastOnce);
        }

        [Fact]
        public void CompatibleExtensionTogetherSuccess()
        {
            // initialize
            _mainHandshaker.Setup(x => x.HandshakeExtension(
                        It.Is<WebSocketExtensionData>(v => v.Name.Equals("main"))))
                .Returns(_mainExtension.Object);
            _mainHandshaker.Setup(x => x.HandshakeExtension(
                        It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))))
                .Returns(default(IWebSocketServerExtension));

            _fallbackHandshaker.Setup(x => x.HandshakeExtension(
                        It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))))
                .Returns(_fallbackExtension.Object);
            _fallbackHandshaker.Setup(x => x.HandshakeExtension(
                        It.Is<WebSocketExtensionData>(v => v.Name.Equals("main"))))
                .Returns(default(IWebSocketServerExtension));

            _mainExtension.Setup(x => x.Rsv).Returns(WebSocketRsv.Rsv1);
            _mainExtension.Setup(x => x.NewReponseData()).Returns(
                new WebSocketExtensionData("main", new Dictionary<string, string>()));
            _mainExtension.Setup(x => x.NewExtensionEncoder()).Returns(new DummyEncoder());
            _mainExtension.Setup(x => x.NewExtensionDecoder()).Returns(new DummyDecoder());

            _fallbackExtension.Setup(x => x.Rsv).Returns(WebSocketRsv.Rsv2);
            _fallbackExtension.Setup(x => x.NewReponseData()).Returns(
                new WebSocketExtensionData("fallback", new Dictionary<string, string>()));
            _fallbackExtension.Setup(x => x.NewExtensionEncoder()).Returns(new Dummy2Encoder());
            _fallbackExtension.Setup(x => x.NewExtensionDecoder()).Returns(new Dummy2Decoder());

            // execute
            WebSocketServerExtensionHandler extensionHandler =
                    new WebSocketServerExtensionHandler(_mainHandshaker.Object, _fallbackHandshaker.Object);
            var ch = new EmbeddedChannel(extensionHandler);

            IHttpRequest req = NewUpgradeRequest("main, fallback");
            ch.WriteInbound(req);

            IHttpResponse res = NewUpgradeResponse(null);
            ch.WriteOutbound(res);

            var res2 = ch.ReadOutbound<IHttpResponse>();
            Assert.True(res2.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value));
            List<WebSocketExtensionData> resExts = WebSocketExtensionUtil.ExtractExtensions(value.ToString());

            // test
            Assert.Null(ch.Pipeline.Context(extensionHandler));
            Assert.Equal(2, resExts.Count);
            Assert.Equal("main", resExts[0].Name);
            Assert.Equal("fallback", resExts[1].Name);
            Assert.NotNull(ch.Pipeline.Get<DummyDecoder>());
            Assert.NotNull(ch.Pipeline.Get<DummyEncoder>());
            Assert.NotNull(ch.Pipeline.Get<Dummy2Decoder>());
            Assert.NotNull(ch.Pipeline.Get<Dummy2Encoder>());

            _mainHandshaker.Verify(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("main"))),
                Times.AtLeastOnce);
            _mainHandshaker.Verify(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))),
                Times.AtLeastOnce);
            _fallbackHandshaker.Verify(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))),
                Times.AtLeastOnce);

            _mainExtension.Verify(x => x.Rsv, Times.Exactly(2));
            _mainExtension.Verify(x => x.NewReponseData());
            _mainExtension.Verify(x => x.NewExtensionEncoder());
            _mainExtension.Verify(x => x.NewExtensionDecoder());

            _fallbackExtension.Verify(x => x.Rsv, Times.Exactly(2));
            _fallbackExtension.Verify(x => x.NewReponseData());
            _fallbackExtension.Verify(x => x.NewExtensionEncoder());
            _fallbackExtension.Verify(x => x.NewExtensionDecoder());
        }

        [Fact]
        public void NoneExtensionMatchingSuccess()
        {
            // initialize
            _mainHandshaker.Setup(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("unknown")))).
                Returns(default(IWebSocketServerExtension));
            _mainHandshaker.Setup(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("unknown2")))).
                Returns(default(IWebSocketServerExtension));

            _fallbackHandshaker.Setup(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("unknown")))).
                Returns(default(IWebSocketServerExtension));
            _fallbackHandshaker.Setup(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("unknown2")))).
                Returns(default(IWebSocketServerExtension));

            // execute
            WebSocketServerExtensionHandler extensionHandler =
                    new WebSocketServerExtensionHandler(_mainHandshaker.Object, _fallbackHandshaker.Object);
            var ch = new EmbeddedChannel(extensionHandler);

            IHttpRequest req = NewUpgradeRequest("unknown, unknown2");
            ch.WriteInbound(req);

            IHttpResponse res = NewUpgradeResponse(null);
            ch.WriteOutbound(res);

            var res2 = ch.ReadOutbound<IHttpResponse>();

            // test
            Assert.Null(ch.Pipeline.Context(extensionHandler));
            Assert.False(res2.Headers.Contains(HttpHeaderNames.SecWebsocketExtensions));

            _mainHandshaker.Verify(x => x.HandshakeExtension(It.Is<WebSocketExtensionData>(v => v.Name.Equals("unknown"))));
            _mainHandshaker.Verify(x => x.HandshakeExtension(It.Is<WebSocketExtensionData>(v => v.Name.Equals("unknown2"))));

            _fallbackHandshaker.Verify(x => x.HandshakeExtension(It.Is<WebSocketExtensionData>(v => v.Name.Equals("unknown"))));
            _fallbackHandshaker.Verify(x => x.HandshakeExtension(It.Is<WebSocketExtensionData>(v => v.Name.Equals("unknown2"))));
        }

        [Fact]
        public void ExtensionHandlerNotRemovedByFailureWritePromise()
        {
            // initialize
            _mainHandshaker.Setup(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("main")))).
                Returns(_mainExtension.Object);
            _mainExtension.Setup(x => x.NewReponseData()).Returns(
                new WebSocketExtensionData("main", new Dictionary<string, string>()));

            // execute
            WebSocketServerExtensionHandler extensionHandler =
                    new WebSocketServerExtensionHandler(_mainHandshaker.Object);
            EmbeddedChannel ch = new EmbeddedChannel(extensionHandler);

            IHttpRequest req = NewUpgradeRequest("main");
            ch.WriteInbound(req);

            IHttpResponse res = NewUpgradeResponse(null);
            var failurePromise = ch.NewPromise();
            ch.WriteOneOutbound(res, failurePromise);
            failurePromise.SetException(new IOException("Cannot write response"));

            // test
            Assert.Null(ch.ReadOutbound<object>());
            Assert.NotNull(ch.Pipeline.Context(extensionHandler));
            Assert.True(ch.Finish());
        }
    }
}
