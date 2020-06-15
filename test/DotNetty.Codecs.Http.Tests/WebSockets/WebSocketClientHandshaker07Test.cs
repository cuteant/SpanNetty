// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Utilities;
    using Xunit;

    public class WebSocketClientHandshaker07Test : WebSocketClientHandshakerTest
    {
        [Fact]
        public void HostHeaderPreserved()
        {
            Uri uri = new Uri("ws://localhost:9999");
            WebSocketClientHandshaker handshaker = NewHandshaker(uri, null,
                    new DefaultHttpHeaders().Set(HttpHeaderNames.Host, "test.netty.io"), false);

            IFullHttpRequest request = handshaker.NewHandshakeRequest();
            try
            {
                Assert.Equal("/", request.Uri);
                Assert.Equal("test.netty.io", request.Headers.Get(HttpHeaderNames.Host, null));
            }
            finally
            {
                request.Release();
            }
        }

        protected override WebSocketClientHandshaker NewHandshaker(Uri uri, string subprotocol, HttpHeaders headers, bool absoluteUpgradeUrl) =>
            new WebSocketClientHandshaker07(uri, WebSocketVersion.V07, subprotocol, false, headers, 1024, true, false, 10000, absoluteUpgradeUrl);

        protected override AsciiString GetOriginHeaderName() => HttpHeaderNames.SecWebsocketOrigin;

        protected override AsciiString GetProtocolHeaderName()
        {
            return HttpHeaderNames.SecWebsocketProtocol;

        }

        protected override AsciiString[] GetHandshakeRequiredHeaderNames()
        {
            return new AsciiString[] {
                HttpHeaderNames.Upgrade,
                HttpHeaderNames.Connection,
                HttpHeaderNames.SecWebsocketKey,
                HttpHeaderNames.Host,
                HttpHeaderNames.SecWebsocketVersion,
            };
        }
    }
}
