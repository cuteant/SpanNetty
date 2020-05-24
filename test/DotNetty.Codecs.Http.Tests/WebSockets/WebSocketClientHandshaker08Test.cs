// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System;
    using DotNetty.Codecs.Http.WebSockets;

    public class WebSocketClientHandshaker08Test : WebSocketClientHandshaker07Test
    {
        protected override WebSocketClientHandshaker NewHandshaker(Uri uri, string subprotocol, HttpHeaders headers, bool absoluteUpgradeUrl) =>
            new WebSocketClientHandshaker08(uri, WebSocketVersion.V08, subprotocol, false, headers, 1024, true, true, 10000, absoluteUpgradeUrl);
    }
}
