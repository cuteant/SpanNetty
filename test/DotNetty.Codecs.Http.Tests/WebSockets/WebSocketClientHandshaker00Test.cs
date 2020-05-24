// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Utilities;

    public class WebSocketClientHandshaker00Test : WebSocketClientHandshakerTest
    {
        protected override WebSocketClientHandshaker NewHandshaker(Uri uri, string subprotocol, HttpHeaders headers, bool absoluteUpgradeUrl) =>
            new WebSocketClientHandshaker00(uri, WebSocketVersion.V00, subprotocol, headers, 1024, 10000, absoluteUpgradeUrl);

        protected override AsciiString GetOriginHeaderName() => HttpHeaderNames.Origin;

        protected override AsciiString GetProtocolHeaderName()
        {
            return HttpHeaderNames.SecWebsocketProtocol;
            
        }

        protected override AsciiString[] GetHandshakeHeaderNames()
        {
            return new AsciiString[] {
                HttpHeaderNames.Connection,
                HttpHeaderNames.Upgrade,
                HttpHeaderNames.Host,
                HttpHeaderNames.Origin,
                HttpHeaderNames.SecWebsocketKey1,
                HttpHeaderNames.SecWebsocketKey2,
            };
        }
    }
}
